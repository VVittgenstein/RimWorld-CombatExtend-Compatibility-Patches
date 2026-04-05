# Landkreuzer P1000 Ratte -- CE Compatibility Patch

The Landkreuzer P1000 Ratte is a 9x15 super-heavy tank carrying 11 turret mounts across 5 turret types, crewed by 1 driver, 7 gunners, and 42 passengers. Under Combat Extended, every turret fires vanilla projectiles because CE's auto-patcher never processes `VehicleTurretDef` (it is not a `ThingDef` and fails the `IsRangedWeapon` gate in `GunAutoPatcher.shouldPatch()`), vanilla-scale armor values become meaningless against CE's mm-RHA penetration model, and the engine explosion at radius 40 would destroy an entire map quadrant. This patch converts all 5 turret types to CE ammo systems via pure XML using Vehicle Framework's `CETurretDataDefModExtension`, reduces the engine explosion to a survivable radius, and adds ammo hyperlinks to the vehicle's info card -- all in a single 336-line patch file gated on both Combat Extended and Landkreuzer P1000 Ratte being active.


## What Breaks Under CE

### Vanilla Projectiles on Every Turret

CE's weapon auto-patcher, `GunAutoPatcher.shouldPatch()`, operates on `ThingDef` instances that pass the `IsRangedWeapon` check. Vehicle Framework turrets are defined as `Vehicles.VehicleTurretDef`, which is a separate def type entirely -- it does not inherit from `ThingDef`. The auto-patcher never sees them.

No CE manual patches exist for the P1000 either. The mod's three custom projectile defs (`P1000_Main_HE`, `P1000_Flak_HE`, and the ball turret projectile) all inherit from `BaseBullet` (vanilla), not `BaseBulletCE`. They carry vanilla damage numbers (120, 20, etc.) and vanilla explosion radii, producing results that are wildly inconsistent with CE's damage model.

The practical effect: a 280mm naval cannon deals the same 120 damage as an improvised charge lance, and the 7.62mm ball turrets fire projectiles with zero armor penetration data, making them functionally inert against anything wearing CE armor.

### Vanilla-Scale Armor

The P1000's hull armor ratings sit at ArmorRating_Sharp 1.4-1.6. In vanilla RimWorld, this is heavy armor. In CE's mm-RHA penetration system, these values parse as 1.4-1.6mm of rolled homogeneous armor equivalent -- thinner than a tin can. A standard CE rifle round (7.62x51mm NATO) penetrates at 12mm Sharp. The largest land vehicle in the game can be perforated by small arms fire.

### Engine Explosion Radius

The P1000's engine component carries `Reactor_Explosive` with radius 40. In vanilla, this creates a dramatic but relatively contained explosion because vanilla explosion damage falls off predictably. Under CE, explosions generate fragment projectiles with independent penetration rolls. A radius-40 CE explosion would spray fragments across a 80-cell diameter, shredding every pawn, wall, and structure in the blast area. For reference, CE's own 28cm HE shell -- the largest conventional round in the ammo system -- has an explosion radius of 7.


## Repair Strategy

The entire patch is pure XML. No C# code, no Harmony patches, no compiled assemblies. This is possible because Vehicle Framework provides `CETurretDataDefModExtension`, a mod extension class that bridges `VehicleTurretDef` to CE's ammo and ballistic systems. Each turret gets a `PatchOperationAddModExtension` that attaches this extension with ammo set, shot height, projectile speed, sway, and spread parameters. Turret timing and range values are adjusted via `PatchOperationReplace` on the turret def's existing fields.

**File:** `Patches/LandkreuzerP1000.xml` (336 lines)

The entire patch is wrapped in a single `PatchOperationFindMod` block gated on both `Combat Extended` and `Landkreuzer P1000 Ratte`:

```xml
<Operation Class="PatchOperationFindMod">
    <mods>
        <li>Combat Extended</li>
        <li>Landkreuzer P1000 Ratte</li>
    </mods>
    <match Class="PatchOperationSequence">
```

All operations inside the sequence execute atomically -- if either mod is absent, nothing fires.


## Ammo Set Selection Rationale

