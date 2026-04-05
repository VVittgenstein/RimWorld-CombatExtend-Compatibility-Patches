# Greyscythe Cybergenetics -- CE 兼容性修复技术设计

| 字段 | 值 |
|------|-----|
| 原 mod | Greyscythe Cybergenetics (Steam `3538434109`, PackageId `feaurie.GreyscytheGenes`) |
| 冲突对象 | Combat Extended (CE) |
| 补丁模块 | V2CEPatch |
| 门控检测 | `ModDetection.cs:17` -- `ModsConfig.IsActive("feaurie.GreyscytheGenes")` |

---

## 1. CE 破坏了什么

Greyscythe Cybergenetics 是一个基于 Biotech DLC 的基因 mod,为殖民者提供战斗向的改造基因与能力 hediff。它的战斗机制在原版数值体系下运作正常,但在 CE 环境中产生五类严重问题。

### 1.1 闪避双重/三重门控

GS_Core.dll 中注册了一个 Harmony prefix,挂载在 `Pawn.PreApplyDamage` 上,实现**命中后闪避**(post-hit evasion):弹药/近战攻击已经判定命中后,再额外滚一次闪避骰。

- **远程**:CE 有自己的弹道模型(ballistic miss model),弹丸在飞行路径上已经过物理层面的命中判定。GS 的 `PreApplyDamage` 闪避叠加在 CE 弹道判定之后,形成**双重门控** -- 先过 CE 弹道,再过 GS 闪避,导致远程攻击几乎无法命中闪避型殖民者。
- **近战**:CE 自带 dodge(闪避)+ parry(格挡)系统。GS 的 `PreApplyDamage` 闪避叠加在 CE 的两层判定之后,形成**三重门控** -- dodge、parry、GS evasion 三层串联,使高等级近战改造体实质上刀枪不入。

### 1.2 数值溢出

GS 的数值设定遵循原版尺度,但 CE 使用完全不同的数值范围:

| 属性 | GS 原始值 | CE 环境下的问题 |
|------|----------|----------------|
| `ShootingAccuracyPawn` +10 | 大幅提升射击精度 | CE 的射击精度上限为 4.5,+10 远超上限,等效于无意义的溢出 |
| `MeleeHitChance` +10 | 大幅提升命中 | 在 CE 的 0-1 范围内,+10 = 100% 保证命中,破坏近战平衡 |
| `MeleeDodgeChance` +20 (ChargeBoost) | 高闪避率 | 在 CE 下近乎保证闪避,与 CE 自身的 dodge 系统叠加后严重失衡 |

### 1.3 无效的护甲穿透

GS `OverdriveHediff` 提供了 `MeleeArmorPenetration` 的 statFactor 加成。但 CE 不读取原版的 `MeleeArmorPenetration` stat -- CE 使用自己的 `MeleePenetrationFactor` 体系。该加成在 CE 下完全无效,改造体的近战穿透被静默吞掉。

### 1.4 无效的天然护甲

GS 基因提供的 `ArmorRating_Sharp/Blunt/Heat` offset 在原版下能提供可观防护。但 CE 使用 mm-RHA(等效均质钢装甲毫米数)尺度,原版的小数点级 offset 在 CE 的毫米级体系下几乎可以忽略。更关键的是,人类 pawn 缺少 `CoveredByNaturalArmor` 标签,意味着这些天然护甲值根本不会被 CE 的护甲计算流程读取。

### 1.5 IncomingDamageFactor 过度堆叠

Guardian hediff 提供 `IncomingDamageFactor: 0.3`,意味着受到的伤害降低 70%。在原版下这只是单层减伤。但在 CE 中,该乘数作用于 CE 护甲计算**之前** -- 先乘 0.3 削减原始伤害,再过 CE 的 mm-RHA 护甲判定。多层 hediff 堆叠后 `IncomingDamageFactor` 可低至 0.216x,配合 CE 护甲后达到近乎免疫的坦度。

---

## 2. 设计框定与方案选型

### 2.1 设计原则

修复必须遵循以下约束:

1. **不侵入 GS 源码** -- 仅通过 Harmony unpatch / XML patch / StatPart 注入实现
2. **复用共享基础设施** -- GS Cybergenetics 与 VQE Ancients 存在共享钩子需求,不应重复实现
3. **数值语义保持** -- GS 基因在 CE 下应保留"闪避型"/"重击型"的设计意图,只重新标定数值

### 2.2 考虑过的方案

#### 方案 A:保留 PreApplyDamage 闪避,仅降低概率(已否决)

将 GS 的 `PreApplyDamage` evasion 概率降到低值,与 CE 系统共存。

否决理由:双重/三重门控是**结构性问题**,不是数值问题。无论概率如何调整,命中后闪避的架构语义都与 CE 的弹道/dodge/parry 模型冲突。玩家会感受到"明明子弹打中了但没造成伤害"的割裂体验。

#### 方案 B:通过 AimingAccuracy 惩罚实现远程闪避(已否决)

在射手身上施加 `AimingAccuracy` 负面 stat,间接实现闪避效果。这是早期设计稿中的方案。

否决理由:闪避应该是**被闪避者的个人属性**,不应修改射手的精度。如果降低射手精度来模拟闪避,会产生附带效果 -- 射手对其他目标的精度也会受影响(collateral hits),且可能影响压制(suppression)溢出。概念上不干净。

#### 方案 C:不做数值缩放,仅处理机制冲突(已否决)

只移除双重门控,不调整具体数值。

否决理由:GS 的数值(如 +10 MeleeHitChance)是为原版尺度设计的。在 CE 的不同数值范围下,不缩放等于放任溢出或失效。

#### 方案 D:移除原生闪避,通过 CE 原生通道重新实现(采纳)

1. Unpatch GS 的 `PreApplyDamage` prefix
2. 远程闪避 -> `ProjectileCE.TryCollideWith` prefix(弹丸碰撞前判定闪避)
3. 近战闪避 -> `MeleeDodgeChance` StatPart(注入 CE 原生闪避 stat)
4. 穿透 -> `MeleePenetrationFactor` StatPart(注入 CE 原生穿透 stat)
5. XML patch 重新标定所有溢出数值

该方案在结构上最干净:每个机制走 CE 对应的原生通道,不存在双重门控,数值在 CE 尺度内可控。

### 2.3 共享钩子架构

GS Cybergenetics 与 VQE Ancients 共享两个 Harmony 钩子:

| 钩子 | 文件路径 | 挂载点 |
|------|---------|--------|
| 远程闪避 | `Source/V2CEPatch/Harmony/Patch_ProjectileCE_RangedDodge.cs` | `ProjectileCE.TryCollideWith` prefix |
| 强制爆头 | `Source/V2CEPatch/Harmony/Patch_ArmorUtilityCE_ForcedHeadshot.cs` | `ArmorUtilityCE.GetAfterArmorDamage` prefix |

两个钩子通过 `V2CEPatchMod.cs:42-55` 中的标志位注册:

```csharp
// V2CEPatchMod.cs:42-43
bool needForcedHeadshot = ModDetection.VQEAncientsActive || ModDetection.GreyscytheCybergeneticsActive;
bool needRangedDodge = ModDetection.VQEAncientsActive || ModDetection.GreyscytheCybergeneticsActive;
```

只要 VQE Ancients **或** GS Cybergenetics 任一处于激活状态,对应钩子即注册。两个 mod 的逻辑在同一个 prefix 方法内按顺序执行,互不干扰。

---

## 3. 具体实现

### 3.1 禁用 GS_Evade 原生闪避

**文件**: `Source/V2CEPatch/Harmony/Patch_PreApplyDamage_GSEvadeDisable.cs`

```csharp
// 第 23 行
harmony.Unpatch(preApplyDamage, HarmonyPatchType.Prefix, "feaurie.GS_Core");
```

