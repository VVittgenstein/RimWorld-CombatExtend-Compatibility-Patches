# Slo' Turret Collection -- CE Compatibility Patch

| Field          | Value                                                     |
|----------------|-----------------------------------------------------------|
| **Mod**        | Slo' turret collection                                    |
| **Steam ID**   | 3038833232                                                |
| **packageId**  | `slo_s_libary.turretcollection`                           |
| **Mod type**   | C# assembly (`slo_s_Libary.dll`) + XML defs + VEF dependency |
| **Patch files**| `Patches/SloTurretCollection.xml` (1346 lines), `Defs/SloTurretCollection/` (6 ammo definition files, 1620 lines) |
| **RimWorld**   | 1.5+                                                      |

---

This is the largest single-mod patch in the V2CEPatch set, converting 28 turret
guns, 3 artillery weapons, 2 wearable turret apparels, and 8 custom damage
types across 7 weapon families. The mod's mixture of custom C# building
classes, exotic projectile types, and a false-positive match against CE's
built-in mod patches creates a situation where zero turrets function under CE
without intervention. The patch file defines full CE weapon conversions for
every turret, introduces 16 custom ammo sets, 4 laser beam projectiles, 10
explosive projectiles, and crafting recipes -- all while deliberately preserving
4 vanilla projectile classes whose exotic payload mechanics define their
turrets' identity.

---

## 1. What Broke

Three independent failures cascade to leave every turret in the collection
non-functional under Combat Extended.

### Gap 1: False-Positive Mod Patch Match

CE ships a `ModPatches/Turret Collection/` folder containing patches for a
completely different mod -- one using `TC_`/`TCEE_`-prefixed defs from an
unrelated "Turret Collection" by a different author. CE's `GunAutoPatcher`
checks for the existence of this folder at startup. When it finds the folder,
it assumes patches exist for any mod whose name matches, and skips all
auto-patching for Slo's turrets.

The result: CE's fallback autopatcher never fires. No `Verb_ShootCE`
replacement. No ammo binding. Every turret fires vanilla hitscan projectiles
with no CE armor penetration, no suppression, no ammo consumption, and no
ballistic arc calculation.

### Gap 2: EnergyTurret Class Conflict

Ten turrets use `slo_s_Libary.EnergyTurret` as their building class. This
class extends `Building_TurretGun` and overrides `Tick()` to manage a
charge/discharge energy cycle via `CompRefuelable` and a custom
`Comp_EnergyChargeable`.

CE replaces `Building_TurretGun` with `Building_TurretGunCE`. Because
`EnergyTurret` hardcodes inheritance from the vanilla class, its `base.Tick()`
call routes to `Building_TurretGun.Tick()` -- bypassing CE's ammo consumption,
reload state machine, fire-mode switching, and magazine tracking entirely. The
turrets appear to work but operate completely outside CE's combat pipeline.

### Gap 3: Custom Projectile Class Incompatibilities

Multiple turrets fire projectiles whose C# classes extend vanilla
`Bullet` or `Projectile` instead of `BulletCE`/`ProjectileCE`:

| Class                                          | Base class             | Turrets affected |
|------------------------------------------------|------------------------|:----------------:|
| `slo_s_Libary.projectiles.LaserBeamProjectile` | `Projectile`           | 4                |
| `ProjectileMultiple_Explosive`                  | `Projectile`           | 2                |
| `PrismProjectile`                               | `Bullet`               | 1                |
| `LongProjectile`                                | `Bullet`               | 1                |
| `Projectile_ClusterMunition`                    | `Projectile_Explosive` | 1                |

These projectiles use `Projectile.Launch()` and `Projectile.Impact()` -- vanilla
methods that CE's Harmony transpilers expect to be replaced by their CE
equivalents. The projectiles fly with vanilla hitscan mechanics, deal flat
damage ignoring CE armor calculations, and generate no suppression.

---

## 2. Design Problem and Options Considered

### Option A: Enable CE's GunAutoPatcher