Before examining each turret in detail, it is worth explaining how ammo sets were chosen. CE ships a finite library of caliber-specific ammo sets, each with internally consistent damage, penetration, velocity, and crafting cost values. The design principle for this patch was to select the CE ammo set that most closely matches the historical weapon each turret represents, then inherit that ammo set's entire stat profile rather than inventing custom values.

This approach has three advantages over defining custom ammo types:

1. **No new defs.** Custom ammo sets require new AmmoDef, ThingDef (projectile), and RecipeDef entries. Each one is a maintenance surface -- new CE versions can change base class fields, rename parent abstracts, or restructure the ammo inheritance tree. By referencing only existing CE ammo sets, the patch contains zero def definitions and is immune to CE-internal refactoring.

2. **Pre-balanced economics.** CE's ammo crafting costs are tuned against each other. A custom 280mm round with arbitrary crafting costs might be too cheap relative to existing CE ordnance, or too expensive relative to its damage output. Using `AmmoSet_28cmSpgrShell` inherits the 600 Steel + 2 Components + 33 FSX cost that CE already validated against its own damage model.

3. **Player familiarity.** Players who have used CE's naval guns or the Bulldog medium tank already understand these ammo types. The P1000's weapons show familiar ammo names in the ammo selection UI, and the crafting recipes appear in the same bill categories.


## Turret Conversions

### Main Cannon: P1000_MainTurret (lines 16-47)

The 280mm main armament maps to CE's `AmmoSet_28cmSpgrShell` (28cm Sprenggranat) -- a direct historical match. The real P1000 design specified twin 280mm Krupp naval guns; CE's 28cm ammo set is described as "Very large obsolete cannon shell used in naval battleship turrets." The caliber, era, and role align exactly.

| Parameter | Vanilla Value | CE Patch Value | Rationale |
|---|---|---|---|
| Projectile | `P1000_Main_HE` (Bomb, 120 dmg, R14.9) | `Bullet_28cmSpgrShell_HE` (837 dmg, R7, 40+80 frags) | CE 28cm naval shell |
| reloadTimer | 20.4s | 16.0s | Compensates for CE's longer engagement ranges; still 2.6x slower than Bulldog (6.2s) |
| warmUpTimer | 4.4s | 5.5s | 280mm aiming time; scaled from Roadkill 57mm at 3.5s |
| AmmoSet | -- | `AmmoSet_28cmSpgrShell` | Historical 28cm Krupp naval |
| shotHeight | -- | 3.5 | Main turret at drawLayer 4 (highest mount); Bulldog reference is 2.5 |
| sway | -- | 0.82 | Matches Bulldog main turret |
| spread | -- | 0.01 | Matches Bulldog main turret |
| Magazine | 2 | 2 (unchanged) | At 300kg per shell, a two-round ready rack is generous |

The reload reduction from 20.4s to 16.0s deserves explanation. CE engagement ranges are longer than vanilla, and CE targets can be suppressed or moving, so time-to-kill matters more than raw per-shot damage. A 20.4s reload on a siege weapon with only a 2-round magazine would leave multi-minute gaps between effective volleys. At 16.0s the gun fires roughly once per 16 seconds, which at 837 damage per HE round is devastating but not rapid enough to trivialize encounters. The Bulldog medium tank reloads in 6.2s -- the P1000's main gun is still 2.6x slower.


### Maus Turrets: P1000_MausTurret x2 + P1000_MausFTTurret x1 (lines 53-138)

Named after the Panzer VIII Maus and its 75mm coaxial gun, these three turrets serve as the P1000's medium-range direct fire weapons. CE's `AmmoSet_75x350mmR` is the canonical medium tank ammo set, used in the Bulldog medium tank CE patch. All timing and ballistic values match the Bulldog exactly:

| Parameter | CE Patch Value | Source |
|---|---|---|
| reloadTimer | 6.2s | Bulldog 75mm |
| warmUpTimer | 2.8s | Bulldog 75mm |
| maxRange | 86 | Bulldog 75mm |
| speed | 124 | Bulldog 75mm |
| sway | 0.82 | Bulldog 75mm |
| spread | 0.01 | Bulldog 75mm |
| AmmoSet | `AmmoSet_75x350mmR` | Bulldog 75mm |

