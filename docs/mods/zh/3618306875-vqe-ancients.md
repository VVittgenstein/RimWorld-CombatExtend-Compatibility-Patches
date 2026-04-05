# VQE Ancients 兼容性修复技术设计文档

> **目标 mod:** Vanilla Quests Expanded - Ancients (Steam `3618306875`, packageId `VanillaExpanded.VQEA`)
>
> **修复范围:** 2 个基因的 CE 兼容缺口 + 1 个 XML 数据缺陷

---

## 1. CE 破坏了什么

VQE Ancients 添加了一系列 archite 强化 Biotech 基因。在 CE 环境下，大多数内容天然兼容 -- 5 个已有的手动 XML 补丁提供了充分的属性转换，所有生物属性、装备和剧本均不受影响。但两个战斗基因存在 CE 兼容缺口：

### 1.1 VQEA_MasterfulShooting（精准爆头）

该基因通过 Harmony prefix 挂接 `DamageWorker_AddInjury.ChooseHitPart`，强制将所有远程射击的命中部位设为头部。

语料分析报告**错误地**将此基因标记为在 CE 下完全失效。实际的静态代码路径分析表明：CE 弹丸创建 `DamageInfo` 时 `HitPart` 为 null，因此 `ChooseHitPart` 仍然会被调用，原版 prefix 照常生效。然而，这条代码路径的正确性依赖于 CE 的内部实现细节 -- 如果未来 CE 版本引入 transpiler 改写 `ChooseHitPart` 的调用链，原版 prefix 可能静默失效。因此需要一个**保险钩子**确保在护甲计算层面做最终兜底。

### 1.2 VQEA_BlurRunner（远程闪避）

该基因提供 `VEF_RangedDodgeChance +0.25` 属性偏移。这在 CE 下**完全无效** -- CE 没有远程闪避系统，弹丸命中判定基于几何射线投射（`ProjectileCE.TryCollideWith`），`VEF_RangedDodgeChance` 属性从未被读取。

不加以修复，BlurRunner 基因在 CE 下的战斗收益为零，这对于一个高阶 archite 基因来说是不可接受的。

### 1.3 已确认正常工作的基因

以下内容**无需额外修补**：

- **VQEA_Prowess**：通过 `postProcessStatFactor` 提供 3x `MeleeHitChance` 和 3x `MeleeDodgeChance`。在 CE 属性体系下自然生效：
  - 3x `MeleeDodgeChance`：技能 15 时 37% 闪避，技能 20 时 49% 闪避
  - 3x `MeleeHitChance`：任意技能等级下命中率均被钳制到 1.0 上限
- **VQEA_MasterfulMelee**：通过相同的 `ChooseHitPart` prefix 实现近战要害瞄准，与 MasterfulShooting 共用代码路径。近战伤害路径不受 CE 弹丸系统影响，天然正常。

### 1.4 Splicefiend 数据缺陷

`VQEA_Splicefiend` 在 `comps` 中存在重复的 `CompProperties_ArmorDurability` -- 一个 `Durability="500"`，一个 `Durability="1200"`。XML 解析顺序下第一个会遮蔽第二个，导致实际耐久度为 500 而非设计意图的 1200。

---

## 2. 设计问题定框与方案考量

### 2.1 MasterfulShooting：保险钩子 vs 无作为

**问题定框：** 原版 prefix 当前可工作，但依赖 CE 内部实现细节。如何在不破坏现有行为的前提下增强鲁棒性？

**考量的方案：**

| 方案 | 评估 |
|------|------|
| **A. 不做任何修补** | 当前可工作但有隐患。CE 未来版本若用 transpiler 重写 `ChooseHitPart` 调用链，prefix 将静默失效，且不会产生任何错误日志。对于 archite 级别基因的核心能力来说风险不可接受。 |
| **B. 在护甲计算层加保险 prefix**（采用） | 在 `ArmorUtilityCE.GetAfterArmorDamage` 前挂接 prefix，在护甲穿透计算前将 `hitPart` 强制设为头部。即使上游 `ChooseHitPart` prefix 失效，此钩子仍保证爆头语义。开销极低 -- 仅对携带该基因的攻击者触发一次部位查找。 |

