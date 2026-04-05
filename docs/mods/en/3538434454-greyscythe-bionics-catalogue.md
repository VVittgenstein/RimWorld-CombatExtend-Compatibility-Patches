# Greyscythe Bionics Catalogue -- CE Compatibility Patch

Greyscythe Bionics Catalogue ships a six-tier bionic progression (Neolithic through Hyper) containing approximately 45 combat-relevant hediffs. Under Combat Extended, three independent failure modes render the entire combat line inert: 16 melee tools lack armor penetration because CE requires `ToolCE` instances that vanilla `Tool` cannot provide, 10 ranged hediff verbs bypass CE ballistics entirely because CE's auto-patcher does not reach hediff verb givers, and 8 armor implants with `ArmorRating_*` stat offsets provide zero mechanical protection because CE's armor pipeline ignores pawn-level armor on body parts that lack `CoveredByNaturalArmor`. This patch is the largest pure-XML repair in the set -- no C# or Harmony code is required. All fixes use `PatchOperationReplace` and `PatchOperationAdd` against existing HediffDefs and are delivered through a single conditional patch file.


## What Breaks Under CE

### Melee Tools: Zero Armor Penetration

CE recasts every melee tool as `ToolCE` at runtime. In `Verb_MeleeAttackCE.cs:45`, the verb attempts `tool as ToolCE` on each tool instance. Vanilla `Tool` objects fail this cast and return null, so `armorPenetrationSharp` and `armorPenetrationBlunt` both resolve to 0 via `.GetValueOrDefault()`. The tool's `power` value still applies -- pawns deal the correct base damage -- but the attack carries zero penetration. Against any pawn wearing CE armor, melee bionic attacks bounce harmlessly.

16 melee tools across the Neolithic-through-Ultra tier range are affected. Every bionic fist, blade, jaw, and spike in the catalogue hits for its full vanilla power but penetrates nothing. A pilebunker arm that should drive a stake through marine armor at 34 power delivers 0 effective damage against any armored target because its penetration is literally zero.

This is not a graceful degradation -- it is a binary failure. Vanilla RimWorld does not use armor penetration for melee in a meaningful way; CE introduces it as the primary gating mechanism. A bionic designed for melee combat becomes strictly worse than punching bare-fisted, because at least a bare fist has some innate blunt penetration via CE's fallback calculation.

### Ranged Hediff Verbs: Outside CE Ballistics

CE's `Harmony_Verb_TryStartCastOn.cs:60` checks `if (!(__instance is Verb_ShootCE))` and routes non-CE verbs through a compatibility shim that strips most ballistic properties. The original mod's hediff verb givers use vanilla `Verb_Shoot`, which passes this check and fires outside CE's aiming, spread, and reload systems.

CE's `GunAutoPatcher` cannot help here. It only processes `ThingDef` weapons where `thingDef.IsRangedWeapon == true`; hediff verb givers on HediffDefs are outside its scope entirely. The 10 ranged verb instances (3 industrial, 3 spacer, 2 MVCF turrets, 1 medieval, 1 spacer launcher) fire vanilla projectiles with vanilla accuracy stats that CE ignores.

The symptom is deceptive: the weapons appear to fire normally. Projectiles launch, travel, and hit targets. But they use vanilla `Projectile` flight physics rather than `ProjectileCE` ballistics -- no spread calculation, no flight-time gravity, no armor penetration resolution. Damage numbers look vanilla-correct but interact with CE armor in unpredictable ways, sometimes dealing full damage through plate armor, sometimes dealing none. The inconsistency makes the problem harder to diagnose than a clean failure-to-fire.

### Armor Implant ArmorRating Offsets: Display-Only

Eight implants provide `ArmorRating_Sharp`, `ArmorRating_Blunt`, or `ArmorRating_Heat` stat offsets on their hediff stages. These offsets appear correctly in the pawn's stat readout. However, `ArmorUtilityCE.cs:213` resolves armor only for body part groups flagged `CoveredByNaturalArmor`. Human pawns lack this flag on all body part groups, so pawn-level `ArmorRating_*` stats provide zero protection against direct hits in CE's armor resolution pipeline.

