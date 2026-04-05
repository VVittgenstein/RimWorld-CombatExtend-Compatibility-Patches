# Landkreuzer P1000 Ratte CE 兼容修复 -- 技术设计文档

> **Steam Workshop ID**: 3026535021
> **Mod PackageId**: `slomow.P1000R`
> **补丁方式**: 0 Harmony hooks / 1 XML patch 文件 / 1 条件加载 Defs 文件

| 字段 | 值 |
|------|-----|
| **补丁文件** | `Patches/LandkreuzerP1000.xml`, `Defs/LandkreuzerP1000/AmmoSets_P1000.xml` |
| **RimWorld** | 1.6 |
| **前置框架** | Vehicle Framework (VVE) |
| **Mod 类型** | 纯 XML, 无 DLL (载具行为由 Vehicle Framework 提供) |

---

## 1. CE 破坏了什么

Landkreuzer P1000 Ratte 通过 Vehicle Framework (VVE) 添加了一辆二战概念超重型坦克载具。该载具搭载 4 种炮塔类型 (共 5 个炮塔实例), 以及一个引擎反应堆爆炸组件。安装 Combat Extended 后, 所有武装系统和引擎均出现严重兼容性故障。

### 1.1 炮塔完全绕过 CE 弹道系统

P1000 的 5 个炮塔均通过 `Vehicles.VehicleTurretDef` 定义。这是 Vehicle Framework 的专有 Def 类型, **不是**标准 RimWorld 的 `TurretDef`。CE 的自动补丁器 (autopatcher) 仅识别标准 RimWorld 炮塔, 无法触及 VVE 载具炮塔。

结果: 所有炮塔继续发射原版 (vanilla) 弹丸, 完全绕过 CE 的弹道系统。具体表现为:

- 弹丸无 CE 穿甲值 (ArmorPenetration), 对穿戴 CE 护甲的目标几乎零伤害
- 不参与 CE 弹药系统 -- 无弹药消耗, 无弹药类型选择
- 命中判定走原版 hitscan 路径而非 CE 的弹道模拟

这不是单个炮塔的问题, 而是**全部 5 个炮塔实例**的系统性失效 -- 一辆 P1000 的全部火力输出在 CE 环境下形同虚设。

### 1.2 引擎爆炸半径产生地图级毁灭

P1000 的引擎组件 (`Reactor_Explosive`) 原始爆炸半径为 **40 格**。在原版 RimWorld 的爆炸伤害模型下, 该半径虽然夸张但伤害随距离衰减, 不至于造成全图毁灭。

CE 显著增强了爆炸伤害模型: 更高的基础伤害、更真实的破片扩散、更强的穿甲效果。在 CE 环境下, 半径 40 的爆炸将产生**地图清除级别**的连锁反应 -- 足以杀死地图上几乎所有单位并摧毁大量建筑。P1000 被摧毁时不是戏剧性事件, 而是直接终结存档。

### 1.3 无现有 CE 补丁覆盖

截至本补丁编写时, CE 社区手动补丁库中**不存在** P1000 Ratte 的兼容补丁。该 mod 在 CE 环境下处于完全未适配状态。

---

## 2. 设计问题定框与方案对比

### 2.1 问题定框

需要解决两个独立问题:

1. **炮塔弹药集成**: 5 个 VVE 炮塔需要接入 CE 弹药系统, 发射 CE 弹丸并遵循 CE 弹道规则
2. **引擎爆炸平衡**: 引擎爆炸半径需要在 CE 增强爆炸模型下重新校准

设计约束:

- P1000 是纯 XML mod, 无 DLL -- 修复方案必须在 XML 层完成
- Vehicle Framework 提供了 `CETurretDataDefModExtension` 作为 VVE 炮塔与 CE 的集成接口 -- 修复应走这一正规通道
- 历史口径必须准确映射: P1000 的武装配置有明确的历史原型, 弹药选择需反映这一事实
- 不得引入冗余 Defs: 若 CE 已有对应口径的弹药定义, 不应创建重复定义