### 2.2 BlurRunner：碰撞级闪避 vs 命中率惩罚

**问题定框：** CE 没有远程闪避概念，需要从零实现。BlurRunner 是二值基因（有/无），不像属性那样可连续缩放。如何忠实还原"archite 级别高速闪避"的设计意图？

**考量的方案：**

| 方案 | 评估 |
|------|------|
| **A. 对射手施加命中率惩罚** | 不自洽。BlurRunner 是目标侧基因，而命中率是射手侧属性。将目标基因映射为射手惩罚在语义上倒置了因果关系，且会与其他命中率 modifier 产生不可预期的交互。 |
| **B. 在 `TryCollideWith` 做碰撞级闪避掷骰**（采用） | 在弹丸碰撞检测时对目标做独立闪避判定。语义清晰：弹丸物理上到达了目标位置，但目标以超自然速度闪开。与 CE 的几何弹道系统正交，不污染射手侧属性。 |

**关键数值决策 -- 20% 而非原版 25%：**

原版 `VEF_RangedDodgeChance +0.25` 的 25% 闪避率设计于 hitscan 命中模型下。CE 的弹道扩散已经天然地造成远距离脱靶，因此基础未命中率比原版高。直接移植 25% 会导致有效存活率过高：

- 25 格距离（CE 约 60% 命中率）：25% 闪避 -> 仅 45% 弹丸命中，远超原版设计意图
- 5 格距离（CE 约 95% 命中率）：25% 闪避 -> 71.25% 命中，仍偏低

下调至 **20%** 后的有效数据：

| 距离 | CE 命中率 | BlurRunner 存活率 | 实际命中率 |
|------|-----------|-------------------|-----------|
| 25 格 | ~60% | 80% | ~48% |
| 5 格 | ~95% | 80% | ~76% |

这些数值在 CE 语境下为 archite 级基因提供了有意义但不过分的远程防护。

### 2.3 Prowess 乘数：是否需要在 CE 下降低？

**结论：不需要。**

CE 的属性基线本身低于原版（CE 的 `MeleeDodgeChance` 和 `MeleeHitChance` 基线较低），加上 CE 独有的 bulk/weight 惩罚系统，3x 乘数在 CE 下的绝对效果远低于在原版下的效果。自然摩擦已经充分抑制了数值膨胀。

---

## 3. 具体实现

### 3.1 Mod 检测门控

**文件：** `Source/V2CEPatch/Utility/ModDetection.cs`

```csharp
// 第 18 行
VQEAncientsActive = ModsConfig.IsActive("VanillaExpanded.VQEA");
```

`ModDetection.Init()` 在 `V2CEPatchMod` 静态构造函数中首先调用（`V2CEPatchMod.cs:13`），所有后续钩子注册均依赖此检测结果。

### 3.2 共享钩子注册机制

**文件：** `Source/V2CEPatch/V2CEPatchMod.cs`，第 42-55 行

```csharp
bool needForcedHeadshot = ModDetection.VQEAncientsActive || ModDetection.GreyscytheCybergeneticsActive;
bool needRangedDodge = ModDetection.VQEAncientsActive || ModDetection.GreyscytheCybergeneticsActive;
```

这是整个 VQE Ancients 兼容修复中最关键的架构决策：**MasterfulShooting 保险钩子和 BlurRunner 闪避钩子与 Greyscythe Cybergenetics 共享。**

注册逻辑为 OR 门控 -- 只要 VQE Ancients 或 Greyscythe Cybergenetics 任一 mod 处于激活状态，对应的 Harmony patch 类就会被注册。这意味着：

- 仅加载 VQE Ancients -> 两个钩子注册，服务 VQE 基因
- 仅加载 GS Cybergenetics -> 两个钩子注册，服务 GS hediff
- 两者同时加载 -> 两个钩子注册一次，同时服务两个 mod