在 V2CEPatch 启动时(`V2CEPatchMod.cs:34-39`),对 `Pawn.PreApplyDamage` 执行 Harmony unpatch,移除 harmony ID 为 `"feaurie.GS_Core"` 的所有 prefix。这一步彻底移除了 GS 的命中后闪避滚骰,消除双重/三重门控的根源。

移除后,GS 的远程/近战闪避功能通过以下两个 CE 原生通道重新实现。

### 3.2 远程闪避:TryCollideWith 闪避滚骰

**文件**: `Source/V2CEPatch/Harmony/Patch_ProjectileCE_RangedDodge.cs`

**挂载点**: `ProjectileCE.TryCollideWith` prefix (第 15 行)

该 prefix 在 CE 弹丸碰撞判定之前插入闪避滚骰。对同一个目标 pawn,按顺序执行两个独立的闪避判定:

**Roll 1 -- BlurRunner (VQE Ancients)**,第 40-50 行:
- 条件:pawn 拥有激活的 `VQEA_BlurRunner` 基因
- 概率:固定 20%(`Rand.Chance(0.20f)`)
- 成功效果:蓝青色(0, 0.8, 1) `"CE_Dodge"` 浮动文字,`__result = false` 跳过碰撞

**Roll 2 -- GS Cybergenetics 远程闪避**,第 52-62 行:
- 条件:`ModDetection.GreyscytheCybergeneticsActive` 为 true,且 `GS_Evade_EvadeProjectileChance` stat 存在
- 概率:从 pawn 读取 `GS_Evade_EvadeProjectileChance` stat 值(`pawn.GetStatValue(evadeProjectileStat)`,第 55 行)
- 成功效果:青绿色(0, 1, 0.6) `"CE_Dodge"` 浮动文字,`__result = false` 跳过碰撞

```csharp
// 第 53-61 行
if (ModDetection.GreyscytheCybergeneticsActive && evadeProjectileStat != null)
{
    float evadeChance = pawn.GetStatValue(evadeProjectileStat);
    if (evadeChance > 0f && Rand.Chance(evadeChance))
    {
        MoteMaker.ThrowText(pawn.DrawPos, pawn.Map, "CE_Dodge".Translate(), new Color(0f, 1f, 0.6f));
        __result = false;
        return false;
    }
}
```

两个 roll 相互独立:如果 pawn 同时拥有 BlurRunner 基因和 GS 闪避 stat,BlurRunner 先滚(20%),失败后 GS 再滚(stat 值)。两者不互斥,但不叠加 -- 是串联独立判定。

前置过滤(第 27-31 行):倒地(`Downed`)、卧床(`InBed`)、眩晕(`Stunned`)的 pawn 跳过所有闪避判定。

### 3.3 近战闪避:MeleeDodgeChance StatPart

**文件**: `Source/V2CEPatch/StatParts/StatPart_CyberneticMeleeDodge.cs`

**注入点**: `V2CEPatchMod.cs:62-68` -- 在启动时将 `StatPart_CyberneticMeleeDodge` 实例添加到 `MeleeDodgeChance.parts` 列表。

```csharp
// V2CEPatchMod.cs:62-68
var meleeDodgeStat = DefDatabase<StatDef>.GetNamedSilentFail("MeleeDodgeChance");
if (meleeDodgeStat != null)
{
    meleeDodgeStat.parts ??= new List<StatPart>();
    meleeDodgeStat.parts.Add(new StatPart_CyberneticMeleeDodge());
}
```

**核心逻辑**:

| 参数 | 值 | 来源 |
|------|-----|------|
| 源 stat | `GS_Evade_EvadeMeleeChance` | GS 定义的近战闪避概率 |
| 缩放因子 | **0.5x** | `ScaleFactor = 0.5f`(第 8 行) |
| 输出 | 加算至 `MeleeDodgeChance` | CE 原生近战闪避 stat |

```csharp
// 第 42-43 行
float rawEvade = pawn.GetStatValue(evadeMeleeStat);
return rawEvade * ScaleFactor;
```