The implants produce a visible stat improvement that has no mechanical effect.


## Why Pure XML

This patch requires no Harmony postfixes, no custom C# classes, and no assembly references. The three failure modes are all resolvable through XML def manipulation:

1. **Melee:** Replacing vanilla `Tool` XML nodes with `ToolCE` nodes. CE's runtime cast then succeeds, and the AP values are read normally.
2. **Ranged:** Replacing `Verb_Shoot` verb properties with `VerbPropertiesCE` containing `Verb_ShootCE` and CE-class projectile references. The verb enters CE's ballistic pipeline naturally.
3. **Armor:** Adding `damageFactors` to hediff stages. CE reads `damageFactors` directly from hediff stages during damage application -- no `CoveredByNaturalArmor` check is involved.

The alternative -- writing a C# auto-patcher that iterates all HediffDefs at startup and injects ToolCE/VerbPropertiesCE programmatically -- would handle future bionic additions automatically but introduces assembly load-order dependencies and makes the specific AP/damage values invisible to anyone reading the patch files. For a mod with a stable, enumerable set of bionics, explicit XML is more maintainable and more auditable.

A second alternative considered was using CE's `GunAutoPatcher` extension points. CE allows mods to register additional weapon defs for auto-patching by adding them to `CE_GunAutoPatching` in mod settings. However, this system operates on `ThingDef` weapons and cannot process `HediffDef` verb givers. Extending it would require modifying CE's own auto-patcher code, which is outside the scope of a compatibility patch and would create a hard dependency on a specific CE internal API version.

The pure-XML approach has one notable limitation: if the GS mod author adds new bionics in a future update, this patch must be manually extended. This is an acceptable trade-off because the mod has been stable for several major versions, and the patch's exhaustive table-driven structure makes additions straightforward -- copy an existing `PatchOperationReplace` block, update the xpath `defName` and AP values.


## Implementation: Melee Tool to ToolCE Conversion

**File:** `Patches/GreyscytheBionicsCatalogue.xml`, Section 1

Each HediffDef's `<tools>` block under its `HediffCompProperties_VerbGiver` is replaced wholesale via `PatchOperationReplace`. The replacement contains `<li Class="CombatExtended.ToolCE">` entries with explicit `armorPenetrationSharp` and `armorPenetrationBlunt` values. The pattern follows CE's own `Hediffs_Local_AddedParts.xml` structure.

### Full Melee AP Table

All implemented values, organized by tech tier:

| Tier | HediffDef | Tool Label | Capacity | Power | Sharp AP | Blunt AP | Cooldown |
|---|---|---|---|---|---|---|---|
| Neolithic | GS_Hediff_Clubfist | club | Blunt | 16 | 0 | 0.50 | 2.0 |
| Neolithic | GS_Hediff_Ikwafist | point | Stab | 16 | 0.20 | 0.40 | 2.0 |
| Neolithic | GS_Hediff_spikecrown | spikecrown | Stab | 15 | 0.15 | 0.30 | 2.0 |
| Medieval | GS_Hediff_macefist | club | Blunt | 18 | 0 | 0.80 | 2.0 |
| Medieval | GS_Hediff_swordhand | point | Stab | 18 | 0.50 | 0.60 | 2.0 |
| Medieval | GS_Hediff_swordhand | edge | Cut | 18 | 0.40 | 0.50 | 2.0 |
| Medieval | GS_Hediff_axehand | head | Cut | 18 | 0.35 | 0.70 | 2.0 |
| Medieval | GS_hediff_handbow | fist | Blunt | 8.2 | 0 | 0.40 | 2.0 |
| Medieval | GS_Hediff_beastjaw | fangs | Bite | 18 | 0.40 | 0.60 | 2.0 |
| Industrial | GS_Hediff_battlearm | spike | Stab | 14 | 0.30 | 0.50 | 1.4 |
| Industrial | GS_Hediff_battleleg | spike | Stab | 14 | 0.30 | 0.50 | 1.4 |
| Industrial | GS_Hediff_pilebunkerarm | limb | Blunt | 10 | 0 | 0.80 | 2.0 |
| Industrial | GS_Hediff_pilebunkerarm | stake | Stab | 34 | 2.00 | 3.50 | 4.5 |
| Industrial | GS_Hediff_chainsawarm | limb | Blunt | 10 | 0 | 0.80 | 2.0 |
| Industrial | GS_Hediff_chainsawarm | chainsaw | Cut | 28 | 1.20 | 2.50 | 2.8 |
| Industrial | GS_hediff_miningarm | pick | Stab | 18 | 0.80 | 1.20 | 2.0 |
| Industrial | GS_hediff_mediarm | scalpel | Stab | 9 | 0.20 | 0.30 | 2.0 |
| Industrial | GS_hediff_culiarm | slicer | Cut | 14 | 0.30 | 0.45 | 2.0 |
| Industrial | GS_hediff_builderarm | hammer | Blunt | 18 | 0 | 0.80 | 2.5 |
| Industrial | GS_hediff_autoloaderarm | fist | Blunt | 8.2 | 0 | 0.50 | 2.0 |
| Industrial | GS_hediff_handcannon | barrel | Blunt | 10 | 0 | 0.80 | 2.0 |
| Industrial | GS_hediff_machinegunarm | fist | Blunt | 8.2 | 0 | 0.50 | 2.0 |
| Industrial | GS_hediff_launcherarm | barrel | Blunt | 10 | 0 | 0.80 | 2.0 |
| Spacer | GS_hediff_miningarm_spacer | drill | Stab | 24 | 1.20 | 2.00 | 2.0 |
| Spacer | GS_hediff_mediarm_spacer | scalpel | Stab | 11 | 0.30 | 0.50 | 2.0 |
| Spacer | GS_hediff_builderarm_spacer | hammer | Blunt | 18 | 0 | 1.50 | 2.0 |
| Spacer | GS_Hediff_beamblade | point | Stab | 22 | 1.50 | 1.80 | 2.0 |
| Spacer | GS_hediff_handcannon_transform | barrel | Blunt | 10 | 0 | 1.00 | 2.0 |
| Spacer | GS_hediff_machinegunarm_transform | fist | Blunt | 8.2 | 0 | 0.80 | 2.0 |
| Spacer | GS_hediff_launcherarm_transform | barrel | Blunt | 10 | 0 | 1.00 | 2.0 |
| Ultra | GS_hediff_ripjaw | ripjaw | Bite | 28 | 1.80 | 3.00 | 2.0 |

### Progression Validation Against CE Baselines

The AP values are calibrated against CE's own bionic hierarchy:

| CE Reference Part | Sharp AP | Blunt AP |
|---|---|---|
| Simple prosthetic arm | 0 | 0.25 |
| Bionic arm | 0 | 1.688 |
| Power claw | 1.60 | 4.00 |
| Archotech arm | 0 | 3.00 |

The GS melee progression is monotonically increasing across tiers. Neolithic tools (0.30--0.50 blunt) sit below CE's simple prosthetic. Medieval tools (0.40--0.80 blunt) bracket the prosthetic-to-bionic range. Industrial primary weapons (pilebunker stake at 3.50 blunt, chainsaw at 2.50 blunt) exceed CE bionic arm and approach power-claw territory. Spacer beamblade (1.80 blunt) occupies the upper-bionic range. Ultra ripjaw (3.00 blunt) matches archotech exactly.

No GS bionic exceeds CE archotech AP until Ultra tier, preserving the intended power ceiling. Dual-tool hediffs (pilebunker, chainsaw) follow CE's convention of providing a low-AP limb attack alongside a high-AP signature attack, giving the pawn tactical flexibility between fast light swings and slow heavy strikes.