The single difference between the three Maus turrets is `shotHeight`, which reflects physical mount position:

| Turret | Mount Position | drawLayer | shotHeight |
|---|---|---|---|
| P1000_MausTurret (x2) | Side sponsons | 2 | 2.5 |
| P1000_MausFTTurret (x1) | Hull front | 1 | 1.8 |

The front turret sits lower in the hull, so its projectiles originate at a lower height. This affects CE's ballistic arc calculations -- the hull-mounted gun has slightly less ability to fire over obstacles but is harder for enemies to target.

The 75x350mmR ammo set provides three-way ammo selection: AP (206 Bullet damage, 88mm Sharp penetration), HE (104 Bomb damage, radius 2), and APCR (197 Bullet damage, 154mm Sharp penetration). This gives players tactical flexibility across engagement types -- AP for armored targets, HE for infantry clusters, APCR for punching through heavy armor at the cost of reduced post-penetration damage.


### Flak Turrets: P1000_FlakTurret x2 (lines 144-219)

The P1000's anti-aircraft mounts use `AmmoSet_20x128mmOerlikon`, CE's implementation of the WW2-era Oerlikon 20mm autocannon. The original P1000 design specified Flakvierling-style AA positions; the Oerlikon is a direct period-appropriate match.

| Parameter | Vanilla Value | CE Patch Value | Rationale |
|---|---|---|---|
| Projectile | `P1000_Flak_HE` (20 dmg, R1.2) | `Bullet_20x128mmOerlikon_HE` | AP/HE/Incendiary/Sabot variants |
| reloadTimer | (original) | 7.8s | Matches Tango autocannon |
| warmUpTimer | (original) | 2.3s | Fast acquisition for AA role |
| magazineCapacity | 40 | 60 | Increased to sustain higher CE fire rate |
| maxRange | 58.9 | 78 | Matches Tango autocannon |
| shotHeight | -- | 2.0 | Mid-height AA mount |
| speed | -- | 183 | 20mm muzzle velocity |
| sway | -- | 1.61 | Matches Tango; autocannons inherently have higher sway than stabilized cannons |
| spread | -- | 0.01 | Standard |

Fire modes are replaced entirely. The vanilla burst pattern (10 shots at 10 ticks between shots) is replaced with CE's standard three-mode selector:

| Mode | Shots/Burst | Ticks Between | Label |
|---|---|---|---|
| Single | 1 | 6 | Single |
| Burst | 3 | 6 | Burst |
| Auto | 8 | 6 | Auto |

The 6-tick interval is CE's standard rate for 20mm autocannon fire. The three-mode selector follows CE convention -- players can conserve ammo with single shots or saturate an area with 8-round automatic bursts.

Magazine capacity was raised from 40 to 60 to compensate for the higher sustained fire rate. At 6 ticks per round in auto mode, a 40-round magazine empties in 240 ticks (4 seconds); at 60 rounds, the gun sustains fire for 360 ticks (6 seconds) before requiring a reload.


### Ball Turrets: P1000_Ball_Turret x4 (lines 225-300)

The four hull-mounted ball turrets convert to `AmmoSet_762x51mmNATO`, matching the CE Highwayman MG patch in every parameter:

| Parameter | CE Patch Value | Source |
|---|---|---|
| reloadTimer | 7.8s | Highwayman MG |
| warmUpTimer | 1.3s | Highwayman MG |
| magazineCapacity | 200 | Highwayman MG |
| maxRange | 55 | Highwayman MG |
| shotHeight | 2.0 | Mid-height hull mount |
| speed | 156 | 7.62mm NATO velocity |
| sway | 0.96 | Highwayman MG |
| spread | 0.04 | Highwayman MG; higher than cannons due to MG class |
| AmmoSet | `AmmoSet_762x51mmNATO` | Highwayman MG |

Fire modes follow CE convention for medium machine guns:

| Mode | Shots/Burst | Ticks Between | Label |
|---|---|---|---|
| Single | 1 | 6 | Single |
| Burst | 5 | 6 | Burst |
| Auto | 10 | 6 | Auto |

