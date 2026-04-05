# Phase Weaponry CE 兼容补丁

| 字段 | 值 |
|------|-----|
| Steam ID | 3222694245 |
| 作者 | DetVisor |
| PackageId | `det.phaseweaponry` |
| 补丁类型 | 纯 XML，无 DLL |
| 补丁文件 | `Patches/PhaseWeaponry.xml`, `Defs/PhaseWeaponry/Projectiles_Phase.xml` |

---

## 问题：三把相位武器在 CE 下完全失效

Phase Weaponry 添加了三把远程武器（Phase Shank / Phase Saw / Phase Scalpel），均使用原版 `Verb_Shoot` 配合 `BaseBullet` 派生弹体。CE 的武器自动补丁默认关闭（`Settings.cs:133`），这些武器不会被自动转换为 CE 弹道系统。结果：武器仍以原版 hitscan 方式开火，穿甲值约为 0，面对 CE 护甲体系几乎无法造成有效伤害。

除武器本身外，还存在三个附带问题：

### 1. 头盔缺少 CE 体积属性

Ranger 头盔（`DV_Apparel_RangerHelmet`，抽象父级 `DV_ApparelRangerHelmetBase`）缺少 CE 要求的 `Bulk` 和 `WornBulk` 统计值，护甲也未针对 CE 进行校准。该头盔具有 `stuffCategories: Fabric, Leathery`，是可选材质的，但缺少 `StuffEffectMultiplierArmor` 导致材质对护甲无影响。

### 2. PawnKind 无弹药装载

`DV_RangerInBlack`（相位游侠，类似原版 StrangerInBlack 的事件 pawn）缺少 `LoadoutPropertiesExtension`，在 CE 环境下生成时不携带弹药，无法使用其相位武器。

### 3. 自定义伤害类型无需修改

`DV_PhaseCut` 是 mod 定义的自定义 DamageDef（Sharp 类别）。CE 的伤害系统原生兼容自定义 Sharp 类 DamageDef，无需额外补丁。

---

## 设计决策

### 自定义弹药族：Phase Energy

创建 `AmmoSet_PhaseEnergy` 而非复用 CE 现有的荷电弹药（如 `AmmoSet_6x24mmCharged`），原因有三：

1. **伤害类型不匹配**：相位武器使用 `DV_PhaseCut`（Sharp 类别），而非荷电等离子伤害。复用荷电弹药会覆盖 mod 原有的伤害类型定义。
2. **附带伤害矛盾**：CE 荷电弹药携带 `Bomb_Secondary`（热能溅射），与相位武器"精准切割"的主题严重矛盾。
3. **穿甲比例差异**：相位弹药的设计目标是高 Sharp AP、极低 Blunt AP、零附带伤害，模拟纯粹的能量切割效果。

#### 弹体参数

两种变体，均继承 `BaseBulletCE`，使用 `DV_PhaseCut` 伤害类型：

| 弹体 | 伤害 | 速度 | Sharp AP | Blunt AP | 附带伤害 |
|------|------|------|----------|----------|---------|
| `Bullet_PhaseEnergy`（标准） | 16 | 140 | 20 | 8 | 无 |
| `Bullet_PhaseEnergy_Focused`（聚焦） | 12 | 140 | 35 | 6 | 无 |

#### 数值依据

**伤害 16**：介于 CE 6x24mm 荷电弹（13）与 8x50mm 重型荷电弹（24）之间。相位武器定位为太空级小型武器，伤害略高于标准荷电口径但远低于重型口径。

**Sharp AP 20 / 35**：标准型超过 6x24mm（15）但低于 5x35mm 长矛（33）；聚焦型达到长矛级穿甲（35），为精确步枪提供对重甲目标的有效打击选项。

**Blunt AP 8 / 6**：刻意压低。相位切割几乎不传递动能，对比 CE 荷电弹药的 Blunt AP 范围（14--120），相位弹药的钝伤穿甲仅为其零头。这是与等离子弹药最强的机制区分点。

**速度 140**：介于荷电手枪（122）和荷电步枪（151）之间，与相位武器跨越手枪至步枪的武器谱系吻合。

**零附带伤害**：最强主题区分要素。CE 荷电弹药的 `Bomb_Secondary` 会在命中点制造热能溅射，而相位切割是纯粹的锐利创伤，不产生爆炸或燃烧。

#### 弹药物理属性

Mass 0.015，Bulk 0.01（介于 CE 6x18mm 和 6x24mm 之间）。使用 `SpacerSmallAmmoBase` 父级。制造配方：10 Plasteel + 8 Steel + 6 Component 产出 500 发，前置研究 `ChargedShot`。

