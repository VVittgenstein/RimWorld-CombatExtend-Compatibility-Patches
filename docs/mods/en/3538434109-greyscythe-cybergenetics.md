# Greyscythe Cybergenetics -- CE Compatibility Patch

Greyscythe Cybergenetics builds a cybernetic super-soldier fantasy around Biotech genes with three pillars: evasion (GS_Evade system), powered ability modes (ChargeBoost, Overdrive, Guardian), and layered passive defense (IncomingDamageFactor stacking, gene-level damageFactors). Under Combat Extended, the mod's Harmony-based evasion system conflicts with CE's ballistic resolution model, vanilla-scale stat offsets produce nonsensical values against CE's compressed stat ranges, and two pawn-level stat factors have no CE reader at all. This patch replaces the evasion system with CE-native hooks, remaps every overscaled stat to CE-appropriate values, and bridges the broken stat factors through programmatically injected StatParts.

## What Breaks Under CE

### Evasion Double-Gating

GS_Evade registers a Harmony prefix on `Pawn.PreApplyDamage` under the ID `feaurie.GS_Core`. This prefix rolls an evasion check and absorbs damage if the roll succeeds. The fundamental problem is timing: by the time `PreApplyDamage` fires, CE's ballistic system has already resolved whether a projectile hit the target through its own flight-path, cover, and shield calculations. A projectile that passed every CE physics check can then be retroactively negated by a post-hit evasion roll.

For ranged combat, this creates double-gating: CE's natural miss model (spread, cover, height) already determines hit probability, and GS_Evade adds a second independent avoidance layer after the hit resolves. If a cybernetic pawn has 40% evasion and CE's projectile system gives 60% hit probability, the effective hit rate drops to 0.60 * 0.60 = 36% -- the evasion stat is providing far more survivability than its face value suggests, because it multiplies with an avoidance layer the mod author did not anticipate.

For melee, the problem is even more severe. CE's melee resolution has three sequential checks: the attacker's hit roll, the defender's dodge check, and the defender's parry check. GS_Evade's `PreApplyDamage` prefix adds a fourth independent avoidance layer after all three have resolved, compounding the already-generous melee defense that cybernetic pawns receive from their gene stats.

### Stat Over-Scaling

Vanilla stat offsets assume ranges that do not exist in CE:

- **ShootingAccuracyPawn +10** (Overdrive hediff): CE clamps this stat at 4.5. A skill-15 shooter has a base of roughly 3.0, so +10 instantly maxes the clamp and wastes 8.5 points of offset.
- **MeleeHitChance +10** (GG_Cyborg gene): CE uses a 0--1 range for this stat. An offset of +10 guarantees hits regardless of any other factor.
- **MeleeDodgeChance +20** (ChargeBoost hediff): same 0--1 range problem. Near-guarantees dodges against all melee attacks.
- **IncomingDamageFactor stacking** (Guardian hediff at 0.3x): when combined with GG_Cyborg (0.90x) and GG_CyberTough (0.80x), the multiplicative stack reaches 0.216x before CE armor is even applied. This makes sustained fire from standard calibers functionally harmless.

### Inert Stat Factors

Two stat factors have no reader in CE's code path:

- **MeleeArmorPenetration** (statFactor on Overdrive/GG_M_Heavy): CE reads `MeleePenetrationFactor` from equipment via `Verb_MeleeAttackCE`, not from pawn-level stats. The vanilla `MeleeArmorPenetration` factor silently does nothing -- a cybernetic pawn with this bonus deals the same penetration as one without it.
- **ArmorRating_Sharp/Blunt/Heat** (stat offsets on various genes): these produce display values in the pawn's stat readout, but human pawns lack the `CoveredByNaturalArmor` body coverage tag, so the values never feed into CE's armor resolution pipeline. The armor rating is displayed but never consulted during damage application.

These inert stats are particularly insidious because they give players the impression that their cybernetic pawns have additional armor or penetration when in fact these bonuses are phantom values under CE. The patch addresses the penetration gap via `StatPart_CyberneticMeleePen` but intentionally leaves the armor display values intact, as they may still inform AI targeting decisions.


## Design Approach

The patch addresses three categories of problems -- and each category demands a different fix technique:

1. **System-level conflict** (evasion): requires Harmony-level intervention to remove the incompatible hook and reimplement at the correct pipeline stage.
2. **Value-level mismatch** (stat offsets/factors): handled purely in XML via `PatchOperationReplace` and `PatchOperationRemove`, requiring no C# code.
3. **Missing stat bridge** (MeleeArmorPenetration): requires a new C# `StatPart` to translate a pawn-level concept into an equipment-level stat that CE actually reads.

This separation keeps the XML patches simple and auditable while confining Harmony patches and StatPart injection to cases where XML alone cannot solve the problem.

All C# components follow a lazy-init pattern: def lookups are performed once via static boolean flags and cached for the lifetime of the game session. This avoids per-frame `DefDatabase` lookups in hot paths like projectile collision and stat evaluation.


## Evasion System Redirect

The evasion fix is three coordinated changes that replace the post-hit absorption model with pre-hit avoidance that integrates into CE's existing resolution layers.

### Step 1: Disable the GS_Evade Prefix

File: `Source/V2CEPatch/Harmony/Patch_PreApplyDamage_GSEvadeDisable.cs`

```csharp
// Line 14-25
public static void Apply(Harmony harmony)
{
    MethodInfo preApplyDamage = AccessTools.Method(typeof(Pawn), "PreApplyDamage");
    // ...
    harmony.Unpatch(preApplyDamage, HarmonyPatchType.Prefix, "feaurie.GS_Core");
}
```

The `Unpatch` call at line 23 targets the specific Harmony ID `feaurie.GS_Core`, removing only the GS_Evade prefix without disturbing other mods' patches on `PreApplyDamage`. This is called during `V2CEPatchMod` static constructor (line 36 of `V2CEPatchMod.cs`) only when `ModDetection.GreyscytheCybergeneticsActive` is true. Mod detection itself is handled by `ModDetection.Init()` (`Source/V2CEPatch/Utility/ModDetection.cs` line 17), which checks `ModsConfig.IsActive("feaurie.GreyscytheGenes")` -- the packageId from the Greyscythe Cybergenetics mod's About.xml.

The alternative -- leaving the prefix active and adding a conditional bypass -- was rejected for two reasons. First, it would require fighting GS_Core's Harmony priority ordering; the prefix runs early in the patch chain, and inserting a conditional check before it would depend on load-order assumptions that can break across mod updates. Second, it is conceptually wrong: a projectile that already resolved a hit in CE's ballistic model should not be retroactively negated. The clean separation is to remove the post-hit roll entirely and reimplement evasion at the correct point in CE's resolution pipeline.

### Step 2: Ranged Evasion via TryCollideWith Dodge Roll

File: `Source/V2CEPatch/Harmony/Patch_ProjectileCE_RangedDodge.cs`

This is a **shared hook** patching `ProjectileCE.TryCollideWith`, also used by VQE Ancients (BlurRunner gene). It is applied if either mod is active (`V2CEPatchMod.cs` lines 42-54).

For Greyscythe Cybergenetics, the relevant logic is at lines 52-61:

```csharp
// Roll 2: GS Cybergenetics ranged evasion -- stat-scaled dodge
if (ModDetection.GreyscytheCybergeneticsActive && evadeProjectileStat != null)
{
    float evadeChance = pawn.GetStatValue(evadeProjectileStat);  // line 55
    if (evadeChance > 0f && Rand.Chance(evadeChance))            // line 56
    {
        MoteMaker.ThrowText(pawn.DrawPos, pawn.Map,
            "CE_Dodge".Translate(), new Color(0f, 1f, 0.6f));    // line 58
        __result = false;                                         // line 59
        return false;                                             // line 60
    }
}
```

The evasion chance is read from the pawn's post-processed `GS_Evade_EvadeProjectileChance` stat value, which already incorporates the original mod's capacity scaling (Hearing 10x, Sight 10x, Moving 12x) and `postProcessCurve` capping output at 0--0.90.

Gating conditions (lines 27-31): the pawn must not be downed, in bed, or stunned. These checks prevent absurd scenarios like a downed pawn dodging bullets. Setting `__result = false` tells CE the projectile did not collide, so no damage is applied -- the projectile continues its flight path and may hit something behind the target.

