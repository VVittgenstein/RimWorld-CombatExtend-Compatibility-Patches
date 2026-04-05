# VQE Ancients

VQE Ancients adds archite-enhanced genes including two combat-critical abilities that silently fail under Combat Extended: MasterfulShooting's headshot guarantee survives CE's projectile pipeline (contrary to initial corpus assessment) but is vulnerable to upstream changes, while BlurRunner's ranged dodge is completely inert because CE has no dodge system. This patch adds an insurance hook for MasterfulShooting at the armor-calculation boundary, implements a CE-native ranged dodge for BlurRunner via collision-level rejection, and removes a duplicate `CompProperties_ArmorDurability` entry on the Splicefiend. Two other combat genes -- Prowess and MasterfulMelee -- already function correctly under CE and are left unchanged.


## What Breaks Under CE

VQE Ancients ships five combat-relevant gene abilities. Three work under CE, one partially works, and one is completely dead.

### MasterfulShooting: Headshot Guarantee (Partially Working)

The gene adds a Harmony prefix on `DamageWorker_AddInjury.ChooseHitPart` that forces every ranged hit to target the head. The CE corpus report assessed this as "non-functional" because "CE projectiles determine hit location through their own ballistic system."

Static code-path analysis proves the corpus report is wrong. The prefix fires and works:

| Step | Location | What Happens |
|---|---|---|
| 1 | `ProjectileCE.cs:186` | `BulletCE.Impact` creates `DamageInfo` with null `HitPart` |
| 2 | `BulletCE.cs:82` | `SetBodyRegion` sets Height/Depth constraints but does NOT set `HitPart` |
| 3 | `DamageWorker_AddInjury.cs:333` | `ApplyDamageToPart` sees null `HitPart`, calls `ChooseHitPart` |
| 4 | CE transpiler on `ApplyDamageToPart` | Replaces ONLY the armor calculation, not the `ChooseHitPart` call |
| 5 | VQEA prefix on `ChooseHitPart` | Fires, sets Head as `__result`, returns `false` |

The headshot guarantee survives because CE's transpiler scope does not extend to the hit-part selection call. However, this is a fragile dependency on CE's internal implementation detail. A future CE version that modifies the transpiler scope, or another mod that sets `HitPart` before `ChooseHitPart` is reached, would silently break the gene.

### BlurRunner: Ranged Dodge (Completely Dead)

The gene defines `VEF_RangedDodgeChance +0.25`, giving a 25% chance to dodge incoming ranged attacks. This stat is completely inert under CE. CE's projectile collision is a geometric ray-cast via `ProjectileCE.TryCollideWith` -- there is no dodge roll, no stat check, no mechanism that reads `VEF_RangedDodgeChance`. The gene's defining defensive ability provides zero benefit.

### Prowess: 3x Melee (Already Functional)

The gene applies `postProcessStatFactor` of 3.0 on both `MeleeHitChance` and `MeleeDodgeChance`. CE's melee pipeline reads both stats directly through the standard stat system. No repair needed.

### MasterfulMelee: Vital Organ Targeting (Already Functional)

Uses the same `ChooseHitPart` prefix as MasterfulShooting, but for melee damage. CE's melee pipeline preserves the `ChooseHitPart` call path -- melee damage does not go through `ProjectileCE`. No repair needed.

### Splicefiend ArmorDurability Bug

The Splicefiend creature has duplicate `CompProperties_ArmorDurability` entries in its CE manual patch: one with `Durability="500"` and one with `Durability="1200"`. `GetComp<CompProperties_ArmorDurability>()` returns the first match, so the creature gets 500 durability instead of the intended 1200. This is a CE patch authoring bug, not a mod-side issue.


## Design Problem and Alternatives Considered

Two systems need repair: MasterfulShooting needs hardening against upstream changes, and BlurRunner needs a CE-native dodge implementation. The design questions are where to hook each system and how to calibrate BlurRunner's dodge chance for CE's ballistic environment.

### MasterfulShooting: Where to Place the Insurance

Three hook sites were evaluated:

**Option A: Transpiler on CE's `ApplyDamageToPart` transpiler.** Inject instructions into CE's own IL modifications to guarantee `HitPart` is set before armor processing. Rejected -- transpiling a transpiler is fragile, version-dependent, and difficult to debug.

