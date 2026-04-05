# Greyscythe Bionics Catalogue CE 兼容修复 -- 技术设计文档

> **Steam Workshop ID**: 3538434454
> **Mod PackageId**: `feaurie.GreyscytheBionics`
> **Mod 类型**: 纯 XML, 依赖 Greyscythe Cybergenetics (GS_Core DLL)
> **补丁方式**: 0 Harmony hooks / 31 XML PatchOperations / 8 新 CE 弹丸 ThingDef
> **补丁文件**: `Patches/GreyscytheBionicsCatalogue.xml`, `Defs/GreyscytheBionicsCatalogue/Projectiles_Bionics.xml`

---

## 1. CE 破坏了什么

Greyscythe Bionics Catalogue 为游戏添加了大量仿生体 hediff, 涵盖从新石器到超级科技的完整科技树。这些仿生体通过 `HediffCompProperties_VerbGiver` 嵌入近战工具和远程 verb, 安装 Combat Extended 后产生两大类致命兼容性故障。

### 1.1 近战工具穿甲值归零 (16 个工具, 13 个 hediff)

所有仿生体近战工具使用原版 `Tool` 类。CE 的近战系统在运行时将工具强制转型 `as ToolCE`, 若转型失败则返回 `null`, 导致所有穿甲计算参数为 0。

具体表现: 仿生体近战攻击**能够命中**目标 (命中判定在 `ToolCE` 转型之前完成), 但穿甲值 (AP) 恒为 0, 无法穿透任何护甲。对穿着任意 CE 护甲的目标造成的实际伤害几乎为零。

受影响 hediff 覆盖全部科技层级: Clubfist、Ikwafist、Spikecrown (新石器); Macefist、Swordhand、Axehand、Handbow、Battlearm、Battleleg、Beastjaw (中世纪); Pilebunkerarm、Chainsawarm、Miningarm、Mediarm、Culiarm、Builderarm、Autoloaderarm (工业); Miningarm_spacer (太空)。

### 1.2 远程武器完全失效 (10 个 verb, 8 个 hediff)

所有仿生体远程武器使用原版 `Verb_Shoot` 发射原版 `Projectile` 子类弹丸。CE 以 `Verb_ShootCE` / `ProjectileCE` 完整替换了射击和弹道管线。原版弹丸**完全不进入** CE 的弹道系统 -- 不参与 CE 穿甲计算, 不执行弹道抛物线模拟, 不与 CE 护甲交互。

具体表现: 远程仿生体武器发射的弹丸在 CE 环境下行为不可预测, 实际上等同于完全无法造成有效伤害。

受影响 hediff: Handbow (中世纪); Handcannon、Machinegunarm、Launcherarm (工业); Handcannon_transform、Machinegunarm_transform、Launcherarm_transform (太空); Shouldergun_ultra、Hipgun_ultra (超级, 通过 MVCF)。

### 1.3 次要问题

- **Hexaeye 的 `MeleeArmorPenetration` statFactor (1.2x)**: CE 不读取此 stat, 该加成在 CE 环境下静默失效。不做修复 -- 该问题仅影响数值微调 (20% 穿甲加成), 修复需要 Harmony hook, 成本不成比例。
- **护甲植入物的 `ArmorRating_Sharp/Blunt/Heat` stat offset**: 在 CE 的 mm-RHA 护甲体系下, 这些原版 stat offset 仅作为显示值, 不提供实际防护。本补丁通过 `damageFactors` 提供真实的 CE 伤害减免 (详见第 3 节)。

### 1.4 为什么现有机制无法覆盖