**数值示例**:一个 pawn 的 `GS_Evade_EvadeMeleeChance` 原始值为 0.20(20% 闪避),经过 0.5x 缩放后,获得 +0.10(10%)的 `MeleeDodgeChance` 加成。该加成走 CE 原生的近战闪避通道,与 CE 的 dodge 系统正确整合,不再产生额外的门控层。

### 3.4 近战穿透:MeleePenetrationFactor StatPart

**文件**: `Source/V2CEPatch/StatParts/StatPart_CyberneticMeleePen.cs`

**注入点**: `V2CEPatchMod.cs:70-76` -- 将 `StatPart_CyberneticMeleePen` 实例添加到 `MeleePenetrationFactor.parts` 列表。

```csharp
// V2CEPatchMod.cs:70-76
var meleePenStat = DefDatabase<StatDef>.GetNamedSilentFail("MeleePenetrationFactor");
if (meleePenStat != null)
{
    meleePenStat.parts ??= new List<StatPart>();
    meleePenStat.parts.Add(new StatPart_CyberneticMeleePen());
}
```

**触发条件**(第 53-57 行):pawn 拥有 `GG_M_Heavy` 基因**或**拥有 `GS_OverdriveHediff` hediff。两者提供相同倍率,不叠加(非乘法堆叠)。

| 参数 | 值 | 来源 |
|------|-----|------|
| 基因条件 | `GG_M_Heavy` | 重型改造基因 |
| hediff 条件 | `GS_OverdriveHediff` | 超频能力 hediff |
| 倍率 | **1.35x** | `Multiplier = 1.35f`(第 9 行) |
| 输出 | 乘算至 `MeleePenetrationFactor` | CE 原生近战穿透 stat |

```csharp
// 第 53-57 行
if (heavyGene != null && pawn.genes?.HasActiveGene(heavyGene) == true)
    return true;
if (overdriveDef != null && pawn.health?.hediffSet?.HasHediff(overdriveDef) == true)
    return true;
```

该 StatPart 替代了 GS 原来的 `MeleeArmorPenetration` statFactor(已通过 XML patch 移除),使穿透加成走 CE 能实际读取的 `MeleePenetrationFactor` 通道。

### 3.5 XML 数值重标定

**文件**: `Patches/GreyscytheCybergenetics.xml`

所有操作由 `PatchOperationFindMod` 双重门控:需要 **Combat Extended** 和 **Greyscythe Cybergenetics** 同时激活(第 5-9 行)。

#### 数值调整总表

| 目标 Def | 属性 | 原值 | 修正值 | XML 行号 | 修正原因 |
|----------|------|------|--------|----------|----------|
| `GG_Cyborg` (GeneDef) | `MeleeHitChance` | +10 | **+0.10** | 17-22 | 原版 +10 在 CE 的 0-1 范围内溢出 |
| `GS_OverdriveHediff` | `ShootingAccuracyPawn` | +10 | **+1.0** | 27-32 | CE 射击精度上限 4.5,+10 无意义 |
| `GS_OverdriveHediff` | `MeleeHitChance` | +10 | **+0.15** | 37-42 | 同 GG_Cyborg,按 CE 尺度重标定 |
| `GS_OverdriveHediff` | `MeleeDamageFactor` | 1.75x | **1.50x** | 47-52 | 配合 CE 护甲体系适度削弱 |
| `GS_OverdriveHediff` | `MeleeArmorPenetration` | 1.5x | **移除** | 59-61 | CE 不读取该 stat,由 C# StatPart 替代 |
| `GS_ChargeBoostHediff` | `MeleeDodgeChance` | +20 | **移除** | 69-71 | 由 StatPart_CyberneticMeleeDodge 统一处理,避免双重计算 |
| `GS_GuardianHediff` | `IncomingDamageFactor` | 0.3 | **0.55** | 77-82 | 防止在 CE 护甲堆叠下达到免疫级减伤 |

#### Guardian IncomingDamageFactor 详解

`GS_GuardianHediff` 的 `IncomingDamageFactor` 从 0.3 调整至 **0.55** 是本补丁中最关键的单项数值修正。

