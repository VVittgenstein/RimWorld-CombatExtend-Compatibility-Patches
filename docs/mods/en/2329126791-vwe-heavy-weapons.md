# VWE Heavy Weapons

VWE Heavy Weapons adds five crew-served weapons whose identity rests on two mechanics that Combat Extended silently destroys: deterministic per-shot weapon degradation and guided multi-target missile bursts. This patch restores both systems through three Harmony postfixes on `Verb_LaunchProjectileCE`, reading the original mod's data structures at runtime so that VEF's in-game settings sliders continue to function without any additional XML changes.


## What Breaks Under CE

Vanilla Weapons Expanded - Heavy Weapons ships five weapons -- autocannon, handheld mortar, heavy flamer, swarm missile launcher, and uranium slug rifle -- that share two mechanics implemented entirely in VEF (Vanilla Expanded Framework) code. Both mechanics fail under CE, and a third visual feature is replaced outright.

### Per-Shot Weapon Degradation

VEF's `Verb_Shoot.TryCastShot()` checks whether the firing weapon carries a `HeavyWeapon` modExtension with a positive `weaponHitPointsDeductionOnShot` value. On every successful shot, it deducts that many hit points from the weapon. When HP reaches zero, the weapon is destroyed mid-combat and the pawn's job queue is cleared.

CE replaces `Verb_Shoot` with `Verb_ShootCE`, which extends a completely different verb chain. `Verb_ShootCE` has no knowledge of VEF's `HeavyWeapon` extension and never calls the HP deduction logic. The result is that all five heavy weapons become indestructible -- the mortar that should break after five shots lasts forever.

| Weapon | HP / Shot | Default HP | Intended Lifetime |
|---|---|---|---|
| Autocannon | 1 | 100 | ~100 shots |
| Handheld Mortar | 20 | 100 | ~5 shots |
| Heavy Flamer | 10 | 100 | ~10 bursts |
| Swarm Missile Launcher | 5 | 100 | ~20 shots |
| Uranium Slug Rifle | 10 | 100 | ~10 shots |

These values define the tactical identity of each weapon. The handheld mortar is balanced around being a five-use disposable siege breaker. Without degradation, it becomes a permanent artillery piece with no resource pressure.

### Guided Missile Tracking

The swarm missile launcher fires 8-round bursts where each rocket acquires a separate target. VEF implements this through `CompGuidedProjectile` with `selectDifferentTargets=true`. The comp steers projectiles after launch using `FieldRef<Projectile, Vector3>` accessors that read and write destination fields on RimWorld's `Projectile` class.

Under CE, projectiles are instances of `ProjectileCE`, which extends `ThingWithComps` rather than `Projectile`. The VEF comp's field accessors target a class that `ProjectileCE` does not inherit from, so the reflection calls either fail silently or never fire. All eight rockets fly toward the original target, eliminating the weapon's signature spread behavior.

### Flame Cone Visual (Accepted Trade)

The heavy flamer uses VEF's `FlamethrowProjectile`, which renders an expanding cone of flame particles on each tick. CE replaces this with `Bullet_Flamethrower_Prometheum` and its prometheum fuel system. The expanding cone visual is lost, but the gameplay role -- area denial via fire -- is preserved through CE's own flamethrower mechanics.

Restoring the VEF cone would require building a hybrid class that bridges `ProjectileCE`'s ballistic trajectory system with VEF's tick-based cone expansion. This is high complexity for a purely cosmetic difference, and CE's prometheum system already provides functionally equivalent area coverage.


## Design Problem and Alternatives Considered

The core design question was: how do you replicate VEF mechanics that depend on `Verb_Shoot` when CE replaces the entire verb chain?

Three approaches were evaluated:

### Option A: Transpiler on Verb_ShootCE

Inject IL instructions into `Verb_ShootCE.TryCastShot` to call VEF's degradation logic directly. This was rejected for several reasons:

- `Verb_ShootCE.TryCastShot` is a large method with multiple early-return paths, ammo checks, and recoil calculations. The IL layout shifts between CE versions as the CE team refactors internals.
- A transpiler targeting specific IL offsets would need to be re-validated against every CE release. A broken transpiler does not fail gracefully -- it corrupts the method body, producing hard-to-diagnose crashes rather than silent no-ops.
- The degradation logic also needs the same hook point for the missile retarget patch. Two transpilers on the same method compound the fragility problem.

### Option B: CE's Built-In weaponDeteriorationChance

CE provides its own weapon deterioration system via `weaponDeteriorationChance` on projectile defs, using `Rand.Chance` per shot. This was rejected for two reasons:

1. **Probabilistic vs. deterministic.** VEF's system is exact: the mortar loses 20 HP per shot, period. CE's system rolls a random chance each shot. "Breaks after exactly 5 shots" is a different gameplay contract than "breaks after statistically around 5 shots." The deterministic model is what the mod author designed around.

