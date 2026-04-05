# EndlessGrowth -- CE Skill Stat Extension Patch

| Field          | Value                                        |
|----------------|----------------------------------------------|
| **Mod**        | EndlessGrowth by Slime-Senpai                |
| **Steam ID**   | 2894401734                                   |
| **packageId**  | `SlimeSenpai.EndlessGrowth`                  |
| **Mod type**   | DLL (Harmony transpilers)                    |
| **Patch file** | `Patches/EndlessGrowth.xml`                  |

---

## 1. What Broke

EndlessGrowth removes RimWorld's hard skill level cap of 20. Its Harmony
transpilers replace `Mathf.Clamp(x, 0, 20)` with `Mathf.Max(x, 0)` and strip
the level-20 learning guards, allowing pawns to train skills to arbitrary
levels. The mod adds passive skill decay above 20 (`-12f * mult` per interval
tick) and superlinear XP curves to create a high-investment, diminishing-returns
progression above the vanilla ceiling.

Combat Extended defines two `StatDef`s that use `SkillNeed_Direct` -- a vanilla
class that maps skill levels to stat values via a flat list called
`valuesPerLevel`:

- **`ReloadSpeed`** -- 21 entries (indices 0-20), ranging from 0.75 to 1.50
- **`AimingDelayFactor`** -- 21 entries (indices 0-20), ranging from 1.25 to 0.75

`SkillNeed_Direct.ValueFor()` returns `valuesPerLevel[level]` when the level is
a valid index. When `level >= valuesPerLevel.Count`, it returns the last entry
in the list: `valuesPerLevel[Count - 1]`. This is the vanilla fallback behavior
and works fine when skills are capped at 20.

With EndlessGrowth active, this fallback produces a stat plateau. A level 100
shooter has ReloadSpeed 1.50 -- identical to a level 20 shooter. A level 100
shooter has AimingDelayFactor 0.75 -- identical to level 20. The entire
progression system above 20 yields zero combat benefit for these two stats.

ReloadSpeed is particularly consequential. It appears as a divisor in CE's
reload duration formula:

```
duration_ticks = ceil(seconds_to_ticks(reloadTime) * weaponReloadFactor / pawnReloadSpeed)
```

Higher ReloadSpeed means shorter reload times. At 1.50, a 4-second weapon
reloads in 2.67 seconds. The value flat-lining at 1.50 means 80 additional
skill levels of investment produce no reload improvement at all. This directly
contradicts EndlessGrowth's core design promise.

### Why Other CE Stats Are Unaffected

Not all CE skill stats break. `AimingAccuracy`, `MeleeCritChance`,
`MeleeParryChance`, and `UnarmedDamage` use `SkillNeed_BaseBonus`, which
computes values via formula:

```
baseValue + bonusPerLevel * level
```

This has no list-based clamping. These stats scale naturally with uncapped
levels, bounded only by their own `postProcessCurve` ceilings and `maxValue`
limits. Only the two `SkillNeed_Direct` stats -- ReloadSpeed and
AimingDelayFactor -- need intervention.

---

## 2. Design Problem and Options Considered

The constraint set is narrow. Two CE StatDefs use a lookup table that is too
short. The table must be extended for levels above 20 with values that provide
diminishing returns consistent with EndlessGrowth's exponential XP scaling and
CE's balance philosophy.

### Option A: Harmony Postfix on SkillNeed_Direct.ValueFor

Intercept `ValueFor()` at runtime to compute extended values via formula when
the skill level exceeds the list length.

Rejected. `SkillNeed_Direct` is a vanilla class, not a CE override.
Transpiling or postfixing it creates a cross-mod coupling surface: the patch
would need to survive updates to both RimWorld core (which owns the class) and
CE (which defines the stats). A Harmony hook also means shipping a compiled
assembly, adding version-coupling to specific RimWorld/CE builds.

### Option B: Replace SkillNeed_Direct with SkillNeed_BaseBonus

Swap the `skillNeedFactors` class on ReloadSpeed and AimingDelayFactor from
`SkillNeed_Direct` to `SkillNeed_BaseBonus`, which auto-scales.

Rejected. CE chose `SkillNeed_Direct` for these stats deliberately. The
hand-tuned per-level values allow non-linear progression curves that
`SkillNeed_BaseBonus` cannot reproduce. Levels 0-10 and 10-20 follow different
gradients in the original tables. Replacing the class would flatten the
sub-20 curve and alter balance for all players, not just EndlessGrowth users.