Rejected. The autopatcher produces generic conversions with no awareness of
weapon role or damage profile. It cannot handle the `EnergyTurret` building
class, cannot convert custom projectile classes, and would assign generic ammo
sets that delete all 8 custom damage types (`STC_ChargedBomb`,
`STC_ThermobaricBomb`, `STC_Prism`, `STC_SmiteBeam`, `STC_BleedingBullet`,
etc.).

### Option B: Harmony Patch EnergyTurret

Rejected. `EnergyTurret` manages energy through `CompRefuelable` and
`Comp_EnergyChargeable`. Changing its base class at runtime would require
replacing its energy management logic. CE already provides
`Building_LaserGunCE` which extends `Building_TurretGunCE` and adds equivalent
charge/drain logic via `CompPowerTrader.PowerNet.CurrentStoredEnergy()`. The
existing CE class requires only XML patching.

### Option C (Chosen): Family-Based Manual Conversion

Classify all turrets into 7 families based on building class, projectile class,
and damage pipeline. Apply a tailored conversion strategy to each family.
This is the most labor-intensive option but the only one that correctly handles
all 7 archetypes while preserving every custom damage type and exotic mechanic.

---

## 3. Implementation

### Patch File: `Patches/SloTurretCollection.xml`

The entire patch is a single `PatchOperationFindMod` block gated on
`Slo' turret collection`, containing a `PatchOperationSequence` with 8
sections organized by weapon family. Total: 1346 lines, ~60 individual
patch operations.

### Def Files: `Defs/SloTurretCollection/`

| File                       | Lines | Content                                         |
|----------------------------|------:|-------------------------------------------------|
| `Ammo_STC_Artillery.xml`  |   359 | HeavyShell (3 variants), ClusterMissile          |
| `Ammo_STC_Ballistic.xml`  |   356 | Railgun (2 variants), AcceleratorRound (2 variants) |
| `Ammo_STC_Energy.xml`     |   122 | Shared EnergyCore AmmoDef, 5 AmmoSets for vanilla-preserved turrets |
| `Ammo_STC_Explosive.xml`  |   443 | ChargedBomb, SmiteCharge, Rocket, Thermobaric, IncendiaryRocket |
| `Ammo_STC_Laser.xml`      |   120 | 4 LaserBeamDefCE projectiles, 4 AmmoSets         |
| `Ammo_STC_Misc.xml`       |   220 | IncendiaryFuel, FuseCharge, Canister              |

---

### Section 1: EnergyTurret Building Conversions (Lines 8-176)

All 10 `EnergyTurret` buildings are converted to
`CombatExtended.Lasers.Building_LaserGunCE` via a 5-operation pattern per
turret:

1. `PatchOperationAttributeSet` -- set def `Class` attribute to `Building_LaserGunDef`
2. `PatchOperationReplace` -- replace `thingClass` with `Building_LaserGunCE`
3. `PatchOperationAdd` -- add `beamPowerConsumption` for energy economy
4. `PatchOperationRemove` -- remove `CompProperties_Refuelable`
5. `PatchOperationRemove` -- remove `Comp_EnergyChargeable`

**Why Building_LaserGunCE:** This class extends `Building_TurretGunCE`, which
solves the base-class inheritance problem. It adds charge/drain logic that
reads from `CompPowerTrader.PowerNet.CurrentStoredEnergy()` and only permits
firing when sufficient charge is stored. This is functionally equivalent to
Slo's `EnergyTurret` but built on CE's turret foundation -- gaining ammo
consumption, reload, fire-mode switching, and magazine tracking.

**Energy Economy Mapping:**

| Turret defName                 | beamPowerConsumption | Role                        |
|--------------------------------|---------------------:|-----------------------------|
| `STC_Turret_Tesla`             |                  833 | Mid-tier EMP chain          |
| `STC_Turret_Smite`             |                 4167 | Endgame cascade beam        |
| `STC_Turret_Beamlance`         |                  833 | Precision sniper laser      |
| `STC_Turret_Heavenly_Needle`   |                 1500 | Orbital bombardment         |
| `STC_Turret_prism`             |                 1250 | Split-beam AoE              |
| `STC_Turret_Arc_Accelerator`   |                    4 | Rapid-fire accelerator      |
| `STC_TurretMoonBeam`           |                    2 | Sustained suppression beam  |
| `STC_Turret_Rupturer`          |                 1500 | Spatial distortion cannon   |

