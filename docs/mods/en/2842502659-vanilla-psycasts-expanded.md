# Vanilla Psycasts Expanded -- CE Compatibility Patch

| Field          | Value                                                    |
|----------------|----------------------------------------------------------|
| **Mod**        | Vanilla Psycasts Expanded by Oskar Potocki, Sarg Bjornson|
| **Steam ID**   | 2842502659                                               |
| **packageId**  | `VanillaExpanded.VPsycastsE`                             |
| **Mod type**   | Defs + C# (depends on VFECore / VEF)                    |
| **Patch file** | `Patches/VanillaPsycastsExpanded.xml`                    |
| **RimWorld**   | 1.5, 1.6                                                |

---

## 1. What Broke

Combat Extended already ships extensive support for Vanilla Psycasts Expanded:
15 manual patch files covering melee weapons, standard projectiles, shields,
races, ability ranges, and apparel, plus a dedicated C# compatibility class.
The vast majority of VPE content works correctly under CE.

This patch addresses four specific defects that CE's own patches do not cover.
Each one results in silent data loss or no-op behavior -- the game loads
without errors, but the affected defs do not function as either VPE or CE
intended.

### Defect 1: Eltex Cape -- Zero Armor After CE Patch

VPE defines `VPE_Apparel_EltexCape` as a non-stuffable apparel item with fixed
ingredient costs (VPE_Eltex + Cloth). It has no `stuffCategories` node.

CE's apparel patch replaces `ArmorRating_Sharp` with
`StuffEffectMultiplierArmor` and removes `ArmorRating_Blunt` and
`ArmorRating_Heat`. This is the standard pattern CE uses for stuffable armor
like plate armor, where the final armor value is computed as:

```
baseMaterialArmor * StuffEffectMultiplierArmor
```

On a non-stuffable item, there is no base material. The multiplier has nothing
to multiply. Net armor after CE's patch: **zero on all three axes** -- Sharp,
Blunt, and Heat. The cape becomes purely cosmetic.

Root cause: the CE patch author applied the stuffable armor template
(designed for plate armor and similar items that accept material selection) to
a non-stuffable item without checking for the presence of `stuffCategories`.

CE's patch does correctly add `Bulk` (5) and `WornBulk` (1.5) to the cape.
These values are preserved.

### Defect 2: ExpandableProjectileDef XPath Namespace Mismatch

CE's projectile patches target the following XPath roots:

```
Defs/VEF.Weapons.ExpandableProjectileDef[defName="VPE_FireBreath"]
Defs/VEF.Weapons.ExpandableProjectileDef[defName="VPE_IceBreathe"]
```

VPE's XML declares these defs using the tag `VFECore.ExpandableProjectileDef`.
The C# class does live in the `VEF.Weapons` namespace, and VFECore provides a
type alias that maps `VFECore.ExpandableProjectileDef` to the same runtime
class. Both resolve to the same C# type at load time.

However, RimWorld's XPath engine operates on **literal XML element names**, not
on resolved C# types. When the XPath selector specifies `VEF.Weapons.X` but
the XML document contains `VFECore.X`, the selector matches nothing. All three
`PatchOperationReplace` operations are silent no-ops.

Impact: `VPE_FireBreath` retains its vanilla `damageAmountBase` of 5 and
`speed` of 45 instead of CE's intended values of 12 and 53. `VPE_IceBreathe`
retains `speed` 45 instead of the intended 48. Both breath weapons are
significantly weaker than CE intended.

### Defect 3: Summoned Skeleton Missing CE Melee Stats

CE patches VPE's Steel and Rock Constructs with a full set of CE melee combat
stats: `MeleeDodgeChance` (0.19), `MeleeCritChance` (0.22),
`MeleeParryChance` (0.09), and others. These stats are required for CE's melee
system to calculate dodge rolls, critical hit chances, and parry attempts.

The summoned skeleton (`VPE_SummonedSkeleton`) was skipped. Without these
stats, CE's melee verb resolution uses the default value of 0 for all three --
the skeleton cannot dodge, cannot crit, and cannot parry. This makes it
functionally inert as a melee combatant despite being a purpose-built
short-lived melee unit (manhunter-locked, weapon-locked, 60000-tick lifespan).

Additionally, as an undead entity the skeleton should logically ignore smoke
effects, but `SmokeSensitivity` was never set.