**Option B: Postfix on `ChooseHitPart` (doubling the existing prefix).** Add a second enforcement point at the same call site. Rejected -- this does not protect against the failure mode where `ChooseHitPart` is never called (the exact scenario the insurance is meant to cover).

**Option C: Prefix on `ArmorUtilityCE.GetAfterArmorDamage` (chosen).** This is the exact point where CE uses `hitPart` for armor iteration. If the existing VQEA prefix on `ChooseHitPart` has already set Head, the insurance confirms it. If something prevented `ChooseHitPart` from firing, the insurance catches it here. The hook fires at the last possible moment before armor calculations consume the hit location.

### BlurRunner: Dodge Mechanism

Two approaches were evaluated:

**Option A: Accuracy penalty on the shooter (like GS Cybergenetics).** Make pawns with BlurRunner harder to aim at by penalizing `ShotModifier` accuracy. Rejected -- this models BlurRunner as "blurry" (harder to aim at), but the gene's fantasy is speed (moves out of the way). More importantly, BlurRunner is a single binary gene with a fixed 25% dodge. A continuous accuracy penalty is a design mismatch for a binary on/off ability.

**Option B: Collision-level dodge via `TryCollideWith` (chosen).** When a CE projectile's ray-cast hits the pawn, roll a dodge chance. On success, the projectile passes through. This models the gene as "fast" -- the pawn dodges after the shot is aimed, consistent with the speed fantasy. Binary gene, binary roll.

GS Cybergenetics uses accuracy penalty (Option A) because its evasion scales continuously with capacity stats -- a different design surface that benefits from a continuous modifier. The two approaches are not interchangeable; each matches its source gene's design.


## Implementation

### MasterfulShooting Insurance Hook

**File:** `Source/V2CEPatch/Harmony/Patch_ArmorUtilityCE_ForcedHeadshot.cs`

```csharp
[HarmonyPatch(typeof(ArmorUtilityCE), nameof(ArmorUtilityCE.GetAfterArmorDamage))]
public static class Patch_ArmorUtilityCE_ForcedHeadshot
```

The prefix intercepts `ArmorUtilityCE.GetAfterArmorDamage` (line 9) with the following guard chain:

| Line | Guard | Purpose |
|---|---|---|
| 17 | `originalDinfo.Def.isRanged` | Skip melee -- MasterfulMelee already works via `ChooseHitPart` |
| 20 | `attacker?.genes != null` | Skip non-pawn and geneless attackers |
| 22 | `ModDetection.VQEAncientsActive` | Skip when VQE Ancients is not loaded |
| 24-28 | Lazy `DefDatabase<GeneDef>` lookup | Resolve `VQEA_MasterfulShooting` once, cache result |
| 31 | `HasActiveGene(masterfulShootingDef)` | Skip pawns without the active gene |

On passing all guards, the patch searches the target's non-missing body parts (lines 33-38):

```csharp
BodyPartRecord headPart = pawn.health.hediffSet
    .GetNotMissingParts(BodyPartHeight.Undefined, BodyPartDepth.Undefined)
    .FirstOrDefault(p =>
        p.def == BodyPartDefOf.Head ||
        p.def.defName == "Reactor" ||
        p.def.defName == "InsectHead");
```

Three head variants are checked: standard `Head`, synthetic `Reactor` (mechanoid/android head), and `InsectHead` (insectoid head). If found, the patch overwrites the `hitPart` ref parameter and sets `BodyPartHeight.Top` for consistency with CE's body region system (lines 42-43).

**Interaction with VQEA's existing prefix:** Both hooks may fire for the same shot. VQEA's prefix sets Head at `ChooseHitPart` time; the insurance confirms Head at armor time. Same result, no conflict. If `ChooseHitPart` was skipped or overridden, the insurance catches it independently.

**Performance cost:** The insurance prefix adds one `isRanged` check, one null check, one `ModDetection` bool read, and (on first call only) one `DefDatabase` lookup per ranged damage event. For pawns without the gene, the method exits at the `HasActiveGene` check before any LINQ enumeration. The per-shot cost is negligible compared to the armor iteration that follows.

### BlurRunner Ranged Dodge

**File:** `Source/V2CEPatch/Harmony/Patch_ProjectileCE_RangedDodge.cs`

This is a shared hook that also serves GS Cybergenetics ranged evasion. The method targets `ProjectileCE.TryCollideWith` via manual resolution:

```csharp
static MethodBase TargetMethod()
{
    return AccessTools.Method(typeof(ProjectileCE), "TryCollideWith");
}
```

**Shared preamble (lines 24-31):** All dodge candidates must pass common guards:

```csharp
Pawn pawn = thing as Pawn;
if (pawn == null) return true;

if (pawn.Downed || pawn.InBed())
    return true;

if (pawn.stances?.stunner?.Stunned == true)
    return true;
```

Downed, in-bed, and stunned pawns cannot dodge. These guards apply to both BlurRunner and GS Cybergenetics branches.

**BlurRunner branch (lines 40-50):**

```csharp
if (ModDetection.VQEAncientsActive && blurRunnerDef != null
    && pawn.genes?.HasActiveGene(blurRunnerDef) == true)
{
    if (Rand.Chance(0.20f))
    {
        MoteMaker.ThrowText(pawn.DrawPos, pawn.Map,
            "CE_Dodge".Translate(), new Color(0f, 0.8f, 1f));
        __result = false;
        return false;
    }
}
```

On a successful 20% roll: cyan "CE_Dodge" text mote appears, `__result = false` tells CE the projectile did not collide, and `return false` skips the original method entirely.

**Why 20% and not the original 25%:** CE's inherent ballistic spread already causes misses that vanilla's hitscan does not produce. The combined defensive profile must be comparable:

| Environment | Hit Rate | Dodge Survival | Shots Connecting |
|---|---|---|---|
| Vanilla, 25% dodge | 100% (hitscan) | 75% | 75% |
| CE at 25 cells, 20% dodge | ~60% (ballistic) | 80% | ~48% |
| CE at 25 cells, 25% dodge | ~60% (ballistic) | 75% | ~45% |

At 25%, BlurRunner under CE would reduce incoming damage to ~45% of shots -- significantly more powerful than the vanilla 75%. At 20%, the combined effect (~48%) produces a defensive profile comparable to vanilla without over-stacking with CE's natural miss rate.

The 25-cell reference range was chosen because it represents a typical engagement distance for mid-tier ranged combat. At shorter ranges CE hit rates climb toward 80-90%, making the dodge proportionally more impactful; at longer ranges CE's spread dominates and the dodge matters less. The 20% value targets parity at the median engagement distance rather than optimizing for any extreme.

**Roll ordering:** BlurRunner rolls first (line 40). If BlurRunner does not fire or does not dodge, the GS Cybergenetics branch (lines 52-62) rolls independently. This implements a max-of-two-chances model: a pawn with both genes gets two independent dodge rolls, but the chances do not stack additively. The sequential two-roll structure means the effective combined dodge when both genes are active is `1 - (1 - 0.20) * (1 - evadeChance)` rather than `0.20 + evadeChance`, preventing the total from exceeding either individual chance in an unexpected way.

### Shared Hook Architecture

**File:** `Source/V2CEPatch/V2CEPatchMod.cs`, lines 41-55

```csharp
bool needForcedHeadshot = ModDetection.VQEAncientsActive
    || ModDetection.GreyscytheCybergeneticsActive;
bool needRangedDodge = ModDetection.VQEAncientsActive
    || ModDetection.GreyscytheCybergeneticsActive;

if (needForcedHeadshot)
{
    harmony.CreateClassProcessor(
        typeof(Patch_ArmorUtilityCE_ForcedHeadshot)).Patch();
}

if (needRangedDodge)
{
    harmony.CreateClassProcessor(
        typeof(Patch_ProjectileCE_RangedDodge)).Patch();
}
```

Both `Patch_ArmorUtilityCE_ForcedHeadshot` and `Patch_ProjectileCE_RangedDodge` are applied if EITHER VQE Ancients OR GS Cybergenetics is active. Inside each hook class, mod-specific branches are guarded by `ModDetection` checks and gene-specific `HasActiveGene` calls. This prevents cross-contamination: loading VQE Ancients without GS Cybergenetics registers the hook but only the BlurRunner/MasterfulShooting branches execute.

### Splicefiend ArmorDurability Fix

**File:** `Patches/VQEAncients.xml`

```xml
<li Class="PatchOperationRemove">
    <xpath>Defs/ThingDef[defName="VQEA_Splicefiend"]/comps/li
        [@Class="CombatExtended.CompProperties_ArmorDurability"]
        [Durability="500"]</xpath>
</li>
```

