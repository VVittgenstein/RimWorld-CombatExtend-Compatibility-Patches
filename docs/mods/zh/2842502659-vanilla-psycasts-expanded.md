# Vanilla Psycasts Expanded CE 兼容补丁

| 字段 | 值 |
|------|-----|
| Steam ID | 2842502659 |
| 作者 | erdelf, Oskar Potocki, legodude17, Taranchuk 等 |
| PackageId | `VanillaExpanded.VPsycastsE` |
| 补丁类型 | 纯 XML，无 DLL |
| 补丁文件 | `Patches/VanillaPsycastsExpanded.xml` |

---

## 背景：CE 已有的 VPE 补丁体系

CE 对 Vanilla Psycasts Expanded 的补丁规模在所有第三方 mod 中属于最大级别：15 个手动 XML 补丁文件加 1 个 C# 兼容类。这些补丁覆盖了护甲、武器、种族、Hediff 等多个子系统。然而，在这套庞大的补丁体系中，仍存在 4 处缺陷——有的是属性系统误用导致数值归零，有的是 XPath 命名空间错误导致补丁静默失效，有的是遗漏了特定单位，有的是未处理 VEF 自定义 Stat。

本补丁通过单一文件 `Patches/VanillaPsycastsExpanded.xml` 中的 8 个操作修复全部 4 处缺陷。

---

## 缺陷 1：Eltex 斗篷装甲值归零

### 现象

CE 的 `Apparel_Patch.xml:36-58` 对 `VPE_Apparel_EltexCape` 执行了以下修改：

- 将 `ArmorRating_Sharp` 替换为 `StuffEffectMultiplierArmor: 4`
- 移除 `ArmorRating_Blunt`
- 移除 `ArmorRating_Heat`

结果：斗篷在所有轴上的装甲值为 **零**。

### 根因

`StuffEffectMultiplierArmor` 是材料派生装甲的倍率因子，仅对拥有 `stuffCategories` 的可选材物品生效。RimWorld 的装甲计算逻辑为：

```
最终装甲 = 材料基础装甲 * StuffEffectMultiplierArmor
```

Eltex 斗篷是非可选材物品——它通过 `costList` 指定固定原料（`VPE_Eltex` + `Cloth`），没有 `stuffCategories` 节点。对非可选材物品而言，不存在"材料基础装甲"可供乘算，`StuffEffectMultiplierArmor` 的乘数作用于零值，结果仍为零。

CE 补丁作者显然是从声望板甲（Prestige Plate Armor）的补丁模板复制而来——声望板甲拥有 `stuffCategories`（Metallic/Woody），`StuffEffectMultiplierArmor` 在那里是正确的。但斗篷的制作方式完全不同，模板不适用。

### 修复方案：直接装甲值

既然斗篷不可选材，应直接设置装甲 rating 而非使用乘数：

| 属性 | CE 原补丁（失效） | 修复值 | 定位依据 |
|------|------------------|--------|---------|
| ArmorRating_Sharp | StuffEffectMultiplierArmor: 4（实际 0） | **3** | 高于 Eltex 面具（0.5），低于防弹背心（4.0）——Shell 层轻型仪式服 |
| ArmorRating_Blunt | 已删除（0） | **2** | 与 Sharp 成比例；布料/Eltex 可吸收部分冲击 |
| ArmorRating_Heat | 已删除（0） | 不恢复 | CE 惯例：非耐热特化护具不设 Heat 值 |
| Bulk / WornBulk | 5 / 1.5 | 保留 | CE 原补丁中的体积设定合理 |

### 为什么不给斗篷添加 stuffCategories？

添加 `stuffCategories` 可以使 `StuffEffectMultiplierArmor` 生效，但这会从根本上改变 mod 的制作设计：固定配方变为材料选择制。这超出了兼容补丁的职责范围——补丁应修复 CE 交互问题，不应改变原 mod 的玩法设计。

---

## 缺陷 2：ExpandableProjectileDef XPath 命名空间错配

### 现象

CE 对 VPE 的火焰吐息和冰霜吐息投射物的补丁全部静默失效——无报错，无效果。3 个操作均为空操作：

