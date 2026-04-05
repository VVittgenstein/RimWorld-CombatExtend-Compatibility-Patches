# Phase Weaponry -- CE Compatibility Patch

| Field          | Value                                          |
|----------------|------------------------------------------------|
| **Mod**        | Phase Weaponry by DetVisor                     |
| **Steam ID**   | 3222694245                                     |
| **packageId**  | `det.phaseweaponry`                            |
| **Mod type**   | XML + custom DamageDef                         |
| **Patch files**| `Patches/PhaseWeaponry.xml`, `Defs/PhaseWeaponry/Projectiles_Phase.xml` |
| **RimWorld**   | 1.5, 1.6 (version-agnostic defName target)    |

---

## 1. What Broke

Phase Weaponry adds three ranged energy weapons (Phase Shank, Phase Saw,
Phase Scalpel), a stuffable Ranger helmet, and a PawnKind
(`DV_RangerInBlack`) that replaces the vanilla Stranger in Black event.
The weapons fire `BaseBullet`-derived projectiles dealing `DV_PhaseCut`
damage -- a custom Sharp-category DamageDef with low infection and
permanent scarring. Thematically these are surgical energy blades, not
brute-force plasma.

Three failures occur when this mod meets Combat Extended.

### Gap 1: Weapons Fire Vanilla Hitscan Against CE Armor

All three weapons use `Verb_Shoot` with `BaseBullet`-parented projectiles.
CE's weapon autopatcher (`Settings.cs:133`) is disabled by default, so
the guns are never converted to `Verb_ShootCE`. They fire vanilla hitscan
with no CE armor penetration values -- effectively zero damage against
any pawn wearing CE-scaled armor.

### Gap 2: Ranger Helmet Has No CE Bulk Stats

`DV_ApparelRangerHelmetBase` lacks `Bulk`, `WornBulk`, and
`StuffEffectMultiplierArmor`. The helmet is stuffable (Fabric/Leathery),
so without the stuff multiplier a hyperweave helmet protects identically
to cloth.

### Gap 3: Ranger In Black Spawns With No Ammo

`DV_RangerInBlack` has no `LoadoutPropertiesExtension`. CE's spawner
generates zero ammunition. The dramatic rescue event becomes a pawn
standing in the open with an empty gun.

---

## 2. Design Problem and Options Considered

### Ammo Strategy: Custom AmmoSet vs. Reusing Charged

CE defines charged ammo families (6x24mm, 5x35mm, 8x50mm) for its own
energy weapons. Reusing one of these was rejected for two reasons:

1. **DV_PhaseCut damage would be lost.** CE charged projectiles deal
   `Bullet` damage. Phase Weaponry's identity rests on `DV_PhaseCut`
   and its scarring/infection modifiers. Reusing charged ammo silently
   deletes the mod's unique damage profile.
2. **Unwanted secondary damage.** CE charged projectiles carry
   `Bomb_Secondary` (thermal splash). Phase weapons are "clean cut"
   energy blades -- no thermal splash, no area damage, no EMP.

A custom `AmmoSet_PhaseEnergy` preserves `DV_PhaseCut` on all projectiles,
carries zero secondary damage, and keeps the weapons thematically coherent.
All three weapons share one AmmoSet with differentiation through verb
stats -- matching CE convention where charge rifle and charge lance share
ammo families internally.

### Numerical Calibration

Two ammo variants follow CE's standard/AP convention:

| Variant               | Damage | Sharp AP | Blunt AP | Speed |
|-----------------------|-------:|---------:|---------:|------:|
| Phase Cell            |     16 |       20 |        8 |   140 |
| Phase Cell (Focused)  |     12 |       35 |        6 |   140 |

Positioned within CE's charged weapon spectrum:

| Reference              | Damage | Sharp AP | Notes                    |
|------------------------|-------:|---------:|--------------------------|
| 6x24mm charged         |     13 |       15 | Charge pistol/rifle ammo |
| **Phase Cell**         | **16** |   **20** | Custom mid-tier          |
| 8x50mm heavy charged   |     24 |       22 | Heavy charge blaster     |
| 5x35mm lance charged   |     25 |       33 | Charge lance ammo        |
| **Phase Cell (Focused)** | **12** | **35** | Custom AP variant       |