2. **Definition site mismatch.** CE defines deterioration on the projectile def; VEF defines it on the weapon's modExtension. Mapping one to the other would require maintaining a parallel lookup table and would break whenever VEF's `DefsAlterer` applies runtime overrides from mod settings.

### Option C: Postfix on Verb_LaunchProjectileCE (Chosen)

`Verb_LaunchProjectileCE` is `Verb_ShootCE`'s parent class and the actual site where `TryCastShot` and `SpawnProjectile` execute. A postfix on these methods runs after CE's own logic completes, with full access to the verb instance, the spawned projectile, and the equipment source. This approach:

- Avoids modifying CE's IL (no transpiler fragility)
- Reads VEF's own data structures via reflection (no parallel definitions)
- Fires after VEF's `DefsAlterer` has applied mod-settings overrides (settings sliders just work)
- Can be conditionally loaded based on mod presence (zero overhead when VWE is absent)

The postfix approach also scales cleanly to the missile problem. The same `TryCastShot` postfix hook serves both durability loss and retargeting, and a second postfix on `SpawnProjectile` handles homing injection. All three patches share the same hook surface with no conflicts.

### Why Not Patch VEF Directly?

An alternative to patching CE's verb chain would be to patch VEF's `Verb_Shoot.TryCastShot` so it also fires under CE's verb hierarchy. This was rejected because VEF's verb is never instantiated when CE is active -- CE's def patching replaces weapon verbs wholesale. Patching a method that is never called accomplishes nothing.


## Implementation

All three patches live under `Source/V2CEPatch/Harmony/` and are conditionally applied from the bootstrap in `Source/V2CEPatch/V2CEPatchMod.cs`.

### Bootstrap: Conditional Loading

```csharp
// Source/V2CEPatch/V2CEPatchMod.cs, lines 25-31
if (ModDetection.VWEHeavyWeaponsActive)
{
    harmony.CreateClassProcessor(typeof(Patch_VerbLaunchProjectileCE_DurabilityLoss)).Patch();
    harmony.CreateClassProcessor(typeof(Patch_VerbLaunchProjectileCE_SwarmMissileHoming)).Patch();
    harmony.CreateClassProcessor(typeof(Patch_VerbLaunchProjectileCE_SwarmMissileRetarget)).Patch();
    Log.Message("[V2CEPatch] Applied VWE Heavy Weapons patches");
}
```

Mod detection is handled by `Source/V2CEPatch/Utility/ModDetection.cs`, line 16:

```csharp
VWEHeavyWeaponsActive = ModsConfig.IsActive("VanillaExpanded.VWEHW");
```

All three class processors are only instantiated and patched when VWE Heavy Weapons is in the active mod list. When it is absent, no Harmony patches are registered and no reflection lookups are attempted.

### Patch 1: Durability Loss

**File:** `Source/V2CEPatch/Harmony/Patch_VerbLaunchProjectileCE_DurabilityLoss.cs`

This postfix on `Verb_LaunchProjectileCE.TryCastShot` replicates VEF's deterministic degradation model.

**Initialization (lines 23-29):** On first invocation, the patch resolves the VEF type and field via reflection:

```csharp
heavyWeaponType = AccessTools.TypeByName("VEF.Weapons.HeavyWeapon");
if (heavyWeaponType != null)
    hpDeductionField = AccessTools.Field(heavyWeaponType, "weaponHitPointsDeductionOnShot");
initialized = true;
```

The `AccessTools.TypeByName` call avoids a hard assembly reference to VEF. If VEF is not loaded (which should not happen since VWE depends on VEF, but defensive coding costs nothing), both fields remain null and every subsequent call exits at the null check on line 31.

**HP deduction (lines 36-39):** On each successful shot, the patch reads the deduction value from the weapon's modExtension and subtracts it from equipment HP:

```csharp
int deduction = (int)hpDeductionField.GetValue(ext);
if (deduction <= 0) return;

equipment.HitPoints -= deduction;
```

The deduction value is read at firing time, not cached at startup. This means VEF's `DefsAlterer` runtime overrides -- which apply when a player changes per-weapon HP deduction sliders in mod settings -- take effect immediately without requiring a game restart or additional patch hooks.

**Destruction (lines 40-48):** When HP drops to zero or below:

```csharp
if (equipment.HitPoints <= 0)
{
    equipment.HitPoints = 0;
    equipment.Destroy(DestroyMode.Vanish);
    if (__instance.CasterIsPawn)
    {
        __instance.CasterPawn.jobs.StopAll(false, true);
    }
}
```

