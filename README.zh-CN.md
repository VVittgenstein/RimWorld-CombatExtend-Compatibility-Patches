# V2 CE 兼容补丁

**V2 模组包 · Combat Extended 兼容层**

本模组为 V2 模组包中的 12 个模组提供完整的 [Combat Extended](https://steamcommunity.com/sharedfiles/filedetails/?id=2890901044) 兼容方案。采用三层条件门控架构——XML 补丁、Def 加载、Harmony 注入均以目标模组存在为前提——安装后自动识别并适配你实际启用的任意模组子集，无需手动配置。

> 本文档为独立中文版。英文版请参阅 [README.md](README.md)。

---

## 设计理念

以下四项原则贯穿本模组的每一处补丁，在设计阶段即已锁定，不因具体模组而妥协。

**一、体验等价**

补丁的目标不是把原模组的数值机械地搬进 CE 的字段里，而是在 CE 体系下重建原模组带给玩家的"手感"。一把被设计为太空级能量长矛的武器，在 CE 中仍然应当让人感受到相同的战术定位和威慑层级。我们追求的是体验层面的忠实还原，而非属性层面的逐字对照。

**二、设计优先于机械移植**

每个补丁都是一次设计决策，而非查找替换。当原模组的机制与 CE 的系统产生冲突时，我们会分析原作者的设计意图，并在 CE 的框架内寻找忠于该意图的解决方案。盲目搬运数值——例如直接将 `ArmorRating_Sharp` 复制到 CE 中含义完全不同的字段——是明确拒绝的做法。

**三、三层数值一致性**

所有数值必须同时满足三个层面的一致性：(1) **模组内部**——补丁后的数值曲线自身连贯；(2) **CE 标尺**——与 CE 现有弹药、护甲、穿深体系对齐；(3) **跨模组**——当多个被补丁模组共享设计空间时（如闪避预算、弹药等级、共用 Harmony 钩子），不能产生互相矛盾的结果。

**四、弹药即体验**

弹药系统不是为了满足 CE 编译要求而敷衍添加的管道。每一套弹药转换都被设计为强化武器的游戏身份——相位步枪的弹药应当传达"精密太空能量"，280mm 舰炮的弹药应当传达"毁灭级攻城弹药"。弹药选择是面向玩家的设计界面，不是需要藏起来的实现细节。

---

## 安装说明

### 前置需求

- [环世界 (RimWorld)](https://store.steampowered.com/app/294100/RimWorld/) 1.6
- [Combat Extended](https://steamcommunity.com/sharedfiles/filedetails/?id=2890901044)
- 下方列表中的至少一个目标模组

### 加载顺序

在模组列表中，将 **V2 CE Compatibility Patches** 放置在以下所有模组之后：

1. Core、皇权 (Royalty)、文化 (Ideology)、生物科技 (Biotech)、异变 (Anomaly)（按需启用）
2. Harmony、HugsLib（如使用）
3. Vanilla Expanded Framework（如使用）
4. Vehicle Framework（如使用）
5. SloLib（如使用）
6. **Combat Extended**
7. 全部 12 个目标模组（彼此之间顺序任意）
8. **→ V2 CE Compatibility Patches ←**

核心要求：本模组必须在 CE 和所有目标模组之后加载。目标模组之间无需特定顺序。

### 模组内容

| 组件 | 说明 |
|------|------|
| `Patches/` | 11 个 XML 补丁文件，均使用 `PatchOperationFindMod` 门控 |
| `Defs/` | 10 个自定义 Def 文件（弹药集、弹体、StatPart），按条件加载 |
| `Assemblies/V2CEPatch.dll` | 7 个 Harmony 补丁 + 2 个 StatPart + 条件引导启动器 |
| `Source/` | 程序集的完整 C# 源代码 |

---

## 补丁索引

下表列出本模组覆盖的全部 12 个目标模组，每行包含创意工坊链接及中英文设计文档。设计文档详述了每个补丁的问题分析、方案论证和实现细节。

| # | 模组 | 概述 | 链接 |
|---|------|------|------|
| 1 | **JumpLifter** | 为 JumpLifter 机甲补充 CE 护甲耐久体系与机械族倒地处理逻辑。 | [创意工坊](https://steamcommunity.com/sharedfiles/filedetails/?id=3493717994) · [设计文档](docs/mods/zh/3493717994-jumplifter.md) · [Design Doc](docs/mods/en/3493717994-jumplifter.md) |
| 2 | **RimThunder - Core** | 将载具机枪炮塔绑定至 CE 的 NATO 弹药集，使其具备正确的穿甲计算。 | [创意工坊](https://steamcommunity.com/sharedfiles/filedetails/?id=3070495204) · [设计文档](docs/mods/zh/3070495204-rimthunder-core.md) · [Design Doc](docs/mods/en/3070495204-rimthunder-core.md) |
| 3 | **Phase Weaponry** | 将相位武器从原版命中扫描转换为 CE 弹道投射物，赋予太空级穿深。 | [创意工坊](https://steamcommunity.com/sharedfiles/filedetails/?id=3222694245) · [设计文档](docs/mods/zh/3222694245-phase-weaponry.md) · [Design Doc](docs/mods/en/3222694245-phase-weaponry.md) |
| 4 | **Vanilla Psycasts Expanded** | 修复披风护甲、XPath 命名空间定位、骷髅种族属性及健康状态映射在 CE 下的兼容问题。 | [创意工坊](https://steamcommunity.com/sharedfiles/filedetails/?id=2842502659) · [设计文档](docs/mods/zh/2842502659-vanilla-psycasts-expanded.md) · [Design Doc](docs/mods/en/2842502659-vanilla-psycasts-expanded.md) |
| 5 | **Doors Expanded** | 修补 CE 弹体碰撞高度检测逻辑，使其正确识别超大尺寸门扇。 | [创意工坊](https://steamcommunity.com/sharedfiles/filedetails/?id=3532342422) · [设计文档](docs/mods/zh/3532342422-doors-expanded.md) · [Design Doc](docs/mods/en/3532342422-doors-expanded.md) |
| 6 | **EndlessGrowth** | 将 CE 的 ReloadSpeed 和 AimingDelayFactor 属性表从 20 级扩展至 100 级，设置渐近上限。 | [创意工坊](https://steamcommunity.com/sharedfiles/filedetails/?id=2894401734) · [设计文档](docs/mods/zh/2894401734-endlessgrowth.md) · [Design Doc](docs/mods/en/2894401734-endlessgrowth.md) |
| 7 | **VWE - Heavy Weapons** | 恢复 CE 替换动词系统后丢失的逐发武器耐久损耗和制导导弹追踪功能。 | [创意工坊](https://steamcommunity.com/sharedfiles/filedetails/?id=2329126791) · [设计文档](docs/mods/zh/2329126791-vwe-heavy-weapons.md) · [Design Doc](docs/mods/en/2329126791-vwe-heavy-weapons.md) |
| 8 | **Greyscythe Cybergenetics** | 通过自定义 StatPart 将基因闪避和伤害减免接入 CE 的属性管线。 | [创意工坊](https://steamcommunity.com/sharedfiles/filedetails/?id=3538434109) · [设计文档](docs/mods/zh/3538434109-greyscythe-cybergenetics.md) · [Design Doc](docs/mods/en/3538434109-greyscythe-cybergenetics.md) |
| 9 | **VQE - Ancients** | 通过 CE 钩子实现远古基因战斗能力（强制爆头、弹体闪避、护甲穿透）。 | [创意工坊](https://steamcommunity.com/sharedfiles/filedetails/?id=3618306875) · [设计文档](docs/mods/zh/3618306875-vqe-ancients.md) · [Design Doc](docs/mods/en/3618306875-vqe-ancients.md) |
| 10 | **Greyscythe Bionics Catalogue** | 将全部仿生近战工具转换为 CE 的 ToolCE 并按等级赋予穿深值，远程健康状态转为 CE 弹体。 | [创意工坊](https://steamcommunity.com/sharedfiles/filedetails/?id=3538434454) · [设计文档](docs/mods/zh/3538434454-greyscythe-bionics-catalogue.md) · [Design Doc](docs/mods/en/3538434454-greyscythe-bionics-catalogue.md) |
| 11 | **Landkreuzer P1000 Ratte** | 为超重型载具的全部五类炮塔（280mm、75mm、20mm、同轴机枪、防空）创建 CE 弹药集。 | [创意工坊](https://steamcommunity.com/sharedfiles/filedetails/?id=3026535021) · [设计文档](docs/mods/zh/3026535021-landkreuzer-p1000-ratte.md) · [Design Doc](docs/mods/en/3026535021-landkreuzer-p1000-ratte.md) |
| 12 | **Slo' turret collection** | 将 7 个武器族共 28 座炮塔转换至 CE 弹药系统、激光光束类和能量炮塔建筑。 | [创意工坊](https://steamcommunity.com/sharedfiles/filedetails/?id=3038833232) · [设计文档](docs/mods/zh/3038833232-slo-turret-collection.md) · [Design Doc](docs/mods/en/3038833232-slo-turret-collection.md) |

---

## 扩展新补丁

如需为第 13 个或更多模组添加兼容支持：

1. 在 `Patches/` 中添加补丁 XML，使用 `PatchOperationFindMod` 门控
2. 在 `Defs/<模组名>/` 中添加自定义 Def，并在 `LoadFolders.xml` 中注册
3. 如需 C# 钩子，在 `V2CEPatchMod.cs` 中添加 Harmony 补丁类并设置条件门控
4. 编写设计文档：`docs/mods/zh/<steamid>-<slug>.md` 和 `docs/mods/en/<steamid>-<slug>.md`
5. 在上方补丁索引表中添加一行

无需对顶层结构做任何调整。

---

## 许可证

各目标模组的许可证请参阅其各自页面。本兼容补丁仅包含将各模组内容桥接至 Combat Extended 体系所必需的最小定义与钩子。
