# Slo' Turret Collection CE 兼容修复 -- 技术设计文档

> **Steam Workshop ID**: 3038833232
> **Mod PackageId**: `slomow.SloTurrettCollec`
> **依赖项**: Slo's Library (`slo_s_Libary`), Vanilla Expanded Framework (VEF)
> **补丁方式**: 0 Harmony hooks / 1 XML patch 文件 / 6 Ammo Def 文件
> **规模**: V2CEPatch 中最大的单 mod 补丁 -- 28 把炮塔武器, 8 座能量建筑转换, 6 个自定义弹药定义文件

---

## 1. CE 破坏了什么

Slo' Turret Collection 为游戏新增 28 把炮塔武器, 横跨弹道、能量、激光、爆炸、火炮五大武器家族。安装 Combat Extended 后, **全部 28 把武器完全失效** -- 发射原版弹道、不消耗弹药、不参与 CE 护甲穿透计算。

### 1.1 根本原因: CE 的 false-match 误判

CE 的 `ModPatches/` 目录中存在一个名为 `Turret Collection/` 的补丁文件夹。该文件夹内的补丁**并非**针对 Slo 的 mod -- 它目标的是完全不同的 mod, 其 Def 使用 `TC_`/`TCEE_` 前缀, 而 Slo 的 Def 使用 `STC_` 前缀。

由于该文件夹在名称层面触发了 CE 的模糊匹配逻辑, CE 的 `GunAutoPatcher` 在运行时**跳过了所有 STC 武器** -- CE 假定这些武器已由 `ModPatches/Turret Collection/` 处理, 实际上该文件夹中没有任何 patch 命中 Slo 的 Def。

这构成了一个**静默失败**: 没有错误日志、没有红色警告, 炮塔看似正常运作但完全处于原版弹道系统中, 与 CE 的护甲/穿透体系完全脱节。

### 1.2 DLL 级自定义投射物的额外复杂性

Slo 的武器不仅使用标准弹丸 -- 大量武器依赖以下 DLL 级自定义投射物类:

| 自定义类 | 来源 | 使用者 |
|---|---|---|
| `slo_s_Libary.EnergyTurret` | Slo's Library | 8 座能量炮塔建筑 |
| `slo_s_Libary.LaserBeamProjectile` | Slo's Library | 激光类武器 |
| `slo_s_Libary.ProjectileMultiple_Explosive` | Slo's Library | Orbital Beam, Rupturer |
| `slo_s_Libary.Projectile_ClusterMunition` | Slo's Library | FlyCracker |
| `DamageWorker_ChargeVapo` | Slo mod | Foreshadow 炮塔 |
| `DamageWorker_ChargeNoFire` | Slo mod | Smite 系列 |
| `VFECore.FlamethrowProjectile` | VEF | Scorch 火焰炮塔 |
| `VFECore.GaussProjectile` | VEF | Railgun 类武器 |
| `VFECore.TeslaProjectile` | VEF | Tesla 线圈炮塔 |

这些自定义类全部不被 CE 的弹道系统识别, 且 `GunAutoPatcher` 因 false-match 跳过了自动转换。

### 1.3 破坏的具体表现

- 28 把炮塔武器发射原版 `Bullet` 投射物, 完全绕过 CE 的弹道抛物线模拟
- 不消耗 CE 弹药, 无限弹药射击
- 伤害不参与 CE 的锐利/钝击穿透计算, 对高护甲目标造成不合理伤害
- 能量炮塔的 `EnergyTurret` 类与 CE 的激光系统冲突
- 所有自定义 DamageWorker (如 `ChargeVapo`、`ChargeNoFire`) 的效果虽然执行但不与 CE 系统协调

---

## 2. 设计问题定框与方案对比

### 2.1 问题定框

需要将 28 把炮塔武器从"完全原版弹道"状态转换为"完全 CE 兼容"状态。核心挑战在于武器类型的**极端多样性**:

1. **标准弹道炮塔** (7 把): 常规枪械, 需映射到 CE 标准弹药集
2. **能量建筑炮塔** (8 座): 使用 `slo_s_Libary.EnergyTurret` 的自定义建筑类, 需转换到 CE 激光系统
3. **VEF 特殊投射物炮塔** (5 把): 使用 VEF 的 `FlamethrowProjectile`/`TeslaProjectile` 等, 需自定义弹药映射
4. **CE 激光弹丸炮塔** (4 把): 需新建 `LaserBeamDefCE` 投射物
5. **自定义爆炸炮塔** (3 把): 使用 `DamageWorker_ChargeVapo` 等, 需新建 `ProjectileCE_Explosive` 投射物
6. **火炮/迫击炮** (6 把): 需转换为 `Verb_ShootMortarCE`, 部分保留原版投射物类
7. **可穿戴炮塔** (2 件): 需添加 CE `Bulk`/`WornBulk` 属性

设计约束:

- 必须绕过 CE 的 false-match 误判, 不能依赖 `GunAutoPatcher`
- 自定义 DamageWorker 在提供独特游戏效果的场景下必须保留, 不能统一替换为 `ProjectileCE`
- 能量炮塔必须从 Slo 的自定义能量系统迁移到 CE 的原生激光系统
- 不得引入 Harmony patch -- 所有修改通过 XML 完成
- 弹药定义必须包含完整的 AmmoDef + AmmoSetDef + ProjectileDef + RecipeDef 四件套

### 2.2 考虑过的方案

#### 方案 A: 修改 CE 的 ModPatches 文件夹名称 (已否决)

将 CE 的 `ModPatches/Turret Collection/` 重命名, 使 `GunAutoPatcher` 不再跳过 STC 武器。

**否决原因**:

- 需要直接修改 CE 的安装文件, 违反"不修改上游 mod"原则
- `GunAutoPatcher` 的自动转换过于粗糙, 无法为 Slo 的高度自定义武器 (能量系统、自定义 DamageWorker、VEF 投射物) 生成正确配置
- 其他依赖 `ModPatches/Turret Collection/` 的 mod (使用 `TC_`/`TCEE_` 前缀的 mod) 会失去已有补丁

#### 方案 B: 全量替换所有投射物为 ProjectileCE (已否决)

将所有 Slo 投射物统一替换为 `ProjectileCE` 或 `ProjectileCE_Explosive` 子类。

**否决原因**:

- `DamageWorker_ChargeVapo` 提供独特的 charge-vaporization 爆炸效果, 替换为标准 CE 爆炸会丢失游戏性
- `ProjectileMultiple_Explosive` 实现多段爆炸, CE 的单段爆炸模型无法复现
- `Projectile_ClusterMunition` 的子母弹机制不能由 `ProjectileCE_Explosive` 的 `explosionRadius` 替代
- VEF 的 `TeslaProjectile` 链式电弧效果是该武器的核心视觉/游戏体验, 不可丢失

#### 方案 C: 仅使用 PatchOperationMakeGunCECompatible 自动转换 (已否决)

CE 提供的 `PatchOperationMakeGunCECompatible` 可以批量处理武器的 stat/verb/ammo 转换。

**否决原因**: `PatchOperationMakeGunCECompatible` 仅处理**武器 ThingDef** 层 -- 它不能:

- 转换**建筑 ThingDef** 的 `thingClass` (能量炮塔需要从 `EnergyTurret` 转到 `Building_LaserGunCE`)
- 移除特定 Comp (如 `CompProperties_Refuelable`、`CompProperties_EnergyChargeable`)
- 添加建筑级属性 (如 `beamPowerConsumption`)
- 创建新的弹药定义 (AmmoSet/Ammo/Projectile/Recipe 四件套)

因此该方案只能覆盖标准弹道炮塔 (7 把), 剩余 21 把武器和 8 座建筑仍需手动处理。

