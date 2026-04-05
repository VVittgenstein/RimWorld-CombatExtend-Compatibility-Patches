# Doors Expanded CE 兼容补丁设计文档

| 字段 | 值 |
|------|-----|
| Steam ID | 3532342422 |
| 作者 | Jecrell, lbmaian, jebjordan |
| PackageId | `jecrell.doorsexpanded` |
| DLL | DoorsExpanded.dll |
| 补丁类型 | C# Harmony patch |
| 补丁文件 | `Source/V2CEPatch/Harmony/Patch_CollisionVertical_CalculateHeightRange.cs` |

---

## 问题：开启状态的扩展门成为 CE 弹道的隐形墙

Doors Expanded 的核心架构决定了这个问题的根源：`Building_DoorExpanded` 继承自 `Building`，而**非** `Building_Door`。这是该 mod 的根本性设计选择——它完全绕开了原版门的类继承体系。

CE 的 `CollisionVertical.CalculateHeightRange()` 在第 79 行执行 `thing is Building_Door` 类型检查以识别门。`Building_DoorExpanded` 无法通过此检查，方法回退至第 82 行的通用 `Building` 分支。该分支判定 `Fillage == FillCategory.Full`（因为所有扩展门的 `fillPercent=1`），于是赋予 **2 米碰撞高度**。

结果：所有处于开启状态的 Doors Expanded 门对 CE 弹道而言是一堵隐形的 2 米墙。关闭的门表现正确（理应阻挡弹道），但开启的门同样阻挡——这是错误行为。

### 完整碰撞链路分析

弹道碰撞的完整调用链：

```
ProjectileCE.CheckCellForCollisions (line 1104)
  → Fillage > 0 过滤器命中开启状态的扩展门（fillPercent=1）
  → 将门加入碰撞候选列表
    → CanCollideWith()
      → CE_Utility.GetBoundsFor(thing) (lines 910-944)
        → CollisionVertical(thing)
          → CalculateHeightRange()
            → thing is Building_Door 失败
            → 回退到 Building 分支
            → Fillage == Full → 返回 heightRange = (0, 2)
              → 非零碰撞包围盒 → IntersectRay 成功
                → 弹道被阻挡
```

### 为什么只有弹道系统受影响

Doors Expanded 在门实体旁放置了不可见的 `Building_DoorRegionHandler` 辅助对象。这些辅助对象**确实**继承自 `Building_Door`，负责处理所有非弹道的门交互。CE 和原版系统中依赖 `GridsUtility.GetDoor()` 或 `is Building_Door` 检查的功能——包括 IncendiaryFuel 传火、Smoke 扩散、SuppressionUtility 压制穿透、寻路系统——全部通过这些不可见辅助对象正常工作。

唯一失败的环节是 `CollisionVertical.CalculateHeightRange()`，因为该方法直接操作弹道碰撞候选列表中的 `Thing` 实例，处理的是 `Building_DoorExpanded` 本体而非辅助对象。

### Doors Expanded 自身的 Harmony patch 为何无效

Doors Expanded 对原版 `Projectile.CheckForFreeIntercept` 和 `Projectile.ImpactSomething` 做了 Harmony patch。但在 CE 环境下，弹道系统由 `ProjectileCE` 完全替代，原版 `Projectile` 的弹道方法不会被调用。这些 patch 是死代码。

---

## 设计决策：单点 Harmony Prefix

### 目标方法

```
CombatExtended.CollisionVertical.CalculateHeightRange
签名: private static void CalculateHeightRange(Thing thing, out FloatRange heightRange, out float shotHeight)
访问级别: private static
```

私有静态方法，通过 Harmony 的 `AccessTools.Method` 进行定位和 patch。

### 为什么选择 Prefix 而非 Transpiler

**Prefix 方案（采用）：**
- Harmony 允许 Prefix 将 `out` 参数作为 `ref` 处理，可直接赋值后返回 `false` 跳过原方法
- 仅对 `Building_DoorExpanded` 实例激活，其他所有 `Thing` 返回 `true`（执行原方法），零侵入性
- 逻辑自包含，不依赖目标方法的内部 IL 结构

**Transpiler 方案（排除）：**
- 目标方法是私有静态方法，其 IL 来自反编译结果
- Transpiler 需要精确匹配 IL 操作码序列，跨 CE 版本更新极易断裂
- 该方法内部包含多个 `if`/`else` 分支和 `is` 类型检查，IL 模式复杂

### 补丁逻辑

```
if thing is Building_DoorExpanded:
    if Open:
        heightRange = (0, 0)    → 弹道穿透
        shotHeight = 0
    if !Open:
        heightRange = (0, WallCollisionHeight)  → 实体墙壁
        shotHeight = WallCollisionHeight
    return false  // 跳过原方法
else:
    return true   // 执行原方法
```

这是一个纯二值修复：开启 = 零碰撞高度（弹道通过），关闭 = 2 米（CE 的 `WallCollisionHeight` 常量，完整墙壁阻挡）。不涉及任何数值平衡调整。

### FreePassage 门类型的处理