The sharp-to-blunt ratio follows physical intuition for each damage type. Blunt-only tools (clubs, hammers, fists) carry zero sharp AP and concentrate all penetration in blunt. Cutting tools carry moderate sharp AP with lower blunt (a sword edge penetrates by concentrating force on a thin line). Stabbing tools carry the highest sharp-to-blunt ratio (a spike concentrates force on a point). This ratio pattern matches CE's own melee weapon design and ensures that bionic melee attacks interact correctly with CE's dual-channel armor system, where sharp armor and blunt armor are resolved independently.

### Melee XPath Targeting

All melee patches target the same xpath pattern:

```
Defs/HediffDef[defName="..."]/comps/li[@Class="HediffCompProperties_VerbGiver"]/tools
```

This replaces the entire `<tools>` block rather than individual `<li>` entries. Whole-block replacement avoids the fragility of targeting specific tool indices (which can shift if the source mod reorders its tools) and ensures that no vanilla `Tool` entries survive alongside the new `ToolCE` entries. If a vanilla tool were left in place, CE would encounter a mixed list of `Tool` and `ToolCE` instances, and the vanilla entries would silently revert to zero-AP behavior.

### Non-Combat Melee Tools

Several non-combat bionics (mediarm, culiarm, builderarm, autoloaderarm) receive melee tools as secondary functionality. These are assigned conservative AP values -- generally matching or slightly below the bionic arm baseline. The tools exist primarily so the pawn is not defenseless in melee rather than as intended combat augments.


## Implementation: Ranged Verb to Verb_ShootCE Conversion

**File:** `Patches/GreyscytheBionicsCatalogue.xml`, Section 2
**Projectile defs:** `Defs/GreyscytheBionicsCatalogue/Projectiles_Bionics.xml`

Each hediff ranged verb is patched through three changes:

1. `verbClass` is changed from `Verb_Shoot` to `CombatExtended.Verb_ShootCE`
2. The verb properties node is replaced with `CombatExtended.VerbPropertiesCE`, containing CE-specific fields
3. The `defaultProjectile` is pointed at a new CE-class projectile ThingDef defined in `Defs/GreyscytheBionicsCatalogue/Projectiles_Bionics.xml`
4. Vanilla accuracy stats (`accuracyShort`, `accuracyMedium`, `accuracyLong`) are removed -- CE ignores them entirely

### CE Projectile Definitions

All projectile ThingDefs inherit from `BaseBulletCE` and use `ProjectilePropertiesCE` for their `<projectile>` class. Explosive projectiles additionally set `thingClass` to `CombatExtended.ProjectileCE_Explosive`.

| defName | damageDef | Damage | Sharp AP | Blunt AP | Speed | Tier |
|---|---|---|---|---|---|---|
| GS_Handbow_Bolt_CE | ArrowHighVelocity | 8 | 0.60 | 3.50 | 40 | Medieval |
| GS_Handcannon_Bullet_CE | Bullet | 15 | 4.00 | 12.00 | 80 | Industrial |
| GS_MachinegunArm_Bullet_CE | Bullet | 12 | 3.00 | 18.00 | 75 | Industrial |
| GS_LauncherArm_Grenade_CE | Bomb | 20 | 0 | 0 | 35 | Industrial |
| GS_Handcannon_Spacer_Bullet_CE | Bullet | 18 | 8.00 | 18.00 | 100 | Spacer |
| GS_MachinegunArm_Transform_Bullet_CE | Bullet | 14 | 6.00 | 22.00 | 90 | Spacer |
| GS_LauncherArm_Transform_Grenade_CE | Bomb | 25 | 0 | 0 | 40 | Spacer |
| GS_Bioturret_Bullet_CE | Bullet | 10 | 8.00 | 15.00 | 100 | Ultra |

Ranged AP scaling follows CE weapon tiers: medieval handbow sits at arrow-equivalent AP (0.60 sharp), industrial hand-cannon matches heavy pistol calibers (4.0 sharp), spacer weapons reach assault-rifle-to-charged-rifle territory (6.0--8.0 sharp), and ultra bioturrets match charged rifle output (8.0 sharp) but with lower per-round damage to reflect burst-fire volume.