钩子内部通过 `ModDetection` 标志位和 Def 查找结果分别判断每个 mod 的逻辑是否应当执行，确保不会对未加载的 mod 产生副作用。

### 3.3 MasterfulShooting 保险钩子

**文件：** `Source/V2CEPatch/Harmony/Patch_ArmorUtilityCE_ForcedHeadshot.cs`

**挂接点：** `ArmorUtilityCE.GetAfterArmorDamage` Harmony Prefix（第 9 行）

**执行流程：**

1. **伤害类型过滤**（第 17 行）：仅处理 `isRanged` 伤害，近战走原版逻辑
2. **攻击者基因检查**（第 19-31 行）：
   - 确认攻击者为 `Pawn` 且具有 `genes` 系统
   - `ModDetection.VQEAncientsActive` 门控（第 22 行）
   - 延迟查找 `GeneDef "VQEA_MasterfulShooting"`（第 24-28 行，惰性初始化避免启动开销）
   - 检查攻击者是否拥有该激活基因（第 31 行）
3. **部位覆写**（第 33-38 行）：在目标身体部位列表中查找头部（支持 `Head`、`Reactor`、`InsectHead` 三种变体以兼容异形体型）
4. **体高一致性**（第 43 行）：将 `BodyPartHeight` 设为 `Top`，确保后续护甲计算和日志输出的一致性

此 prefix **不阻止原方法执行**（无 `__result` 赋值 / 返回 `false`），仅修改传入参数。护甲计算正常进行，只是命中部位被锁定为头部。

### 3.4 BlurRunner 远程闪避

**文件：** `Source/V2CEPatch/Harmony/Patch_ProjectileCE_RangedDodge.cs`

**挂接点：** `ProjectileCE.TryCollideWith` Harmony Prefix（第 15 行，通过 `TargetMethod()` 手动解析）

**执行流程：**

1. **目标类型检查**（第 24-25 行）：仅处理 `Pawn` 类型目标
2. **状态门控**（第 27-31 行）：倒地（`Downed`）、卧床（`InBed()`）、眩晕（`stunner.Stunned`）状态下不触发闪避
3. **Def 惰性查找**（第 33-38 行）：首次执行时查找 `VQEA_BlurRunner` GeneDef 和 `GS_Evade_EvadeProjectileChance` StatDef
4. **Roll 1 -- BlurRunner 闪避**（第 41-50 行）：
   - `ModDetection.VQEAncientsActive` 门控（第 41 行）
   - 检查目标是否拥有 `VQEA_BlurRunner` 激活基因（第 42 行）
   - 固定 **20% 概率**闪避（第 44 行：`Rand.Chance(0.20f)`）
   - 闪避时在目标位置显示青色（`Color(0, 0.8, 1)`）`"CE_Dodge"` 浮动文字（第 46 行）
   - 设置 `__result = false` 并返回 `false`，阻止碰撞判定（第 47-48 行）
5. **Roll 2 -- GS Cybergenetics 闪避**（第 53-61 行）：独立于 BlurRunner 的第二次掷骰，服务于 Greyscythe Cybergenetics 的 `GS_Evade_EvadeProjectileChance` 属性

**两次掷骰的顺序和独立性：** BlurRunner 优先掷骰。若 BlurRunner 闪避成功，则不再进行 GS 掷骰（短路返回）。若 BlurRunner 未触发或未闪避，才进行 GS 掷骰。两者的概率是**乘法叠加**关系：同时拥有两种基因的 pawn，弹丸实际命中概率为 `(1 - 0.20) * (1 - GS闪避率)`。

### 3.5 Splicefiend 耐久度修复

**文件：** `Patches/VQEAncients.xml`，第 11-13 行

```xml
<li Class="PatchOperationRemove">
    <xpath>Defs/ThingDef[defName="VQEA_Splicefiend"]/comps/li[@Class="CombatExtended.CompProperties_ArmorDurability"][Durability="500"]</xpath>
</li>
```

