# EndlessGrowth CE 兼容补丁

| 字段 | 值 |
|------|-----|
| Steam ID | 2894401734 |
| 作者 | Slime-Senpai |
| PackageId | `SlimeSenpai.EndlessGrowth` |
| DLL | EndlessGrowth.dll |
| 补丁类型 | 纯 XML，无 DLL |
| 补丁文件 | `Patches/EndlessGrowth.xml` |

---

## 问题：技能等级超过 20 后 CE 射击属性完全停滞

EndlessGrowth 通过 Harmony transpiler 解除原版技能等级上限：将 `SkillRecord.Level` 中的 `Mathf.Clamp(x, 0, 20)` 替换为 `Mathf.Max(x, 0)`，同时移除 `SkillRecord.Learn` 中的 20 级硬顶。玩家的 pawn 可以将射击技能提升至 100 甚至更高。

CE 的 `ReloadSpeed` StatDef 使用 `SkillNeed_Direct` 机制，配有一个 21 条目的 `valuesPerLevel` 列表（对应等级 0-20，值从 0.75 到 1.50）。原版 `SkillNeed_Direct.ValueFor()` 方法（`GameSource/RimWorld/SkillNeed_Direct.cs:10-26`）内建钳位逻辑：当 `level >= valuesPerLevel.Count` 时，返回列表最后一个条目。因此等级 21 及以上永远返回 1.50。

`AimingDelayFactor` 存在完全相同的问题：21 条目列表（1.25 到 0.75），等级 21 及以上永远返回 0.75。

**后果**：一个射击技能 100 的 pawn 在换弹速度和瞄准延迟上与 20 级 pawn 完全一致。这直接违背了 EndlessGrowth 的核心设计承诺——持续投入经验值应获得递减但有意义的回报。

### 不受影响的 CE 战斗属性

并非所有 CE 射击属性都存在此问题。以下属性使用 `SkillNeed_BaseBonus`，其计算公式为 `baseValue + bonusPerLevel * level`，天然随等级线性增长，由 `postProcessCurve` 提供上界约束：

- `AimingAccuracy`：自动缩放，代码级上限 1.50
- `MeleeCritChance`：自动缩放，曲线上限 0.80
- `MeleeParryChance`：自动缩放
- `UnarmedDamage`：自动缩放

只有使用 `SkillNeed_Direct`（查表法）的 `ReloadSpeed` 和 `AimingDelayFactor` 因列表长度硬编码为 21 而被截断。

---

## 设计决策

### 纯 XML 方案——无需 Harmony

`SkillNeed_Direct` 是原版类，CE 未修改其实现。该类的设计意图就是通过 `valuesPerLevel` 列表驱动——将列表从 21 条目扩展至 101 条目是其原生扩展机制，无需 C# 代码介入。

### ReloadSpeed 扩展曲线

等级 0-20 保留 CE 原始值不变。等级 21-100 按指数衰减公式生成：

```
f(L) = 1.50 + 0.50 * (1 - e^(-0.05 * (L - 20)))    L > 20
```

参数定义：

| 参数 | 值 | 含义 |
|------|----|------|
| 基值 | 1.50 | 与等级 20 的值完全连续 |
| 渐近上限 | 2.00 | 最大换弹速度倍率 |
| 振幅 | 0.50 | 从等级 20 到理论极限的总增量 |
| 衰减常数 k | 0.05 | 半衰期约 14 级；主要收益集中在 20-40 级区间 |

关键里程碑：

| 等级 | ReloadSpeed | 换弹时间（4s 武器） | 累计经验需求 |
|------|-------------|-------------------|-------------|
| 0 | 0.750 | 5.33s | 0 |
| 10 | 1.100 | 3.64s | ~55k |
| 20 | 1.500 | 2.67s | ~210k |
| 25 | 1.611 | 2.48s | ~370k |
| 30 | 1.697 | 2.36s | ~595k |
| 40 | 1.816 | 2.20s | ~1.4M |
| 50 | 1.888 | 2.12s | ~2.6M |
| 100 | 1.991 | 2.01s | ~20.2M |

