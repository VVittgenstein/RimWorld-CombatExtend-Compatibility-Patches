# RimThunder - Core CE 兼容补丁设计文档

> **Steam ID:** 3070495204  
> **PackageId:** `RimThunder.Core`  
> **补丁文件:** `Patches/RimThunderCore.xml`

---

## 问题现象：MG 炮塔弹药系统缺失

RimThunder Core 自带一套通过 `LoadFolders.xml` 条件加载的 CE 兼容层。该兼容层对重型火炮（100mm / 120mm / 125mm）和榴弹发射器（`RT_GrenadeLauncher`）做了完整的 CE 集成——它们有正确的 `CETurretDataDefModExtension` ammoSet 绑定、CE 原生弹药定义和弹道参数。

但三个机枪炮塔基类被遗漏了：

| 抽象基类 | 口径类别 | 原版弹体 | 原版伤害 |
|---------|---------|---------|---------|
| `RT_BaseVehicleMG_Light` | 5.56mm | `RT_Bullet_LMG` | 11 |
| `RT_BaseVehicleMG_Medium` | 7.62mm | `RT_Bullet_MMG` | 18 |
| `RT_BaseVehicleMG_Heavy` | 12.7mm | `RT_Bullet_HMG` | 45 |

这三个 def 均为 `Abstract="True"`。RT 的 CE 层对它们做了射速和射程调整（20 发连射、6-10 tick 间隔、射程 55/62/75），但**没有添加 `CETurretDataDefModExtension`**。结果是：炮塔仍然发射继承自原版 `BaseBullet` 的平伤弹体，完全绕过了 CE 的穿甲计算（sharp/blunt AP）、压制系统和弹药类型选择。

这意味着所有从这三个抽象基类继承的下游 RimThunder 载具包炮塔——所有轻机枪、中型机枪、重机枪——都存在同样的问题。

---

## 问题定界：CETurretDataDefModExtension 的架构角色

修复这个问题之前，必须理解 `CETurretDataDefModExtension` 的归属和调用链。

**关键发现：`CETurretDataDefModExtension` 不是 CE 的类，而是 Vehicle Framework 的类。** 它定义在 `Vehicles` 命名空间下（源码路径：`ModSource/3014915404/Vehicles/Vehicles/CETurretDataDefModExtension.cs`）。其字段包括：

```csharp
namespace Vehicles;
public class CETurretDataDefModExtension : DefModExtension
{
    public string ammoSet = null;   // AmmoSetDef.defName 字符串引用
    public float shotHeight = 1f;   // 射击高度偏移
    public float speed = -1f;       // 弹体速度覆写（>0 生效）
    public float sway = -1f;       // 武器摇摆（>=0 生效）
    public float spread = -1f;     // 弹体散布（>=0 生效）
    public float recoil = -1f;     // 后坐力
}
```

调用链路径：`VehicleTurret.cs:1424` 读取此 extension，获取 `ammoSet` 字符串后，调用 CE 注册的 `LookupAmmosetCE` 委托（`VehicleTurret` 的静态字段）将其解析为 `CombatExtended.AmmoSetDef`。整条链路为：

```
VehicleTurretDef (XML)
  → CETurretDataDefModExtension.ammoSet (字符串)
    → VehicleTurret.LookupAmmosetCE 委托 (Vehicle Framework)
      → CombatExtended.AmmoSetDef (CE 运行时解析)
```

没有 `CETurretDataDefModExtension`，`VehicleTurret` 拿不到 ammoSet，机枪炮塔退化为原版弹体。

---

## 设计方案评估

面对三个缺失 ammoSet 的机枪基类，有几种可能的修复路径：

### 方案 A：自定义弹药集（排除）

为每个口径创建独立的 `AmmoSetDef`、弹体 `ThingDef` 和制造 `RecipeDef`。

**排除原因：** RT 的口径注释（"5.56 / 5.8"、"7.62 / 7.92"、"12.7 / 14.5"）直接对应 CE 已有的 NATO 标准弹药。自定义弹药集只会复制已有的平衡数据，毫无收益。RT 自身的榴弹发射器之所以使用自定义 `RTC_AmmoSet_SmokeGrenade`，是因为烟雾弹在 CE 中没有现成等价物——机枪不存在这个问题。