原值 0.3 意味着所有来源伤害降低 70%。在原版护甲体系下尚可接受,但在 CE 中,`IncomingDamageFactor` 作用于 CE mm-RHA 护甲判定**之前**:

```
最终伤害 = 原始伤害 x IncomingDamageFactor x (CE护甲穿透结果)
```

多个 GS hediff 可堆叠 `IncomingDamageFactor`,实测可达 0.216x(即伤害削减 ~78%),再经过 CE 护甲后,绝大多数攻击造成 0 伤害。修正至 0.55(削减 45%)后,配合 CE 护甲仍然提供显著坦度,但不再达到免疫级别。

### 3.6 StatPart 配置 Def

**文件**: `Defs/GreyscytheCybergenetics/StatParts_Cybernetic.xml`

通过 `LoadFolders.xml` 第 10 行的条件加载控制:

```xml
<!-- LoadFolders.xml:10 -->
<li IfModActive="feaurie.GreyscytheGenes">Defs/GreyscytheCybergenetics</li>
```

仅当 `feaurie.GreyscytheGenes` 激活时才加载该 Defs 目录。

定义了两个 `StatPartConfigDef`:

**V2CE_CyberneticMeleeDodge**:

```xml
<V2CEPatch.StatPartConfigDef>
    <defName>V2CE_CyberneticMeleeDodge</defName>
    <targetStat>MeleeDodgeChance</targetStat>
    <sourceStat>GS_Evade_EvadeMeleeChance</sourceStat>
    <scaleFactor>0.5</scaleFactor>
</V2CEPatch.StatPartConfigDef>
```

**V2CE_CyberneticMeleePen**:

```xml
<V2CEPatch.StatPartConfigDef>
    <defName>V2CE_CyberneticMeleePen</defName>
    <targetStat>MeleePenetrationFactor</targetStat>
    <geneDefName>GG_M_Heavy</geneDefName>
    <hediffDefName>GS_OverdriveHediff</hediffDefName>
    <multiplier>1.35</multiplier>
</V2CEPatch.StatPartConfigDef>
```

`StatPartConfigDef` 类定义于 `Source/V2CEPatch/Utility/StatPartConfigDef.cs`,是一个轻量的 `Def` 子类,提供 `targetStat`、`sourceStat`、`scaleFactor`、`geneDefName`、`hediffDefName`、`multiplier` 六个字段。

---

## 4. 为什么采纳方案优于替代方案

### 4.1 结构正确性:消除门控叠加

保留 `PreApplyDamage` 闪避(方案 A)在架构上就是错误的。CE 的弹道模型已经处理了"弹丸是否命中"的物理判定,在命中之后再滚一次闪避是语义重复。无论概率如何微调,都无法消除"子弹命中了但被闪避"这一违反直觉的体验。

采纳方案将远程闪避移至 `TryCollideWith`(弹丸碰撞前),近战闪避移至 `MeleeDodgeChance`(CE 原生 stat),每个机制恰好经过一层判定,没有冗余门控。

### 4.2 属性归属正确:闪避是被动方的能力

AimingAccuracy 方案(方案 B)将闪避建模为射手精度的降低。这在逻辑上是错误的 -- 闪避应归属于被闪避的 pawn,而非射击的 pawn。`TryCollideWith` prefix 在弹丸即将与特定目标碰撞时才判定,闪避效果仅作用于该目标,不会产生对其他目标的精度溢出或压制系统的副作用。

### 4.3 共享钩子的工程效率

`Patch_ProjectileCE_RangedDodge` 和 `Patch_ArmorUtilityCE_ForcedHeadshot` 两个钩子同时服务于 VQE Ancients 和 GS Cybergenetics。通过 `needForcedHeadshot` / `needRangedDodge` 标志位(`V2CEPatchMod.cs:42-43`),两个 mod 的钩子需求被合并为单次注册:

- 任一 mod 激活 -> 钩子注册
- prefix 方法内部按 mod 检测分支执行各自逻辑
- 两个 mod 同时激活时,所有逻辑自然共存(BlurRunner 先滚,GS 后滚)