The higher spread (0.04 vs 0.01 for cannons) reflects the inherent inaccuracy of a ball-mounted machine gun firing from a moving vehicle. At 200-round magazines and 7.62mm logistics costs, these turrets provide sustained suppressive fire cheaply -- the intended role for hull MGs on a super-heavy.

The Highwayman MG was chosen as the reference over other CE vehicle MG configurations because it uses the same 7.62mm NATO caliber and serves an identical tactical role (hull-mounted suppression weapon on an armored vehicle). The 200-round belt size, 7.8s reload, and 1.3s warmup represent CE's standard for vehicle-mounted medium MGs -- values that are tested and balanced within CE's existing vehicle ecosystem.


## Engine Explosion Radius (lines 309-314)

The engine explosion radius is reduced from 40 to 3 via a single `PatchOperationReplace` on the `Reactor_Explosive` component:

```xml
<xpath>Defs/Vehicles.VehicleDef[defName="P1000_tank"]/components/li[key="Engine"]/reactors/li[@Class="Vehicles.Reactor_Explosive"]/radius</xpath>
```

CE's standard practice for VVE vehicle engine explosions is radius 1. The P1000 receives radius 3 -- larger than the CE standard -- because a vehicle carrying 28cm naval ordnance and an implied 1000+ liters of chemfuel should produce a more significant explosion when its engine detonates. But radius 40 under CE, where explosions generate lethal fragment projectiles with independent penetration rolls, would be a map-clearing event. For context, the 28cm HE shell itself only has explosion radius 7.


## Ammo Hyperlinks (lines 320-330)

A `PatchOperationAdd` injects `descriptionHyperlinks` into the `P1000_tank` VehicleDef, listing all four ammo sets:

- `AmmoSet_28cmSpgrShell`
- `AmmoSet_75x350mmR`
- `AmmoSet_20x128mmOerlikon`
- `AmmoSet_762x51mmNATO`

This follows CE VVE convention -- when a player inspects the P1000's info card, they see clickable links to each ammo type with crafting costs, damage values, and penetration data.


## Weapon Hierarchy

The five turret types form a coherent combined-arms system at CE scale:

| Turret | Count | Caliber | Role | CE Ammo Damage (HE) | AP Pen (Sharp) | Range |
|---|---|---|---|---|---|---|
| Main Cannon | 1 | 280mm | Siege / structure demolition | 837 + R7 + 120 fragments | HE only | 78.9 |
| Maus Cannon | 3 | 75mm | Medium direct fire | 104 + R2 | 88mm | 86 |
| Flak Turret | 2 | 20mm | Area denial / anti-air | 75 + 35 Bomb | 36mm | 78 |
| Ball Turret | 4 | 7.62mm | Anti-infantry suppression | ~12 | ~12mm | 55 |

Damage output scales monotonically with caliber: the 280mm deals 7x more damage than the 75mm, which deals 8.7x more than the 7.62mm. Each tier fills a distinct engagement niche -- the main cannon demolishes fortifications, the 75mm guns handle vehicles and mechanoids, the 20mm Oerlikons deny areas to light vehicles and massed infantry, and the ball turrets suppress individual combatants.


## Ammo Logistics

| Ammo | Mass per Round | Crafting Cost (per batch) | Batch Size |
|---|---|---|---|
| 28cm Spgr. HE | 300.0 kg | 600 Steel + 2 Components + 33 FSX | 5 rounds |
| 75x350mmR HE | ~8.8 kg | 88 Steel + 2 Components + 6 FSX | 5 rounds |
| 20x128mm Oerlikon AP | 0.353 kg | 142 Steel | 200 rounds |
| 7.62x51mm FMJ | 0.025 kg | Standard rifle ammo | Standard |

The 28cm shells at 300kg each and 600 Steel per 5-round batch make the P1000's main gun the most logistics-intensive weapon in the CE ecosystem. A full engagement cycle (2 rounds, reload, 2 rounds) consumes 4 shells -- nearly an entire crafting batch. This is the primary balance lever: the P1000's firepower is enormous but demands an industrial base to sustain. Colonies that cannot maintain steady steel and FSX production will find the main gun starved of ammunition long before it runs out of targets.