### Defect 4: VEF Custom Stat Hediffs (BladeFocus, ControlledFrenzy)

VPE's Warlord psycast path uses VEF-defined stats to implement its combat
buffs:

- `VEF_MeleeAttackSpeedFactor` -- used by BladeFocus
- `VEF_MeleeAttackDamageFactor` -- used by ControlledFrenzy
- `VEF_RangeAttackDamageFactor` -- used by ControlledFrenzy

VEF applies these stats via Harmony patches on vanilla combat methods. CE
replaces those vanilla methods entirely: `Verb_MeleeAttackCE` bypasses VEF's
melee hooks, and `ProjectileCE` bypasses VEF's ranged hooks. A grep across CE's
source for any of these three stat defNames returns zero results. CE has no
awareness that these stats exist.

CE already solved this problem for one Warlord psycast: `VPE_FiringFocus` uses
`VEF_RangeAttackSpeedFactor`, which CE remapped in `Hediffs_Patch.xml` to
`AimingDelayFactor` (-0.50) and `ReloadSpeed` (+1.0). BladeFocus and
ControlledFrenzy were missed -- the same remap pattern was never applied.

Net result: BladeFocus ("2x melee attack speed") and ControlledFrenzy
("2x melee + 2x ranged damage") are both completely inert under CE. A pawn
casting either psycast receives the hediff, sees the glow, but gets no
mechanical benefit.

---

## 2. Design Problem and Options Considered

### Eltex Cape: Restore Fixed Armor vs. Add Stuff Categories

Two approaches could solve the zero-armor problem:

1. **Add `stuffCategories` to the cape** so that `StuffEffectMultiplierArmor`
   becomes functional. This would change the cape's crafting behavior (players
   choose a material), alter its market value calculation, and diverge from
   VPE's design intent of a fixed-recipe item.

2. **Replace `StuffEffectMultiplierArmor` back to direct `ArmorRating` stats.**
   This preserves VPE's crafting design and gives the cape a definite armor
   value on the CE scale.

Option 2 is correct. The cape should not become stuffable -- that is VPE's
design decision to make, not ours. The appropriate CE-scale armor values are
`ArmorRating_Sharp: 3` (between a duster and flak vest) and
`ArmorRating_Blunt: 2`, consistent with a light magical textile garment.

### Breath Weapons: Fix XPath vs. Duplicate Defs

The XPath mismatch could be solved by creating new projectile defs that
override the originals, but this is fragile and conflicts with any other mod
that patches the same defs. The correct fix is to use the literal XML element
name (`VFECore.ExpandableProjectileDef`) in the XPath selectors and apply the
same values CE intended.

### Skeleton: Full Stat Block vs. Melee-Only Subset

CE's construct patches include carry capacity, shooting stats, and other
values. The skeleton is manhunter-locked (cannot be drafted, cannot carry
items, cannot use ranged weapons) and dies after 60000 ticks. Only melee
combat stats are mechanically relevant. Adding carry or shooting stats would
be dead code.

### Hediff Remap: Direct Stat Translation vs. Approximate CE Equivalents

VEF's `VEF_MeleeAttackSpeedFactor` maps cleanly to CE's `MeleeCooldownFactor`
(inverse relationship: halving cooldown doubles speed). VEF's damage factor
stats have no direct CE equivalent -- CE does not expose a flat damage
multiplier.

The established pattern from CE's own `FiringFocus` remap is to decompose a
single VEF stat into multiple CE-native stats that approximate the intended
effect through different mechanical channels. The patch follows this pattern:

- **Speed** is remapped via `MeleeCooldownFactor` / `RangedCooldownFactor`
- **Damage** is approximated through `MeleeCritChance` (CE crits deal 2x
  sharp damage), `MeleeHitChance` (more hits land), `AimingAccuracy`, and
  `ReloadSpeed`

This is an approximation, not an exact 1:1 translation. The alternative --
writing a C# Harmony patch to inject a flat damage multiplier into CE's
projectile and melee verb pipelines -- would be fragile across CE updates and
disproportionate to the scope of an XML compatibility patch.

---

## 3. Implementation

The entire patch is a single file: **`Patches/VanillaPsycastsExpanded.xml`**.

### Patch Structure

The outer operation is `PatchOperationFindMod`, gated on "Vanilla Psycasts
Expanded" being active. Inside, a `PatchOperationSequence` runs 10 operations
in order.