### 2.2 弹药方案: 复用 CE 现有弹药 vs 自定义弹药

P1000 搭载的 4 种口径均有明确的历史对应:

| 炮塔口径 | 历史原型 | CE 现有弹药集 |
|:---------|:---------|:-------------|
| 280mm 主炮 | 28cm SK C/34 舰炮 | `AmmoSet_28cmSpgrShell` |
| 75mm 鼠式炮塔 | 7.5cm KwK 44 | `AmmoSet_75x350mmR` |
| 20mm 防空炮 | 2cm Oerlikon | `AmmoSet_20x128mmOerlikon` |
| 7.62mm 机枪 | MG3/MG42 系列 | `AmmoSet_762x51mmNATO` |

CE 的弹药库已包含所有 4 种口径的完整定义 (AmmoDef、ProjectileDef、AmmoSetDef)。这些定义经过 CE 社区充分平衡和测试。

**否决自定义弹药的原因**:

- 创建自定义弹药集意味着维护 4 套 AmmoDef + ProjectileDef + RecipeDef, 工程量大且无必要
- 自定义弹药在 CE 弹药经济中引入未经平衡的新物品, 可能扰乱交易和制造体系
- 与 CE 的历史口径弹药并存会造成玩家困惑 (例如两种不同的 280mm HE 弹在库存中共存)
- CE 现有弹药的穿甲值、伤害、弹速均已按历史数据校准, 直接复用即可获得准确的弹道表现

### 2.3 引擎爆炸方案: 半径选择

考虑了三个选项:

#### 选项 A: 保留原始半径 40 (已否决)

在 CE 增强爆炸模型下, 半径 40 的爆炸产生的破片密度和穿甲伤害足以清除整个地图区域。P1000 被摧毁时将触发不可接受的连锁毁灭, 直接终结游戏进程。

#### 选项 B: 缩减至 CE 标准载具上限 1 (已否决)

CE 对标准 VVE 载具的爆炸半径上限为 1。对于常规装甲车辆, 这一保守值是合理的。但 P1000 是一辆千吨级超重型坦克, 配备舰炮级主武器和反应堆引擎。将其爆炸效果等同于普通装甲车, 在叙事和游戏体验上都不合理 -- 失去了 P1000 作为超级武器的戏剧感。

#### 选项 C: 折中至半径 3 (采用)

半径 3 在 CE 增强爆炸模型下产生的破坏力约等于原版半径 12-15 的效果: 足以摧毁 P1000 周围的近距离单位和建筑, 产生震撼性的爆炸场面, 但不会波及整个基地或地图。

这一数值是 CE 标准载具上限 (1) 的 3 倍, 明确传达了 P1000 的"超级载具"定位; 同时与 CE 的爆炸伤害模型协调, 不会产生游戏终结级的连锁反应。

### 2.4 炮塔集成机制: CETurretDataDefModExtension

Vehicle Framework 为 VVE 载具炮塔与 CE 的集成提供了专用接口: `Vehicles.CETurretDataDefModExtension`。该 ModExtension 挂载到 `VehicleTurretDef` 上, 提供以下字段:

- `ammoSet`: 绑定 CE 弹药集
- `shotHeight`: 炮口高度 (影响弹道起始点)
- `speed`: 弹丸初速
- `sway`: 瞄准摇摆系数
- `spread`: 散布角度

运行时, Vehicle Framework 的 CE 集成层读取该 ModExtension, 替代原版射击路径执行 CE 弹道计算。这是正规的框架级集成通道, 不需要 C# 补丁或 Harmony hooks。

### 2.5 不需要 C# 补丁

由于 Vehicle Framework 已提供 `CETurretDataDefModExtension` 接口, 全部修复可在 XML 层完成:

- 无 Harmony hooks -- `V2CEPatchMod.cs` 和 `ModDetection.cs` 中不需要添加任何入口
- 不涉及程序集依赖或反射调用
- 所有修改通过 `PatchOperation` 和条件 Defs 加载实现

---

## 3. 具体实现

### 3.1 文件概览

| 文件 | 用途 |
|:-----|:-----|
| `Patches/LandkreuzerP1000.xml` | 全部炮塔补丁 + 引擎爆炸修复 + 弹药超链接 |
| `Defs/LandkreuzerP1000/AmmoSets_P1000.xml` | 弹药引用辅助 Def (条件加载) |
| `LoadFolders.xml` (第 11 行) | 条件加载门控 |

### 3.2 门控机制

#### XML 补丁门控

`Patches/LandkreuzerP1000.xml` 第 4-8 行:

```xml
<Operation Class="PatchOperationFindMod">
    <mods>
        <li>Combat Extended</li>
        <li>Landkreuzer P1000 Ratte</li>
    </mods>
```

仅当 Combat Extended **和** Landkreuzer P1000 Ratte 同时激活时, 内部的 `PatchOperationSequence` 才执行。

#### Defs 条件加载

`LoadFolders.xml` 第 11 行:

```xml
<li IfModActive="slomow.P1000R">Defs/LandkreuzerP1000</li>
```

`Defs/LandkreuzerP1000/` 目录仅在 P1000 mod 激活时加载, 避免在未安装该 mod 的环境中引入无效 Def。

#### 无 C# 门控

本补丁不涉及 Harmony hooks, 因此 `Source/V2CEPatch/V2CEPatchMod.cs` 和 `Source/V2CEPatch/Utility/ModDetection.cs` 中无需添加任何条件注册入口。

### 3.3 炮塔补丁: P1000_MainTurret (280mm 舰炮)

**`Patches/LandkreuzerP1000.xml` 第 16-47 行**

280mm 主炮是 P1000 的核心武装, 基于历史上的 28cm SK C/34 舰炮。

| 补丁操作 | 行号 | xpath 目标 | 变更内容 |
|:---------|:-----|:----------|:---------|
| `PatchOperationReplace` | 16-21 | `projectile` | → `Bullet_28cmSpgrShell_HE` |
| `PatchOperationReplace` | 23-28 | `reloadTimer` | → `16.0` (秒) |
| `PatchOperationReplace` | 30-35 | `warmUpTimer` | → `5.5` (秒) |
| `PatchOperationAddModExtension` | 37-47 | 炮塔 Def 根节点 | 注入 `CETurretDataDefModExtension` |

ModExtension 参数:

| 字段 | 值 | 说明 |
|:-----|:---|:-----|
| `ammoSet` | `AmmoSet_28cmSpgrShell` | CE 280mm 舰炮 HE 弹药集 |
| `shotHeight` | `3.5` | 主炮塔顶部, 全车最高射击点 |
| `sway` | `0.82` | 重型固定炮座, 摇摆系数较低 |
| `spread` | `0.01` | 舰炮级精度 |

`shotHeight` 3.5 反映了 280mm 主炮位于 P1000 车体最高处的双联装炮塔中, 远高于其他炮位。`reloadTimer` 16.0 秒体现了 280mm 舰炮弹药的装填复杂度。

### 3.4 炮塔补丁: P1000_MausTurret (75mm 鼠式侧炮塔)

**`Patches/LandkreuzerP1000.xml` 第 53-92 行**

P1000 两侧各配备一座鼠式坦克炮塔, 装备 75mm KwK 44。

| 补丁操作 | 行号 | xpath 目标 | 变更内容 |
|:---------|:-----|:----------|:---------|
| `PatchOperationReplace` | 53-58 | `projectile` | → `Bullet_75x350mmR_HE` |
| `PatchOperationReplace` | 60-65 | `reloadTimer` | → `6.2` (秒) |
| `PatchOperationReplace` | 67-72 | `warmUpTimer` | → `2.8` (秒) |
| `PatchOperationReplace` | 74-79 | `maxRange` | → `86` (格) |
| `PatchOperationAddModExtension` | 81-92 | 炮塔 Def 根节点 | 注入 `CETurretDataDefModExtension` |