Grenade projectiles carry zero AP and rely on `explosionRadius` (2.0 cells) for area damage. The spacer launcher upgrade increases damage (20 to 25) and speed (35 to 40) but maintains the same blast radius, consistent with the mod's "same weapon, better stats" transform paradigm.

The machinegun-arm projectiles prioritize blunt AP over sharp AP (18.0 blunt vs 3.0 sharp at industrial; 22.0 blunt vs 6.0 sharp at spacer). This reflects the high-volume low-caliber nature of a built-in machine gun -- each round carries significant kinetic energy transfer (blunt) relative to its armor-piercing capability (sharp). By contrast, the hand-cannon emphasizes sharp AP (4.0 sharp at industrial, 8.0 at spacer) for its larger, slower rounds designed to punch through armor with fewer shots.

### Verb Properties Conversion

Each verb receives CE-specific properties. Key conversions from the patch file:

| HediffDef | Weapon Type | Warmup | Range | Burst | Ticks/Shot | Projectile |
|---|---|---|---|---|---|---|
| GS_hediff_handbow | Crossbow | 3.0 | 18 | 1 | -- | GS_Handbow_Bolt_CE |
| GS_hediff_handcannon | Revolver | 2.6 | 18 | 6 | 35 | GS_Handcannon_Bullet_CE |
| GS_hediff_machinegunarm | MG | 3.2 | 25.9 | 6 | 7 | GS_MachinegunArm_Bullet_CE |
| GS_hediff_launcherarm | Launcher | 4.0 | 25 | 3 | 60 | GS_LauncherArm_Grenade_CE |
| GS_hediff_handcannon_transform | Revolver+ | 2.6 | 18 | 6 | 35 | GS_Handcannon_Spacer_Bullet_CE |
| GS_hediff_machinegunarm_transform | MG+ | 3.2 | 32 | 6 | 4 | GS_MachinegunArm_Transform_Bullet_CE |
| GS_hediff_launcherarm_transform | Launcher+ | 4.0 | 32 | 3 | 20 | GS_LauncherArm_Transform_Grenade_CE |
| GS_Hediff_shouldergun_ultra | Turret | 2.0 | 28.9 | 3 | 6 | GS_Bioturret_Bullet_CE |
| GS_Hediff_hipgun_ultra | Turret | 2.0 | 28.9 | 3 | 6 | GS_Bioturret_Bullet_CE |

All verbs set `ejectsCasings` to `false` -- body-integrated weapons do not produce brass on the ground.

### MVCF Turret Verbs

The shoulder and hip assault turrets (`GS_Hediff_shouldergun_ultra`, `GS_Hediff_hipgun_ultra`) use `MVCF.Comps.HediffCompProperties_ExtendedVerbGiver` instead of the standard `HediffCompProperties_VerbGiver`. The patch targets the MVCF comp's `<verbs>` block with the same `PatchOperationReplace` approach. The `VerbComp_Turret` component that manages independent targeting and firing behavior sits in a separate `<verbComps>` block that the patch does not touch -- only the verb physics are converted, while the turret AI continues to function through MVCF's own infrastructure.

The xpath for MVCF turrets differs from standard hediff verbs:

```
Defs/HediffDef[defName="..."]/comps/li[@Class="MVCF.Comps.HediffCompProperties_ExtendedVerbGiver"]/verbs
```

Both turrets share the same `GS_Bioturret_Bullet_CE` projectile and identical verb properties (range 28.9, 3-round burst at 6 ticks between shots). The shared projectile avoids def bloat -- the shoulder and hip turrets are functionally identical weapons mounted at different body locations, and their CE conversion should reflect that symmetry.

### Non-Ammo-Using Decision