### 2.3 最终方案

采用**混合策略**: 将 `PatchOperationMakeGunCECompatible` 用于武器层转换, 手动 `PatchOperation` 用于建筑层转换, 6 个新建 Ammo Def 文件覆盖所有非标准弹药需求。对于携带自定义 DamageWorker 或特殊行为的投射物, **有选择地保留原版投射物类**并仅转换射击 Verb。

具体分层:

| 层级 | 处理方式 | 覆盖范围 |
|---|---|---|
| 能量建筑 | 手动 `PatchOperationAttributeSet` + `PatchOperationReplace` + `PatchOperationRemove` | 8 座建筑, 每座 5 个 patch 操作 |
| 标准弹道武器 | `PatchOperationMakeGunCECompatible` + CE 内置弹药集 | 7 把武器 |
| VEF 特殊投射物武器 | `PatchOperationMakeGunCECompatible` + 自定义 AmmoSet (保留原版投射物) | 5 把武器 |
| 激光武器 | `PatchOperationMakeGunCECompatible` + 新建 `LaserBeamDefCE` 投射物 | 4 把武器 |
| 自定义爆炸武器 | `PatchOperationMakeGunCECompatible` + 新建 `ProjectileCE_Explosive` 投射物 | 3 把武器 |
| 火炮/迫击炮 | `PatchOperationMakeGunCECompatible` (`Verb_ShootMortarCE`) + 自定义/CE 内置炮弹 | 6 把武器 |
| 可穿戴装备 | `PatchOperationAdd` 追加 `Bulk`/`WornBulk` | 2 件装备 |
| 激光分裂光束 | `PatchOperationReplace` 替换 `thingClass` 为 `LaserBeamCE` | 3 个投射物 Def |

---

## 3. 具体实现

### 3.0 文件总览

| 文件路径 | 行数 | 用途 |
|---|---|---|
| `Patches/SloTurretCollection.xml` | 1346 | 主补丁: 全部炮塔转换逻辑 |
| `Defs/SloTurretCollection/Ammo_STC_Ballistic.xml` | 357 | 弹道弹药: Railgun (Standard/AP), Accelerator Round (Standard/AP) |
| `Defs/SloTurretCollection/Ammo_STC_Energy.xml` | 122 | 能量核心弹药: 共享 `Ammo_STC_EnergyCore` + 5 个 AmmoSet |
| `Defs/SloTurretCollection/Ammo_STC_Explosive.xml` | 443 | 爆炸弹药: ChargedBomb, SmiteCharge, Rocket, Thermobaric, IncendiaryRocket |
| `Defs/SloTurretCollection/Ammo_STC_Laser.xml` | 120 | 激光弹药: 4 个 AmmoSet + 4 个 `LaserBeamDefCE` 投射物 |
| `Defs/SloTurretCollection/Ammo_STC_Artillery.xml` | 359 | 火炮弹药: HeavyShell (HE/Incendiary/EMP), ClusterMissile |
| `Defs/SloTurretCollection/Ammo_STC_Misc.xml` | 220 | 杂项弹药: IncendiaryFuel, FuseCharge, Canister |
| `LoadFolders.xml` (第 12 行) | -- | 条件加载门控 |

**门控机制**:

- XML 补丁层: `PatchOperationFindMod` 检查 `"Slo' turret collection"` (第 3-4 行)
- Def 加载层: `LoadFolders.xml` 第 12 行 `<li IfModActive="slomow.SloTurrettCollec">Defs/SloTurretCollection</li>`
- 不涉及 Harmony -- `V2CEPatchMod.cs` 中无对应条目

### 3.1 Section 1: 能量炮塔建筑转换 (第 8-176 行)

8 座能量炮塔从 `slo_s_Libary.EnergyTurret` 整体迁移至 CE 内置的激光炮塔系统 `CombatExtended.Lasers.Building_LaserGunCE`。每座炮塔执行 5 个 patch 操作:

1. `PatchOperationAttributeSet`: ThingDef `Class` 属性 → `CombatExtended.Lasers.Building_LaserGunDef`
2. `PatchOperationReplace`: `thingClass` → `CombatExtended.Lasers.Building_LaserGunCE`
3. `PatchOperationAdd`: 添加 `beamPowerConsumption` (光束功耗, 单位 W)
4. `PatchOperationRemove`: 移除 `CompProperties_Refuelable` (CE 激光系统自管能量)
5. `PatchOperationRemove`: 移除 `slo_s_Libary.CompProperties_EnergyChargeable` (被 CE 能量系统替代)

**各炮塔 beamPowerConsumption 精确数值**:

| defName | 炮塔名称 | beamPowerConsumption | 行号 |
|---|---|---|---|
| `STC_Turret_Tesla` | Tesla | 833 | 22 |
| `STC_Turret_Smite` | Smite | 4167 | 43 |
| `STC_Turret_Beamlance` | Beamlance | 833 | 64 |
| `STC_Turret_Heavenly_Needle` | Heavenly Needle | 1500 | 85 |
| `STC_Turret_prism` | Prism | 1250 | 106 |
| `STC_Turret_Arc_Accelerator` | Arc Accelerator | 4 | 127 |
| `STC_TurretMoonBeam` | MoonBeam | 2 | 148 |
| `STC_Turret_Rupturer` | Rupturer | 1500 | 169 |

功耗设计逻辑: `beamPowerConsumption` 正比于原始武器的伤害输出。Smite 作为终局级武器设为最高值 4167; Arc Accelerator 和 MoonBeam 作为持续低伤害压制武器设为极低值 (4/2), 使其几乎不消耗能量但依靠高射速弥补。

### 3.2 Section 2: 标准弹道炮塔 -- Family A (第 178-435 行)

7 把使用常规弹丸的炮塔, 通过 `PatchOperationMakeGunCECompatible` 整合进 CE 的标准弹药系统。每把武器配置完整的 statBases + Properties + AmmoUser + FireModes 四段。

| defName | 名称 | 弹药集 | 弹匣 | 连射 | 射程 | 行号 |
|---|---|---|---|---|---|---|
| `STC_Gun_Jumbo` | Jumbo | `AmmoSet_762x51mmNATO` | 300 | 10 发 | 45 | 181 |
| `STC_Gun_Bolter` | Bolter | `AmmoSet_20x102mm` | 60 | 1 发 | 55 | 218 |
| `STC_Gun_Shotgun` | Shotgun | `AmmoSet_12Gauge` | 60 | 6 发 | 20 | 254 |
| `STC_Gun_SpeewerTurret` | Speewer | `AmmoSet_762x51mmNATO` | 600 | 20 发 | 45 | 291 |
| `STC_Gun_PaladinTurret` | Paladin | `AmmoSet_20x102mm` | 90 | 3 发 | 55 | 328 |
| `STC_Gun_Mini_Spiner` | Mini Spiner | `AmmoSet_556x45mmNATO` | 120 | 10 发 | 30 | 364 |
| `STC_Gun_Deserter` | Deserter | `AmmoSet_762x51mmNATO` | 600 | 30 发 | 50 | 401 |

弹药选型逻辑:
- 重机枪类 (Jumbo, Speewer, Deserter) → 7.62x51mm NATO, 体现中等口径持续火力
- 重炮类 (Bolter, Paladin) → 20x102mm HE, 体现大口径单发/短连射高伤害
- 霰弹类 (Shotgun) → 12 Gauge Buck, 体现近距离面杀伤
- 轻机枪类 (Mini Spiner) → 5.56x45mm NATO, 体现小口径轻型武器

所有武器使用 `Verb_ShootCE` 作为 verbClass, `recoilPattern` 统一为 `Mounted` (固定炮塔)。