Standard sits above 6x24mm in both damage and penetration, reflecting
spacer-tech advancement. Focused matches lance-class penetration (AP 35
vs. lance's 33) at significantly lower damage.

Blunt AP is deliberately low (8/6). Phase energy blades transfer almost
no kinetic energy -- they cut, they do not stagger. This is the key
differentiator from charged ammo: high sharp AP, near-zero blunt, zero
secondary. Surgically lethal against flesh, no stagger through heavy
plate, no EMP option vs. shields.

Speed 140 places them between charge pistol (122) and charge rifle
(151).

### Weapon Conversion: Manual vs. Autopatcher

CE's autopatcher produces generic conversions with no role awareness.
Manual conversion via `PatchOperationMakeGunCECompatible` was chosen
because the three weapons form a deliberate sidearm-rifle-sniper
progression requiring distinct CE personality tuning for each.

### Helmet and PawnKind

The helmet gets `Bulk` 3, `WornBulk` 1, `StuffEffectMultiplierArmor` 5.
With hyperweave this yields ~4.65 Sharp -- credible spacer protection
without competing with power armor helmets. The mod's signature
`AimingDelayFactor -0.2` is preserved untouched.

`DV_RangerInBlack` gets `primaryMagazineCount` 5-8 via
`LoadoutPropertiesExtension`. No backpack (all five apparel slots are
occupied; phase cells at 0.015 mass each fit in pockets).

---

## 3. Implementation

### File 1: `Patches/PhaseWeaponry.xml`

The entire file is wrapped in `PatchOperationFindMod` gated on
`Phase Weaponry`. Five operations run inside a `PatchOperationSequence`.

#### Ops 1-3: Weapon Conversions (lines 12-113)

Each weapon is converted via `CombatExtended.PatchOperationMakeGunCECompatible`,
which replaces `Verb_Shoot` with `Verb_ShootCE`, injects CE stat bases,
sets verb properties, and configures ammo and fire modes atomically.

| Stat             | Shank (pistol) | Saw (rifle) | Scalpel (sniper) |
|------------------|---------------:|------------:|-----------------:|
| defName          | DV_Gun_PhasePistol | DV_Gun_PhaseRifle | DV_Gun_PhasePrecisionRifle |
| Lines            |          12-43 |       46-79 |          82-113  |
| Mass             |            2.5 |         4.0 |              5.5 |
| Bulk             |            3.0 |         8.0 |             11.0 |
| Range            |             25 |          50 |               60 |
| BurstShotCount   |              1 |    3 / 8 tt |                1 |
| MagazineSize     |             15 |          24 |                8 |
| ReloadTime       |            3.0 |         4.0 |              4.0 |
| ShotSpread       |           0.12 |        0.07 |             0.02 |
| SwayFactor       |           1.30 |        1.15 |             0.90 |
| SightsEfficiency |           1.00 |        1.10 |             1.30 |
| Recoil           |            0.8 |         1.3 |              0.9 |
| aiUseBurstMode   |          false |        true |            false |
| aiAimMode        |      AimedShot |   AimedShot |        AimedShot |

The progression is coherent: range increases (25 -> 50 -> 60), spread
tightens (0.12 -> 0.07 -> 0.02), optics improve (1.0 -> 1.1 -> 1.3),
mass and bulk increase. Sway decreases as weapons become heavier and
more stabilized. Magazine size peaks at the burst-fire Saw (24 rounds =
8 full bursts) and drops to 8 for the precision Scalpel where each shot
is deliberate.

All three use `defaultProjectile` `Bullet_PhaseEnergy` (standard cell)
and `ammoSet` `AmmoSet_PhaseEnergy`.

#### Op 4: Helmet Stats (lines 116-123)

```xml
<li Class="PatchOperationAdd">
    <xpath>Defs/ThingDef[@Name="DV_ApparelRangerHelmetBase"]/statBases</xpath>
    <value>
        <Bulk>3</Bulk>
        <WornBulk>1</WornBulk>
        <StuffEffectMultiplierArmor>5</StuffEffectMultiplierArmor>
    </value>
</li>
```

Appends to the abstract base def's `<statBases>`. The xpath targets
`@Name` (not `defName`) so all inheriting helmets receive the stats.

#### Op 5: PawnKind Loadout (lines 126-136)

```xml
<li Class="PatchOperationAddModExtension">
    <xpath>Defs/PawnKindDef[defName="DV_RangerInBlack"]</xpath>
    <value>
        <li Class="CombatExtended.LoadoutPropertiesExtension">
            <primaryMagazineCount>
                <min>5</min>
                <max>8</max>
            </primaryMagazineCount>
        </li>
    </value>
</li>
```

`PatchOperationAddModExtension` auto-creates `<modExtensions>` if absent,
which is the correct defensive pattern. The spawner generates 5-8
magazines of whichever ammo type the pawn's equipped weapon uses.

### File 2: `Defs/PhaseWeaponry/Projectiles_Phase.xml`

Defines the complete ammo pipeline from crafting to impact.

**ThingCategoryDef** (lines 6-10): `AmmoPhaseEnergy` under `AmmoAdvanced`.
Places phase cells in the Advanced ammo category for stockpile filters
and trade screens.

**AmmoSetDef** (lines 14-21): `AmmoSet_PhaseEnergy` maps two AmmoDefs
to ProjectileDefs:

| AmmoDef                    | ProjectileDef                |
|----------------------------|------------------------------|
| `Ammo_PhaseEnergy`         | `Bullet_PhaseEnergy`         |
| `Ammo_PhaseEnergy_Focused` | `Bullet_PhaseEnergy_Focused` |

**AmmoDefs** (lines 25-67): Both inherit from `SpacerSmallAmmoBase`, mass
0.015, bulk 0.01. Trade tags `CE_AutoEnableTrade` and
`CE_AutoEnableCrafting_TableMachining` enable orbital trader stock and
machining table crafting. Standard uses `AmmoClasses.Charged`, focused
uses `AmmoClasses.ChargedAP`. Both borrow charged ammo stack graphics.

**ProjectileDefs** (lines 71-101): Both inherit from `BaseBulletCE` with
`CombatExtended.ProjectilePropertiesCE`. Both retain `DV_PhaseCut` as
`damageDef` -- this is the entire reason the custom AmmoSet exists.
Neither defines secondary damage.

**RecipeDef** (lines 105-152): Crafts 500 phase energy cells per batch.

| Ingredient          | Count | Notes                          |
|---------------------|------:|--------------------------------|
| Plasteel            |    10 | Spacer-tech base material      |
| Steel               |     8 | Structural component           |
| ComponentIndustrial |     6 | Energy regulation circuitry    |

Requires Crafting 7 and `ChargedShot` research. Inherits from
`AmmoRecipeBase` (machining table). Work amount 8000 ticks.

---

## 4. Why This Fix Beat Alternatives

### Why Custom AmmoSet Over Reusing Charged

Reusing 6x24mm charged would be simpler (no new defs) but would:

1. Replace `DV_PhaseCut` with `Bullet` damage, deleting the mod's custom
   damage type and its scarring/infection modifiers.
2. Add `Bomb_Secondary` thermal splash to weapons designed as clean
   energy cutters.
3. Introduce EMP variants that contradict the mod's damage philosophy.

The custom AmmoSet costs one def file but preserves the mod author's
design intent completely.

### Why Not the Autopatcher

The autopatcher (`Settings.cs:133`, disabled by default) produces
one-size-fits-all conversions. Manual `PatchOperationMakeGunCECompatible`
gives precise control over each weapon's spread, sway, optics, and fire
mode -- preserving the sidearm-rifle-sniper progression.

### Why StuffEffectMultiplierArmor, Not Raw Armor Override

The helmet is stuffable. Without the multiplier, CE ignores stuff
quality. A raw armor override would give identical protection regardless
of whether the helmet is cloth or hyperweave, breaking CE's material
differentiation system. The multiplier of 5 lets material choice matter.

### Why No Backpack on the Ranger

All five apparel slots are occupied. Adding a backpack requires removing
canonical outfit pieces. Phase cells at 0.015 mass each means 5-8
magazines fit in pocket carry -- even 64 cells (8 x Scalpel magazine)
weigh under 1.0.

---

## 5. Testing Notes

1. Load a game with Combat Extended, Phase Weaponry, and V2CEPatch.
2. Spawn each weapon via dev mode. Confirm CE stats (Mass, Bulk,
   ShotSpread, SwayFactor, SightsEfficiency) and `AmmoSet_PhaseEnergy`
   with both cell types in the inspect pane.
3. Fire each weapon. Confirm `DV_PhaseCut` in the combat log (not
   `Bullet`). Confirm no secondary explosion or thermal splash.
4. Equip focused cells and fire at heavy armor. Confirm AP 35 penetrates
   where standard AP 20 does not.
5. Spawn a hyperweave Ranger helmet. Confirm Bulk 3, WornBulk 1, Sharp
   armor ~4.65.
6. Trigger Stranger in Black (or spawn `DV_RangerInBlack`). Confirm the
   pawn has phase energy cells in inventory and fires immediately.
7. Craft phase energy cells at the machining table. Confirm Crafting 7
   requirement, ChargedShot research gate, 500 cells output from
   10 Plasteel + 8 Steel + 6 ComponentIndustrial.