The lower-caliber weapons follow standard CE logistics curves. The 7.62mm ball turrets use the same ammunition as every other NATO-caliber weapon in the game, meaning they draw from the colony's existing rifle ammo stockpile with no additional crafting infrastructure.


## Deliberate Omissions

### Vehicle Armor -- Not Rescaled

The original design analysis identified CE-scale armor values (front 30mm Sharp, back 15mm Sharp) for the P1000's hull. However, the implemented patch does not include armor rescaling. Vehicle hull armor is expected to be handled at the Vehicle Framework integration layer rather than per-mod. If Vehicle Framework implements a systematic armor conversion for all VVE vehicles under CE, per-mod rescaling would conflict with or duplicate that system.

The patch focuses exclusively on turret weapon functionality -- the domain where CE's auto-patcher provably fails and no framework-level solution exists.

### Custom DamageDefs -- Left as Dead Code

The P1000 mod defines custom damage types (`P1000_spookyBomb`, `P1000_flakBomb`) used by the vanilla projectiles. Once turrets point to CE ammo projectiles, these DamageDefs become unreferenced. They are deliberately left in place rather than removed:

1. Removing defs via XML patch risks breaking other mods that might cross-reference them.
2. Unreferenced defs impose negligible runtime cost (they sit in the def database but are never instantiated).
3. If the CE patch is removed (mod load order change, mod update), the original projectiles resume referencing these defs without error.


## Why Pure XML

Three factors make a C#-free approach both possible and preferable for this patch:

1. **Vehicle Framework provides the bridge.** `CETurretDataDefModExtension` handles all the runtime integration between `VehicleTurretDef` and CE's ammo/ballistic systems. The extension class is defined in Vehicle Framework's own assembly, not in V2CEPatch. This patch merely attaches the extension to each turret def via `PatchOperationAddModExtension`.

2. **No behavioral changes required.** Unlike the VWE Heavy Weapons patch (which must replicate VEF's deterministic degradation model via Harmony postfixes), the P1000 patch only needs to redirect existing turrets to CE ammo sets and adjust numerical parameters. There is no custom firing logic, no guidance system, no per-shot side effects.

3. **No custom ammo types.** Every ammo set used (`28cmSpgrShell`, `75x350mmR`, `20x128mmOerlikon`, `762x51mmNATO`) is already defined in CE's base ammo database. The patch creates no new AmmoDefs, no new projectile ThingDefs, and no new CompProperties. It exclusively references existing CE content.

A pure XML patch eliminates an entire class of failure modes: no assembly version mismatches, no Harmony conflict potential, no reflection targets that shift between CE versions. The patch either applies cleanly at game startup (both mods present, XPath resolves) or does nothing (either mod absent, `PatchOperationFindMod` skips).


## Why Not Custom Ammo or Projectile Defs

An alternative approach would have been to define new CE-compatible projectile ThingDefs that preserve the P1000 mod's original damage numbers and explosion radii, scaled to CE conventions. For example, creating a `Bullet_P1000_Main_HECE` projectile with custom damage values tuned specifically to the P1000's intended power level.

This was rejected for three reasons:

1. **Maintenance cost.** Every custom projectile ThingDef must inherit from `BaseBulletCE` and specify CE-specific fields (`armorPenetrationSharp`, `armorPenetrationBlunt`, `explosionRadius`, `fragments`, etc.). When CE updates these field names or changes the inheritance chain -- which has happened in past major versions -- custom defs break. Referencing existing CE ammo sets delegates this maintenance to the CE team.

2. **Balance isolation.** Custom damage numbers exist in a vacuum. CE's 28cm shell at 837 damage with 7 radius and 120 fragments is balanced against CE's armor system, CE's cover mechanics, and CE's suppression model. A custom projectile with, say, 500 damage and radius 10 would need independent testing against the full CE combat pipeline to verify it does not trivialize or underperform. Using CE's own ammo inherits that testing.

3. **Ammo crafting integration.** Custom projectiles would require custom RecipeDefs for crafting, custom AmmoSetDefs for ammo selection, and custom ThingDefs for the ammo items themselves. This is 15-20 additional defs per caliber. The current patch achieves the same result with zero new defs by pointing turrets at CE's existing ammo library.