Values are derived from the original energy economy:
`beamPowerConsumption = basePower x TicksPerFuel x fuelPerShot / 60`.
MoonBeam and Arc Accelerator have low values because they fire many shots
per cycle; Smite has the highest because a single beam draws enormous power.

---

### Section 2: Family A -- Standard Ballistic (Lines 178-435)

Seven turrets whose vanilla defs use `Building_TurretGun` and fire standard
`Bullet`-class projectiles. These are the most straightforward conversions:
each gun def gets `PatchOperationMakeGunCECompatible` binding to existing CE
NATO ammo sets.

| Turret Gun defName         | CE AmmoSet                 | Caliber     | Burst | Mag  | Range |
|----------------------------|----------------------------|-------------|------:|-----:|------:|
| `STC_Gun_Jumbo`            | `AmmoSet_762x51mmNATO`     | 7.62mm NATO |    10 |  300 |    45 |
| `STC_Gun_Bolter`           | `AmmoSet_20x102mm`         | 20x102mm    |     1 |   60 |    55 |
| `STC_Gun_Shotgun`          | `AmmoSet_12Gauge`          | 12 gauge    |     6 |   60 |    20 |
| `STC_Gun_SpeewerTurret`    | `AmmoSet_762x51mmNATO`     | 7.62mm NATO |    20 |  600 |    45 |
| `STC_Gun_PaladinTurret`    | `AmmoSet_20x102mm`         | 20x102mm    |     3 |   90 |    55 |
| `STC_Gun_Mini_Spiner`      | `AmmoSet_556x45mmNATO`     | 5.56mm NATO |    10 |  120 |    30 |
| `STC_Gun_Deserter`         | `AmmoSet_762x51mmNATO`     | 7.62mm NATO |    30 |  600 |    50 |

No custom ammo defs are introduced for Family A. All turrets consume from the
player's existing NATO ammunition stockpiles. All use `recoilPattern: Mounted`
and `RangedWeapon_Cooldown: 0.36`. Mass ranges from 4 (Mini Spiner) to 25
(Deserter); ShotSpread from 0.07 (Paladin) to 0.30 (Shotgun); AI modes split
between SuppressFire for volume-of-fire turrets and AimedShot for precision
platforms (Bolter, Paladin).

---

### Section 3: Family B -- VEF Special Projectile (Lines 437-657)

Six turrets whose projectiles originate from Vanilla Expanded Framework or use
exotic delivery mechanics. Each requires a custom AmmoSet mapping to its
original projectile def, with a new AmmoDef representing the physical ammo
item.

| Turret Gun defName       | CE AmmoSet                      | Custom Ammo Item             | Projectile Preserved? |
|--------------------------|---------------------------------|------------------------------|-----------------------|
| `STC_Gun_Scorch`         | `AmmoSet_STC_IncendiaryFuel`    | `Ammo_STC_IncendiaryFuel`   | Yes -- VEF flamethrower |
| `STC_Gun_TurretLancer`   | `AmmoSet_STC_Railgun`           | Railgun Standard/AP          | No -- new CE projectile |
| `STC_Gun_TeslaTurret`    | `AmmoSet_STC_TeslaCharge`       | `Ammo_STC_EnergyCore`       | Yes -- VEF TeslaProjectile |
| `STC_Gun_FuseTurret`     | `AmmoSet_STC_FuseCharge`        | `Ammo_STC_FuseCharge`       | Yes -- VEF expandable |
| `STC_Gun_WatchGuard`     | `AmmoSet_STC_Canister`          | `Ammo_STC_Canister`         | Yes -- VEF shrapnel   |
| `STC_Gun_HellFlare`      | `AmmoSet_STC_IncendiaryRocket`  | `Ammo_STC_IncendiaryRocket` | No -- new CE explosive |

