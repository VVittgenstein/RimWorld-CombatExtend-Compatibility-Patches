# V2 CE Compatibility Patches

**V2 模组包 · Combat Extended 兼容补丁**

A standalone compatibility layer that adapts 12 mods from the V2 Mod Pack to work correctly under [Combat Extended](https://steamcommunity.com/sharedfiles/filedetails/?id=2890901044). Each target mod's patches activate only when that mod is present — install once, and the patch silently covers whichever subset of the 12 you actually run.

本模组为 V2 模组包中的 12 个模组提供 Combat Extended 兼容补丁。采用按需加载设计，仅在目标模组存在时激活对应补丁。每个模组的详细设计文档提供中英双语版本，见下方索引。

---

## Design Philosophy / 设计理念

Four locked principles govern every patch in this mod. They were established during design and are not subject to per-patch negotiation.

**1. Experience Equivalence / 体验等价**
Each patch preserves the target mod's intended gameplay experience under CE. A JumpLifter player should feel the same tactical weight; a Phase Weaponry user should see the same relative firepower tier. The goal is not mechanical stat translation but experiential fidelity — if the original mod made a weapon feel like a spacer-tier energy lance, the CE conversion must land in the same gameplay niche.

**2. Design Work over Mechanical Porting / 设计优先于机械移植**
Every patch represents a deliberate design decision, not a find-and-replace operation. When a mod's mechanic conflicts with CE's systems, we analyze the original designer's intent and engineer a solution that honors it within CE's framework. Blind stat mapping (e.g., copying `ArmorRating_Sharp` into a CE field that means something different) is rejected in favor of understanding what the number was meant to accomplish.

**3. Three-Layer Numerical Consistency / 三层数值一致性**
All numerical values must be consistent across three layers simultaneously: (1) **internal** consistency within the patched mod's own progression curve, (2) **CE-scale** alignment with CE's existing ammunition, armor, and penetration hierarchies, and (3) **cross-mod** consistency where multiple patched mods share design space — overlapping dodge budgets, ammo tiers, or shared Harmony hooks must not produce contradictory outcomes.

**4. Ammunition as Experience / 弹药即体验**
Ammo systems are not mere technical plumbing to satisfy CE's compile-time requirements. Each ammo conversion is designed to reinforce the weapon's gameplay identity — a phase rifle's ammo should communicate "precision spacer energy" through its damage/AP profile, a 280mm naval gun's ammo should communicate "apocalyptic siege ordnance." Ammo choice is a player-facing design surface, not an implementation detail to hide.

---

## Installation / 安装说明

### Requirements / 前置需求

- [RimWorld](https://store.steampowered.com/app/294100/RimWorld/) 1.6
- [Combat Extended](https://steamcommunity.com/sharedfiles/filedetails/?id=2890901044)
- One or more of the 12 supported target mods listed below

### Load Order / 加载顺序

Place **V2 CE Compatibility Patches** after all of the following in your mod list:

1. Core, Royalty, Ideology, Biotech, Anomaly (as applicable)
2. Harmony, HugsLib (if used)
3. Vanilla Expanded Framework (if used)
4. Vehicle Framework (if used)
5. SloLib (if used)
6. **Combat Extended**
7. All 12 target mods (any order among themselves)
8. **→ V2 CE Compatibility Patches ←**

The mod must load after both CE and every target mod it patches. The target mods themselves do not require a specific order relative to each other.

### What This Mod Contains / 模组内容

| Component | Description |
|-----------|-------------|
| `Patches/` | 11 XML patch files with `PatchOperationFindMod` guards |
| `Defs/` | 10 custom def files (ammo sets, projectiles, StatParts) loaded conditionally |
| `Assemblies/V2CEPatch.dll` | 7 Harmony patches + 2 StatParts + conditional bootstrap |
| `Source/` | Full C# source for the assembly |

---

## Patched Mods / 补丁索引

Each row links to the upstream Steam Workshop page and to bilingual design documents explaining the patch rationale in detail.

每行包含创意工坊链接和中英文设计文档，详细说明补丁设计思路。

| # | Mod / 模组 | Summary / 概述 | Links / 链接 |
|---|-----------|---------------|-------------|
| 1 | **JumpLifter** | Adds CE armor durability and downed-mechanoid handling to the JumpLifter mech. | [Steam](https://steamcommunity.com/sharedfiles/filedetails/?id=3493717994) · [设计文档](docs/mods/zh/3493717994-jumplifter.md) · [Design Doc](docs/mods/en/3493717994-jumplifter.md) |
| 2 | **RimThunder - Core** | Binds vehicle MG turrets to CE NATO ammo sets for proper armor penetration. | [Steam](https://steamcommunity.com/sharedfiles/filedetails/?id=3070495204) · [设计文档](docs/mods/zh/3070495204-rimthunder-core.md) · [Design Doc](docs/mods/en/3070495204-rimthunder-core.md) |
| 3 | **Phase Weaponry** | Converts phase-tech weapons from vanilla hitscan to CE ballistic projectiles with spacer-tier AP. | [Steam](https://steamcommunity.com/sharedfiles/filedetails/?id=3222694245) · [设计文档](docs/mods/zh/3222694245-phase-weaponry.md) · [Design Doc](docs/mods/en/3222694245-phase-weaponry.md) |
| 4 | **Vanilla Psycasts Expanded** | Fixes cape armor, XPath namespace targeting, skeleton race stats, and hediff mappings for CE. | [Steam](https://steamcommunity.com/sharedfiles/filedetails/?id=2842502659) · [设计文档](docs/mods/zh/2842502659-vanilla-psycasts-expanded.md) · [Design Doc](docs/mods/en/2842502659-vanilla-psycasts-expanded.md) |
| 5 | **Doors Expanded** | Patches CE projectile-collision height checks to respect oversized door dimensions. | [Steam](https://steamcommunity.com/sharedfiles/filedetails/?id=3532342422) · [设计文档](docs/mods/zh/3532342422-doors-expanded.md) · [Design Doc](docs/mods/en/3532342422-doors-expanded.md) |
| 6 | **EndlessGrowth** | Extends CE ReloadSpeed and AimingDelayFactor stat tables from level 20 to level 100 with asymptotic caps. | [Steam](https://steamcommunity.com/sharedfiles/filedetails/?id=2894401734) · [设计文档](docs/mods/zh/2894401734-endlessgrowth.md) · [Design Doc](docs/mods/en/2894401734-endlessgrowth.md) |
| 7 | **VWE - Heavy Weapons** | Restores per-shot weapon degradation and guided missile tracking lost when CE replaces the verb system. | [Steam](https://steamcommunity.com/sharedfiles/filedetails/?id=2329126791) · [设计文档](docs/mods/zh/2329126791-vwe-heavy-weapons.md) · [Design Doc](docs/mods/en/2329126791-vwe-heavy-weapons.md) |
| 8 | **Greyscythe Cybergenetics** | Redirects gene-based evasion and damage reduction through CE's stat pipeline via custom StatParts. | [Steam](https://steamcommunity.com/sharedfiles/filedetails/?id=3538434109) · [设计文档](docs/mods/zh/3538434109-greyscythe-cybergenetics.md) · [Design Doc](docs/mods/en/3538434109-greyscythe-cybergenetics.md) |
| 9 | **VQE - Ancients** | Implements archite-gene combat abilities (forced headshots, projectile dodge, armor bypass) via CE hooks. | [Steam](https://steamcommunity.com/sharedfiles/filedetails/?id=3618306875) · [设计文档](docs/mods/zh/3618306875-vqe-ancients.md) · [Design Doc](docs/mods/en/3618306875-vqe-ancients.md) |
| 10 | **Greyscythe Bionics Catalogue** | Converts all bionic melee tools to CE ToolCE with tiered AP values and ranged hediffs to CE projectiles. | [Steam](https://steamcommunity.com/sharedfiles/filedetails/?id=3538434454) · [设计文档](docs/mods/zh/3538434454-greyscythe-bionics-catalogue.md) · [Design Doc](docs/mods/en/3538434454-greyscythe-bionics-catalogue.md) |
| 11 | **Landkreuzer P1000 Ratte** | Creates CE ammo sets for all five turret types (280mm, 75mm, 20mm, coaxial MG, AA) on the super-heavy vehicle. | [Steam](https://steamcommunity.com/sharedfiles/filedetails/?id=3026535021) · [设计文档](docs/mods/zh/3026535021-landkreuzer-p1000-ratte.md) · [Design Doc](docs/mods/en/3026535021-landkreuzer-p1000-ratte.md) |
| 12 | **Slo' turret collection** | Converts 28 turrets across 7 weapon families to CE ammo systems, laser beam classes, and energy turret buildings. | [Steam](https://steamcommunity.com/sharedfiles/filedetails/?id=3038833232) · [设计文档](docs/mods/zh/3038833232-slo-turret-collection.md) · [Design Doc](docs/mods/en/3038833232-slo-turret-collection.md) |

---

## Adding a New Patch / 扩展新补丁

To add support for a 13th (or later) mod:

1. Add the patch XML to `Patches/` with `PatchOperationFindMod` guards
2. Add any custom defs to `Defs/<ModName>/` and register them in `LoadFolders.xml`
3. If C# hooks are needed, add the Harmony patch class and gate it in `V2CEPatchMod.cs`
4. Write the design documents: `docs/mods/zh/<steamid>-<slug>.md` and `docs/mods/en/<steamid>-<slug>.md`
5. Add one row to the mod index table above

No top-level restructuring is required.

---

## License

See the individual target mods for their respective licenses. This compatibility patch contains only the minimum definitions and hooks necessary to bridge each mod's content to Combat Extended's systems.