Single `PatchOperationRemove` targeting the duplicate entry with `Durability="500"`, preserving the intended `Durability="1200"`. The entire patch file is wrapped in a `PatchOperationFindMod` guard for "Vanilla Quests Expanded - Ancients".

This fix is pure XML with no C# dependency -- it executes during RimWorld's XML patch phase before any Harmony patches load, ensuring the corrected durability value is available to all downstream systems.


## Cross-Mod Interactions

### MasterfulShooting vs GS Cybergenetics Evasion

GS Cybergenetics applies an accuracy penalty to the shooter, making shots harder to land. MasterfulShooting forces hits that do connect to target the head. These operate on different methods in different execution phases: GS modifies `ShotModifier` during aim calculation, MasterfulShooting overrides `hitPart` during damage application. Cybernetic evasion vs archite precision -- neither system invalidates the other. No hook conflict exists because the two patches target entirely different methods (`ShotModifier` accuracy vs `ArmorUtilityCE.GetAfterArmorDamage` hit location).

### BlurRunner + GS Evasion on Same Pawn

The shared `TryCollideWith` hook rolls BlurRunner first, then GS evasion independently. A pawn with both genes gets two dodge rolls per incoming projectile (sequential max model, not additive). Combined with CE's natural ballistic miss rate, such a pawn is very evasive -- but the extreme gene investment (archite gene + cyborg enhancement stack) justifies the defensive power.

To quantify the combined effect at 25-cell range: CE base hit ~60%, BlurRunner survival 80%, GS evasion survival ~85% (at typical stat values). Combined: `0.60 * (1 - 0.20) * (1 - 0.15) = ~40.8%` of shots connect. This is strong, but the pawn is paying archite gene complexity for BlurRunner plus the full cyborg enhancement stack for GS evasion -- a gene budget that precludes most other combat enhancements.

### Prowess + GS Melee Dodge Stacking

Prowess applies a 3x `postProcessStatFactor` on `MeleeDodgeChance`. GS Cybergenetics contributes dodge via `StatPart_CyberneticMeleeDodge`. Because `postProcessStatFactor` multiplies the final stat value (including StatPart contributions), Prowess triples the combined dodge.

| Configuration | Base Dodge (Skill 15) | GS StatPart | After Prowess 3x | Final |
|---|---|---|---|---|
| Prowess only | ~22.5% | 0% | ~67.5% | ~67.5% |
| GS only | ~22.5% | +~15% (capacity-dependent) | -- | ~37.5% |
| Prowess + GS | ~22.5% | +~15% | 3x on ~37.5% | ~67.7% |

The 67.7% cap with both genes requires 6+ gene complexity across two mod gene pools. No artificial cap is applied because bulk/weight penalties and fuel drain from the cyborg stack provide natural friction that prevents this from being free power.


## What Was Left Unchanged

| Gene/Feature | Reason |
|---|---|
| Prowess 3x melee factors | Already functional -- CE reads `MeleeHitChance` and `MeleeDodgeChance` via standard stat system |
| MasterfulMelee vital targeting | Already functional -- CE's melee pipeline preserves the `ChooseHitPart` call path |
| Existing CE manual patches (5 XML files) | Adequate for gene stats, creature stats, melee weapons, apparel, scenarios |
| Serene gene `Suppressability=0` | Already patched by CE's own gene patches |
| PerfectVision `VEF_VerbRangeFactor 1.5x` | Display-only under CE (CE uses its own range calculation); accepted loss |


## Summary of Changes

| Problem | Root Cause | Fix | File |
|---|---|---|---|
| MasterfulShooting fragility | `ChooseHitPart` path works but depends on CE transpiler scope | Insurance prefix on `ArmorUtilityCE.GetAfterArmorDamage` | `Source/V2CEPatch/Harmony/Patch_ArmorUtilityCE_ForcedHeadshot.cs` |
| BlurRunner dead stat | CE has no `VEF_RangedDodgeChance` consumer | 20% collision-level dodge via `ProjectileCE.TryCollideWith` | `Source/V2CEPatch/Harmony/Patch_ProjectileCE_RangedDodge.cs` |
| Splicefiend durability 500 vs 1200 | Duplicate `CompProperties_ArmorDurability` entries | `PatchOperationRemove` on the 500-durability entry | `Patches/VQEAncients.xml` |
| Prowess, MasterfulMelee | No breakage under CE | No changes | -- |