### 3.3 Section 3: VEF 特殊投射物炮塔 -- Family B (第 437-619 行)

5 把武器使用来自 VEF 或 Slo 自定义的特殊投射物类。这些投射物携带不可替代的游戏效果, 因此**保留原版投射物类** (`defaultProjectile` 指向原始 STC 投射物 defName), 仅将射击系统转换为 `Verb_ShootCE`。

每把武器对应一个**自定义 AmmoSet**, 将新建的 AmmoDef 映射到原始投射物:

| defName | 名称 | AmmoSet | 原始投射物 | 保留原因 | 行号 |
|---|---|---|---|---|---|
| `STC_Gun_Scorch` | Scorch | `AmmoSet_STC_IncendiaryFuel` | `STC_Bullet_Scorch` | VEF `FlamethrowProjectile` 火焰扩散效果 | 440 |
| `STC_Gun_TurretLancer` | Turret Lancer | `AmmoSet_STC_Railgun` | `Bullet_STC_Railgun_Standard` (CE 新建) | 转为 CE 弹道但保留 railgun 视觉 | 477 |
| `STC_Gun_TeslaTurret` | Tesla Turret Gun | `AmmoSet_STC_TeslaCharge` | `STC_Bullet_TeslaCoil` | VEF `TeslaProjectile` 链式电弧 | 513 |
| `STC_Gun_FuseTurret` | Fuse Turret Gun | `AmmoSet_STC_FuseCharge` | `STC_Bullet_Fuse` | 自定义引信延迟爆炸逻辑 | 549 |
| `STC_Gun_WatchGuard` | WatchGuard | `AmmoSet_STC_Canister` | `STC_Bullet_WatchGuard` | 自定义散弹扩散模式 | 585 |

**关键设计决策**: `STC_Bullet_TeslaCoil`、`STC_Bullet_Fuse`、`STC_Bullet_Scorch` 被**故意保留为原版投射物类** (非 `ProjectileCE`), 因为它们携带的自定义 DamageWorker 和特殊行为在 CE 的 `ProjectileCE` 体系下会被剥离。补丁通过 `Verb_ShootCE` 转换了瞄准和射击机制, 同时保持投射物命中后的游戏效果不变。

### 3.4 Section 4: 激光炮塔 -- Family C (第 659-820 行)

4 把激光武器 + 3 个分裂光束投射物转换。

**武器转换** -- 通过 `PatchOperationMakeGunCECompatible`, 弹药消耗 CE 内置的 `Ammo_LaserChargePack`:

| defName | 名称 | AmmoSet | 默认投射物 | 弹匣 | 射程 | 行号 |
|---|---|---|---|---|---|---|
| `STC_Gun_Beamlance` | Beamlance Gun | `AmmoSet_STC_Beamlance` | `Bullet_STC_Beamlance` | 100 | 65 | 662 |
| `STC_Gun_Prism` | Prism Gun | `AmmoSet_STC_PrismBeam` | `Bullet_STC_PrismBeam` | 100 | 55 | 698 |
| `STC_Gun_MoonBeam` | MoonBeam Gun | `AmmoSet_STC_MoonBeam` | `Bullet_STC_MoonBeam` | 1000 | 120 | 734 |
| `STC_Gun_Smite` | Smite Gun | `AmmoSet_STC_SmiteBeam` | `Bullet_STC_SmiteBeam` | 100 | 100 | 771 |

**激光投射物定义** (`Defs/SloTurretCollection/Ammo_STC_Laser.xml`):

每个投射物使用 `CombatExtended.Lasers.LaserBeamDefCE` 父类, 继承自 `BaseLaserBullet`, 并配有独立颜色编码:

| defName | 伤害 | 穿甲(锐利) | 颜色 RGB | 用途 |
|---|---|---|---|---|
| `Bullet_STC_Beamlance` | 30 | 35 | (255,100,100) 红色 | 高伤害单体精准 |
| `Bullet_STC_PrismBeam` | 20 | 20 | (200,100,255) 紫色 | 分裂爆炸激光 |
| `Bullet_STC_MoonBeam` | 1 | 5 | (200,200,255) 白蓝 | 压制型低伤害 |
| `Bullet_STC_SmiteBeam` | 40 | 45 | (100,200,255) 蓝色 | 终局级级联激光 |

**分裂光束修补** (第 807-820 行): 3 个原版分裂光束投射物的 `thingClass` 替换为 `CombatExtended.Lasers.LaserBeamCE`:
- `STC_Bullet_SmiteBeamSplit` (第 808 行)
- `STC_Bullet_SmiteBeamSplitSecondary` (第 812 行)
- `STC_Bullet_PrismBeamSplit` (第 818 行)

### 3.5 Section 5: 自定义爆炸武器 -- Family D (第 822-932 行)

3 把使用非标准爆炸机制的武器:

| defName | 名称 | AmmoSet | verbClass | 投射物类 | 行号 |
|---|---|---|---|---|---|
| `STC_Gun_ForeshadowTurret` | Foreshadow | `AmmoSet_STC_ChargedBomb` | `Verb_ShootMortarCE` | `Bullet_STC_ChargedBomb` (CE 新建, `ProjectileCE_Explosive`, 伤害 80, 爆炸半径 5) | 825 |
| `STC_Gun_ShockPoralizer` | ShockPoralizer | `AmmoSet_40x46mmGrenade` | `Verb_ShootCE` | `Bullet_40x46mmGrenade_EMP` (CE 内置) | 862 |
| `STC_Gun_SmiteTurret` | SmiteTurret | `AmmoSet_STC_SmiteCharge` | `Verb_ShootCE` | `Bullet_STC_SmiteCharge` (CE 新建, `ProjectileCE_Explosive`, 伤害 12, 爆炸半径 2.9) | 898 |

**自定义爆炸投射物** (`Defs/SloTurretCollection/Ammo_STC_Explosive.xml`):

| 投射物 defName | damageDef | 伤害 | 爆炸半径 | 速度 | 用途 |
|---|---|---|---|---|---|
| `Bullet_STC_ChargedBomb` | `STC_ChargedBomb` | 80 | 5 | 60 | Foreshadow: charge-vaporization 终局炮弹 |
| `Bullet_STC_SmiteCharge` | `STC_ChargedBomb` | 12 | 2.9 | 55 | SmiteTurret: 连射型中等爆炸 |
| `Bullet_STC_Rocket` | `Bomb` | 10 | 1.9 | 80 | Rain 炮塔: 高速小型火箭 |
| `Bullet_STC_Thermobaric` | `STC_ThermobaricBomb` | 75 | 10 | 50 | DawnMaker: 超大范围温压弹 |
| `Bullet_STC_IncendiaryRocket` | `Flame` | 10 | 1 | 60 | HellFlare: 燃烧火箭 (`ai_IsIncendiary`, 生成 `Filth_Fuel`) |

### 3.6 Section 6: 火炮/迫击炮 -- Family E (第 934-1210 行)

6 把重型武器, 其中多数使用 `Verb_ShootMortarCE` + `indirectFirePenalty=0.2`:

| defName | 名称 | AmmoSet | 弹匣 | 射程 | 投射物保留策略 | 行号 |
|---|---|---|---|---|---|---|
| `STC_Gun_RainTurret` | Rain Turret | `AmmoSet_STC_Rocket` | 200 | 75 | CE 新建 `Bullet_STC_Rocket` | 937 |
| `STC_Gun_DawnMaker` | DawnMaker | `AmmoSet_STC_Thermobaric` | 20 | 500 | CE 新建 `Bullet_STC_Thermobaric` | 974 |
| `STC_Gun_PatriachTurret` | Patriarch | `AmmoSet_STC_Drone` | 10 | 500 | **保留原版** `STC_Bullet_Drone` | 1011 |
| `STC_Gun_FlyCracker` | FlyCracker | `AmmoSet_STC_ClusterMunition` | 10 | 200 | **保留原版** `STC_FlyCracker_Projectile` | 1048 |
| `STC_Guardian_TurretHead` | Guardian Head | `AmmoSet_81mmMortarShell` | 1 | 500 | CE 内置 `Bullet_81mmMortarShell_HE` | 1096 |
| `STC_EarthShatterer_TurretHead` | EarthShatterer Head | `AmmoSet_STC_HeavyShell` | 1 | 500 | CE 新建 `Bullet_STC_HeavyShell_HE` (3 变体) | 1135 |
| `STC_Rain_TurretHead` | Rain Head | `AmmoSet_STC_ClusterMissile` | 1 | 500 | CE 新建 `Bullet_STC_ClusterMissile` | 1174 |

**额外操作**:
- 移除 3 座迫击炮建筑的 `ITab_Shells` 检查器标签页 (第 1085-1093 行): `STC_GuardianCannon`, `STC_EarthShatterer`, `STC_Rain` -- CE 的弹药系统替代了原版装弹 UI
- 移除 3 座迫击炮武器的 `CompProperties_ChangeableProjectile` (第 1131, 1170, 1209 行) -- CE 通过 AmmoUser 管理弹种切换

**火炮弹药** (`Defs/SloTurretCollection/Ammo_STC_Artillery.xml`):

Heavy Shell 提供 HE/Incendiary/EMP 三变体:
- HE: `Bomb` 伤害 65, 爆炸半径 8
- Incendiary: `Flame` 伤害 20, 爆炸半径 8, 生成 `Filth_Fuel`
- EMP: `EMP` 伤害 50, 爆炸半径 6

Cluster Missile: `Bomb` 伤害 40, 爆炸半径 4

### 3.7 Section 7: 超科技能量武器 -- Family F (第 1212-1321 行)

3 把保留原版投射物的终局级能量武器:

| defName | 名称 | AmmoSet | 默认投射物 | 保留原因 | 行号 |
|---|---|---|---|---|---|
| `STC_Gun_ArcAccelerator` | Arc Accelerator Gun | `AmmoSet_STC_AcceleratorRound` | `Bullet_STC_AcceleratorRound_Standard` | 自定义 `STC_BleedingBullet` damageDef | 1215 |
| `STC_Gun_Heavenly_needle` | Heavenly Needle Gun | `AmmoSet_STC_OrbitalCharge` | **原版** `STC_bullet_OrbitalBeam` | `ProjectileMultiple_Explosive` 多段爆炸 | 1252 |
| `STC_Gun_Rupturer` | Rupturer Gun | `AmmoSet_STC_RuptureCharge` | **原版** `STC_bullet_Rupturer` | `ProjectileMultiple_Explosive` 多段爆炸 | 1288 |

**Accelerator Round 弹药** (`Defs/SloTurretCollection/Ammo_STC_Ballistic.xml`):

Standard 和 AP 两变体:
- Standard: `STC_BleedingBullet` damageDef, 伤害 10, 锐利穿甲 25, 钝击穿甲 50, 速度 175
- AP: `STC_BleedingBullet` damageDef, 伤害 8, 锐利穿甲 40, 钝击穿甲 60, 速度 175

**共享能量核心弹药** (`Defs/SloTurretCollection/Ammo_STC_Energy.xml`):

单一 `Ammo_STC_EnergyCore` 物理弹药项, 通过 5 个独立 AmmoSet 映射到不同的原版投射物:
- `AmmoSet_STC_TeslaCharge` → `STC_Bullet_TeslaCoil`
- `AmmoSet_STC_OrbitalCharge` → `STC_bullet_OrbitalBeam`
- `AmmoSet_STC_RuptureCharge` → `STC_bullet_Rupturer`
- `AmmoSet_STC_ClusterMunition` → `STC_FlyCracker_Projectile`
- `AmmoSet_STC_Drone` → `STC_Bullet_Drone`