**Lancer Railgun** receives full CE projectile conversion with two ammo
variants:

| Variant           | Damage | Sharp AP | Blunt AP | Speed |
|-------------------|-------:|---------:|---------:|------:|
| Railgun Standard  |     35 |       40 |      100 |   200 |
| Railgun AP        |     30 |       55 |      120 |   200 |

Speed 200 is the fastest projectile in the patch set, reflecting
electromagnetic acceleration. AP sacrifices 14% damage for 38% more
penetration.

**HellFlare** uses `Verb_ShootMortarCE` with `indirectFirePenalty: 0.2`,
firing 20-round bursts of incendiary rockets at range 120.

**Tesla chain-bounce** is preserved by keeping the original
`STC_Bullet_TeslaCoil` projectile. `TeslaProjectile.Impact()` handles
chain logic independently of the verb class, so converting the verb to
`Verb_ShootCE` does not break the EMP cascade.

---

### Section 4: Family C -- Laser Beam (Lines 659-820)

Four turrets that originally used `slo_s_Libary.projectiles.LaserBeamProjectile`.
All are converted to `CombatExtended.Lasers.LaserBeamCE` projectiles defined
in `Defs/SloTurretCollection/Ammo_STC_Laser.xml`. All four consume
`Ammo_LaserChargePack` (CE's existing laser ammo) -- no new AmmoDefs needed.

| Turret Gun defName  | Projectile defName       | Damage | Sharp AP | Color          | Burst | Mag   |
|---------------------|--------------------------|-------:|---------:|----------------|------:|------:|
| `STC_Gun_Beamlance` | `Bullet_STC_Beamlance`   |     30 |       35 | (255,100,100)  |     1 |   100 |
| `STC_Gun_Prism`     | `Bullet_STC_PrismBeam`   |     20 |       20 | (200,100,255)  |     1 |   100 |
| `STC_Gun_MoonBeam`  | `Bullet_STC_MoonBeam`    |      1 |        5 | (200,200,255)  |   100 | 1,000 |
| `STC_Gun_Smite`     | `Bullet_STC_SmiteBeam`   |     40 |       45 | (100,200,255)  |     1 |   100 |

All projectiles use `LaserBeamDefCE` def class, `BaseLaserBullet` parent, and
`armorPenetrationBlunt: 0.001` (lasers transfer near-zero kinetic energy).

**MoonBeam** is the suppression weapon: 1 damage per hit but 100-round bursts
at 2 ticks between shots, range 120. At 1 damage with Sharp AP 5, it barely
scratches armor but generates massive CE suppression through volume of fire.

**Smite** is the endgame weapon: 40 damage, Sharp AP 45 (above CE's
charge lance at AP 33), 5-second warmup, range 100. It retains its custom
`STC_SmiteBeam` damage type.

**Split-beam mechanics** are preserved by converting the secondary beam
projectiles to `LaserBeamCE`:

- `STC_Bullet_SmiteBeamSplit` --> `LaserBeamCE` (line 808)
- `STC_Bullet_SmiteBeamSplitSecondary` --> `LaserBeamCE` (line 812)
- `STC_Bullet_PrismBeamSplit` --> `LaserBeamCE` (line 818)

Slo's `MoteDualAttached` beam rendering is replaced by CE's
`LaserBeamGraphicCE`. Visual style changes but gameplay integrity is preserved
because CE's `SpawnBeamReflections()` handles the split logic.

---

### Section 5: Family D -- Custom Explosive (Lines 822-932)

Three turrets whose projectiles use custom DamageWorkers
(`DamageWorker_ChargeVapo`, `DamageWorker_ChargeNoFire`) that are CE-compatible
at the damage layer.

| Turret Gun defName          | CE AmmoSet                 | verbClass             | Damage | Radius |
|-----------------------------|----------------------------|-----------------------|-------:|-------:|
| `STC_Gun_ForeshadowTurret`  | `AmmoSet_STC_ChargedBomb`  | `Verb_ShootMortarCE`  |     80 |    5.0 |
| `STC_Gun_ShockPoralizer`    | `AmmoSet_40x46mmGrenade`   | `Verb_ShootCE`        |   (CE) |   (CE) |
| `STC_Gun_SmiteTurret`       | `AmmoSet_STC_SmiteCharge`  | `Verb_ShootCE`        |     12 |    2.9 |

**Foreshadow** is the flagship explosive: range 500, `Verb_ShootMortarCE`,
6-second warmup, `STC_ChargedBomb` damage at 80 base with R5 explosion. Its
custom `DamageWorker_ChargeVapo` calls `base.ExplosionAffectCell()` which
eventually reaches `ApplyDamageToPart` where CE's armor transpiler fires --
so CE armor calculations apply to explosion damage without any projectile
class conversion.

**ShockPoralizer** reuses CE's existing `AmmoSet_40x46mmGrenade` with the
EMP variant as default. No custom ammo needed; the turret's EMP role maps
directly to CE's 40mm grenade EMP round.

**SmiteTurret** (distinct from the laser Smite) uses a smaller CE explosive
projectile at 12 damage, R2.9, fired in 20-round bursts. Burst-fire explosive
turrets are unusual in CE; the low per-round damage prevents it from being
overpowered despite the high burst count.

---

### Section 6: Family E -- Heavy Ordnance / Artillery (Lines 934-1210)

Seven turrets covering direct-fire rocket platforms and mortar-class artillery.
This section includes the only turrets that use `CompChangeableProjectile`
in vanilla, which must be removed since CE replaces shell-loading with its
ammo system.

#### Direct-Fire Ordnance

| Turret Gun defName        | CE AmmoSet                | Burst | Mag | Range |
|---------------------------|---------------------------|------:|----:|------:|
| `STC_Gun_RainTurret`      | `AmmoSet_STC_Rocket`      |    20 | 200 |    75 |
| `STC_Gun_DawnMaker`       | `AmmoSet_STC_Thermobaric` |     4 |  20 |   500 |
| `STC_Gun_PatriachTurret`  | `AmmoSet_STC_Drone`       |     1 |  10 |   500 |
| `STC_Gun_FlyCracker`      | `AmmoSet_STC_ClusterMunition` | 1 |  10 |   200 |

**DawnMaker** uses `STC_ThermobaricBomb` damage at 75 base, R10 explosion.
4-round burst with 60 ticks between shots creates a staggered bombardment
pattern. Ammo requires `ComponentSpacer`, placing it firmly in ultra-tech tier.

**Patriarch and FlyCracker preserve vanilla projectiles** (see Section 8).

#### Mortar-Class Artillery

| Building defName       | Turret Head defName             | CE AmmoSet                   | Reload |
|------------------------|---------------------------------|------------------------------|-------:|
| `STC_GuardianCannon`   | `STC_Guardian_TurretHead`       | `AmmoSet_81mmMortarShell`    |     5s |
| `STC_EarthShatterer`   | `STC_EarthShatterer_TurretHead` | `AmmoSet_STC_HeavyShell`    |     8s |
| `STC_Rain`             | `STC_Rain_TurretHead`           | `AmmoSet_STC_ClusterMissile` |     8s |

Guardian reuses CE's standard 81mm mortar shell. EarthShatterer and Rain
require custom ammo:

**STC Heavy Shell** (EarthShatterer) -- 3 variants:

| Variant     | damageDef | Damage | Radius | Speed |
|-------------|-----------|-------:|-------:|------:|
| HE          | `Bomb`    |     65 |      8 |    40 |
| Incendiary  | `Flame`   |     20 |      8 |    40 |
| EMP         | `EMP`     |     50 |      6 |    40 |

**STC Cluster Missile** (Rain TurretHead) -- `Bomb` damage, 40 base, R4.

All three mortar buildings have `ITab_Shells` removed (lines 1085-1093) and
`CompProperties_ChangeableProjectile` removed from their turret heads
(lines 1131, 1170, 1209). CE's ammo system replaces both.

---

### Section 7: Family F -- Ultra-Tech Energy (Lines 1212-1321)

Three turrets that combine `EnergyTurret` building class (converted in
Section 1) with exotic projectile mechanics.

| Turret Gun defName          | CE AmmoSet                       | Projectile Strategy         |
|-----------------------------|----------------------------------|-----------------------------|
| `STC_Gun_ArcAccelerator`    | `AmmoSet_STC_AcceleratorRound`   | New CE projectile           |
| `STC_Gun_Heavenly_needle`   | `AmmoSet_STC_OrbitalCharge`      | Vanilla preserved           |
| `STC_Gun_Rupturer`          | `AmmoSet_STC_RuptureCharge`      | Vanilla preserved           |

**Arc Accelerator** gets full CE conversion with two ammo variants:

| Variant              | damageDef           | Damage | Sharp AP | Blunt AP | Speed |
|----------------------|---------------------|-------:|---------:|---------:|------:|
| Standard             | `STC_BleedingBullet`|     10 |       25 |       50 |   175 |
| AP                   | `STC_BleedingBullet`|      8 |       40 |       60 |   175 |

Both retain `STC_BleedingBullet` damage, preserving the accelerator's
bleeding-on-hit identity. 20-round bursts at 6 ticks between shots make
this a CE-compatible suppression platform with DOT pressure.

**Heavenly Needle and Rupturer preserve vanilla projectiles** (see Section 8).
Both use `Verb_ShootMortarCE` at range 500 with shared `Ammo_STC_EnergyCore`
ammo.

---

### Section 8: Family G -- Apparel Turret Deployers (Lines 1323-1341)

Two wearable turret items that use `Projectile_SpawnsThing` to deploy turrets.
The spawning mechanic does not interact with CE projectiles. Only CE
`Bulk`/`WornBulk` stats are needed.

| Apparel defName                    | Bulk | WornBulk |
|------------------------------------|-----:|---------:|
| `STC_Apparel_MiniSpinerTurret`     |    8 |        4 |
| `STC_Apparel_FlyCrackerTurret`     |   10 |        5 |

---

### Vanilla Projectile Preservation -- 4 Exceptions

Four turrets keep their original vanilla projectile classes because the exotic
payload mechanic IS the weapon's identity:

| Turret                | Projectile defName            | Original Class                | Reason preserved                                  |
|-----------------------|-------------------------------|-------------------------------|---------------------------------------------------|
| Heavenly Needle       | `STC_bullet_OrbitalBeam`      | `ProjectileMultiple_Explosive`| Multi-explosion cascade with growing radius        |
| Rupturer              | `STC_bullet_Rupturer`         | `ProjectileMultiple_Explosive`| Spatial distortion debuff via multi-explosion       |
| Patriarch             | `STC_Bullet_Drone`            | `Projectile_Explosive` + VEF homing | Guided drone is the weapon's identity        |
| FlyCracker            | `STC_FlyCracker_Projectile`   | `Projectile_ClusterMunition`  | Cluster separation mechanic                        |

These accept partial CE integration: the verb is converted to
`Verb_ShootMortarCE`, ammo consumption is tracked via
`Ammo_STC_EnergyCore`, but the projectile flight uses vanilla mechanics.
CE damage application still occurs because `GenExplosion.DoExplosion()` calls
`DamageWorker_AddInjury.ApplyDamageToPart`, which CE's Harmony transpiler
intercepts to apply CE armor calculations.

All four use the shared `Ammo_STC_EnergyCore` as their physical ammo item,
defined in `Defs/SloTurretCollection/Ammo_STC_Energy.xml`. One AmmoDef,
5 AmmoSets, one crafting recipe (500 cores per batch: 12 Plasteel + 6 Steel +
8 ComponentIndustrial, 10000 work ticks, fabrication bench).

---

### Custom Ammo Pipeline Summary

21 AmmoSets total, organized by ammo source:

**Reuse CE standard ammo (no new AmmoDefs):**
- Family A: `AmmoSet_556x45mmNATO`, `AmmoSet_762x51mmNATO`, `AmmoSet_12Gauge`, `AmmoSet_20x102mm`
- Family C lasers: 4 AmmoSets in `Ammo_STC_Laser.xml`, all consuming `Ammo_LaserChargePack`
- Family D ShockPoralizer: `AmmoSet_40x46mmGrenade`
- Family E Guardian: `AmmoSet_81mmMortarShell`

**Shared custom AmmoDef (`Ammo_STC_EnergyCore`):**
- 5 AmmoSets in `Ammo_STC_Energy.xml` for Tesla, Orbital, Rupture, ClusterMunition, Drone

**Per-turret custom AmmoDefs:**
- `Ammo_STC_Ballistic.xml`: Railgun (2 variants), AcceleratorRound (2 variants)
- `Ammo_STC_Explosive.xml`: ChargedBomb, SmiteCharge, Rocket, Thermobaric, IncendiaryRocket
- `Ammo_STC_Misc.xml`: IncendiaryFuel, FuseCharge, Canister
- `Ammo_STC_Artillery.xml`: HeavyShell (3 variants), ClusterMissile

This structure minimizes new physical ammo items. 9 of 21 AmmoSets consume
existing CE ammo. 5 more share a single `Ammo_STC_EnergyCore`. Only 11
unique AmmoDefs are introduced.

---

## 4. Why This Fix Beats Alternatives

### Family-Based Classification Over Uniform Autopatcher

The autopatcher cannot distinguish between a shotgun turret that should use
12-gauge buckshot and a laser turret that should use `LaserBeamCE` projectiles.
Manual per-family conversion is the only approach that correctly handles all 7
weapon archetypes. The upfront cost (1346 lines of patch XML, 1620 lines of
ammo defs) is the price of correctness.

### Building_LaserGunCE Over Harmony-Patched EnergyTurret

CE already solved the "energy turret on CE turret base" problem.
`Building_LaserGunCE` provides charge/drain logic, extends
`Building_TurretGunCE` for full CE combat pipeline integration, and reads
from `CompPowerTrader` -- a component already present on all 10 EnergyTurret
buildings. XML class replacement is simpler, more auditable, and carries no
C# version-coupling risk compared to a Harmony patch against `EnergyTurret`.

### Selective Vanilla Projectile Preservation Over Full Conversion

Converting all 28+ turrets to CE projectiles would be consistent but would
destroy the identity of 4 weapons whose gameplay rests entirely on exotic
payload mechanics (multi-explosion cascades, homing drones, cluster
separation). The partial-integration approach -- CE verb, CE ammo tracking,
vanilla projectile flight, CE damage application via transpiler -- preserves
these mechanics while still bringing them into CE's economy (ammo consumption,
crafting, trade).