#### 上限 2.0 的论证

ReloadSpeed 在换弹公式中作为**除数**出现：`duration = ceil(seconds_to_ticks(reloadTime) * weaponReloadFactor / pawnReloadSpeed)`（`JobDriver_Reload.cs:229-234`）。这意味着 ReloadSpeed 直接乘算 DPS。

- ReloadSpeed 2.0 时，4 秒武器换弹变为 2.0 秒——仍然是战术层面的显著窗口
- 等级 0-20 的增益是 2x 倍率（0.75 到 1.50）；等级 20-100 仅增加 1.33x（1.50 到 2.00）
- 一把 8 秒换弹的重型武器在等级 100 仍需 4 秒，保留了 CE 压制系统的脆弱窗口
- 上限 2.0 是硬安全边界，即便 pawn 技能远超 100 也不会突破

### AimingDelayFactor 扩展曲线

与 ReloadSpeed 对称的递减曲线（AimingDelayFactor 越低越好）：

```
g(L) = 0.75 - 0.25 * (1 - e^(-0.05 * (L - 20)))    L > 20
```

| 参数 | 值 |
|------|----|
| 基值 | 0.75（等级 20 连续） |
| 渐近下限 | 0.50 |
| 振幅 | 0.25 |
| 衰减常数 k | 0.05 |

关键点位：等级 30 为 0.652，等级 40 为 0.592，等级 100 为 0.505。

### postProcessCurve 不构成约束

`ReloadSpeed` 的 `postProcessCurve` 在输入值 0.5 以上实质上是恒等映射（定义点：(0.5, 0.5) 到 (999, 999)）。`valuesPerLevel` 的最小条目为 0.75，远高于 0.5 阈值，因此后处理曲线不会截断或修改扩展值。

---

## 实现细节

补丁文件：`Patches/EndlessGrowth.xml`

### 门控结构

外层 `PatchOperationFindMod` 同时检测 "Combat Extended" 和 "EndlessGrowth" 两个 mod 是否加载，仅在两者同时存在时执行。内层 `PatchOperationSequence` 包含两个操作。

### 操作 1：ReloadSpeed valuesPerLevel 替换

```xml
<li Class="PatchOperationReplace">
    <xpath>Defs/StatDef[defName="ReloadSpeed"]/skillNeedFactors/li[@Class="SkillNeed_Direct"]/valuesPerLevel</xpath>
    <value>
        <valuesPerLevel>
            <!-- Level 0-20: 原始 CE 值不变 -->
            <li>0.75</li>
            ...
            <li>1.5</li>
            <!-- Level 21-100: 指数衰减扩展 -->
            <li>1.524</li>
            ...
            <li>1.991</li>
        </valuesPerLevel>
    </value>
</li>
```

使用 `PatchOperationReplace` 整体替换 `valuesPerLevel` 节点，列表从 21 条目扩展至 101 条目。等级 0-20 的值与 CE 原始定义完全一致，逐值保留。

### 操作 2：AimingDelayFactor valuesPerLevel 替换

```xml
<li Class="PatchOperationReplace">
    <xpath>Defs/StatDef[defName="AimingDelayFactor"]/skillNeedFactors/li[@Class="SkillNeed_Direct"]/valuesPerLevel</xpath>
    <value>
        <valuesPerLevel>
            <!-- Level 0-20: 原始 CE 值不变 -->
            <li>1.25</li>
            ...
            <li>0.75</li>
            <!-- Level 21-100: 指数衰减扩展 -->
            <li>0.738</li>
            ...
            <li>0.505</li>
        </valuesPerLevel>
    </value>
</li>
```

同样使用 `PatchOperationReplace`，列表从 21 条目扩展至 101 条目。