通过 `PatchOperationFindMod` 门控，仅在 VQE Ancients 加载时执行。精确定位 `Durability="500"` 的重复条目并移除，保留设计意图的 `Durability=1200`。

---

## 4. 方案优劣对比与最终决策理由

### 4.1 保险钩子优于无作为

MasterfulShooting 的原版 `ChooseHitPart` prefix 当前确实在 CE 下工作 -- 这是经过静态代码路径分析确认的事实，而非猜测。但"当前可工作"与"未来可靠"是两个不同命题。

保险钩子的成本极低：一个 `isRanged` 检查 + 一次 `HasActiveGene` 查询 + 一次身体部位遍历，仅在携带该基因的攻击者射击时触发。相比之下，如果未来 CE 某个版本通过 transpiler 改变了 `ChooseHitPart` 的调用链，MasterfulShooting 将静默失效 -- 没有错误日志，没有异常，只是爆头不再发生。这种静默回归对于 archite 级基因的核心能力来说不可接受。

保险钩子挂接在 `ArmorUtilityCE.GetAfterArmorDamage` 而非 `ChooseHitPart` 上，与原版 prefix 处于不同的代码层。即使原版 prefix 正常工作，保险钩子的覆写也是幂等的（头部 -> 头部），不会产生冲突。

### 4.2 碰撞级闪避优于命中率惩罚

BlurRunner 是一个**二值基因**：要么拥有，要么没有。将其映射为射手侧的命中率惩罚在概念上就是错误的 -- 射手面对的不是一个"更难瞄准的目标"，而是一个"弹丸到达后闪开的目标"。这个语义区别不仅关乎代码整洁度，还影响与其他 mod 的交互行为：命中率惩罚会被命中率加成抵消，但碰撞级闪避是独立于射手属性的最终裁决。

在 `TryCollideWith` 层面做闪避掷骰与 CE 的几何弹道系统正交：弹道扩散决定弹丸是否飞到目标格子，`TryCollideWith` 决定到达后是否碰撞。BlurRunner 的闪避发生在第二阶段，符合"超自然速度闪避"的设计直觉。

### 4.3 共享钩子架构优于独立注册

将 VQE Ancients 和 Greyscythe Cybergenetics 的远程闪避/强制爆头逻辑合并到同一个 Harmony patch 类中，而非各自注册独立的 prefix，带来三个优势：

1. **确定性执行顺序**：两个 mod 的掷骰在同一个 prefix 方法内按明确顺序执行（BlurRunner 先于 GS），消除了多个独立 prefix 之间的 Harmony 优先级不确定性
2. **避免重复注册**：`TryCollideWith` 和 `GetAfterArmorDamage` 各只被 patch 一次，无论有多少消费者 mod 需要使用
3. **交互行为可审计**：同时拥有两种基因时的乘法叠加关系在代码中一目了然（同一个方法、连续的 if 块），而非散布在两个独立 patch 类中需要推理 Harmony 执行顺序

### 4.4 跨 Mod 叠加的合理性论证

当 VQE Ancients 与 Greyscythe Cybergenetics 同时加载时，存在三种叠加场景：

**MasterfulShooting + GS 远程闪避：** GS 闪避使射击更难命中，但命中后 MasterfulShooting 保证爆头。这在主题上是自洽的 -- 义体闪避 vs archite 精准，两者不矛盾。

**BlurRunner + GS 闪避叠加于同一 pawn：** 乘法叠加。一个同时拥有两种能力的 pawn 需要来自两个不同 mod 池的高价基因投入，这种极端投入理应带来极端回报。以 GS 闪避 15% 为例：实际命中率 = `(1-0.20) * (1-0.15) = 0.68`，即 32% 总闪避率。考虑到所需的基因点数投入，这是合理的。

**Prowess + GS 近战闪避：** 加法叠加于 `MeleeDodgeChance`。技能 15 pawn 同时拥有两者可达约 67.7% 近战闪避。虽然数值较高，但需要的 archite + 义体双重基因投入本身就是游戏内最高等级的资源消耗，高闪避率与之匹配。