No `PatchOperationFindMod` gate on "Combat Extended" is needed because the
patch file lives inside V2CEPatch, which itself has CE as a hard dependency.
If CE is not loaded, V2CEPatch is not loaded, and this file is never read.

### Op 1: Eltex Cape -- Replace Inert Multiplier (line 13)

`PatchOperationReplace` targets `StuffEffectMultiplierArmor` on the cape and
swaps it for `ArmorRating_Sharp: 3` (CE scale, between duster and flak vest).
If CE fixes this upstream, the XPath misses and the op is a safe no-op.

### Op 2: Eltex Cape -- Restore Blunt Armor (line 21)

`PatchOperationAdd` appends `ArmorRating_Blunt: 2` to the cape's `statBases`.

### Ops 3--5: ExpandableProjectileDef XPath Fixes (lines 29--49)

Three `PatchOperationReplace` operations applying CE's originally intended
values, now targeting the correct XML element name:

| Op | Target                 | Field             | Value |
|----|------------------------|-------------------|-------|
| 3  | `VPE_FireBreath`       | `damageAmountBase`| 12    |
| 4  | `VPE_FireBreath`       | `speed`           | 53    |
| 5  | `VPE_IceBreathe`       | `speed`           | 48    |

All three use `VFECore.ExpandableProjectileDef` as the XPath root element,
matching the literal XML tag that VPE uses.

### Op 6: Skeleton Melee Stats (line 53)

`PatchOperationAdd` appends to the skeleton's `statBases`:
`MeleeDodgeChance: 0.19`, `MeleeCritChance: 0.22`, `MeleeParryChance: 0.09`,
`SmokeSensitivity: 0`. Values match CE's treatment of the Steel and Rock
Constructs. Only melee-relevant stats are included.

### Ops 7a--7b: BladeFocus Remap (lines 64--81)

**Op 7a** (`PatchOperationReplace`) replaces the hediff's `statFactors` block
(which contains the inert `VEF_MeleeAttackSpeedFactor`) with
`MeleeCooldownFactor: 0.5`. Halving cooldown produces 2x attack speed. This
stat flows through `VerbProperties.AdjustedCooldown()` in CE's melee pipeline.

**Op 7b** (`PatchOperationAdd`) adds `statOffsets` to the hediff stage:
`MeleeHitChance: 5`, `MeleeCritChance: 0.5`. These represent the psychic
focus component -- more precise strikes from a psycaster channeling blade
focus.

### Ops 8a--8b: ControlledFrenzy Remap (lines 85--106)

**Op 8a** (`PatchOperationReplace`) replaces `statFactors` with
`MeleeCooldownFactor: 0.75` and `RangedCooldownFactor: 0.75`. Both axes get a
33% throughput increase. The factor is less aggressive than BladeFocus (0.75
vs 0.5) because the power budget is split across melee and ranged.

**Op 8b** (`PatchOperationAdd`) adds `statOffsets`: `MeleeHitChance: 10`,
`MeleeCritChance: 1.0`, `AimingAccuracy: 0.5`, `ReloadSpeed: 1.0`. The ranged
stats match CE's own FiringFocus remap pattern. `MeleeCritChance: 1.0` is the
primary damage amplifier -- CE crits deal 2x sharp damage, approximating the
intended 2x damage factor without a non-existent flat multiplier stat.

### What Is Not Patched

This patch deliberately omits everything CE already handles correctly:

- **Melee weapons** (eltex staff, etc.) -- CE's weapon patches work.
- **Standard projectiles** -- CE's projectile patches work.
- **Shields** (skipshield, etc.) -- CE's shield patches work.
- **Races and body types** -- CE's race patches work.
- **Ability ranges** -- CE's ability patches work.
- **SpeedBoost, GuidedShot, FiringFocus** -- These either use vanilla stats
  (SpeedBoost uses `MoveSpeed`, which CE does not replace) or are already
  remapped by CE's own hediff patches (FiringFocus, GuidedShot).

---

## 4. Why This Approach

### Why Fix CE's XPath Rather Than Duplicate the Defs

Duplicating projectile defs (creating new defs that inherit from the originals
with corrected values) would conflict with any other mod that patches the same
defNames. XPath correction is the minimal, conflict-free fix: it applies the
exact same values CE intended, just through a selector that actually matches.