ModExtension 参数:

| 字段 | 值 | 说明 |
|:-----|:---|:-----|
| `ammoSet` | `AmmoSet_75x350mmR` | CE 75mm 莱茵金属弹药集 |
| `shotHeight` | `2.5` | 侧置炮塔安装位, 中等高度 |
| `speed` | `124` | 75mm 弹丸初速 |
| `sway` | `0.82` | 与主炮一致的重型炮座稳定性 |
| `spread` | `0.01` | 坦克炮精度 |

### 3.5 炮塔补丁: P1000_MausFTTurret (75mm 前置炮)

**`Patches/LandkreuzerP1000.xml` 第 99-138 行**

前置 75mm 炮使用与侧炮塔相同的弹药和射击参数, 关键差异在于安装位置。

| 补丁操作 | 行号 | xpath 目标 | 变更内容 |
|:---------|:-----|:----------|:---------|
| `PatchOperationReplace` | 99-104 | `projectile` | → `Bullet_75x350mmR_HE` |
| `PatchOperationReplace` | 106-111 | `reloadTimer` | → `6.2` (秒) |
| `PatchOperationReplace` | 113-118 | `warmUpTimer` | → `2.8` (秒) |
| `PatchOperationReplace` | 120-125 | `maxRange` | → `86` (格) |
| `PatchOperationAddModExtension` | 127-138 | 炮塔 Def 根节点 | 注入 `CETurretDataDefModExtension` |

ModExtension 参数:

| 字段 | 值 | 说明 |
|:-----|:---|:-----|
| `ammoSet` | `AmmoSet_75x350mmR` | 与 MausTurret 相同 |
| `shotHeight` | **`1.8`** | 车体下部球形炮座, 显著低于侧炮塔的 2.5 |
| `speed` | `124` | 与 MausTurret 相同 |
| `sway` | `0.82` | 与 MausTurret 相同 |
| `spread` | `0.01` | 与 MausTurret 相同 |

`shotHeight` 1.8 vs 2.5 是前置炮与侧炮塔的唯一弹道差异: 前置炮安装在车体下部的固定球形炮座中, 射击起始点更低, 影响 CE 弹道计算中的抛物线轨迹和遮蔽判定。

### 3.6 炮塔补丁: P1000_FlakTurret (20mm Oerlikon 防空炮)

**`Patches/LandkreuzerP1000.xml` 第 144-219 行**

Oerlikon 20mm 防空炮是 P1000 的对空/轻装甲火力点。

| 补丁操作 | 行号 | xpath 目标 | 变更内容 |
|:---------|:-----|:----------|:---------|
| `PatchOperationReplace` | 144-149 | `projectile` | → `Bullet_20x128mmOerlikon_HE` |
| `PatchOperationReplace` | 151-156 | `reloadTimer` | → `7.8` (秒) |
| `PatchOperationReplace` | 158-163 | `warmUpTimer` | → `2.3` (秒) |
| `PatchOperationReplace` | 165-170 | `magazineCapacity` | → `60` (发) |
| `PatchOperationReplace` | 172-177 | `maxRange` | → `78` (格) |
| `PatchOperationReplace` | 179-206 | `fireModes` | 3 种射击模式 (见下表) |
| `PatchOperationAddModExtension` | 208-219 | 炮塔 Def 根节点 | 注入 `CETurretDataDefModExtension` |

射击模式配置 (第 182-204 行):

| 模式 | `shotsPerBurst` | `ticksBetweenShots` | `ticksBetweenBursts` |
|:-----|:----------------|:--------------------|:---------------------|
| Single | 1 | 6 | 60 |
| Burst | 3 | 6 | 60 |
| Auto | 8 | 6 | 60 |

ModExtension 参数:

| 字段 | 值 | 说明 |
|:-----|:---|:-----|
| `ammoSet` | `AmmoSet_20x128mmOerlikon` | CE 20mm Oerlikon 弹药集 |
| `shotHeight` | `2.0` | 上层甲板防空炮位 |
| `speed` | `183` | 20mm 高射速弹丸 |
| `sway` | `1.61` | 自动武器较高摇摆, 反映连射精度衰减 |
| `spread` | `0.01` | 单发基础散布 |

`sway` 1.61 是全车最高值, 体现了 20mm 自动炮在高射速连射时的精度衰减特性。与之对比, 重型坦克炮 (280mm/75mm) 的 sway 仅为 0.82。

### 3.7 炮塔补丁: P1000_Ball_Turret (7.62mm 球形机枪塔)

**`Patches/LandkreuzerP1000.xml` 第 225-300 行**

7.62mm 机枪是 P1000 的反步兵基础火力。

| 补丁操作 | 行号 | xpath 目标 | 变更内容 |
|:---------|:-----|:----------|:---------|
| `PatchOperationReplace` | 225-230 | `projectile` | → `Bullet_762x51mmNATO_FMJ` |
| `PatchOperationReplace` | 232-237 | `reloadTimer` | → `7.8` (秒) |
| `PatchOperationReplace` | 239-244 | `warmUpTimer` | → `1.3` (秒) |
| `PatchOperationReplace` | 246-251 | `magazineCapacity` | → `200` (发) |
| `PatchOperationReplace` | 253-258 | `maxRange` | → `55` (格) |
| `PatchOperationReplace` | 260-287 | `fireModes` | 3 种射击模式 (见下表) |
| `PatchOperationAddModExtension` | 289-300 | 炮塔 Def 根节点 | 注入 `CETurretDataDefModExtension` |

射击模式配置 (第 263-285 行):

| 模式 | `shotsPerBurst` | `ticksBetweenShots` | `ticksBetweenBursts` |
|:-----|:----------------|:--------------------|:---------------------|
| Single | 1 | 6 | 60 |
| Burst | 5 | 6 | 60 |
| Auto | 10 | 6 | 60 |

ModExtension 参数:

| 字段 | 值 | 说明 |
|:-----|:---|:-----|
| `ammoSet` | `AmmoSet_762x51mmNATO` | CE 7.62mm NATO 弹药集 |
| `shotHeight` | `2.0` | 与 FlakTurret 同层 |
| `speed` | `156` | 7.62mm 步枪弹初速 |
| `sway` | `0.96` | 中等摇摆, 介于坦克炮 (0.82) 和防空炮 (1.61) 之间 |
| `spread` | `0.04` | 机枪散布, 全车最高 (反映机枪压制射击特性) |

`spread` 0.04 是全车最高散布值: 280mm/75mm/20mm 炮均为 0.01, 而 7.62mm 机枪为 0.04。这反映了机枪的设计定位是面积压制而非精确射击。

### 3.8 引擎爆炸修复

**`Patches/LandkreuzerP1000.xml` 第 309-314 行**

```xml
<li Class="PatchOperationReplace">
    <xpath>Defs/Vehicles.VehicleDef[defName="P1000_tank"]/components/li[key="Engine"]/reactors/li[@Class="Vehicles.Reactor_Explosive"]/radius</xpath>
    <value>
        <radius>3</radius>
    </value>
</li>
```

将 `Reactor_Explosive` 的爆炸半径从原始值 **40** 修改为 **3**。

### 3.9 弹药超链接注入

**`Patches/LandkreuzerP1000.xml` 第 320-330 行**