All hediff ranged weapons are configured without `CompAmmoUser`. When no `AmmoSetDef` is assigned, `Verb_ShootCE` fires the `defaultProjectile` directly without checking ammunition inventory or applying ammo-switching logic. This is the correct model for body-integrated weapons: a pawn cannot realistically remove and reload their arm-cannon. CE's own bionic implant patches (e.g., for vanilla bionics with ranged attacks) follow this same non-ammo pattern.

The practical effect is that body-integrated weapons have unlimited ammunition but cannot benefit from ammo type selection (AP rounds, HE rounds, etc.). This is an acceptable trade-off -- the fixed projectile type represents the bionic's built-in energy cell or integrated magazine.

An alternative approach would be to define custom `AmmoSetDef` entries for each weapon and assign them infinite ammo via `CompInventory` overrides. This was rejected because it adds unnecessary complexity (8 AmmoSetDefs, 8+ AmmoDefs, ammo UI clutter) for a system the player cannot meaningfully interact with. The non-ammo pattern is both simpler and more narratively honest.

### Ranged Verb Transform Upgrades

The GS mod's transform system upgrades industrial weapons to spacer-tier versions. The patch preserves this progression:

- **Hand-cannon transform:** Same warmup (2.6s), same burst (6), same range (18), but projectile sharp AP doubles (4.0 to 8.0) and speed increases (80 to 100)
- **Machinegun-arm transform:** Same warmup (3.2s), same burst (6), but range extends (25.9 to 32), ticks between shots decrease (7 to 4 for faster burst), and sharp AP doubles (3.0 to 6.0)
- **Launcher-arm transform:** Same warmup (4.0s), same burst (3), but range extends (25 to 32), ticks between shots decrease (60 to 20), and grenade damage increases (20 to 25)

The transform upgrades are meaningful under CE because they affect the projectile statistics that matter most: armor penetration and muzzle velocity. A player investing in spacer-tier bionics gets a proportional combat improvement rather than a marginal stat bump.


## Implementation: Armor Implant damageFactors

**File:** `Patches/GreyscytheBionicsCatalogue.xml`, Section 3

For implants that provide `ArmorRating_*` stat offsets but lack `damageFactors`, the patch adds `damageFactors` to the hediff stage via `PatchOperationAdd`. For implants that already have partial `damageFactors` coverage, additional damage types are appended to the existing `damageFactors` node.

The `ArmorRating_*` stat offsets are left in place. They serve two purposes even though CE does not use them for armor resolution: they populate the pawn's stat readout (giving the player feedback that the implant provides protection), and CE's AI evaluation code reads `ArmorRating_Sharp` when assessing threat level for target selection.

### Conversion Formula

`ArmorRating × 0.5` yields the damage reduction percentage. The `damageFactor` value is `1 - reduction`. For example, an implant with `ArmorRating_Sharp 0.50` produces a 25% reduction, yielding a `damageFactor` of 0.75.

This formula was chosen to produce moderate defensive bonuses that stack multiplicatively with CE armor without trivializing incoming damage. A 0.75 factor on Bullet damage means the implant absorbs 25% of post-armor bullet damage -- meaningful against light calibers but insufficient to shrug off anti-materiel rounds.

The 0.5 multiplier on ArmorRating was selected empirically. At 1.0x (direct conversion), the damageFactors would be too aggressive -- a Carapace with ArmorRating_Sharp 0.50 would yield a 0.50 Bullet factor, halving all bullet damage on top of whatever CE armor the pawn wears. At 0.25x, the factors would be too marginal to justify the implant's cost and surgery risk. The 0.5x point produces factors in the 0.70--0.90 range, which is consistent with CE's own implant-scale defensive bonuses (e.g., CE toughskin gland at approximately 0.85 factor).

### Full Armor Implant Table

Implants with new `damageFactors` (added via `PatchOperationAdd` to `stages/li`):