| 投射物 | 属性 | 原始值（未修补） | CE 意图值 |
|--------|------|-----------------|----------|
| VPE_FireBreath | damageAmountBase | 5 | 12 |
| VPE_FireBreath | speed | 45 | 53 |
| VPE_IceBreathe | speed | 45 | 48 |

### 根因

CE 补丁的 XPath 使用 `VEF.Weapons.ExpandableProjectileDef` 作为 Def 元素名，但 VPE 的 XML 实际使用 `VFECore.ExpandableProjectileDef`（VFECore 别名）。RimWorld 的 XPath 引擎在 **字面 XML 元素名** 上匹配，不解析 C# 类型别名。`VEF.Weapons.ExpandableProjectileDef` 在 XML 文档中不存在任何匹配节点，所以 `PatchOperationReplace` 直接跳过，不产生错误日志。

```
CE 补丁 XPath（错误）:  Defs/VEF.Weapons.ExpandableProjectileDef[defName="VPE_FireBreath"]/...
VPE XML 实际标签:       Defs/VFECore.ExpandableProjectileDef[defName="VPE_FireBreath"]/...
```

### 修复方案

将 XPath 中的命名空间从 `VEF.Weapons.` 修正为 `VFECore.`。CE 原定的目标值（damage 12, speed 53/48）经过合理设计，直接沿用。

---

## 缺陷 3：召唤骷髅缺少 CE 近战属性

### 现象

CE 的 `Races_Patch.xml:6-57` 为 VPE 的构造体（`VPE_SteelConstruct`、`VPE_RockConstruct`）添加了 CE 近战属性，但遗漏了 `VPE_SummonedSkeleton`。缺失的属性：

- `MeleeDodgeChance` — 近战闪避率
- `MeleeCritChance` — 近战暴击率
- `MeleeParryChance` — 近战格挡率
- `SmokeSensitivity` — 烟雾敏感度

CE 近战系统在计算闪避/暴击/格挡时读取这些 stat。缺失时回退到默认值 0，导致骷髅在 CE 的近战子系统中完全无法闪避、暴击或格挡——本质上是一个只会硬挨打的木桩。

### 数值选择：构造体等级，而非精英等级

VPE 中还有另一种召唤近战单位——Runesmith 的 Warrior Spirit，其 CE 补丁设定了 MeleeDodgeChance 0.40 / MeleeCritChance 0.60 / MeleeParryChance 1.50 的精英级数值。

骷髅不应使用这些数值。原因在于定位差异：

| 属性 | 骷髅 | Warrior Spirit |
|------|------|---------------|
| 存续时间 | 60000 ticks（~1 天） | 永久 |
| 设计定位 | 一次性消耗品，量产炮灰 | 精英近战伴侣 |
| 预期作战方式 | 以数量换时间，消耗后丢弃 | 长期培养，前线核心 |

选用与钢铁/岩石构造体相同的数值：

| CE Stat | 值 | 含义 |
|---------|-----|------|
| MeleeDodgeChance | 0.19 | 基础闪避能力 |
| MeleeCritChance | 0.22 | 基础暴击能力 |
| MeleeParryChance | 0.09 | 有限格挡能力 |
| SmokeSensitivity | 0 | 不死生物不受烟雾影响 |

这组数值使骷髅能参与 CE 近战子系统的完整交互（闪避、暴击、格挡判定均有效），同时保持其炮灰定位——不会强到改变战术决策。

---

## 缺陷 4：VEF 自定义 Stat Hediff 在 CE 下失效

### 现象

两个 Warlord 路径灵能在 CE 下完全无效：

- **VPE_BladeFocus**（剑刃聚焦，T2）：原始效果为 `VEF_MeleeAttackSpeedFactor: 2`（2 倍近战速度）
- **VPE_ControlledFrenzy**（受控狂暴，T3）：原始效果为 `VEF_MeleeAttackDamageFactor: 2` + `VEF_RangeAttackDamageFactor: 2`（2 倍全伤害）

### 根因

