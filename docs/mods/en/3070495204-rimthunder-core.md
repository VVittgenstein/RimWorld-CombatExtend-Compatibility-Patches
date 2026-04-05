# RimThunder - Core: CE Vehicle MG Turret AmmoSet Binding

**Mod**: RimThunder - Core  
**Steam Workshop ID**: 3070495204  
**Package ID**: `RimThunder.Core`  
**Patch file**: `Patches/RimThunderCore.xml`

---

## 1. What Broke

RimThunder Core ships a first-party Combat Extended layer. The mod's
`LoadFolders.xml` conditionally loads CE-specific content when CE is present,
including `MotorizationCE.dll`, CE ammo defs for heavy ordnance, and a
CE-converted grenade launcher. Heavy ordnance turrets (cannons, grenade
launchers) are fully CE-integrated with proper `CETurretDataDefModExtension`
ammoSet bindings.

The machine gun turrets are not.

Three abstract `VehicleTurretDef` base classes define all vehicle-mounted MGs
across the RimThunder ecosystem:

- `RT_BaseVehicleMG_Light` -- light machine guns (5.56mm class)
- `RT_BaseVehicleMG_Medium` -- medium machine guns (7.62mm class)
- `RT_BaseVehicleMG_Heavy` -- heavy machine guns (12.7mm class)

These abstracts carry CE-oriented fire-rate and range adjustments, but lack the
critical `CETurretDataDefModExtension` that binds a turret to a CE ammo set.
Without it, the turrets fire their vanilla `BaseBullet` projectiles --
`RT_Bullet_LMG`, `RT_Bullet_MMG`, and `RT_Bullet_HMG` -- straight through CE's
combat pipeline. The consequences:

- **No armor penetration calculation.** Vanilla bullets carry flat damage, not
  CE's sharp/blunt armor penetration values. Shots that should bounce off
  armored targets deal full damage.
- **No suppression.** CE suppression is driven by ammo-class properties on
  `AmmoDef` projectiles. Vanilla bullets generate zero suppression.
- **No ammo consumption.** Vehicle ammo inventories are never drawn down. MGs
  have infinite effective ammunition.
- **No ammo-type selection.** Players cannot switch between FMJ, AP, incendiary,
  or other ammo variants. The turret fires one fixed projectile type.

Every vehicle pack that inherits from these three abstracts -- across all
RimThunder expansion modules -- exhibits the same breakage. The problem is
architectural, rooted in three missing XML nodes on three abstract defs.

## 2. Design Problem and Options Considered

### The delegation chain

Understanding the fix requires understanding who owns what.
`CETurretDataDefModExtension` is **not** a CE class. It belongs to **Vehicle
Framework** (`Vehicles` namespace). Vehicle Framework's `VehicleTurret` reads
the extension at initialization and delegates to CE through a set of static
`Func<>` callback fields:

- `LaunchProjectileCE` -- fire a CE projectile with full ballistic simulation
- `LookupAmmosetCE` -- resolve an `AmmoSetDef` by `defName` string
- `GetAmmoCountCE`, `TryFindAmmoInInventoryCE`, etc.

CE registers implementations into these fields at runtime. The `ammoSet` string
on `CETurretDataDefModExtension` is the bridge: it must match an
`AmmoSetDef.defName` exactly. If the extension is absent, VehicleTurret falls
back to vanilla projectile behavior -- the exact failure mode observed.

### Option A: Custom ammo defs

Create new `AmmoDef` and `AmmoSetDef` entries calibrated specifically to
RimThunder's MG stat profiles.

Rejected. RimThunder's MGs map cleanly to standard NATO calibers. Custom defs
would duplicate existing CE content, require ongoing maintenance as CE rebalances
ammo stats, and prevent players from using their existing NATO ammo stockpiles.

### Option B: Per-vehicle patching

Patch each concrete `VehicleTurretDef` (one per vehicle model per expansion
pack) individually.

Rejected. RimThunder's vehicle packs define concrete turrets that inherit from
the three abstract bases. Patching the abstracts propagates to all descendants
automatically, both for current vehicle packs and any future ones. Per-vehicle
patches would create an O(n) maintenance burden that grows with every new
RimThunder release.

### Option C: PatchOperationReplace on the abstract defs

Replace the full turret defs with corrected versions.

Rejected. RimThunder's own CE layer already applies `PatchOperationReplace` to
some of these defs for fire-rate tuning. Two Replace operations on the same xpath
create a load-order race. `PatchOperationAddModExtension` appends to the def's
extension list without touching existing content, making it conflict-free.

### Option D (chosen): AddModExtension on abstract bases with CE standard ammo

Append `CETurretDataDefModExtension` to the three abstract bases using
`PatchOperationAddModExtension`. Bind each to the appropriate CE standard NATO
ammo set. No custom ammo defs. No per-vehicle patches. No conflict with existing
RT patches.

## 3. Implementation

### Patch file: `Patches/RimThunderCore.xml`

The patch is a single `PatchOperationFindMod` block gated on
`RimThunder - Core`, containing a `PatchOperationSequence` of three
`PatchOperationAddModExtension` operations.

Each operation targets an abstract base via the `@Name` xpath selector on
`Vehicles.VehicleTurretDef`, then appends a `Vehicles.CETurretDataDefModExtension`
with caliber-appropriate ballistic parameters.

### AmmoSet assignments