- **CE 手动补丁**: 社区现有零个针对 Greyscythe Bionics Catalogue 的 CE 兼容补丁。
- **CE GunAutoPatcher 不可达**: CE 的自动武器补丁器 (`GunAutoPatcher`) 仅处理 `ThingDef` 层级的武器。仿生体武器嵌入在 `HediffDef` 的 `HediffCompProperties_VerbGiver` 中, 对 `GunAutoPatcher` 完全不可见。
- **无需 Harmony**: 本 mod 是纯 XML 定义, 所有问题均可通过 `PatchOperationReplace` / `PatchOperationAdd` 和新增 Def 解决, 不需要任何 C# 层介入。

---

## 2. 设计问题定框与方案对比

### 2.1 问题定框

需要修复两个独立故障类别:

1. **近战**: 将所有 `Tool` 转换为 `CombatExtended.ToolCE`, 赋予正确的 `armorPenetrationSharp` / `armorPenetrationBlunt` 值。
2. **远程**: 将所有 `Verb_Shoot` 替换为 `CombatExtended.Verb_ShootCE`, 将原版弹丸替换为新的 CE 弹丸 (`ProjectilePropertiesCE`)。

附加目标:

3. **护甲植入物**: 为已有护甲 hediff 添加 `damageFactors`, 在 CE 环境下提供真实的伤害减免。

设计约束:

- 纯 XML + Def 解决方案, 零 C# 代码
- 远程仿生体武器不消耗弹药 -- 这些是身体集成武器, 由身体供能而非外部弹药
- 数值必须按科技层级合理递进
- 保留原始伤害类型 (Blunt、Stab、Cut、Bite) 不改变

### 2.2 考虑过的方案

#### 方案 A: 让远程 hediff 武器使用 CE 现有弹药弹丸 (已否决)

复用 CE 已有的弹药弹丸 (如 `Bullet_556x45NATO_FMJ`), 并通过 `CompAmmoUser` 给仿生体武器绑定弹药集。

**否决原因**:

- **语义错误**: 身体集成武器由仿生体本身供能, 不消耗外部弹药。让仿生手炮"消耗 .45 ACP 弹药"在游戏性上完全不合理 -- 棋子不会"给自己的手臂装弹"。
- **数值不匹配**: CE 通用弹药弹丸的伤害/穿甲值是为对应口径的独立武器设计的。仿生体武器各有独特的伤害档位和科技定位, 强制套用通用弹药会丢失科技层级间的数值递进。
- **AmmoSet 依赖**: 绑定 `CompAmmoUser` 后, 仿生体远程武器将显示弹药计数 UI, 需要"装填"操作, 与身体集成武器的设计预期完全矛盾。

#### 方案 B: 仅对远程武器做 stat patch, 保留原版弹丸 (已否决)

只修改 verb 的 stat 属性 (射速、精度等), 但保留 `Verb_Shoot` 和原版弹丸不变。

**否决原因**: 技术上完全不可行。原版弹丸不进入 CE 弹道管线 -- 不执行 `ProjectileCE` 的弹道计算, 不参与 CE 护甲穿透, 不受 CE 弹道参数 (弹速、穿甲值、抛物线) 控制。保留原版弹丸意味着远程仿生体武器在 CE 环境下仍然完全无效。

#### 方案 C: 使用 C# HediffComp 进行运行时转换 (已否决)

编写自定义 `HediffComp`, 在仿生体安装时通过 C# 代码动态替换 verb 和 tool。

**否决原因**: 不必要的复杂度。Greyscythe Bionics Catalogue 是纯 XML mod, 所有 hediff 定义均可通过 XPath patch 精确定位和替换。引入 C# 层意味着额外的编译依赖、版本维护成本, 以及对 CE 和 GS_Core 程序集的硬引用风险。纯 XML patch 能达到完全相同的结果, 且维护成本远低于 C# 方案。

### 2.3 最终方案

采用纯 XML + Def 方案:

1. **`Patches/GreyscytheBionicsCatalogue.xml`** -- 31 个 `PatchOperation`, 包含近战 `ToolCE` 转换 (Section 1)、远程 `Verb_ShootCE` 转换 (Section 2)、护甲 `damageFactors` 补充 (Section 3)。
2. **`Defs/GreyscytheBionicsCatalogue/Projectiles_Bionics.xml`** -- 8 个新 CE 弹丸 `ThingDef`, 供远程仿生体武器使用。所有弹丸均不绑定 AmmoSet。

条件加载门控:

- Patch XML 由 `PatchOperationFindMod` 门控, 仅在 `Greyscythe Bionics Catalogue` 激活时执行
- Def 文件夹由 `LoadFolders.xml` 第 9 行 `IfModActive="feaurie.GreyscytheBionics"` 条件加载

---

## 3. 具体实现

### 3.1 条件加载门控

**`LoadFolders.xml` 第 9 行**:

```xml
<li IfModActive="feaurie.GreyscytheBionics">Defs/GreyscytheBionicsCatalogue</li>
```

**`Patches/GreyscytheBionicsCatalogue.xml` 第 7-11 行**:

```xml
<Operation Class="PatchOperationFindMod">
    <mods>
        <li>Greyscythe Bionics Catalogue</li>
    </mods>
    <match Class="PatchOperationSequence">
```

全部 31 个 patch operation 包裹在 `PatchOperationSequence` 内, 任意一个失败将中止序列并输出 XML patch 错误日志, 便于排查目标 mod 更新导致的 defName/结构变更。

### 3.2 Section 1: 近战 Tool -> ToolCE 转换

所有近战转换使用 `PatchOperationReplace`, xpath 精确定位到 `HediffCompProperties_VerbGiver/tools` 节点, 整体替换 `<tools>` 内容。

每个 `ToolCE` 保留原始工具的标签 (`label`)、伤害类型 (`capacities`) 和冷却时间 (`cooldownTime`), 新增 CE 穿甲字段。

#### 3.2.1 新石器时代 (Neolithic)

Patch 文件第 21-71 行。

| hediff defName | 工具标签 | 伤害类型 | 攻击力 | 冷却 (秒) | AP_Sharp | AP_Blunt |
|:---|:---|:---|---:|---:|---:|---:|
| `GS_Hediff_Clubfist` | club | Blunt | 16 | 2.0 | -- | 0.5 |
| `GS_Hediff_Ikwafist` | point | Stab | 16 | 2.0 | 0.2 | 0.4 |
| `GS_Hediff_spikecrown` | spikecrown | Stab | 15 | 2.0 | 0.15 | 0.3 |

新石器层级穿甲值较低, 与原版 CE 新石器武器 (木棒 AP_B ~0.4, 短矛 AP_S ~0.15) 对齐。Clubfist 作为纯钝击武器仅设 `armorPenetrationBlunt`; Ikwafist 和 Spikecrown 为穿刺类, 兼有少量钝击穿甲。

#### 3.2.2 中世纪 (Medieval)

Patch 文件第 76-201 行。

| hediff defName | 工具标签 | 伤害类型 | 攻击力 | 冷却 (秒) | AP_Sharp | AP_Blunt | 备注 |
|:---|:---|:---|---:|---:|---:|---:|:---|
| `GS_Hediff_macefist` | club | Blunt | 18 | 2.0 | -- | 0.8 | |
| `GS_Hediff_swordhand` | point | Stab | 18 | 2.0 | 0.5 | 0.6 | 双工具: 刺 + 斩 |
| | edge | Cut | 18 | 2.0 | 0.4 | 0.5 | |
| `GS_Hediff_axehand` | head | Cut | 18 | 2.0 | 0.35 | 0.7 | |
| `GS_hediff_handbow` | fist | Blunt | 8.2 | 2.0 | -- | 0.4 | 远程 hediff 上的近战后备 |
| `GS_Hediff_battlearm` | spike | Stab | 14 | 1.4 | 0.3 | 0.5 | 快速攻击 (1.4s 冷却) |
| `GS_Hediff_battleleg` | spike | Stab | 14 | 1.4 | 0.3 | 0.5 | 快速攻击 (1.4s 冷却) |
| `GS_Hediff_beastjaw` | fangs | Bite | 18 | 2.0 | 0.4 | 0.6 | |