`DestroyMode.Vanish` removes the weapon without spawning a damaged remnant or leaving debris, matching VEF's original behavior. `StopAll(false, true)` clears the pawn's job queue so they stop attempting to fire a weapon that no longer exists.

### Patch 2: Swarm Missile Homing

**File:** `Source/V2CEPatch/Harmony/Patch_VerbLaunchProjectileCE_SwarmMissileHoming.cs`

This postfix on `Verb_LaunchProjectileCE.SpawnProjectile` (resolved via manual `TargetMethod()` on line 18-21) enables CE's native homing trajectory system for swarm missile projectiles.

**Target method resolution (lines 18-21):**

```csharp
static MethodBase TargetMethod()
{
    return AccessTools.Method(typeof(Verb_LaunchProjectileCE), "SpawnProjectile");
}
```

`SpawnProjectile` is used instead of `TryCastShot` because the postfix needs access to the spawned `ProjectileCE` instance via `__result`. By the time `TryCastShot` returns, the projectile reference is no longer directly available.

**Weapon filter (line 28):** The postfix only fires for the swarm missile launcher:

```csharp
if (equipment?.def.defName != "VWE_Gun_SwarmMissileLauncher") return;
```

**Homing configuration (lines 43-47):**

```csharp
__result.homingAcceleration = 0.15f;

if (trajectoryWorkerField != null && homingWorkerInstance != null)
{
    trajectoryWorkerField.SetValue(__result, homingWorkerInstance);
}
```

Two things happen: the projectile's `homingAcceleration` is set to `0.15` rad/tick, and CE's `HomingBulletTrajectoryWorker` singleton is injected via the `forcedTrajectoryWorker` field.

CE's `HomingBulletTrajectoryWorker` provides smooth velocity steering using `Vector3.RotateTowards`. After a 3-tick scatter phase (built into CE's trajectory worker), homing strength linearly ramps from 0 to 1 over 8 ticks. At 0.15 rad/tick maximum turn rate, a rocket can course-correct approximately 8.6 degrees per tick -- sufficient to track walking pawns at 20+ cell distances but not so aggressive that rockets snap-track to targets instantaneously.

**Cross-patch communication (line 50):** The spawned projectile reference is stored in a static field:

```csharp
lastSpawnedMissile = __result;
```

This reference is consumed by the retarget patch (Patch 3) in the same tick. Since `SpawnProjectile` is called from within `TryCastShot` on the same thread within a single verb firing cycle, there is no race condition. The retarget postfix nulls the field after reading it (line 25 of the retarget patch), ensuring stale references do not leak across burst boundaries or between different weapons.

### Patch 3: Swarm Missile Retargeting

**File:** `Source/V2CEPatch/Harmony/Patch_VerbLaunchProjectileCE_SwarmMissileRetarget.cs`

This postfix on `Verb_LaunchProjectileCE.TryCastShot` distributes rockets across distinct targets during a burst, replicating VEF's `selectDifferentTargets` behavior.

**Execution order (line 12):**

```csharp
[HarmonyAfter("v2modpack.cepatch.durability")]
```

The `HarmonyAfter` attribute ensures this postfix runs after the durability postfix. Ordering matters because the durability patch may destroy the weapon mid-burst; if that happens, subsequent burst shots will not fire and this patch will not execute, which is the correct behavior.

**Target tracking (line 15):**

```csharp
private static readonly Dictionary<Thing, HashSet<Thing>> launcherAssignedTargets = new();
```

The dictionary maps each launcher (by `Thing` identity) to the set of targets already assigned during the current burst. This handles the edge case of multiple pawns firing swarm missile launchers simultaneously -- each launcher tracks its own assignments independently.

**Retargeting logic (lines 37-44):**

```csharp
float searchRange = Mathf.Clamp(
    __instance.verbProps.range * 0.66f, 2f, 20f);

IAttackTarget altTarget = AttackTargetFinder.BestAttackTarget(
    (IAttackTargetSearcher)launcher,
    TargetScanFlags.NeedReachable | TargetScanFlags.NeedThreat,
    x => x is Thing t && !assigned.Contains(t),
    0f, searchRange);
```

When the current rocket's intended target is already in the assigned set, the patch searches for an alternative using RimWorld's `AttackTargetFinder.BestAttackTarget`. The search radius is clamped to `range * 0.66` within `[2, 20]` cells, mirroring VEF's original search parameters. The predicate excludes any target already assigned in this burst.

If no alternative target is found, the rocket keeps its original target. This gracefully degrades: when there are fewer valid targets than rockets in a burst, multiple rockets converge on the same target rather than failing to fire.

**Burst cleanup (lines 55-57):**

```csharp
int burstShotsLeft = (int)(AccessTools.Field(typeof(Verb), "burstShotsLeft")?.GetValue(__instance) ?? 0);
if (burstShotsLeft <= 1)
    launcherAssignedTargets.Remove(launcher);
```