Doors Expanded 的 FreePassage 门类型（始终开启的门，如拱门）的 `Open` 属性始终返回 `true`。本 patch 无需特殊处理——FreePassage 门自然被归入 `Open == true` 分支，碰撞高度为零，弹道正常穿透。

---

## 实现细节

C# 源码：`Source/V2CEPatch/Harmony/Patch_CollisionVertical_CalculateHeightRange.cs`

### 软引用与反射机制

V2CEPatch.dll 为全部 12 个目标 mod 提供补丁，无论 Doors Expanded 是否安装都会加载。如果对 DoorsExpanded.dll 添加硬引用（直接 `using` 或类型引用），在 Doors Expanded 未安装时会导致程序集加载崩溃。

实现采用反射软引用：

```csharp
doorExpandedType = AccessTools.TypeByName("DoorsExpanded.Building_DoorExpanded");
if (doorExpandedType != null)
    openProperty = AccessTools.Property(doorExpandedType, "Open");
```

- `AccessTools.TypeByName` 在目标程序集未加载时安全返回 `null`
- `AccessTools.Property` 获取 `Open` 属性的 `PropertyInfo` 用于后续反射调用
- 这是 CE 兼容 mod 的标准做法——在运行时按名称解析类型，避免编译期硬依赖

### 延迟初始化

```csharp
private static Type doorExpandedType;
private static PropertyInfo openProperty;
private static bool initialized;
```

`doorExpandedType` 和 `openProperty` 在第一次调用时通过 `initialized` 标志完成一次性解析。此后每次调用仅执行一次 `IsInstanceOfType` 类型检查和一次 `PropertyInfo.GetValue` 反射调用。

当 Doors Expanded 未加载时：`doorExpandedType == null` → 第 31 行直接返回 `true`（执行原方法）→ 对非 Doors Expanded 用户零性能开销。

### TargetMethod 动态绑定

```csharp
static MethodBase TargetMethod()
{
    return AccessTools.Method(typeof(CollisionVertical), "CalculateHeightRange");
}
```

因 `CalculateHeightRange` 是私有方法，无法使用 `[HarmonyPatch(typeof(...), "...")]` 注解直接指定。通过 `TargetMethod()` 配合 `AccessTools.Method` 在运行时定位目标方法。

---

## 为什么不需要额外的 Patch 点

修复仅需 patch `CalculateHeightRange` 这一个方法。下游所有依赖碰撞高度的系统自动获得正确行为：

### 1. ProjectileCE.CheckCellForCollisions（line 1104）

第 1104 行的 `Fillage > 0` 过滤器仍然会将开启状态的扩展门加入碰撞候选列表（fillPercent 仍为 1）。但后续调用链 `CanCollideWith()` -> `GetBoundsFor()` -> `CollisionVertical()` 现在返回零高度碰撞包围盒 -> `IntersectRay` 计算失败 -> 候选被丢弃。整条链路通过现有逻辑自愈。

### 2. ProjectileCE.ImpactSomething（~line 1680）

使用相同的碰撞链路，同样自愈。

### 3. Verb_LaunchProjectileCE.GetTargetHeight

使用 `CollisionVertical(cover)` 计算掩体高度。开启的扩展门 -> 零高度 -> 无掩体效果。关闭的扩展门 -> 完整墙壁高度。两种状态均正确。

### 4. IncendiaryFuel / Smoke / SuppressionUtility

这些系统使用 `is Building_Door` 类型检查或 `GridsUtility.GetDoor()` 方法，命中的是不可见的 `Building_DoorRegionHandler` 辅助对象。完全不受本 patch 影响。

---

## 备选方案排除

**为什么不用 Transpiler？**
`CalculateHeightRange` 是私有静态方法，IL 来自反编译。Transpiler 需要精确匹配 IL 操作码模式，CE 版本更新可能改变编译器生成的 IL 序列（分支优化、局部变量重排等）。Prefix 仅依赖方法签名稳定性，对方法内部实现变更完全免疫。

**为什么不让 `Building_DoorExpanded` 对 CE 表现为 `Building_Door`？**
`Building_DoorExpanded` 有自己独立定义的 `Open` 属性，而非从 `Building_Door` 继承。伪造继承关系（例如通过 Harmony patch `is` 运算符的底层实现）有破坏 Doors Expanded 自身 Harmony patch 的风险。且不可见的 `Building_DoorRegionHandler` 辅助对象已经正确处理了所有非弹道的门交互——修复范围应限制在唯一失败的环节。

**为什么不动态修改 `fillPercent`？**
`fillPercent=1` 是 Doors Expanded 刻意设定的值，控制关闭状态门的掩体行为。动态修改会影响所有读取 `fillPercent` 的系统，而非仅限 CE 弹道碰撞。副作用范围不可控。

**为什么不硬引用 DoorsExpanded.dll？**
V2CEPatch.dll 为 12 个目标 mod 提供补丁，所有用户都会加载该程序集。硬引用 DoorsExpanded.dll 意味着在 Doors Expanded 未安装时触发 `TypeLoadException` 导致崩溃。基于反射的软引用是 CE 兼容 mod 的标准实践，`doorExpandedType == null` 时直接返回，零开销退出。