### Shared Ammo_STC_EnergyCore Over Per-Turret Ammo Items

Five turrets with preserved vanilla projectiles all consume
`Ammo_STC_EnergyCore`. A shared ammo item means one crafting recipe, one
stockpile category, one trade entry. Per-turret ammo items would create 5
separate crafting recipes for items that are functionally identical (generic
energy cells feeding exotic projectile launchers).

### CE Standard NATO Ammo Over Custom Ballistic Ammo

Family A's 7 turrets map cleanly to 5.56mm, 7.62mm, 12 gauge, and 20x102mm.
Custom ammo defs for these would duplicate CE's maintained definitions, require
ongoing maintenance as CE rebalances, and prevent players from using existing
ammunition stockpiles. Reusing CE's NATO ammo sets costs nothing and
automatically inherits CE balance updates.

### Custom Ammo Where Damage Types Require It

Conversely, turrets with custom damage types (`STC_ChargedBomb`,
`STC_ThermobaricBomb`, `STC_BleedingBullet`, `STC_Prism`, `STC_SmiteBeam`)
cannot use CE standard ammo without losing those damage types. The 11 custom
AmmoDefs are the minimum set required to preserve the mod author's damage
design while bringing projectiles into CE's ballistic system.

### Purely XML, No C# Dependency

The entire patch operates through RimWorld's XML patching system and CE's
existing class infrastructure. No compiled assemblies, no Harmony patches, no
runtime reflection. Only XML schema changes in CE's def structure could
require updates.