### 方案 B：逐车辆补丁具体炮塔 def（排除）

直接对每个具体的（non-abstract）炮塔 def 添加 extension。

**排除原因：** `_Light`、`_Medium`、`_Heavy` 是抽象基类，下游所有 RimThunder 载具包的机枪炮塔都从它们继承。逐个补丁具体 def 意味着每新增一个车辆包就需要新增补丁，维护成本不可控。RT 自身的 CE 层也是在抽象基类级别做射速调整，我们应沿用同一模式。

### 方案 C：覆写弹体速度（排除）

在 `CETurretDataDefModExtension` 中设置 `speed` 字段覆写 CE 弹药的默认速度。

**排除原因：** CE 弹药弹体有各自精确校准的速度值（5.56mm: 168, 7.62mm: 156, .50 BMG: 163），覆写会破坏 CE 的弹道弧线计算。RT 榴弹发射器之所以设 `speed=20`，是因为烟雾弹是慢速抛射武器；直射机枪应使用 CE 标准弹道速度。

### 方案 D：复用 CE 标准 NATO 弹药集 + 补丁抽象基类（采用）

对三个抽象基类各添加一个 `CETurretDataDefModExtension`，`ammoSet` 字段引用 CE 已有的标准弹药集：

| 炮塔基类 | CE AmmoSetDef | 口径匹配依据 |
|---------|---------------|------------|
| `RT_BaseVehicleMG_Light` | `AmmoSet_556x45mmNATO` | RT 注释 "5.56 / 5.8" = CE mini-turret 同口径 |
| `RT_BaseVehicleMG_Medium` | `AmmoSet_762x51mmNATO` | RT 注释 "7.62 / 7.92" = CE medium turret 同口径 |
| `RT_BaseVehicleMG_Heavy` | `AmmoSet_50BMG` | RT 注释 "12.7 / 14.5" = CE 标准 HMG 口径 |

CE 的 NATO 弹药集自带完整的弹种变体（FMJ、AP、HP、燃烧弹、HE、脱壳穿甲弹）及全套制造配方。无需自定义弹药定义。

---

## 实现细节

### 补丁文件结构

实现位于单一文件 `Patches/RimThunderCore.xml`。外层使用 `PatchOperationFindMod` 门控 "RimThunder - Core"，内层 `PatchOperationSequence` 包含三个 `PatchOperationAddModExtension` 操作。

XPath 目标使用 `@Name` 选择器定位抽象 def：

```xml
Defs/Vehicles.VehicleTurretDef[@Name="RT_BaseVehicleMG_Light"]
Defs/Vehicles.VehicleTurretDef[@Name="RT_BaseVehicleMG_Medium"]
Defs/Vehicles.VehicleTurretDef[@Name="RT_BaseVehicleMG_Heavy"]
```

每个操作添加 `Vehicles.CETurretDataDefModExtension`，包含 `ammoSet` 和四个弹道参数。

### 弹道参数设计

参数设计参考 CE 固定炮塔武器数据和 RT 自身 CE 层的榴弹发射器参数，在两者之间取合理的载具机枪定位。

#### shotHeight = 1.5（三型统一）

载具炮塔从高于地面的位置射击。RT 的榴弹发射器使用 `shotHeight=2.0`；机枪炮塔通常在车体上的位置低于榴弹发射器，取 1.5 作为载具机枪的标准射击高度。

#### speed：不覆写（默认 -1，不生效）

CE 弹药弹体自带速度定义。当 `speed` 字段为默认值 -1 时，`VehicleTurret.cs` 不会覆写弹体速度，弹药的原始弹道特性得以保留。各口径 CE 弹体速度：5.56mm = 168，7.62mm = 156，.50 BMG = 163。

#### sway、spread、recoil：递进设计

| 参数 | LMG | MMG | HMG | 设计锚点 |
|------|-----|-----|-----|---------|
| sway | 0.8 | 1.0 | 1.4 | CE mini-turret 0.67 -- autocannon 1.61 区间内递进 |
| spread | 0.07 | 0.05 | 0.02 | 与 CE 炮塔直接对标：mini 0.07、medium 0.05；HMG 0.02 介于 medium 和 autocannon 0.01 之间 |
| recoil | 0.5 | 0.8 | 1.2 | 低于 CE 固定炮塔（mini 1.02、medium 0.95），因载具底盘吸收更多后坐力 |