The def lookup for `GS_Evade_EvadeProjectileChance` is cached via a static boolean (`defsLookedUp`, line 20) and `DefDatabase<StatDef>.GetNamedSilentFail` (line 36). If the stat def does not exist (e.g., Greyscythe Cybergenetics is not loaded), `evadeProjectileStat` remains null and the entire block is skipped with zero per-frame cost.

**Why not an accuracy penalty on the shooter?** This was considered for ranged evasion but rejected. An accuracy penalty would make cybernetic pawns "blurry targets" -- conceptually a miss, not a dodge. The shooter's aim was fine; the target moved. The GS_Evade stat already provides direct dodge-chance scaling, and the dodge text feedback ("CE_Dodge") preserves the intended fantasy of actively evading projectiles. Additionally, an accuracy penalty would affect all shots against the target uniformly, including suppression fire, while a per-collision dodge roll creates the correct variance where some rounds in a burst connect and others are evaded.

### Step 3: Melee Evasion via StatPart on MeleeDodgeChance

File: `Source/V2CEPatch/StatParts/StatPart_CyberneticMeleeDodge.cs`

Rather than adding another Harmony patch, melee evasion is implemented as a `StatPart` injected into CE's `MeleeDodgeChance` stat definition at startup:

```csharp
// V2CEPatchMod.cs lines 60-68
private static void InjectCyberneticStatParts()
{
    var meleeDodgeStat = DefDatabase<StatDef>.GetNamedSilentFail("MeleeDodgeChance");
    if (meleeDodgeStat != null)
    {
        meleeDodgeStat.parts ??= new List<StatPart>();
        meleeDodgeStat.parts.Add(new StatPart_CyberneticMeleeDodge());
    }
    // ...
}
```

The StatPart reads `GS_Evade_EvadeMeleeChance` post-processed value (line 42) and scales it by 0.5x (line 43, constant at line 8), then adds the result to the pawn's `MeleeDodgeChance` (line 19):

```csharp
private const float ScaleFactor = 0.5f;  // line 8

private static float GetBonus(Pawn pawn)
{
    float rawEvade = pawn.GetStatValue(evadeMeleeStat);  // line 42
    return rawEvade * ScaleFactor;                        // line 43
}
```

This means a pawn with 0.20 raw melee evasion gets +0.10 dodge chance added to CE's `MeleeDodgeChance`. Critically, because this feeds into the stat system rather than bypassing it, CE's bulk and weight penalties naturally apply to the combined dodge value. A cybernetic pawn wearing heavy CE armor will have their dodge chance reduced by the armor's bulk modifier -- exactly the trade-off CE intends.

The 0.5x scale factor was chosen because the melee evasion stat already represents a strong baseline, and CE's dodge mechanic is more impactful per point than vanilla's. A full 1.0x pass-through would stack too aggressively with other dodge sources (equipment, traits), while 0.5x keeps the cybernetic bonus meaningful but not dominant.

The `ExplanationPart` override (lines 22-30) surfaces the bonus in the stat tooltip as "V2CEPatch_CyberneticMeleeDodge: +X%", ensuring players can see exactly how much dodge chance the cybernetic genes contribute.


## Stat Remapping -- XML Patches

File: `Patches/GreyscytheCybergenetics.xml`

All patches are gated on `PatchOperationFindMod` requiring both `Combat Extended` and `Greyscythe Cybergenetics` (lines 5-9). This dual-mod gate ensures the patches are never applied when only one of the two mods is loaded -- the vanilla stat values remain correct when CE is absent, and CE runs normally when Greyscythe Cybergenetics is absent.

| Original Stat | Original Value | CE Value | Rationale | Patch Lines |
|---|---|---|---|---|
| GG_Cyborg `MeleeHitChance` offset | +10 | +0.10 | CE range 0--1; +0.10 = solid 10pp bonus without guaranteeing hits | 17-22 |
| Overdrive `ShootingAccuracyPawn` offset | +10 | +1.0 | CE clamp 4.5; skill-15 shooter (3.0) + 1.0 = 4.0, strong but below max | 27-32 |
| Overdrive `MeleeHitChance` offset | +10 | +0.15 | Active ability deserves more than passive gene (+0.10) | 37-42 |
| Overdrive `MeleeDamageFactor` factor | 1.75x | 1.50x | CE melee already lethal; slight reduction prevents routine one-shots | 47-52 |
| Overdrive `MeleeArmorPenetration` factor | 1.5x | **Removed** | No CE reader; replaced by `StatPart_CyberneticMeleePen` | 59-61 |
| ChargeBoost `MeleeDodgeChance` offset | +20 | **Removed** | Handled exclusively by `StatPart_CyberneticMeleeDodge` to avoid double-counting | 69-71 |
| Guardian `IncomingDamageFactor` factor | 0.3x | 0.55x | Prevents immunity-tier tankiness under CE armor stacking | 77-82 |