Swordhand 是唯一的双工具 hediff (point + edge), 保留了原 mod 的刺斩双攻击设计。Battlearm/Battleleg 使用 1.4 秒冷却, 反映其"快速轻刺"的定位, 与其他 2.0 秒标准冷却形成对比。

#### 3.2.3 工业时代: 纯近战 hediff (Industrial melee)

Patch 文件第 206-336 行。

| hediff defName | 工具标签 | 伤害类型 | 攻击力 | 冷却 (秒) | AP_Sharp | AP_Blunt | 备注 |
|:---|:---|:---|---:|---:|---:|---:|:---|
| `GS_Hediff_pilebunkerarm` | limb | Blunt | 10 | 2.0 | -- | 0.8 | 双工具: 肢体 + 桩钉 |
| | stake | Stab | 34 | 4.5 | 2.0 | 3.5 | 高伤害长冷却重击 |
| `GS_Hediff_chainsawarm` | limb | Blunt | 10 | 2.0 | -- | 0.8 | 双工具: 肢体 + 链锯 |
| | chainsaw | Cut | 28 | 2.8 | 1.2 | 2.5 | |
| `GS_hediff_miningarm` | pick | Stab | 18 | 2.0 | 0.8 | 1.2 | |
| `GS_hediff_mediarm` | scalpel | Stab | 9 | 2.0 | 0.2 | 0.3 | 低伤害医疗工具 |
| `GS_hediff_culiarm` | slicer | Cut | 14 | 2.0 | 0.3 | 0.45 | |
| `GS_hediff_builderarm` | hammer | Blunt | 18 | 2.5 | -- | 0.8 | |
| `GS_hediff_autoloaderarm` | fist | Blunt | 8.2 | 2.0 | -- | 0.5 | |

Pilebunkerarm 的桩钉攻击 (stake) 是本补丁中穿甲值最高的近战工具: AP_Sharp 2.0 / AP_Blunt 3.5, 代价是 4.5 秒的超长冷却。这与打桩机的设计语义一致 -- 单次极高穿透力的重击。

Chainsawarm 的链锯攻击 AP_Sharp 1.2 反映持续切割的穿甲特性, 攻击力 28 是中世纪层级的 1.5 倍以上, 体现工业科技优势。

#### 3.2.4 工业时代: 远程 hediff 上的近战后备 (Industrial ranged melee)

Patch 文件第 340-385 行。

| hediff defName | 工具标签 | 伤害类型 | 攻击力 | 冷却 (秒) | AP_Sharp | AP_Blunt |
|:---|:---|:---|---:|---:|---:|---:|
| `GS_hediff_handcannon` | barrel | Blunt | 10 | 2.0 | -- | 0.8 |
| `GS_hediff_machinegunarm` | fist | Blunt | 8.2 | 2.0 | -- | 0.5 |
| `GS_hediff_launcherarm` | barrel | Blunt | 10 | 2.0 | -- | 0.8 |

远程 hediff 的近战后备工具均为纯钝击, 攻击力偏低 -- 设计意图是"不得已的近身防御", 而非主力近战手段。

#### 3.2.5 太空时代 (Spacer)

Patch 文件第 389-504 行。