---

### 武器转换

三把武器均通过 `Patches/PhaseWeaponry.xml` 中的 `PatchOperationMakeGunCECompatible` 转换，共享 `AmmoSet_PhaseEnergy` 弹药族。武器间的差异化完全通过 verb 参数实现，符合 CE"一种弹药族、多种武器特征"的设计惯例。

#### Phase Shank（相位短刃 -- 随身武器）

defName: `DV_Gun_PhasePistol`

| 参数 | 值 | 说明 |
|------|----|------|
| Mass | 2.5（原 8.2） | CE 写实化质量，手枪级 |
| Bulk | 3.0 | 紧凑随身武器 |
| Range | 25 | 手枪射程 |
| BurstShotCount | 1 | 单发 |
| MagazineSize | 15 | 手枪标准弹容 |
| ReloadTime | 3s | |
| ShotSpread | 0.12 | 手枪级散布 |
| SwayFactor | 1.30 | 单手持握，摇摆较大 |
| SightsEfficiency | 1.0 | 基础瞄具 |
| Recoil | 0.8 | 能量武器后坐力低 |

AI 射击模式：`aiUseBurstMode` false，`aiAimMode` AimedShot。

#### Phase Saw（相位锯 -- 突击步枪）

defName: `DV_Gun_PhaseRifle`

| 参数 | 值 | 说明 |
|------|----|------|
| Mass | 4.0（原 12.2） | CE 写实化质量，步枪级 |
| Bulk | 8.0 | 标准步枪体积 |
| Range | 50 | 突击步枪射程 |
| BurstShotCount | 3 / 8 ticks | 3 发点射，间隔 8 ticks |
| MagazineSize | 24 | |
| ReloadTime | 4s | |
| ShotSpread | 0.07 | 步枪级散布 |
| SwayFactor | 1.15 | 双手持握，摇摆适中 |
| SightsEfficiency | 1.10 | 中等瞄具 |
| Recoil | 1.3 | 连射后坐力 |

AI 射击模式：`aiUseBurstMode` true，`aimedBurstShotCount` 3，`aiAimMode` AimedShot。

#### Phase Scalpel（相位手术刀 -- 精确步枪）

defName: `DV_Gun_PhasePrecisionRifle`

| 参数 | 值 | 说明 |
|------|----|------|
| Mass | 5.5（原 16） | CE 写实化质量，重型步枪级 |
| Bulk | 11.0 | 重型步枪体积 |
| Range | 60 | 精确步枪射程 |
| BurstShotCount | 1 | 单发 |
| MagazineSize | 8 | 低弹容，精确武器特征 |
| ReloadTime | 4s | |
| ShotSpread | 0.02 | 极低散布 |
| SwayFactor | 0.90 | 狙击级稳定性 |
| SightsEfficiency | 1.30 | 高精度瞄具 |
| Recoil | 0.9 | |

AI 射击模式：`aiUseBurstMode` false，`aiAimMode` AimedShot。

---

### 头盔转换

Ranger 头盔的抽象父级 `DV_ApparelRangerHelmetBase` 具有 `stuffCategories: Fabric, Leathery`，是可选材质的护甲。通过 `PatchOperationAdd` 向 `statBases` 追加三项 CE 属性：

```
Bulk:                      3
WornBulk:                  1
StuffEffectMultiplierArmor: 5
```

**StuffEffectMultiplierArmor 5 的依据**：乘数 5 配合 hyperweave（sharpDamageMultiplier 0.93）产出约 4.65 Sharp 护甲值，落在 CE 简易头盔（乘数 4）和高级头盔（固定值 8）之间。该头盔定位为太空级轻型护具，高于简易头盔但低于固定护甲的高级头盔。

头盔原有的 `AimingDelayFactor -0.2`（瞄准速度加成，该头盔的标志性 buff）未被修改，补丁仅追加 CE 所需属性。

---

### PawnKind 弹药装载

`DV_RangerInBlack` 通过 `PatchOperationAddModExtension` 注入 `LoadoutPropertiesExtension`：

```
primaryMagazineCount:
    min: 5
    max: 8
```

该范围与 CE 对原版 StrangerInBlack 的配置一致。未添加背包：RangerInBlack 已有 5 件必穿装备，而相位弹药极轻（Mass 0.015），5--8 个弹匣的弹药总重量在 1.1--1.8 之间，无需额外负重容器。

---

## 实现文件

### `Patches/PhaseWeaponry.xml`

外层 `PatchOperationFindMod` 检测 "Phase Weaponry" 是否加载，内层 `PatchOperationSequence` 包含五个操作：