```xml
<li Class="PatchOperationAdd">
    <xpath>Defs/Vehicles.VehicleDef[defName="P1000_tank"]</xpath>
    <value>
        <descriptionHyperlinks>
            <CombatExtended.AmmoSetDef>AmmoSet_28cmSpgrShell</CombatExtended.AmmoSetDef>
            <CombatExtended.AmmoSetDef>AmmoSet_75x350mmR</CombatExtended.AmmoSetDef>
            <CombatExtended.AmmoSetDef>AmmoSet_20x128mmOerlikon</CombatExtended.AmmoSetDef>
            <CombatExtended.AmmoSetDef>AmmoSet_762x51mmNATO</CombatExtended.AmmoSetDef>
        </descriptionHyperlinks>
    </value>
</li>
```

向 `P1000_tank` 的 VehicleDef 注入 `descriptionHyperlinks`, 使玩家在游戏内信息面板中可以直接点击查看各弹药集的详细信息 (伤害、穿甲值、弹速等)。

### 3.10 条件加载 Defs: AmmoSets_P1000.xml

**`Defs/LandkreuzerP1000/AmmoSets_P1000.xml`**

该文件定义了一个抽象 ThingDef (`P1000_AmmoReference`), 仅包含 `descriptionHyperlinks` 列表, 作为弹药引用辅助:

```xml
<ThingDef Abstract="True" Name="P1000_AmmoReference">
    <descriptionHyperlinks>
        <CombatExtended.AmmoSetDef>AmmoSet_28cmSpgrShell</CombatExtended.AmmoSetDef>
        <CombatExtended.AmmoSetDef>AmmoSet_75x350mmR</CombatExtended.AmmoSetDef>
        <CombatExtended.AmmoSetDef>AmmoSet_20x128mmOerlikon</CombatExtended.AmmoSetDef>
        <CombatExtended.AmmoSetDef>AmmoSet_762x51mmNATO</CombatExtended.AmmoSetDef>
    </descriptionHyperlinks>
</ThingDef>
```

关键特征:

- 标记为 `Abstract="True"` -- 不会生成游戏内实体, 仅作为引用容器
- 所有弹药集引用均指向 **CE 已有定义** -- 本补丁不创建任何自定义 AmmoDef 或 AmmoSetDef
- 通过 `LoadFolders.xml` 第 11 行的 `IfModActive="slomow.P1000R"` 条件加载

### 3.11 全车武装参数总览

| 炮塔 | 口径 | 弹药集 | 装填 | 预热 | 射程 | 弹匣 | 射高 | 弹速 | 摇摆 | 散布 |
|:-----|:-----|:------|:-----|:-----|:-----|:-----|:-----|:-----|:-----|:-----|
| MainTurret | 280mm | `AmmoSet_28cmSpgrShell` | 16.0s | 5.5s | -- | -- | 3.5 | -- | 0.82 | 0.01 |
| MausTurret | 75mm | `AmmoSet_75x350mmR` | 6.2s | 2.8s | 86 | -- | 2.5 | 124 | 0.82 | 0.01 |
| MausFTTurret | 75mm | `AmmoSet_75x350mmR` | 6.2s | 2.8s | 86 | -- | 1.8 | 124 | 0.82 | 0.01 |
| FlakTurret | 20mm | `AmmoSet_20x128mmOerlikon` | 7.8s | 2.3s | 78 | 60 | 2.0 | 183 | 1.61 | 0.01 |
| Ball_Turret | 7.62mm | `AmmoSet_762x51mmNATO` | 7.8s | 1.3s | 55 | 200 | 2.0 | 156 | 0.96 | 0.04 |

武装构成呈现清晰的层级递进:

- **7.62mm 机枪** (反步兵): 最大弹匣 200 发, 最短预热 1.3 秒, 最高散布 0.04 -- 持续压制火力
- **20mm 防空炮** (对空/轻装甲): 60 发弹匣, 最高弹速 183, 最高摇摆 1.61 -- 高射速对空拦截
- **75mm 鼠式炮** (中型装甲): 中等射程 86 格, 坦克炮级精度 -- 反装甲主力
- **280mm 舰炮** (攻坚/要塞): 最长装填 16 秒, 最长预热 5.5 秒, 最高射点 3.5 -- 单发摧毁级火力