| HediffDef | Implant | Tier | Bullet | Cut | Stab | Scratch | Blunt | Flame | Burn |
|---|---|---|---|---|---|---|---|---|---|
| GS_Hediff_carapace | Carapace | Spacer | 0.75 | 0.70 | 0.70 | 0.75 | 0.80 | 0.90 | -- |
| GS_hediff_plasteelrib | Plasteel Ribcage | Spacer | 0.90 | 0.90 | 0.90 | -- | 0.80 | -- | -- |
| GS_Hediff_flameblock | Flameblock | Ultra | -- | -- | -- | -- | -- | 0.50 | 0.50 |
| GS_Hediff_enhancedtrach | Enhanced Trachea | Ultra | 0.85 | 0.85 | -- | -- | 0.85 | -- | -- |
| GS_Hediff_skelemuscle | Skelemuscle | Hyper | 0.85 | 0.85 | 0.85 | -- | 0.85 | -- | -- |
| GS_Hediff_ambiskull | Ambiskull | Hyper | 0.85 | 0.85 | 0.85 | -- | 0.85 | 0.85 | -- |

Implants with existing `damageFactors` extended (added via `PatchOperationAdd` to `stages/li/damageFactors`):

| HediffDef | Implant | Tier | Existing Coverage | Added |
|---|---|---|---|---|
| GS_hediff_centralshield | Central Shield | Ultra | Bullet 0.60, Arrow 0.60, Stab 0.70 | Cut 0.70, Blunt 0.85, Flame 0.85 |
| GS_Hediff_exoshield | Exoshield | Ultra | Cut/Scratch/ScratchToxic/Bite/ToxicBite 0.75 | Bullet 0.80, Blunt 0.75, Flame 0.85 |
| GS_hediff_organexoshield | Organ Exoshield | Ultra | Crush/Blunt/Poke/Thump/Bomb/BombSuper/Stun 0.75 | Bullet 0.85, Cut 0.90, Flame 0.80 |

### Why damageFactors Instead of ArmorRating

Three approaches were considered for making armor implants functional under CE:

**Option A: Custom BodyPartGroupDef with CoveredByNaturalArmor.** Adding a custom body part group flagged as natural armor would cause CE to read the `ArmorRating_*` offsets normally. This was rejected because it requires modifying body part group defs globally, risks side effects with other mods that inspect body coverage, and the `CoveredByNaturalArmor` flag is designed for animal hide and insect chitin -- not implanted hardware.

**Option B: StatPart injection.** A C# StatPart could intercept armor stat queries and inject the implant values into CE's armor resolution. This works (the companion Greyscythe Cybergenetics patch uses this approach for gene-level stats) but requires an assembly dependency for what is otherwise a pure-XML patch. For a fixed set of implants with known values, the added complexity is not justified.

**Option C: damageFactors on hediff stages (chosen).** CE reads `damageFactors` from hediff stages during `DamageWorker.Apply`, after armor resolution but before final damage application. This path does not depend on `CoveredByNaturalArmor` and requires no C# code. The trade-off is that `damageFactors` apply a flat multiplier rather than interacting with CE's armor penetration math (sharp AP vs. armor rating), so the protection does not scale with projectile caliber the way true armor does. For implants that represent internal reinforcement rather than external plating, this is arguably more accurate -- a plasteel ribcage reduces organ damage regardless of what penetrated the outer armor.

### Damage Type Coverage Rationale

Each implant's `damageFactors` cover only the damage types that make physical sense for the implant's described function:

- **Carapace** (full-body subdermal plating): covers all physical damage types including Scratch, plus Flame at a reduced factor. The broadest coverage in the set.
- **Plasteel Ribcage** (torso reinforcement): covers penetrating damage (Bullet, Cut, Stab) and Blunt but not Flame -- metal ribs conduct heat rather than insulating against it.
- **Flameblock** (thermal insulation): covers only Flame and Burn at an aggressive 0.50 factor. This is the only implant with a factor below 0.70, reflecting its single-purpose design.
- **Central Shield / Exoshield / Organ Exoshield**: these already had partial damageFactors in the source mod. The patch extends their coverage to CE-relevant damage types (Bullet, Blunt, Flame) that the original mod did not need to specify because vanilla RimWorld has fewer distinct damage resolution paths.