The two removal patches (`PatchOperationRemove` at lines 59-61 and 69-71) strip stats that are either inert or replaced by C# StatParts. Leaving them in would either do nothing (MeleeArmorPenetration) or double-count with the StatPart (MeleeDodgeChance).

### Stat Remapping Rationale

The target values follow two principles. First, offsets are scaled to match CE's actual stat ranges rather than vanilla's arbitrary ranges -- vanilla `MeleeHitChance` runs from 0 to ~20 in practice, while CE normalizes it to 0--1, so a +10 offset becomes +0.10 (preserving the proportional bonus). Second, active abilities (Overdrive) are given modestly higher values than passive genes (GG_Cyborg) to preserve the power hierarchy between always-on bonuses and resource-gated abilities. Overdrive's `MeleeHitChance` at +0.15 versus GG_Cyborg's +0.10 reflects this: activating Overdrive should feel like a meaningful combat escalation, not a marginal improvement.

The `MeleeDamageFactor` reduction from 1.75x to 1.50x is specific to CE's damage model. CE melee damage is already substantially higher than vanilla because weapon penetration factors determine whether damage is fully applied or deflected. A 1.75x multiplier on top of successful penetration frequently produced one-shot kills against armored targets; 1.50x keeps Overdrive melee strikes powerful without trivializing armor.


## MeleeArmorPenetration Replacement

File: `Source/V2CEPatch/StatParts/StatPart_CyberneticMeleePen.cs`

Vanilla `MeleeArmorPenetration` is a pawn-level stat factor. CE ignores it entirely -- `Verb_MeleeAttackCE` reads `MeleePenetrationFactor` from the equipment source, not the pawn. The StatPart bridges this gap by attaching to `MeleePenetrationFactor` on the weapon and checking the wielder for qualifying conditions.

### Wielder Resolution

The core challenge with `MeleePenetrationFactor` is that CE evaluates it on the weapon, not the pawn. When CE's `Verb_MeleeAttackCE` asks "what is this weapon's penetration factor?", the stat request's `Thing` is the weapon itself. The StatPart must traverse the ownership chain to find the pawn wielding it and check that pawn's genes and hediffs.

The StatPart receives a `StatRequest` for a weapon, not a pawn. It resolves the wielder through the `ParentHolder` chain (lines 40-45):

```csharp
private static Pawn GetWielder(Thing weapon)
{
    if (weapon is ThingWithComps twc)
    {
        if (twc.ParentHolder is Pawn_EquipmentTracker eq)
            return eq.pawn;
    }
    return null;
}
```

### Condition Check

If the wielder has either `GG_M_Heavy` gene or `GS_OverdriveHediff` hediff (lines 53-57), the StatPart applies a 1.35x multiplier to `MeleePenetrationFactor` (line 23, constant at line 9). The bonus is non-stacking: gene and hediff provide the same multiplier via early return, so a pawn with both active still gets 1.35x, not 1.35 * 1.35.

```csharp
private const float Multiplier = 1.35f;  // line 9

public override void TransformValue(StatRequest req, ref float val)
{
    // ...
    if (HasCyberneticPenBonus(wielder))
        val *= Multiplier;                // line 23
}
```

Injection into `MeleePenetrationFactor` happens alongside the dodge StatPart in `V2CEPatchMod.InjectCyberneticStatParts()` (lines 70-76). Like the dodge StatPart, the penetration StatPart provides its own `ExplanationPart` (lines 26-35) that surfaces the bonus in the weapon's stat tooltip as "V2CEPatch_CyberneticMeleePen: x1.35", so players inspecting their weapon can see why its penetration is higher than the base value.