| hediff defName | 工具标签 | 伤害类型 | 攻击力 | 冷却 (秒) | AP_Sharp | AP_Blunt | 备注 |
|:---|:---|:---|---:|---:|---:|---:|:---|
| `GS_hediff_miningarm_spacer` | drill | Stab | 24 | 2.0 | 1.2 | 2.0 | 工业采矿臂的太空升级 |
| `GS_hediff_mediarm_spacer` | scalpel | Stab | 11 | 2.0 | 0.3 | 0.5 | |
| `GS_hediff_builderarm_spacer` | hammer | Blunt | 18 | 2.0 | -- | 1.5 | |
| `GS_Hediff_beamblade` | point | Stab | 22 | 2.0 | 1.5 | 1.8 | 光束刃, 高穿甲 |
| `GS_hediff_handcannon_transform` | barrel | Blunt | 10 | 2.0 | -- | 1.0 | 变形远程近战后备 |
| `GS_hediff_machinegunarm_transform` | fist | Blunt | 8.2 | 2.0 | -- | 0.8 | 变形远程近战后备 |
| `GS_hediff_launcherarm_transform` | barrel | Blunt | 10 | 2.0 | -- | 1.0 | 变形远程近战后备 |

太空时代的采矿臂 AP_Sharp 从工业层的 0.8 提升至 1.2, 体现科技升级。Beamblade 作为太空近战专精武器, AP_Sharp 1.5 / AP_Blunt 1.8 定位高于工业层链锯但低于打桩机的极端穿甲。太空远程 hediff 的近战后备相比工业层有微幅提升 (AP_Blunt 0.8/1.0 vs 0.5/0.8)。

#### 3.2.6 超级时代 (Ultra)

Patch 文件第 508-524 行。

| hediff defName | 工具标签 | 伤害类型 | 攻击力 | 冷却 (秒) | AP_Sharp | AP_Blunt |
|:---|:---|:---|---:|---:|---:|---:|
| `GS_hediff_ripjaw` | ripjaw | Bite | 28 | 2.0 | 1.8 | 3.0 |

Ripjaw 作为超级科技的终极咬合武器, 攻击力 28 / AP_Sharp 1.8 / AP_Blunt 3.0 处于本补丁近战工具的顶端。

### 3.3 Section 2: 远程 Verb_Shoot -> Verb_ShootCE 转换

所有远程转换使用 `PatchOperationReplace`, xpath 精确定位到 `HediffCompProperties_VerbGiver/verbs` (标准 hediff) 或 `MVCF.Comps.HediffCompProperties_ExtendedVerbGiver/verbs` (MVCF 超级炮塔) 节点。

每个 verb 替换包含:
- `verbClass` 从 `Verb_Shoot` 改为 `CombatExtended.Verb_ShootCE`
- `VerbPropertiesCE` 替换原版 `VerbProperties`
- `defaultProjectile` 指向新建的 CE 弹丸 def
- `ejectsCasings` 设为 `false` (身体集成武器不抛壳)
- 不添加 `CompAmmoUser` (身体集成武器不消耗弹药)

#### 中世纪: Handbow (第 534-550 行)

```xml
<verbClass>CombatExtended.Verb_ShootCE</verbClass>
<defaultProjectile>GS_Handbow_Bolt_CE</defaultProjectile>
<warmupTime>3</warmupTime>
<range>18</range>
<burstShotCount>1</burstShotCount>
<soundCast>Bow_Recurve</soundCast>
```

单发慢射弓, 保留了原版的弓弦音效。

#### 工业: Handcannon (第 558-571 行)

```xml
<defaultProjectile>GS_Handcannon_Bullet_CE</defaultProjectile>
<warmupTime>2.6</warmupTime>
<range>18</range>
<ticksBetweenBurstShots>35</ticksBetweenBurstShots>
<burstShotCount>6</burstShotCount>
```

6 发连射, 射间隔 35 tick (~0.58 秒), 模拟左轮式手炮的逐发点射。

#### 工业: Machinegunarm (第 582-595 行)

```xml
<defaultProjectile>GS_MachinegunArm_Bullet_CE</defaultProjectile>
<warmupTime>3.2</warmupTime>
<range>25.9</range>
<ticksBetweenBurstShots>7</ticksBetweenBurstShots>
<burstShotCount>6</burstShotCount>
```