### Option C: postProcessCurve modification

Add or modify a `postProcessCurve` on these StatDefs to extrapolate beyond the
table's range.

Rejected. The `postProcessCurve` operates on the output of
`SkillNeed_Direct.ValueFor()`, not the input. Since `ValueFor()` already
flat-lines at the last entry for levels above 20, a postProcessCurve receives
the same input (1.50 for ReloadSpeed, 0.75 for AimingDelayFactor) for every
level above 20. There is no signal to curve-process.

### Option D (chosen): Extend valuesPerLevel to 101 entries

Replace each 21-entry `valuesPerLevel` list with a 101-entry list (indices
0-100). Levels 0-20 are unchanged. Levels 21-100 follow an exponential decay
curve that provides diminishing returns. Levels above 100 hit the vanilla
fallback and return the level-100 value.

This is the intended configuration mechanism. `SkillNeed_Direct` exists to be
configured via its list. Extending the list is the minimal, non-invasive fix.
No Harmony hooks, no stat worker overrides, no C# assembly.

---

## 3. Implementation

The entire patch is a single file: **`Patches/EndlessGrowth.xml`**.

### Patch Structure

The outer operation is `PatchOperationFindMod`, gated on both `Combat Extended`
AND `EndlessGrowth` being active. The patch is completely inert unless both mods
are loaded -- players running either mod alone are unaffected. Inside, a
`PatchOperationSequence` runs two `PatchOperationReplace` operations.

### Curve Design

Both stats use the same exponential decay model with shared decay constant
`k = 0.05`, chosen so the half-life is approximately 14 levels. Most of the
gain concentrates in the level 20-40 range, with returns tapering sharply
above 50. This mirrors EndlessGrowth's own XP scaling: the exponential cost of
reaching level 60+ is enormous, and the stat reward should reflect that.

#### ReloadSpeed Extension

Formula for levels 21-100:

```
f(L) = 1.50 + 0.50 * (1 - e^(-0.05 * (L - 20)))
```

| Level | ReloadSpeed | Reload Time (4s weapon) |
|-------|-------------|-------------------------|
| 0     | 0.750       | 5.33s                   |
| 20    | 1.500       | 2.67s                   |
| 30    | 1.697       | 2.36s                   |
| 40    | 1.816       | 2.20s                   |
| 60    | 1.932       | 2.07s                   |
| 100   | 1.991       | 2.01s                   |

The curve asymptotes toward 2.00. Levels 0-20 deliver a 2x improvement
(0.75 to 1.50). Levels 20-100 add only 1.33x more (1.50 to 1.991). The
level 20 to 30 window provides approximately 7.8% reload time reduction;
levels 30 to 100 provide approximately 8.7% total. Diminishing returns are
steep and intentional.

#### AimingDelayFactor Extension

Formula for levels 21-100:

```
g(L) = 0.75 - 0.25 * (1 - e^(-0.05 * (L - 20)))
```

| Level | AimingDelayFactor |
|-------|-------------------|
| 0     | 1.250             |
| 20    | 0.750             |
| 30    | 0.652             |
| 40    | 0.592             |
| 60    | 0.534             |
| 100   | 0.505             |

The curve asymptotes toward 0.50. Same decay constant, inverted direction.
A level 100 shooter's aim delay is 67% of a level 20 shooter's -- meaningful
but not game-breaking.

### Cap Rationale

**ReloadSpeed cap at 2.0.** A 4-second weapon at ReloadSpeed 2.0 reloads in
2.0 seconds -- still tactically significant. Even 8-second heavy weapons
retain 4-second-plus reload windows, preserving CE's suppression mechanics.
The cap aligns with CE's broader stat ceiling philosophy: `AimingAccuracy`
caps at 1.5 via code, `ReloadSpeed` caps at 2.0 via curve.

**AimingDelayFactor floor at 0.50.** Aim delay halves at most. This prevents
superhuman snap-aim behavior that would undermine CE's suppression and cover
systems.

**Conservative relative to other stats.** ReloadSpeed is the most restrained
extension because it directly multiplies DPS:

| Stat           | Level 20 to 40 Gain | Level 20 to 100 Gain | Cap        |
|----------------|---------------------|----------------------|------------|
| ReloadSpeed    | +21%                | +33%                 | 2.00       |
| AimingAccuracy | +22%                | +60%                 | 1.50 (code)|
| MeleeCritChance| +36%                | +89%                 | 0.80 (curve)|