这两个 Hediff 依赖 VEF（Vanilla Expanded Framework）定义的自定义 StatDef：`VEF_MeleeAttackSpeedFactor`、`VEF_MeleeAttackDamageFactor`、`VEF_RangeAttackDamageFactor`。VEF 通过 Harmony 补丁将这些 Stat 注入原版方法的计算流程。

问题在于：CE 用自己的实现**替换**了这些原版方法。VEF 的 Harmony 补丁挂载点不复存在，自定义 Stat 因此无法生效。在 CE 源码中搜索这三个 StatDef 名称，返回零结果——CE 完全不知道它们的存在。

### 先例：FiringFocus 的成功重映射

CE 已经处理过同类问题。`VPE_FiringFocus`（射击聚焦，T2）原始效果为 `VEF_RangeAttackSpeedFactor: 5`（5 倍远程射速），CE 在 `Hediffs_Patch.xml:15-23` 中将其重映射为：

- `AimingDelayFactor` (statFactor) — CE 的瞄准延迟因子
- `ReloadSpeed` (statOffset) — CE 的装填速度

BladeFocus 和 ControlledFrenzy 需要同样的处理——将 VEF Stat 翻译为 CE 理解的等效属性。

### BladeFocus 重映射设计

原始效果"2 倍近战攻击速度"翻译为 CE 属性：

| CE Stat | 类型 | 值 | 设计逻辑 |
|---------|------|-----|---------|
| MeleeCooldownFactor | statFactor | 0.5 | 冷却时间减半 = 2 倍攻速。CE 在 `VerbProperties.AdjustedCooldown()`（`VerbProperties.cs:537`）读取此值 |
| MeleeHitChance | statOffset | +5 | 聚焦状态提升命中精度 |
| MeleeCritChance | statOffset | +0.5 | 聚焦状态使暴击成为可能。CE 暴击对锐器攻击造成 2 倍伤害（`Verb_MeleeAttackCE.cs:424-425`） |

核心翻译是 `MeleeCooldownFactor: 0.5`——这是"2 倍速度"的数学等价。额外的 HitChance 和 CritChance 使灵能与 CE 近战子系统产生更丰富的交互，而非仅仅加速攻击动画。

### ControlledFrenzy 重映射设计

原始效果"2 倍近战伤害 + 2 倍远程伤害"。CE 没有 pawn 级伤害乘数 stat，需要通过组合属性近似实现：

| CE Stat | 类型 | 值 | 设计逻辑 |
|---------|------|-----|---------|
| MeleeCooldownFactor | statFactor | 0.75 | 近战加速 25%（弱于 BladeFocus 的 50%，因为 Frenzy 是全能型非专精） |
| RangedCooldownFactor | statFactor | 0.75 | 远程加速 25% |
| MeleeHitChance | statOffset | +10 | 狂暴反射——T3 高于 BladeFocus 的 +5 |
| MeleeCritChance | statOffset | +1.0 | 近乎必定暴击。暴击 2 倍伤害近似还原"2 倍近战伤害" |
| AimingAccuracy | statOffset | +0.5 | 超人瞄准精度，提升远程命中与伤害输出 |
| ReloadSpeed | statOffset | +1.0 | 装填速度翻倍（与 FiringFocus 等值） |

**关键设计约束——Warlord 路径层级关系：**

ControlledFrenzy 是 T3 灵能，必须严格强于同路径的 T2 专精（BladeFocus 和 FiringFocus）。它通过"近战 + 远程双修"的全覆盖特性实现这一层级优势——单项不一定超过 T2 专精，但总输出能力必须明显更高。

### 为什么不保留 VEF Stat 让 VEF Harmony 自行处理？

不可靠。VEF 的 Harmony 补丁挂载在原版方法上，CE 替换了这些方法。即使在某些 Harmony 加载顺序下 VEF 的 Postfix 可能侥幸执行，这依赖于不可控的加载顺序，不是可维护的兼容方案。将 Stat 重映射到 CE 原生属性确保 buff 对 CE 武器系统稳定生效。

---

## Warlord 灵能层级验证

修复完成后，Warlord 路径的 5 个灵能在 CE 下的功能完整性：