6 发连射, 射间隔 7 tick (~0.12 秒), 典型的机枪高射速模式。

#### 工业: Launcherarm (第 601-619 行)

```xml
<defaultProjectile>GS_LauncherArm_Grenade_CE</defaultProjectile>
<warmupTime>4</warmupTime>
<range>25</range>
<forcedMissRadius>1.9</forcedMissRadius>
<ticksBetweenBurstShots>60</ticksBetweenBurstShots>
<burstShotCount>3</burstShotCount>
```

3 发连射榴弹, 射间隔 60 tick (1 秒), `forcedMissRadius` 1.9 模拟榴弹发射器的散布。

#### 太空: Transform 变体 (第 628-690 行)

| 武器 | 弹丸 | 射程 | 连射数 | 射间隔 (tick) | 与工业版对比 |
|:---|:---|---:|---:|---:|:---|
| Handcannon_transform | `GS_Handcannon_Spacer_Bullet_CE` | 18 | 6 | 35 | 弹丸穿甲翻倍 (AP_S 4->8) |
| Machinegunarm_transform | `GS_MachinegunArm_Transform_Bullet_CE` | 32 | 6 | 4 | 射程 +6, 射速更快, 穿甲翻倍 |
| Launcherarm_transform | `GS_LauncherArm_Transform_Grenade_CE` | 32 | 3 | 20 | 射程 +7, 射速 3x, 爆伤 +5 |

太空变体在保持相同 verb 结构的基础上, 通过更强的弹丸定义和微调的 verb 参数体现科技升级。

#### 超级: Bioturret (第 694-738 行)

```xml
<xpath>Defs/HediffDef[defName="GS_Hediff_shouldergun_ultra"]/comps/li[@Class="MVCF.Comps.HediffCompProperties_ExtendedVerbGiver"]/verbs</xpath>
```

肩部炮塔和髋部炮塔共用 `GS_Bioturret_Bullet_CE` 弹丸, 射程 28.9, 3 发连射 (射间隔 6 tick)。注意 xpath 目标为 `MVCF.Comps.HediffCompProperties_ExtendedVerbGiver` 而非标准 `HediffCompProperties_VerbGiver` -- 这些炮塔通过 MVCF (Multi Verb Combat Framework) 实现独立于主武器的自动开火。

### 3.4 CE 弹丸定义

**文件**: `Defs/GreyscytheBionicsCatalogue/Projectiles_Bionics.xml`

全部 8 个弹丸继承自 `BaseBulletCE`, 使用 `CombatExtended.ProjectilePropertiesCE`。爆炸型弹丸额外指定 `thingClass` 为 `CombatExtended.ProjectileCE_Explosive`。

| defName | damageDef | 伤害 | AP_Sharp | AP_Blunt | 弹速 | 爆炸半径 | 科技层 |
|:---|:---|---:|---:|---:|---:|---:|:---|
| `GS_Handbow_Bolt_CE` | ArrowHighVelocity | 8 | 0.6 | 3.5 | 40 | -- | 中世纪 |
| `GS_Handcannon_Bullet_CE` | Bullet | 15 | 4 | 12 | 80 | -- | 工业 |
| `GS_MachinegunArm_Bullet_CE` | Bullet | 12 | 3 | 18 | 75 | -- | 工业 |
| `GS_LauncherArm_Grenade_CE` | Bomb | 20 | 0 | 0 | 35 | 2.0 | 工业 |
| `GS_Handcannon_Spacer_Bullet_CE` | Bullet | 18 | 8 | 18 | 100 | -- | 太空 |
| `GS_MachinegunArm_Transform_Bullet_CE` | Bullet | 14 | 6 | 22 | 90 | -- | 太空 |
| `GS_LauncherArm_Transform_Grenade_CE` | Bomb | 25 | 0 | 0 | 40 | 2.0 | 太空 |
| `GS_Bioturret_Bullet_CE` | Bullet | 10 | 8 | 15 | 100 | -- | 超级 |