---

## 4. 方案优势分析

### 4.1 复用 CE 现有弹药 vs 自定义弹药

最终方案将全部 4 种口径直接映射到 CE 已有弹药集, 不创建任何自定义 AmmoDef。

**优势**:

- **零弹药经济冲击**: 不引入新弹药物品, 不影响 CE 的交易、制造、库存体系。玩家使用已熟悉的弹药类型为 P1000 补给, 学习成本为零。
- **继承 CE 平衡成果**: CE 社区对这 4 种口径的穿甲值、伤害、弹速已经过大量实战平衡。直接复用意味着 P1000 的火力输出自动处于合理区间, 无需独立校准。
- **维护成本极低**: 当 CE 更新弹药参数时, P1000 自动继承变更, 无需同步维护自定义弹药定义。
- **历史准确性**: P1000 的 4 种口径 (28cm 舰炮、75mm 坦克炮、20mm Oerlikon、7.62mm NATO) 与 CE 弹药库中的对应物完全匹配 -- 不存在口径近似或需要妥协的情况。

### 4.2 引擎爆炸半径 3 vs 保留 40 vs 缩减至 1

| 方案 | 半径 | CE 环境下效果 | 叙事合理性 |
|:-----|:-----|:-------------|:----------|
| 保留原始 | 40 | 地图清除级毁灭, 终结存档 | 过度夸张 |
| CE 标准上限 | 1 | 仅影响直接相邻格, 几乎无感 | 千吨超重型坦克与普通装甲车等同, 不合理 |
| **折中方案** | **3** | **摧毁近距离单位/建筑, 不波及基地** | **超级载具定位明确, 爆炸具有戏剧性** |

半径 3 是 CE 标准载具上限 (1) 的 3 倍, 在数值上明确将 P1000 与普通 VVE 载具区分开。同时, 在 CE 的增强爆炸模型下, 半径 3 产生的实际破坏范围和伤害强度已足够震撼, 无需依赖不可控的超大半径来制造戏剧效果。

### 4.3 纯 XML 实现 vs C# 补丁

Vehicle Framework 的 `CETurretDataDefModExtension` 接口使得全部炮塔修复可在 XML 层完成。这带来:

- **无程序集依赖**: 不引入对 CE 或 VVE DLL 的编译期依赖, 不存在版本不匹配导致的 TypeLoadException 风险
- **无 Harmony hooks**: 不占用 Harmony 补丁槽位, 不与其他 mod 的 Harmony 补丁产生顺序冲突
- **透明可审查**: 全部修改以声明式 XML 形式存在, 可直接阅读和验证, 无需反编译或调试 C# 代码
- **热加载友好**: XML 补丁在游戏启动时由 RimWorld 的补丁引擎处理, 无需担心静态构造函数初始化时序或程序集加载顺序

### 4.4 射击模式设计: FlakTurret 和 Ball_Turret

FlakTurret 和 Ball_Turret 是 P1000 上仅有的两个配备多射击模式的炮塔, 均采用 Single/Burst/Auto 三级模式结构, 遵循 CE 对自动武器的标准约定:

- **Single**: 单发精确射击, 适用于弹药节约或对单一高价值目标
- **Burst**: 短连射 (Flak 3 发, MG 5 发), 平衡精度与火力密度
- **Auto**: 全自动压制 (Flak 8 发, MG 10 发), 最大化瞬时火力输出

两者共享 `ticksBetweenShots: 6` (即每 0.1 秒一发), 反映自动武器的高射速特性。Burst 射弹数的差异 (3 vs 5) 和 Auto 射弹数的差异 (8 vs 10) 反映了 20mm 炮弹与 7.62mm 弹药在后坐力和弹匣容量上的差异: 更轻的 7.62mm 允许更长的连射。

280mm 舰炮和 75mm 坦克炮均为单发射击, 不设置射击模式 -- 这符合大口径火炮的操作现实。