If CE fixes the namespace in a future release, our ops silently fail on XPath
miss (the `VFECore.ExpandableProjectileDef` values will already be correct from
CE's own now-working patches). No conflict, no double-application.

### Why Not a C# Harmony Patch for the VEF Stat Hooks

A Harmony patch that re-injects VEF stat logic into CE's `Verb_MeleeAttackCE`
and `ProjectileCE` would provide exact stat fidelity. It would also:

- Require maintenance against two upstream codebases (CE and VEF)
- Risk transpiler conflicts with other mods patching the same methods
- Promote an XML-only compatibility patch to a C# assembly

The established CE convention -- demonstrated by the FiringFocus remap in
`Hediffs_Patch.xml` -- is to decompose VEF stats into CE-native equivalents
via XML. The approximation is imperfect but stable, maintainable, and
consistent with how CE itself solves the same category of problem.

### Why These Specific Stat Values

BladeFocus: `MeleeCooldownFactor: 0.5` is an exact 2x speed translation.
`MeleeHitChance: 5` and `MeleeCritChance: 0.5` are modest tier-2 bonuses.

ControlledFrenzy is strictly stronger on the melee axis (higher crit, higher
hit chance) while adding ranged capability -- consistent with tier 3 vs.
BladeFocus at tier 2. `MeleeCritChance: 1.0` is the primary damage amplifier
(CE crits deal 2x sharp). `AimingAccuracy` and `ReloadSpeed` match the
FiringFocus remap pattern.

### Why the Skeleton Gets Construct-Matching Values

CE treats summoned constructs as a category: Steel and Rock Constructs share
identical melee stat blocks. The skeleton is mechanically equivalent -- a
summoned, temporary, melee-only entity. Using the same values maintains
internal consistency. No carry or shooting stats are needed (manhunter-locked,
melee-only, 60000-tick lifespan).

---

## 5. Warlord Path Tier Summary

Post-patch status of the complete Warlord psycast path under CE:

| Psycast          | Tier | Intended Effect         | CE Status                  |
|------------------|------|-------------------------|----------------------------|
| SpeedBoost       | 1    | 3x MoveSpeed            | Works (vanilla stat)       |
| BladeFocus       | 2    | 2x melee attack speed   | **Fixed by this patch**    |
| FiringFocus      | 2    | 5x ranged attack speed  | Fixed by CE (Hediffs_Patch)|
| ControlledFrenzy | 3    | 2x melee + ranged dmg   | **Fixed by this patch**    |
| GuidedShot       | 3    | Never-miss + 2x range   | Fixed by CE (Hediffs_Patch)|

The Warlord path is now fully functional under CE. Tiers 1--3 all provide
meaningful mechanical benefits. The two fixes in this patch restore the
mid-tier melee buff (BladeFocus) and the capstone dual-mode buff
(ControlledFrenzy) that were previously no-ops.

---

## 6. Testing Notes

Load with CE + VPE + V2CEPatch active. All verification uses dev mode.

- **Eltex Cape**: Spawn `VPE_Apparel_EltexCape`. Confirm `ArmorRating_Sharp: 3`
  and `ArmorRating_Blunt: 2` in stat readout. Verify `Bulk: 5` / `WornBulk: 1.5`
  are still present from CE's patch.
- **Breath Weapons**: Inspect `VPE_FireBreath` def -- confirm `damageAmountBase: 12`,
  `speed: 53`. Inspect `VPE_IceBreathe` -- confirm `speed: 48`. Or enable XML
  loading log and verify no "patch operation did not find a node" warnings.
- **Skeleton**: Summon via Necromancy path. Confirm `MeleeDodgeChance: 0.19`,
  `MeleeCritChance: 0.22`, `MeleeParryChance: 0.09` in stat pane. Engage in
  melee and observe dodge/crit/parry rolls in combat log (CE verbose mode).
- **BladeFocus**: Add hediff `VPE_BladeFocus`. Confirm `MeleeCooldownFactor: x0.50`
  in stat inspector.
- **ControlledFrenzy**: Add hediff `VPE_ControlledFrenzy`. Confirm
  `MeleeCooldownFactor: x0.75`, `RangedCooldownFactor: x0.75`, and stat offsets
  in hediff tooltip. Verify crit rolls in melee combat log.