**sway 递进逻辑：** 从轻机枪到重机枪，口径增大带来更大的武器摇摆。LMG（0.8）略高于 CE mini-turret 的 0.67，因为载具平台引入额外不稳定性；HMG（1.4）接近 autocannon 的 1.61 但保持低于该值，因为 .50 BMG 后坐力小于 20x102mm。

**spread 递进逻辑：** 与 CE 炮塔武器直接对标。LMG 取 mini-turret 的 0.07；MMG 取 medium turret 的 0.05；HMG 取 0.02，位于 medium（0.05）和 autocannon（0.01）之间——.50 BMG 精度极高但略低于 20mm 机关炮。

**recoil 递进逻辑：** 载具底盘比固定建筑炮塔吸收更多后坐力。CE 固定炮塔 recoil 范围为 0.95-1.50，载具机枪取 0.5-1.2，全面低于固定炮塔。

### CE 弹药对比参照

绑定标准 NATO 弹药集后，FMJ 弹种（最常用弹种）的战斗数据：

| 口径 | 伤害 | 锐穿 (sharp AP) | 钝穿 (blunt AP) |
|------|------|-----------------|-----------------|
| 5.56mm FMJ | 14 | 6 | 34.18 |
| 7.62mm FMJ | 20 | 7 | 66.72 |
| .50 BMG FMJ | 42 | 14 | 360.34 |

伤害阶梯 LMG < MMG < HMG 与 RT 原版伤害阶梯（11 < 18 < 45）保持同向。

---

## 跨模组对齐验证

将修复后的 RT 载具机枪与 CE 原生固定炮塔对比，验证数值定位合理性：

| 武器 | 口径 | 射程 | 连射数 | FMJ 伤害 | 锐穿 |
|------|------|------|-------|---------|------|
| CE Mini-turret | 5.56mm | 48 | 10 | 14 | 6 |
| **RT LMG** | **5.56mm** | **55** | **20** | **14** | **6** |
| CE Medium turret | 7.62mm | 55 | 10 | 20 | 7 |
| **RT MMG** | **7.62mm** | **62** | **20** | **20** | **7** |
| CE Autocannon | 20x102mm | 78 | 10 | 61 | 21 |
| **RT HMG** | **.50 BMG** | **75** | **20** | **42** | **14** |

同口径的 RT 载具机枪相比 CE 固定炮塔拥有更高的连射数（20 vs 10）和略长的射程。这是合理的：载具安装的机枪有专用弹链供弹系统和更好的安装基座，火力持续性和有效射程应优于固定建筑炮塔。

HMG 使用 .50 BMG 而非 autocannon 的 20x102mm，因此伤害和穿甲值自然低于 autocannon——这正确反映了口径差异，而非设计失误。

---

## 为什么这是最优方案

1. **零自定义弹药开销。** 三个口径精确匹配 CE 已有的 NATO 标准弹药集，复用 CE 已经平衡好的伤害、穿甲、弹速和弹种变体数据。

2. **抽象基类补丁自动传播。** 对 `Abstract="True"` 的基类添加 extension，所有下游 RimThunder 载具包的机枪炮塔自动获得 CE 弹药支持，无需逐车辆维护。这与 RT 自身 CE 层的补丁策略一致。

3. **纯 XML 实现。** 整个修复通过 `PatchOperationAddModExtension` 完成，不需要 C# 代码或 Harmony 补丁。所有运行时逻辑由 Vehicle Framework 的 `VehicleTurret.cs` 和 CE 委托链现有机制处理。

4. **弹道参数有锚点。** 每个数值都能在 CE 炮塔武器或 RT 自身 CE 层中找到参照，不是凭空设定。`speed` 不覆写是因为 CE 弹药有自校准速度；其余参数在 CE 固定炮塔数值范围内按口径递进。

5. **不干扰 RT 现有 CE 层。** `PatchOperationAddModExtension` 向 def 添加新的 extension，不会与 RT 自身 CE 层的 `PatchOperationReplace` 操作冲突。RT 已有的射速、射程、弹夹、装填时间调整全部保留。