### XPath 目标说明

`AimingDelayFactor` 定义在 CE 的 `Patches/Core/Stats/Stats.xml` 中，而非 Defs 目录。但这不影响 XPath 寻址——CE 的补丁在我们的下游补丁之前执行，补丁应用后 `AimingDelayFactor` StatDef 已存在于 Defs 池中，XPath 路径 `Defs/StatDef[defName="AimingDelayFactor"]` 正常命中。

---

## 跨属性对齐表

| 属性 | 等级 20→40 增益 | 等级 20→100 增益 | 上限 | 机制 |
|------|---------------|-----------------|------|------|
| ReloadSpeed（本补丁） | +21% | +33% | 2.00 | 扩展 valuesPerLevel |
| AimingDelayFactor（本补丁） | -21% | -33% | 0.50 | 扩展 valuesPerLevel |
| AimingAccuracy | +22% | +60% | 1.50（代码级） | SkillNeed_BaseBonus（自动缩放） |
| MeleeCritChance | +36% | +89% | 0.80（曲线） | SkillNeed_BaseBonus（自动缩放） |

ReloadSpeed 的扩展在所有属性中最为保守——这是有意为之。换弹速度直接乘算 DPS（作为除数出现在换弹公式中），过大的增益会破坏 CE 的战斗节奏。相比之下，AimingAccuracy 自动获得 +60% 的增益而 ReloadSpeed 仅 +33%，确保超高等级射手的主要优势体现在命中率而非火力密度。

---

## 对 EndlessGrowth 衰减机制的依赖

EndlessGrowth 在等级 20 以上引入被动经验衰减：每个 interval tick 扣除 `-12f * mult` 经验值（`SkillRecord_Interval_Patch.cs:12-18`）。这防止了 pawn 在不活跃使用技能时永久维持极端等级。

该衰减机制意味着活跃射手的稳定高等级区间大约在 20-40 级——恰好是扩展曲线提供最大收益增量的区间。超过 40 级需要极高的经验投入来对抗衰减，而曲线在该区间已接近渐近线，额外收益微乎其微。

上限 2.0 / 下限 0.50 作为硬安全边界存在，即便衰减机制被其他 mod 覆盖或绕过也不会产生失控的战斗数值。

---

## 备选方案排除

**为什么不用 Harmony 补丁 `SkillNeed_Direct.ValueFor()`？**
不必要的复杂度。`SkillNeed_Direct` 的设计就是通过 `valuesPerLevel` 列表驱动查表，扩展列表长度是其原生扩展机制。引入 Harmony 补丁意味着额外的 DLL 依赖、加载顺序风险和运行时性能开销，而纯 XML 方案零成本达到相同效果。

**为什么不用无上限的线性扩展？**
会摧毁 CE 战斗节奏。如果 ReloadSpeed 线性增长到 5.0，一把 4 秒武器的换弹时间降至 0.8 秒，换弹窗口几乎消失，CE 的压制系统失去意义。指数衰减配合渐近上限既满足了 EndlessGrowth "持续有收益"的承诺，又将绝对增益控制在 CE 可承受范围内。

**为什么上限不设为 1.75 而是 2.0？**
上限 1.75 意味着从等级 20 到极限仅有 16.7% 的提升，等级 40 以上几乎没有可感知的差异。这会让等级 40-100 的投入失去意义，违背 EndlessGrowth 的核心承诺。上限 2.0 提供 33% 的总提升空间，与经验投资曲线的陡峭程度对齐。

**为什么不扩展到 200 级以上？**
EndlessGrowth 的经验曲线使 100 级以上的成本呈天文数字级增长（100 级需 ~20.2M 经验）。101 条目覆盖了全部实际可达范围。等级 101 及以上通过 `SkillNeed_Direct` 的内建钳位逻辑返回最后一个条目（1.991 / 0.505），实质上已在上限。