这种"一个物理弹药 + 多个 AmmoSet"设计避免了为每种能量武器创建独立弹药物品, 降低了玩家的后勤复杂度。

### 3.8 Section 8: 可穿戴炮塔装备 (第 1323-1341 行)

2 件可穿戴炮塔装备添加 CE 的 `Bulk` 和 `WornBulk` stat:

| defName | Bulk | WornBulk | 行号 |
|---|---|---|---|
| `STC_Apparel_MiniSpinerTurret` | 8 | 4 | 1326 |
| `STC_Apparel_FlyCrackerTurret` | 10 | 5 | 1335 |

---

## 4. 方案优势与替代方案对比

### 4.1 为什么选择 CE 激光系统而非保留 EnergyTurret

CE 内置的 `Building_LaserGunCE` + `Building_LaserGunDef` 系统提供:

- **光束渲染**: 内置的 `LaserBeamCE` 渲染管线, 无需 Slo 自己的光束代码
- **能量管理**: `beamPowerConsumption` 直接挂接到 CE 的能量/热量框架
- **弹药集成**: 通过 AmmoSet 映射到 CE 的标准弹药/制造体系

保留 `EnergyTurret` 的替代路径需要同时维护 Slo 的能量系统 (`CompProperties_EnergyChargeable` + `CompProperties_Refuelable`) 和 CE 的弹药系统, 产生双重资源消耗逻辑, 对玩家造成困惑。

### 4.2 为什么选择性保留原版投射物

全量转换为 `ProjectileCE` 虽然技术上更"干净", 但以下投射物的原版行为不可替代:

| 投射物 | 保留的核心行为 | CE 替代方案的损失 |
|---|---|---|
| `STC_Bullet_TeslaCoil` | VEF 链式电弧 (`TeslaProjectile`) | 电弧视觉效果和链式跳转逻辑丢失 |
| `STC_Bullet_Scorch` | VEF 火焰扩散 (`FlamethrowProjectile`) | 火焰锥形扩散效果丢失 |
| `STC_Bullet_Fuse` | 自定义引信延迟机制 | 延迟爆炸行为丢失, 变为即时爆炸 |
| `STC_bullet_OrbitalBeam` | `ProjectileMultiple_Explosive` 多段爆炸 | 退化为单次爆炸, 丢失"轨道轰击"体验 |
| `STC_bullet_Rupturer` | `ProjectileMultiple_Explosive` 多段爆炸 | 退化为单次爆炸 |
| `STC_FlyCracker_Projectile` | `Projectile_ClusterMunition` 子母弹 | 子弹药分散机制丢失 |
| `STC_Bullet_Drone` | 自导航爆炸无人机 | 追踪行为丢失 |

### 4.3 为什么这是 V2CEPatch 中最复杂的补丁

规模对比:

| 维度 | Slo Turret Collection | 第二大补丁 |
|---|---|---|
| 武器数量 | 28 | 通常 < 10 |
| Def 文件数量 | 6 | 通常 0-2 |
| Patch XML 行数 | 1346 | 通常 < 300 |
| 武器类型数量 | 7 (弹道/能量建筑/VEF 特殊/激光/爆炸/火炮/可穿戴) | 通常 1-2 |
| 自定义弹药种类 | ~20 (含变体) | 通常 0-3 |
| 建筑级转换 | 8 | 通常 0 |

复杂性不仅来自数量, 更来自**类型多样性**: 每种武器家族需要完全不同的转换策略, 从"直接映射 CE 内置弹药"到"重建 CE 投射物 + 新建弹药四件套"到"保留原版投射物仅转换 Verb", 需要对 CE 内部系统和 Slo 自定义代码的深入理解。