数值设计逻辑:

- **Handbow 弓矢**: damageDef 为 `ArrowHighVelocity` 而非 `Bullet`, 与 CE 弩箭体系对齐。弹速 40 匹配中世纪投射物速度。
- **工业 -> 太空递进**: Handcannon 穿甲从 AP_S 4 升至 8 (2x), 弹速从 80 升至 100; Machinegunarm 穿甲从 AP_S 3 升至 6 (2x), 弹速从 75 升至 90。
- **榴弹**: AP_Sharp/Blunt 均为 0 -- 榴弹通过 `explosionRadius` 的范围伤害杀伤, 不依赖弹头穿甲。太空版伤害从 20 升至 25, 弹速从 35 升至 40。
- **Bioturret**: AP_Sharp 8 与太空手炮持平, 但单发伤害仅 10 -- 炮塔定位为持续压制火力 (3 发连射, 快速射间隔), 而非单发杀伤。

### 3.5 Section 3: 护甲植入物 damageFactors

Patch 文件第 748-864 行。使用 `PatchOperationAdd` 为护甲 hediff 添加或扩展 `damageFactors`。

CE 环境下, 原版 `ArmorRating_Sharp/Blunt/Heat` stat offset 仅作为显示值和 AI 评估参考, 不提供实际伤害减免。`damageFactors` 是 CE 中 hediff 提供真实防护的正确机制 -- 值小于 1.0 表示对该伤害类型的百分比减免。

#### 新增 damageFactors (PatchOperationAdd 到 `stages/li`)

| hediff defName | 名称 | Bullet | Cut | Stab | Scratch | Blunt | Flame | Burn | 科技层 |
|:---|:---|---:|---:|---:|---:|---:|---:|---:|:---|
| `GS_Hediff_carapace` | 甲壳 | 0.75 | 0.70 | 0.70 | 0.75 | 0.80 | 0.90 | -- | 太空 |
| `GS_hediff_plasteelrib` | 超钢肋骨 | 0.90 | 0.90 | 0.90 | -- | 0.80 | -- | -- | 太空 |
| `GS_Hediff_flameblock` | 防火体 | -- | -- | -- | -- | -- | 0.50 | 0.50 | 超级 |
| `GS_Hediff_enhancedtrach` | 强化气管 | 0.85 | 0.85 | -- | -- | 0.85 | -- | -- | 超级 |
| `GS_Hediff_skelemuscle` | 骨骼肌肉 | 0.85 | 0.85 | 0.85 | -- | 0.85 | -- | -- | 高级 |
| `GS_Hediff_ambiskull` | 全能颅骨 | 0.85 | 0.85 | 0.85 | -- | 0.85 | 0.85 | -- | 高级 |

#### 扩展已有 damageFactors (PatchOperationAdd 到 `stages/li/damageFactors`)

| hediff defName | 名称 | 已有减免 | 新增 | 科技层 |
|:---|:---|:---|:---|:---|
| `GS_hediff_centralshield` | 中枢护盾 | Bullet 0.6, Arrow 0.6, Stab 0.7 | Cut 0.70, Blunt 0.85, Flame 0.85 | 超级 |
| `GS_Hediff_exoshield` | 外骨骼护盾 | Cut/Scratch/ScratchToxic/Bite/ToxicBite 0.75 | Bullet 0.80, Blunt 0.75, Flame 0.85 | 超级 |
| `GS_hediff_organexoshield` | 器官外护盾 | Crush/Blunt/Poke/Thump/Bomb/BombSuper/Stun 0.75 | Bullet 0.85, Cut 0.90, Flame 0.80 | 超级 |

对已有 `damageFactors` 的 hediff, 使用 `PatchOperationAdd` 追加 CE 相关伤害类型到现有节点, 不覆盖原 mod 已定义的减免值。

---