| 灵能 | 层级 | 原始 VEF 效果 | CE 重映射 | 状态 |
|------|------|-------------|----------|------|
| SpeedBoost | T1 | 3x MoveSpeed | 原生兼容，无需补丁 | 已生效 |
| BladeFocus | T2 | 2x 近战速度 | MeleeCooldownFactor 0.5 + MeleeHitChance +5 + MeleeCritChance +0.5 | **本补丁修复** |
| FiringFocus | T2 | 5x 远程射速 | AimingDelayFactor -0.50 + ReloadSpeed +1.0 | CE 已有补丁 |
| ControlledFrenzy | T3 | 2x 全伤害 | 完整近战+远程属性块（6 项） | **本补丁修复** |
| GuidedShot | T3 | 必中 + 2x 射程 | AimingAccuracy / ShootingAccuracyPawn | CE 已有补丁 |

层级递进完整：T1 移速 -> T2 单项专精 -> T3 全面强化。

---

## 实现细节

补丁文件：`Patches/VanillaPsycastsExpanded.xml`

### 门控结构

外层使用 `PatchOperationFindMod` 检测 "Vanilla Psycasts Expanded" 是否加载，匹配时执行内层 `PatchOperationSequence`，包含 8 个操作。

### 操作 1：修复斗篷 Sharp 装甲

```xml
<li Class="PatchOperationReplace">
    <xpath>Defs/ThingDef[defName="VPE_Apparel_EltexCape"]/statBases/StuffEffectMultiplierArmor</xpath>
    <value>
        <ArmorRating_Sharp>3</ArmorRating_Sharp>
    </value>
</li>
```

使用 `PatchOperationReplace` 将失效的 `StuffEffectMultiplierArmor` 节点替换为 `ArmorRating_Sharp`。Replace 操作在原地替换单个 XML 节点，避免残留无效元素。

### 操作 2：补回斗篷 Blunt 装甲

```xml
<li Class="PatchOperationAdd">
    <xpath>Defs/ThingDef[defName="VPE_Apparel_EltexCape"]/statBases</xpath>
    <value>
        <ArmorRating_Blunt>2</ArmorRating_Blunt>
    </value>
</li>
```

使用 `PatchOperationAdd` 在 `statBases` 末尾追加。CE 原补丁删除了 Blunt 但没有补回（因为在 stuffable 模板中 `StuffEffectMultiplierArmor` 隐含覆盖了所有装甲轴），此处需要显式添加。

### 操作 3-5：修正投射物命名空间

```xml
<!-- Op 3: FireBreath damage -->
<xpath>Defs/VFECore.ExpandableProjectileDef[defName="VPE_FireBreath"]/projectile/damageAmountBase</xpath>

<!-- Op 4: FireBreath speed -->
<xpath>Defs/VFECore.ExpandableProjectileDef[defName="VPE_FireBreath"]/projectile/speed</xpath>

<!-- Op 5: IceBreathe speed -->
<xpath>Defs/VFECore.ExpandableProjectileDef[defName="VPE_IceBreathe"]/projectile/speed</xpath>
```

三个操作的唯一区别是 XPath 中使用正确的 `VFECore.ExpandableProjectileDef` 而非 CE 原补丁错误的 `VEF.Weapons.ExpandableProjectileDef`。替换目标值沿用 CE 的原始设计意图。

### 操作 6：骷髅近战属性注入

```xml
<li Class="PatchOperationAdd">
    <xpath>Defs/ThingDef[defName="VPE_SummonedSkeleton"]/statBases</xpath>
    <value>
        <MeleeDodgeChance>0.19</MeleeDodgeChance>
        <MeleeCritChance>0.22</MeleeCritChance>
        <MeleeParryChance>0.09</MeleeParryChance>
        <SmokeSensitivity>0</SmokeSensitivity>
    </value>
</li>
```

向骷髅的 `statBases` 追加 4 个 CE 近战属性。数值与 `VPE_SteelConstruct` / `VPE_RockConstruct` 对齐。

### 操作 7a+7b：BladeFocus 重映射