这避免了重复注册相同 prefix 导致的 Harmony 冲突,也避免了维护两套几乎相同的钩子代码。

### 4.4 数值通道的 CE 原生性

方案 D 的每项修改都走 CE 实际读取的 stat 通道:

| GS 原始通道 | CE 是否读取 | V2CEPatch 替代通道 | CE 是否读取 |
|------------|-----------|------------------|-----------|
| `MeleeArmorPenetration` statFactor | 否 | `MeleePenetrationFactor` StatPart | 是 |
| `PreApplyDamage` evasion roll | -- (旁路) | `MeleeDodgeChance` StatPart | 是 |
| `PreApplyDamage` evasion roll | -- (旁路) | `TryCollideWith` prefix | 是 (弹道层) |
| `ShootingAccuracyPawn` +10 | 溢出 | +1.0 (XML patch) | 有效 |
| `MeleeHitChance` +10 | 溢出 | +0.10 / +0.15 (XML patch) | 有效 |

不存在 CE 无法读取的"死 stat",所有修改对游戏机制产生实际效果。

---

## 附录:启动流程与文件索引

### 启动序列

```
V2CEPatchMod 静态构造函数
  |
  +-- ModDetection.Init()                           [ModDetection.cs:13]
  |     读取 GreyscytheCybergeneticsActive            [ModDetection.cs:17]
  |
  +-- if GreyscytheCybergeneticsActive:              [V2CEPatchMod.cs:34]
  |     +-- Patch_PreApplyDamage_GSEvadeDisable       [V2CEPatchMod.cs:36]
  |     |     unpatch "feaurie.GS_Core" prefix        [..GSEvadeDisable.cs:23]
  |     +-- InjectCyberneticStatParts()               [V2CEPatchMod.cs:37]
  |           +-- MeleeDodgeChance.parts.Add(...)     [V2CEPatchMod.cs:66]
  |           +-- MeleePenetrationFactor.parts.Add(...) [V2CEPatchMod.cs:74]
  |
  +-- needRangedDodge? (VQE || GS)                   [V2CEPatchMod.cs:43]
  |     +-- Patch_ProjectileCE_RangedDodge            [V2CEPatchMod.cs:53]
  |
  +-- needForcedHeadshot? (VQE || GS)                [V2CEPatchMod.cs:42]
        +-- Patch_ArmorUtilityCE_ForcedHeadshot       [V2CEPatchMod.cs:47]
```

### 文件索引

| 文件 | 路径 | 用途 |
|------|------|------|
| 入口 | `Source/V2CEPatch/V2CEPatchMod.cs` | 启动注册、StatPart 注入 |
| Mod 检测 | `Source/V2CEPatch/Utility/ModDetection.cs` | PackageId 检测 |
| 禁用原生闪避 | `Source/V2CEPatch/Harmony/Patch_PreApplyDamage_GSEvadeDisable.cs` | unpatch GS_Core |
| 远程闪避 | `Source/V2CEPatch/Harmony/Patch_ProjectileCE_RangedDodge.cs` | TryCollideWith prefix (共享) |
| 强制爆头 | `Source/V2CEPatch/Harmony/Patch_ArmorUtilityCE_ForcedHeadshot.cs` | GetAfterArmorDamage prefix (共享) |
| 近战闪避 | `Source/V2CEPatch/StatParts/StatPart_CyberneticMeleeDodge.cs` | MeleeDodgeChance StatPart |
| 近战穿透 | `Source/V2CEPatch/StatParts/StatPart_CyberneticMeleePen.cs` | MeleePenetrationFactor StatPart |
| 配置 Def 类 | `Source/V2CEPatch/Utility/StatPartConfigDef.cs` | StatPartConfigDef 定义 |
| 数值 patch | `Patches/GreyscytheCybergenetics.xml` | XML 数值重标定 |
| StatPart Defs | `Defs/GreyscytheCybergenetics/StatParts_Cybernetic.xml` | StatPart 配置声明 |
| 条件加载 | `LoadFolders.xml` | Defs 目录条件加载 |