When the burst completes (one or zero shots remaining), the tracking state for that launcher is removed entirely. This prevents the dictionary from growing unboundedly across multiple engagements and ensures the next burst starts with a clean assignment slate. The `burstShotsLeft` field is read via reflection from the base `Verb` class because it is a protected field not exposed through any public API on `Verb_LaunchProjectileCE`.


## Numerical Decisions

### Degradation Values: Preserved Without Rebalancing

The original VEF HP deduction values are used as-is under CE. No CE-specific rebalancing was applied, despite CE's ammo system adding a parallel resource cost (players must craft ammunition in addition to the weapon degrading per shot).

This was a deliberate choice. Both cost layers -- ammo crafting and weapon HP -- apply simultaneously under the patched configuration, making the economy strictly harsher than either system alone. However:

1. The combined cost is self-balancing: heavy weapons already demand significant ammo investment under CE, so the HP cost functions as a soft cap on sustained use rather than a primary resource gate.
2. VEF provides per-weapon HP deduction sliders in its mod settings UI. Players who find the combined cost excessive can reduce or zero out the degradation for individual weapons without touching CE configuration. This patch preserves that tuning surface by reading deduction values at runtime.

### Homing Acceleration: 0.15 rad/tick

The value `0.15` was chosen to sit in the middle of CE's own homing range. CE's `CustomWeaponTraitDef` homing implementations use values between 0.05 (gentle course correction) and 0.30 (aggressive tracking). At 0.15:

- Rockets can track a walking pawn at 20+ cells, consistent with the weapon being a purpose-built guided munition system
- Rockets cannot snap-track to a target that moves perpendicular to the flight path at close range, preventing the weapon from being an unavoidable death sentence
- The 3-tick scatter phase built into `HomingBulletTrajectoryWorker` creates a natural spread pattern during the initial launch, visually resembling the original VEF swarm behavior


## XML Patch File

**File:** `Patches/VWEHeavyWeapons.xml`

The XML patch file is deliberately minimal. It contains only a `PatchOperationFindMod` wrapper with comments documenting the C# patches:

```xml
<Operation Class="PatchOperationFindMod">
    <mods>
        <li>Vanilla Weapons Expanded - Heavy Weapons</li>
    </mods>
    <match Class="PatchOperationSequence">
        <operations>
            <!-- VWE Heavy Weapons CE repairs are implemented via Harmony patches:
                 - Patch_VerbLaunchProjectileCE_DurabilityLoss
                 - Patch_VerbLaunchProjectileCE_SwarmMissileHoming
                 - Patch_VerbLaunchProjectileCE_SwarmMissileRetarget
                 See Source/V2CEPatch/Harmony/ for implementation. -->
        </operations>
    </match>
</Operation>
```

No XML-level weapon def adjustments are needed because CE's existing manual patch for VWE Heavy Weapons already handles weapon stat conversion, ammo binding, and projectile replacement. Critically, CE's manual patch preserves the `HeavyWeapon` modExtension data on weapon defs -- it does not strip the extension during conversion. This means the durability postfix can read `weaponHitPointsDeductionOnShot` directly from the patched defs without needing to re-inject the extension via XML.


## No Additional Dependencies

This patch introduces no new ThingDefs, AmmoDefs, StatDefs, or any other def types. It creates no new projectile classes and defines no custom trajectory workers. All three postfixes operate by reading existing VEF data structures and leveraging CE's existing `HomingBulletTrajectoryWorker` infrastructure.

The only runtime dependency beyond CE and VEF is the reflection lookup for `VEF.Weapons.HeavyWeapon`, which is guaranteed to be available when VWE Heavy Weapons is active (VWE has a hard dependency on VEF).


## Summary of Changes

| Mechanic | Original System | CE Breakage | Patch Approach |
|---|---|---|---|
| Weapon degradation | VEF `Verb_Shoot` reads `HeavyWeapon.weaponHitPointsDeductionOnShot` | `Verb_ShootCE` has no knowledge of the extension | Postfix on `Verb_LaunchProjectileCE.TryCastShot` replicates exact deduction logic |
| Guided missiles | VEF `CompGuidedProjectile` steers `Projectile` subclasses | `ProjectileCE` does not extend `Projectile` | Postfix on `SpawnProjectile` injects CE's `HomingBulletTrajectoryWorker` |
| Target distribution | VEF `CompGuidedProjectile.selectDifferentTargets` | Comp cannot function on `ProjectileCE` | Postfix on `TryCastShot` maintains per-burst assignment tracking |
| Flame cone visual | VEF `FlamethrowProjectile` tick-based cone | CE replaces with `Bullet_Flamethrower_Prometheum` | Accepted trade; gameplay role preserved |