The 1.35x multiplier was chosen to approximate the original 1.5x `MeleeArmorPenetration` factor after accounting for the difference in how CE applies penetration. In CE, penetration determines whether a strike fully defeats armor or is partially deflected, making each point of penetration more impactful than in vanilla's proportional damage reduction model. A lower multiplier achieves comparable effective bonus.


## StatPart Configuration Defs

File: `Defs/GreyscytheCybergenetics/StatParts_Cybernetic.xml`

Two `StatPartConfigDef` entries declare the configuration consumed by the C# StatPart classes:

```xml
<!-- Lines 28-35 -->
<V2CEPatch.StatPartConfigDef>
    <defName>V2CE_CyberneticMeleeDodge</defName>
    <targetStat>MeleeDodgeChance</targetStat>
    <sourceStat>GS_Evade_EvadeMeleeChance</sourceStat>
    <scaleFactor>0.5</scaleFactor>
</V2CEPatch.StatPartConfigDef>

<!-- Lines 37-45 -->
<V2CEPatch.StatPartConfigDef>
    <defName>V2CE_CyberneticMeleePen</defName>
    <targetStat>MeleePenetrationFactor</targetStat>
    <geneDefName>GG_M_Heavy</geneDefName>
    <hediffDefName>GS_OverdriveHediff</hediffDefName>
    <multiplier>1.35</multiplier>
</V2CEPatch.StatPartConfigDef>
```

These Defs are loaded conditionally via `LoadFolders.xml` (line 10):

```xml
<li IfModActive="feaurie.GreyscytheGenes">Defs/GreyscytheCybergenetics</li>
```

The `StatPartConfigDef` class (`Source/V2CEPatch/Utility/StatPartConfigDef.cs`) is a minimal `Def` subclass providing typed fields for `targetStat`, `sourceStat`, `scaleFactor`, `geneDefName`, `hediffDefName`, and `multiplier`. It inherits from `Verse.Def`, which means RimWorld's XML deserializer loads and validates these entries automatically during startup. The fields are intentionally stringly-typed (e.g., `string geneDefName` rather than `GeneDef geneDefName`) to allow graceful failure when the target mod is not loaded -- `DefDatabase.GetNamedSilentFail` in the C# StatPart classes handles the null case rather than producing red errors during Def resolution.


## Guardian Protocol Rebalancing

### The 0.55 Decision

Guardian's `IncomingDamageFactor` interacts multiplicatively with other IDF sources on the pawn. The full stacking envelope under CE:

| Configuration | IDF Stack | Post-Armor Reduction |
|---|---|---|
| GG_Cyborg only | 0.90 | 10% reduction |
| + GG_CyberTough | 0.72 | 28% reduction |
| + Overdrive | 0.576 | 42% reduction |
| + Guardian (CE: 0.55) | 0.72 x 0.55 = 0.396 | 60% reduction |

At the original 0.3x, a 5.56mm FMJ round (10 damage) passing through medium CE armor (~50% absorption) delivers ~5 damage post-armor, then 5 x 0.216 = ~1.1 effective damage with the full IDF stack. This trivializes sustained automatic fire -- a full magazine of 5.56 would barely scratch a Guardian-mode cyborg.

At 0.55x, the full stack yields ~40% post-armor effective damage. A Guardian cyborg is still extremely durable -- absorbing 60% of post-armor damage -- but concentrated AP weapons or headshots can meaningfully threaten them.

### Gating Constraints

The 0.55x value accounts for Guardian's built-in gating:

- **Gene complexity**: requires 4+ complexity slots and negative metabolism, representing significant xenotype investment
- **Fuel management**: drains 4% per minute plus additional on-hit drain, creating operational windows
- **Mutual exclusivity**: locks out Overdrive and ChargeBoost, so the full IDF stack shown above requires separate gene sources for the additional factors

The practical test case: a squad of three colonists with assault rifles (5.56mm FMJ, 30-round magazines) engaging a Guardian-mode cyborg at medium range. At 0.3x IDF, the combined 90 rounds would deal roughly 100 effective damage total through medium armor and the IDF stack -- barely enough to threaten the pawn. At 0.55x IDF, the same engagement deals roughly 180 effective damage, making focused fire a viable counter-strategy without making Guardian mode feel weak. The cyborg still needs to be focused by multiple shooters or hit with AP rounds to go down, which is the correct fantasy for a fuel-gated super-soldier ability.