1. `PatchOperationMakeGunCECompatible` -- `DV_Gun_PhasePistol`（Phase Shank）
2. `PatchOperationMakeGunCECompatible` -- `DV_Gun_PhaseRifle`（Phase Saw）
3. `PatchOperationMakeGunCECompatible` -- `DV_Gun_PhasePrecisionRifle`（Phase Scalpel）
4. `PatchOperationAdd` -- 向 `DV_ApparelRangerHelmetBase` 的 `statBases` 追加 Bulk / WornBulk / StuffEffectMultiplierArmor
5. `PatchOperationAddModExtension` -- 向 `DV_RangerInBlack` 注入 `LoadoutPropertiesExtension`

### `Defs/PhaseWeaponry/Projectiles_Phase.xml`

定义完整的相位能量弹药族：

| Def 类型 | defName | 说明 |
|----------|---------|------|
| `ThingCategoryDef` | `AmmoPhaseEnergy` | 弹药分类，父级 `AmmoAdvanced` |
| `AmmoSetDef` | `AmmoSet_PhaseEnergy` | 弹药集，关联两种弹药与弹体 |
| `AmmoDef` | `Ammo_PhaseEnergy` | 标准相位能量电池，`SpacerSmallAmmoBase` |
| `AmmoDef` | `Ammo_PhaseEnergy_Focused` | 聚焦相位能量电池，`SpacerSmallAmmoBase` |
| `ProjectileDef` | `Bullet_PhaseEnergy` | 标准弹体，`BaseBulletCE`，damageDef `DV_PhaseCut` |
| `ProjectileDef` | `Bullet_PhaseEnergy_Focused` | 聚焦弹体，`BaseBulletCE`，damageDef `DV_PhaseCut` |
| `RecipeDef` | `MakeAmmo_PhaseEnergy` | 制造配方，前置研究 `ChargedShot` |

两种 AmmoDef 分别使用 `CombatExtended.AmmoClasses.Charged` 和 `CombatExtended.AmmoClasses.ChargedAP` 作为 `ammoClass`，复用 CE 荷电弹药的贴图（`Things/Ammo/Charged/Regular` 和 `Things/Ammo/Charged/AP`）。

---

## 跨 Mod 对齐表

| 武器 | CE 最近类比物 | 关键差异 |
|------|-------------|---------|
| Phase Shank | 荷电手枪（6x18mm） | 更高伤害（16 vs 10），更高 AP（20 vs 12），无附带伤害 |
| Phase Saw | 荷电步枪（6x24mm） | 相近伤害（16 vs 13），更高 AP（20 vs 15），3 发点射 vs 6 发点射，无附带伤害 |
| Phase Scalpel | 荷电长矛（5x35mm） | 更高伤害（16 vs 13），更低 AP（20 vs 33），但可玩家制造 |

三把武器在数值上均略强于对应的 CE 荷电武器（更高伤害、更高或相当的穿甲），但以丧失 `Bomb_Secondary` 热能溅射为代价。聚焦弹药（AP 35）为 Phase Scalpel 提供了追平长矛级穿甲的选项，但代价是伤害降至 12。

---

## 备选方案排除

**为什么不复用 `AmmoSet_6x24mmCharged`？**
荷电弹药使用等离子伤害并附带 `Bomb_Secondary` 热能溅射，会覆盖 mod 定义的 `DV_PhaseCut` 伤害类型，彻底破坏相位武器"精准切割"的身份特征。自定义弹药族是保留原 mod 伤害设计的唯一方案。

**为什么不为 Phase Scalpel 单独创建更高伤害的弹药族？**
CE 惯例：一种弹药族对应一个弹药家族，武器间的差异化通过 verb 参数（散布、摇摆、瞄具效率、射程）实现。Phase Scalpel 通过 ShotSpread 0.02、SwayFactor 0.90、SightsEfficiency 1.30 获得精确射击优势，无需在弹药层面做额外区分。

**为什么不给头盔固定护甲值而用 StuffEffectMultiplierArmor？**
头盔定义了 `stuffCategories: Fabric, Leathery`，是可选材质的。使用固定护甲值会使材质选择失去意义，破坏 mod 原有的制造设计。StuffEffectMultiplierArmor 保留了材质对护甲的影响。

**为什么不给 RangerInBlack 添加背包？**
该 PawnKind 已有 5 件必穿装备。相位弹药极轻（Mass 0.015），5--8 个弹匣的弹药（75--120 发）总重量仅约 1.1--1.8。添加背包属于范围蔓延，且无实际需求。