```xml
<!-- 7a: Replace statFactors -->
<li Class="PatchOperationReplace">
    <xpath>Defs/HediffDef[defName="VPE_BladeFocus"]/stages/li/statFactors</xpath>
    <value>
        <statFactors>
            <MeleeCooldownFactor>0.5</MeleeCooldownFactor>
        </statFactors>
    </value>
</li>

<!-- 7b: Add statOffsets -->
<li Class="PatchOperationAdd">
    <xpath>Defs/HediffDef[defName="VPE_BladeFocus"]/stages/li</xpath>
    <value>
        <statOffsets>
            <MeleeHitChance>5</MeleeHitChance>
            <MeleeCritChance>0.5</MeleeCritChance>
        </statOffsets>
    </value>
</li>
```

分两步操作：7a 用 Replace 将原始 `statFactors`（包含失效的 `VEF_MeleeAttackSpeedFactor`）整体替换为 CE 属性；7b 用 Add 追加 `statOffsets` 节点（原 Hediff 中不存在该节点）。

必须分两步的原因：`PatchOperationReplace` 替换的是目标节点本身，无法同时在同级位置创建新的兄弟节点。`statOffsets` 与 `statFactors` 是 stage 的兄弟元素，需要独立的 Add 操作。

### 操作 8a+8b：ControlledFrenzy 重映射

```xml
<!-- 8a: Replace statFactors -->
<li Class="PatchOperationReplace">
    <xpath>Defs/HediffDef[defName="VPE_ControlledFrenzy"]/stages/li/statFactors</xpath>
    <value>
        <statFactors>
            <MeleeCooldownFactor>0.75</MeleeCooldownFactor>
            <RangedCooldownFactor>0.75</RangedCooldownFactor>
        </statFactors>
    </value>
</li>

<!-- 8b: Add statOffsets -->
<li Class="PatchOperationAdd">
    <xpath>Defs/HediffDef[defName="VPE_ControlledFrenzy"]/stages/li</xpath>
    <value>
        <statOffsets>
            <MeleeHitChance>10</MeleeHitChance>
            <MeleeCritChance>1.0</MeleeCritChance>
            <AimingAccuracy>0.5</AimingAccuracy>
            <ReloadSpeed>1.0</ReloadSpeed>
        </statOffsets>
    </value>
</li>
```

结构与 BladeFocus 相同：Replace 替换 statFactors，Add 追加 statOffsets。ControlledFrenzy 作为 T3 灵能，statOffsets 数量更多（4 项 vs 2 项），反映其更高的功率预算。

---

## 备选方案排除

**斗篷：为什么不添加 stuffCategories 使 StuffEffectMultiplierArmor 生效？**
改变了 mod 的制作设计（固定配方变为材料选择制），超出兼容补丁的职责。直接装甲值是侵入性最小的修复。

**骷髅：为什么不用 Warrior Spirit 的精英数值（0.40/0.60/1.50）？**
骷髅是 60000 tick 一次性召唤物，不是永久精英伴侣。构造体数值匹配其炮灰定位。给炮灰精英属性会扭曲召唤灵能的战术角色。

**BladeFocus：为什么不仅用 MeleeCooldownFactor 单一属性？**
CE 近战系统大量使用暴击/命中判定（`Verb_MeleeAttackCE.cs`）。仅加速冷却使灵能只影响攻击频率，无法与 CE 的暴击/格挡/命中子系统交互。添加 HitChance 和 CritChance 使 buff 的体验更接近"专注聚焦"的灵能幻想。

**ControlledFrenzy：为什么不尝试保留 VEF_RangeAttackDamageFactor 用于灵能投射物？**
依赖 VEF Harmony 补丁的加载顺序——如果 CE 的方法替换先于 VEF 的 Postfix 注册，效果完全丢失。将所有属性重映射到 CE 原生 Stat 确保 buff 对 CE 武器系统稳定生效，不受加载顺序影响。

**投射物命名空间：为什么不用通配符 XPath 避免命名空间依赖？**
RimWorld 的 PatchOperation XPath 不支持通配符匹配 Def 元素名。使用正确的字面标签是唯一可靠的方案。