| Abstract base | Caliber class | CE AmmoSet | Rationale |
|---|---|---|---|
| `RT_BaseVehicleMG_Light` | 5.56x45mm | `AmmoSet_556x45mmNATO` | Direct caliber match. CE's mini-turret gun uses the same set. |
| `RT_BaseVehicleMG_Medium` | 7.62x51mm | `AmmoSet_762x51mmNATO` | Primary caliber match. CE's medium turret gun uses the same set. |
| `RT_BaseVehicleMG_Heavy` | 12.7x99mm | `AmmoSet_50BMG` | Direct caliber match. CE's standard HMG caliber. |

Each NATO ammo set provides a full variant spread: FMJ, AP, HP, incendiary, HE,
and Sabot. No custom ammo defs are introduced.

### Ballistic parameters

Parameters were set against CE's own turret gun anchors (mini-turret, medium
turret, autocannon turret) and adjusted for vehicle-mounted characteristics.

| Parameter | LMG | MMG | HMG | Design basis |
|---|---|---|---|---|
| `ammoSet` | `AmmoSet_556x45mmNATO` | `AmmoSet_762x51mmNATO` | `AmmoSet_50BMG` | Caliber match |
| `shotHeight` | 1.5 | 1.5 | 1.5 | Vehicle-mounted elevation; consistent across weapon classes |
| `sway` | 0.8 | 1.0 | 1.4 | CE turret reference: mini=0.67, medium=0.84, autocannon=1.61. Vehicle platforms sit slightly above fixed emplacements due to hull vibration and suspension movement. |
| `spread` | 0.07 | 0.05 | 0.02 | Matches CE turret progression: mini=0.07, medium=0.05, autocannon=0.01. Vehicle mounts introduce marginally more dispersion than fixed emplacements at heavy caliber. |
| `recoil` | 0.5 | 0.8 | 1.2 | Vehicle mounts absorb recoil through hull mass. Values are lower than CE fixed turrets, reflecting superior recoil absorption on a multi-ton platform. |

**`speed` is intentionally omitted.** CE ammo projectile defs define their own
muzzle velocities (5.56mm: 168, 7.62mm: 156, .50 BMG: 163). Setting a speed
override on the turret extension would replace these values and break CE's
ballistic arc calculations, which depend on projectile-defined velocity for
gravity drop and flight time.

### Resulting combat characteristics

With the ammoSet binding active, CE's ammo projectiles replace the vanilla
bullets entirely. Default FMJ loadout stats:

| Caliber | Damage | ArmorPen (Sharp) | ArmorPen (Blunt) |
|---|---|---|---|
| 5.56mm NATO FMJ | 14 | 6 | 34.18 |
| 7.62mm NATO FMJ | 20 | 7 | 66.72 |
| .50 BMG FMJ | 42 | 14 | 360.34 |

Players may switch to AP, incendiary, or other variants from their ammo
stockpiles, with corresponding stat shifts per CE's ammo system.

### Cross-mod stat alignment

RimThunder vehicle MGs retain their native burst and range profiles:

- 20-round bursts (vs CE fixed turrets' typical 10-round bursts)
- Ranges of 55/62/75 tiles for LMG/MMG/HMG (vs CE turrets' 48/55/78)

These values are appropriate for vehicle-mounted weapons with belt-feed
mechanisms and stabilized mounts. The patch does not alter them; it only adds
the missing ammo integration layer.

### Vanilla projectile disposition

The vanilla projectile defs `RT_Bullet_LMG`, `RT_Bullet_MMG`, and
`RT_Bullet_HMG` remain loaded in the def database. When CE is active and the
ammoSet binding is present, `VehicleTurret` uses the CE launch path exclusively.
The vanilla bullet defs are never instantiated. They occupy negligible memory and
require no cleanup -- removing them would risk breaking non-CE load orders where
RimThunder is used without Combat Extended.

### Reference pattern

This patch follows the same pattern established by RimThunder Core's own
grenade launcher CE integration. The mod's `RT_GrenadeLauncher` turret uses
`PatchOperationAddModExtension` to append
`Vehicles.CETurretDataDefModExtension` with `RTC_AmmoSet_SmokeGrenade`. Our
patch applies the identical mechanism to the three MG abstract bases.

## 4. Why This Fix Beats Alternatives

**Abstract-base propagation.** Three patch operations cover every vehicle MG
turret in every RimThunder expansion pack, present and future. Any new vehicle
pack that inherits from `RT_BaseVehicleMG_Light`, `_Medium`, or `_Heavy`
receives correct CE ammo binding automatically, with zero additional patch
work.

**CE standard ammo reuse.** By binding to `AmmoSet_556x45mmNATO`,
`AmmoSet_762x51mmNATO`, and `AmmoSet_50BMG`, the patch leverages CE's
maintained ammo definitions. When CE rebalances NATO ammo stats, RimThunder
turrets inherit those changes. Players use ammo from a single shared stockpile,
not a parallel set of mod-specific rounds.

**Non-destructive patching.** `PatchOperationAddModExtension` appends to the
def's `modExtensions` list. It does not modify or replace any existing def
content. This avoids conflicts with RimThunder's own CE patches, which use
`PatchOperationReplace` on the same defs for fire-rate adjustments. Both
patch types coexist regardless of load order.

**Purely XML, no C# dependency.** The fix operates entirely through RimWorld's
XML patching system and Vehicle Framework's existing `Func<>` delegate bridge
to CE. No compiled assemblies, no Harmony patches, no runtime reflection. This
eliminates version-coupling to specific CE or VF assembly builds.

**Minimal surface area.** One file, three operations, fifteen meaningful XML
nodes. The patch is auditable in its entirety within `Patches/RimThunderCore.xml`
at 51 lines.