## Shared Hooks

Two Harmony patches are shared with VQE Ancients and applied if EITHER mod is active (`V2CEPatchMod.cs` lines 42-55):

- **`Patch_ArmorUtilityCE_ForcedHeadshot`** (`Source/V2CEPatch/Harmony/Patch_ArmorUtilityCE_ForcedHeadshot.cs`): prefixes `ArmorUtilityCE.GetAfterArmorDamage` to redirect hits to the head for VQE Ancients' MasterfulShooting gene. Not directly used by Greyscythe Cybergenetics, but the hook is registered when either mod is active.
- **`Patch_ProjectileCE_RangedDodge`** (`Source/V2CEPatch/Harmony/Patch_ProjectileCE_RangedDodge.cs`): prefixes `ProjectileCE.TryCollideWith` with sequential dodge rolls -- first BlurRunner (fixed 20%), then GS_Evade (stat-scaled). Each roll is independently gated on its source mod being active.

The shared hook pattern (`V2CEPatchMod.cs` lines 42-43) uses boolean OR:

```csharp
bool needForcedHeadshot = ModDetection.VQEAncientsActive || ModDetection.GreyscytheCybergeneticsActive;
bool needRangedDodge = ModDetection.VQEAncientsActive || ModDetection.GreyscytheCybergeneticsActive;
```

This ensures the patches are applied exactly once regardless of which combination of consumer mods is loaded, while the internal logic of each patch checks for its specific mod/gene/stat before acting. If both VQE Ancients and Greyscythe Cybergenetics are loaded simultaneously, a pawn with both BlurRunner and GS_Evade genes would roll BlurRunner first (20% fixed chance, line 44) and GS_Evade second (stat-scaled chance, line 56). The rolls are independent and sequential -- if BlurRunner succeeds, the GS_Evade roll is never reached due to the early return at line 48.


## What Was Left Unchanged

Several Greyscythe Cybergenetics features were evaluated and intentionally left unmodified:

- **ArmorRating_Sharp/Blunt/Heat offsets**: kept for display and AI evaluation purposes. These values appear in the pawn's info panel and may influence AI threat evaluation, but they do not contribute to actual CE armor calculations for human pawns. Real protection comes from `IncomingDamageFactor` and gene-level `damageFactors`, both of which are functional under CE.
- **VEF_RangeAttackDamageFactor and VEF_VerbRangeFactor**: these are VEF-defined stats. CE-VEF compatibility is the VEF team's responsibility.
- **AimingDelayFactor and RangedCooldownFactor**: uncertain CE interaction, but their vanilla values are reasonable if CE does read them.
- **Gene damageFactors** (Bulletproof, Ripshield, etc.): fully functional under CE. These are applied in `Pawn.PreApplyDamage` pre-armor, which CE's transpiler preserves. Unlike `IncomingDamageFactor` (which is a stat and can be patched via XML), `damageFactors` are hard-coded per gene def and apply to specific damage types, making them both correctly targeted and correctly timed in CE's pipeline.
- **Self-bomb abilities**: damage values are reasonable for CE scale and flow through CE's armor transpiler normally.
- **Fuel drain on damage**: functional via `Notify_PawnPostApplyDamage`, which fires correctly under CE's damage pipeline.
- **Capacity scaling on GS_Evade stats**: the Hearing 10x, Sight 10x, Moving 12x capacity weights and postProcessCurve (capping at 0.90) are left intact. These already produce reasonable output ranges, and the patch's TryCollideWith hook and StatPart read the post-processed values, so any changes the original mod author makes to capacity scaling automatically flow through without patch updates.

The guiding principle for what to leave alone: if a feature already works correctly under CE or is another mod team's responsibility, no patch is applied. Patches are applied only where a feature is broken, inert, or produces degenerate gameplay outcomes. Each stat and system was individually evaluated against CE's source to determine whether it had a functional reader, and if so, whether its vanilla-scale value produced reasonable results under CE's damage and accuracy models.

The resulting patch touches seven XML stat values, removes two inert/superseded stats, adds two C# StatParts, registers one Harmony unpatch, and shares two Harmony prefixes with VQE Ancients -- a minimal footprint for the scope of incompatibilities addressed.