### Op 1: ReloadSpeed (lines 22-151 of `Patches/EndlessGrowth.xml`)

```xml
<li Class="PatchOperationReplace">
    <xpath>Defs/StatDef[defName="ReloadSpeed"]/skillNeedFactors
           /li[@Class="SkillNeed_Direct"]/valuesPerLevel</xpath>
    <value>
        <valuesPerLevel>
            <!-- Level 0-20: unchanged from CE defaults -->
            <li>0.75</li>
            ...
            <li>1.5</li>
            <!-- Level 21-100: exponential decay curve -->
            <li>1.524</li>
            ...
            <li>1.991</li>
        </valuesPerLevel>
    </value>
</li>
```

The `PatchOperationReplace` targets the `valuesPerLevel` node specifically,
not the enclosing `SkillNeed_Direct` or the `StatDef`. This preserves all
other StatDef properties (label, description, `postProcessCurve`, `maxValue`,
skill association) untouched.

Levels 0-20 are reproduced verbatim from CE's original definition. This is
required because `PatchOperationReplace` swaps the entire target node. Using
`PatchOperationAdd` to append entries is not possible here:
`SkillNeed_Direct.ValueFor()` uses list indexing, so new entries must occupy
exact index positions 21-100, not be appended to an unordered collection.

### Op 2: AimingDelayFactor (lines 156-285 of `Patches/EndlessGrowth.xml`)

Identical structure, targeting `StatDef[defName="AimingDelayFactor"]`. Levels
0-20 reproduced from CE defaults. Levels 21-100 follow the inverted curve.

### Level 101+ Behavior

`SkillNeed_Direct.ValueFor()` returns `valuesPerLevel[Count - 1]` for any
level at or above the list length. With 101 entries, levels 101 and above
return the level-100 value (1.991 for ReloadSpeed, 0.505 for
AimingDelayFactor). This is the same vanilla fallback that previously
triggered at level 21. The asymptotic curve means the level-100 value is
already within 0.5% of the theoretical cap, so the fallback is seamless.

### Dependency on EndlessGrowth Decay

The curve design assumes EndlessGrowth's passive decay (`-12f * mult` above
level 20) prevents pawns from permanently sustaining extreme skill levels
without ongoing training investment. Even if decay were disabled, the 2.0 and
0.50 caps provide hard safety bounds that prevent balance-breaking stat values
regardless of skill level.

---

## 4. Why This Fix Beats Alternatives

**Pure XML, zero cross-mod coupling.** The patch modifies CE StatDef XML via
XPath. It does not hook, transpile, or postfix any C# method. It ships no
compiled assembly. It has no version-coupling to specific CE or RimWorld
builds. When CE updates its StatDefs, only the levels-0-20 values may need
verification -- and those are already reproduced verbatim from CE's own
definitions.

**Extends the intended configuration surface.** `SkillNeed_Direct` exists
specifically to be configured via `valuesPerLevel`. The 21-entry list was
sized for vanilla's 0-20 skill range. Extending it to 101 entries is the
mechanism the class was designed for. No behavioral assumptions are violated.

**Preserves sub-20 balance exactly.** Levels 0-20 are byte-identical to CE's
original values. Players without EndlessGrowth, or EndlessGrowth players
whose skills are below 20, see zero stat changes.

**Diminishing returns match the XP curve.** EndlessGrowth's exponential XP
scaling means reaching level 40 costs orders of magnitude more XP than
reaching level 20. The exponential decay stat curve mirrors this: the marginal
stat gain per level drops in proportion to the marginal XP cost. Investment
always yields some return, but the efficiency frontier is steep.

**No conflict surface.** The patch targets two specific `valuesPerLevel` nodes
via narrow XPath selectors. No other mod patches these nodes. The
`PatchOperationFindMod` gate ensures the patch only fires when both mods are
present. There is no load-order sensitivity beyond the standard requirement
that V2CEPatch loads after CE and EndlessGrowth.

**Single file, auditable in full.** `Patches/EndlessGrowth.xml` is 290 lines,
most of which are the 202 list entries (101 per stat). The design is
immediately legible: two lookup tables extended with precomputed curve values.
No runtime behavior, no hidden state, no initialization order dependencies.