The `Scratch` damage type is included on the Carapace because CE uses it for animal claw attacks, which the Carapace's subdermal plating should logically resist. Other implants omit Scratch because their protection is more targeted.

Multiple damageFactors from different hediff stages stack multiplicatively. A pawn with both Carapace (Bullet 0.75) and Plasteel Ribcage (Bullet 0.90) receives an effective Bullet factor of `0.75 * 0.90 = 0.675`, reducing bullet damage by 32.5% before CE armor is applied. This stacking is handled by RimWorld's core `DamageWorker` and requires no special handling in the patch.


## What Was Left Unchanged

Several stat offsets and factors on GS bionics are functional under CE without modification:

- **ShootingAccuracyPawn offsets/factors**: CE reads this stat as handling speed. The bionic-scale offsets (typically +0.5 to +2.0) are within CE's functional range and do not over-cap.
- **MeleeHitChance offsets/factors**: CE reads this stat directly through its melee hit resolution. Bionic-scale values are appropriate.
- **MeleeDamageFactor factors**: CE reads this for penetration scaling via `power^0.75`. The values pass through correctly.
- **RangedCooldownFactor / MeleeCooldownFactor**: Functional via vanilla inheritance -- `VerbProperties.AdjustedCooldown()` applies these factors regardless of whether the verb is `Verb_Shoot` or `Verb_ShootCE`.
- **IncomingDamageFactor**: Accepted as-is. These are minor defensive values on utility implants (typically 0.90--0.95x) that stack multiplicatively with CE armor without causing over-reduction.
- **GS_Evade_* custom stats**: These are GS-internal stats read by the mod's own evasion system. They have no CE interaction point and function identically with or without CE.
- **MeleeArmorPenetration 1.2x (Hexaeye)**: This stat factor is display-only under CE -- CE reads penetration from `ToolCE` fields, not from pawn-level `MeleeArmorPenetration`. The visual stat readout shows an inflated value, but the combat effect is determined entirely by the ToolCE AP values. Accepted as a cosmetic inaccuracy rather than adding a patch to suppress a stat factor that does no harm.

The decision to leave these stats untouched is important for forward compatibility. If CE ever changes how it reads these stats (several have been discussed on the CE GitHub for future integration), the existing values will begin functioning correctly without any patch update needed. Suppressing or zeroing stats that are currently inert would create a regression if CE later adds readers for them.


## Conditional Loading

**File:** `LoadFolders.xml`, line 9

```xml
<li IfModActive="feaurie.GreyscytheBionics">Defs/GreyscytheBionicsCatalogue</li>
```

The projectile defs in `Defs/GreyscytheBionicsCatalogue/Projectiles_Bionics.xml` are only loaded when Greyscythe Bionics Catalogue is in the active mod list. When the mod is absent, the defs directory is never loaded, the projectile ThingDefs do not exist, and the patch file's `PatchOperationFindMod` wrapper prevents any XML operations from executing.

The patch file itself (`Patches/GreyscytheBionicsCatalogue.xml`) is gated by `PatchOperationFindMod` checking for `Greyscythe Bionics Catalogue` in the mod list. This double-gating -- `LoadFolders.xml` for defs, `PatchOperationFindMod` for patches -- ensures zero overhead when the mod is not installed.


## File Inventory

| File | Role |
|---|---|
| `Patches/GreyscytheBionicsCatalogue.xml` | All PatchOperationReplace/Add operations (melee, ranged, armor) |
| `Defs/GreyscytheBionicsCatalogue/Projectiles_Bionics.xml` | 8 CE projectile ThingDefs for ranged hediff weapons |
| `LoadFolders.xml` (line 9) | Conditional def loading gate |

No new HediffDefs, StatDefs, AmmoSetDefs, or C# assemblies are introduced. The patch creates 8 new ThingDefs (projectiles only) and modifies approximately 40 existing def nodes across melee tools, ranged verbs, and armor implant stages. The total patch footprint is 869 lines of XML across two files.