## 4. 方案优势分析

### 4.1 自定义 CE 弹丸 vs 复用 CE 通用弹药弹丸

最终方案为每个仿生体远程武器创建独立的 CE 弹丸 ThingDef。

与复用 CE 通用弹药弹丸相比:

- **无需 AmmoSet / CompAmmoUser**: 身体集成武器的核心语义是"由仿生体自身供能, 不消耗外部弹药"。自定义弹丸配合 `defaultProjectile` 直接引用, 无需弹药系统介入, 完整保留了这一语义。复用通用弹药则必须引入 `CompAmmoUser` + `AmmoSet`, 导致弹药消耗 UI 和装填操作出现在身体部位上。
- **科技层级数值独立**: 每个弹丸的伤害/穿甲/弹速均独立配置, 精确对应其科技层级。工业手炮 AP_S 4 -> 太空手炮 AP_S 8 的 2x 递进, 无法通过任何单一 CE 通用弹药实现。
- **damageDef 自由度**: Handbow 使用 `ArrowHighVelocity` 而非 `Bullet`, 榴弹使用 `Bomb` -- 各弹丸保留语义正确的伤害类型。复用通用弹药会强制统一为该弹药的 damageDef, 丧失差异化。

### 4.2 PatchOperationReplace 整体替换 vs 逐字段 PatchOperationAdd

最终方案对 `tools` 和 `verbs` 节点使用整体 `PatchOperationReplace`, 而非逐字段添加/修改。

- **原子性**: 整体替换保证 ToolCE 的所有字段 (`power`, `cooldownTime`, `armorPenetrationSharp`, `armorPenetrationBlunt`, `capacities`, `alwaysTreatAsWeapon`) 在同一操作中完成。逐字段方式下, 若中途失败可能产生半转换状态 (有 `armorPenetrationSharp` 但缺 `armorPenetrationBlunt` 的 ToolCE)。
- **Class 属性替换**: 原版 `Tool` 到 `CombatExtended.ToolCE` 的 class 变更无法通过 `PatchOperationAdd` 实现 -- 必须替换整个 `<li>` 元素才能改变其 `Class` 属性。
- **可读性**: 每个 hediff 的完整转换结果在单个 `<value>` 块中一目了然, 便于代码审查和数值核对。

### 4.3 纯 XML 方案 vs C# HediffComp

- **零编译依赖**: 纯 XML 补丁不引用任何程序集, 不受 CE 或 GS_Core DLL 版本更新影响。C# 方案需要硬引用两个程序集, 任一更新公开 API 变更都需重新编译。
- **透明可审计**: 所有数值变更直接写在 XML 中, 任何读者都能通过文本编辑器验证每个 hediff 的穿甲值和弹丸配置。C# 方案将数值隐藏在编译后的 DLL 中。
- **维护成本最低**: 当 Greyscythe Bionics Catalogue 添加新仿生体时, 仅需在 XML 中追加对应的 `PatchOperationReplace` 条目, 无需 C# 编译/发布周期。

### 4.4 damageFactors 方案 vs 尝试修复原版 ArmorRating stat offset

CE 内部的护甲计算基于 mm-RHA 刻度, 原版 `ArmorRating_Sharp/Blunt/Heat` stat offset 在此刻度下的数值语义完全不同。尝试"修正" stat offset 到 CE 刻度 (如将 Sharp +0.15 改为 +15 mm-RHA) 会:

1. 破坏非 CE 环境下的数值平衡 (本补丁仅在 CE 环境加载)
2. 与 CE 的护甲叠加公式产生不可预测的交互

`damageFactors` 是 CE 为 hediff 提供伤害减免的标准机制, 与 CE 护甲系统正交工作, 不干扰原版 stat offset 的显示功能。保留原版 stat offset 作为 UI 显示和 AI 评估参考, 通过 `damageFactors` 提供真实 CE 防护, 是两全其美的设计。
