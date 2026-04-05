# VWE Heavy Weapons CE 兼容修复 -- 技术设计文档

> **Steam Workshop ID**: 2329126791  
> **Mod PackageId**: `VanillaExpanded.VWEHW`  
> **补丁方式**: 3 Harmony hooks / 0 XML patches / 0 new Defs

---

## 1. CE 破坏了什么

VWE Heavy Weapons 为游戏添加了 5 种重型武器 (Autocannon、Handheld Mortar、Heavy Flamer、Swarm Missile Launcher、Uranium Slug Rifle), 并在 VEF 框架层实现了两个独有机制。安装 Combat Extended 后, 这两个机制均完全失效。

### 1.1 逐发耐久扣除失效

VEF 定义了 `VEF.Weapons.HeavyWeapon` ModExtension, 其中 `weaponHitPointsDeductionOnShot` 字段指定每次射击扣除的武器 HP。该逻辑寄生在 VEF 自定义的 `Verb_Shoot` 子类中: 每次成功射击后读取该字段, 从武器 HitPoints 中减去对应值。

CE 以 `Verb_ShootCE` 和 `Verb_LaunchProjectileCE` 完整替换了原版 `Verb_Shoot` 的射击路径。VEF 的重载逻辑在 CE 环境下永远不会被调用, 导致所有重型武器变为无限耐久 -- 这直接破坏了 VWE Heavy Weapons 的核心平衡设计 (Handheld Mortar 设计寿命仅 5 发, 无限耐久使其严重失衡)。

### 1.2 群蜂导弹制导失效

Swarm Missile Launcher 使用 VEF 的 `CompGuidedProjectile` 组件 (`selectDifferentTargets=true`) 在 8 发连射中将火箭分配至不同目标。该组件的实现基于原版 `Projectile` 类: 内部使用 `FieldRef<Projectile, Vector3>` 访问弹丸位置和速度。

CE 的弹丸体系以 `ProjectileCE` 替换了原版弹丸。关键在于 `ProjectileCE` 的继承链是 `ThingWithComps`, **不是** `Projectile`。因此 VEF 的 `CompGuidedProjectile` 在类型层面无法操作 CE 弹丸, 导弹制导和多目标分配均完全失效, 所有 8 发火箭将命中同一目标。

### 1.3 已有 CE 手动补丁覆盖的内容

现有 CE 社区手动补丁已正确处理以下方面, 本修复**不涉及**这些内容:

- 武器 stat 转换 (射速、精度、后坐力等 CE 化)
- 弹药集 (AmmoSet) 绑定
- 外骨骼框架 (Exoframe) 适配

### 1.4 接受的行为差异

Heavy Flamer 的扇形火焰锥视觉效果在 CE 下丢失, 由 CE 的 Prometheum 燃料系统替代。恢复锥形视觉需要构建 `ProjectileCE` 与 VEF 扩展锥渲染的混合继承类, 工作量极高而游戏性收益微小, 属于可接受的行为差异。

---

## 2. 设计问题定框与方案对比

### 2.1 问题定框

需要修复两个独立失效:

1. **耐久扣除**: 需要在 CE 射击路径中重新注入"每发扣 HP"逻辑, 且必须与 VEF 原始行为语义一致 (确定性扣除, 非概率性)。
2. **导弹制导**: 需要在 CE 弹丸体系内实现追踪转向和多目标分配, 且不能依赖 VEF 的 `CompGuidedProjectile` (类型不兼容)。

设计约束:

- 不得引入新 Def, 避免与 CE 手动补丁的 Def 层冲突
- 必须保持 VEF 设置 UI (DefsAlterer 修改的滑块数值) 继续生效
- Mod 检测门控: 仅在 VWE Heavy Weapons 激活时注册 Harmony hooks

### 2.2 考虑过的方案

#### 方案 A: 使用 CE 内置 `weaponDeteriorationChance` (已否决)

CE 自身提供基于 `ProjectileDef.weaponDeteriorationChance` 的武器损耗系统。

**否决原因**:

- CE 模型是**概率性**的 (每发射击有 X% 概率扣 HP), VEF 模型是**确定性**的 (每发精确扣除 N 点 HP)。两者语义根本不同 -- 使用 CE 模型会导致 Handheld Mortar 可能在第 2 发就损坏, 也可能第 8 发才损坏, 完全背离 "精确 5 发寿命" 的设计意图。
- CE 的损耗基于弹药 Def 维度, VEF 的扣除基于武器 Def 维度。维度不匹配意味着无法通过 CE 机制忠实还原 VEF 的逐武器独立配置。

#### 方案 B: 将 VEF CompGuidedProjectile 挂载到 CE 弹丸 (已否决)

直接给 `ProjectileCE` 添加 `CompGuidedProjectile`。

**否决原因**: 技术上不可行。VEF 的制导组件内部使用 `FieldRef<Projectile, Vector3>` 通过反射访问原版 `Projectile` 的位置/速度字段。`ProjectileCE` 继承自 `ThingWithComps` 而非 `Projectile`, 这些 FieldRef 在 CE 弹丸上会直接抛出类型不匹配异常。

#### 方案 C: 为 ProjectileCE 编写自定义 ThingComp 实现追踪 (已否决)

创建一个新的 `ThingComp` 挂载到 CE 弹丸上, 在 `CompTick` 中实现转向逻辑。

**否决原因**: CE 弹丸的飞行轨迹由 `TrajectoryWorker` 系统统一管理。在 `CompTick` 中独立修改弹丸位置/速度会与 `TrajectoryWorker` 的计算发生冲突, 产生不可预测的弹丸行为 (抖动、穿模、轨迹突变)。

#### 方案 D: 恢复 VEF 火焰锥视觉效果 (已否决)

构建继承 `ProjectileCE` 的混合弹丸类, 同时实现 VEF 的扇形扩散渲染。

**否决原因**: 需要维护一个同时依赖 CE 弹道系统和 VEF 渲染系统的混合类, 任一上游更新都可能导致兼容性破裂。对比 CE 的 Prometheum 系统已提供了功能等价的火焰投射, 视觉差异的游戏性影响微乎其微, 工程代价不成比例。

### 2.3 最终方案

采用 3 个 Harmony Postfix hook, 全部挂载在 `Verb_LaunchProjectileCE` 的方法上:

1. `TryCastShot` Postfix -- 恢复确定性耐久扣除
2. `SpawnProjectile` Postfix -- 注入 CE 原生追踪系统
3. `TryCastShot` Postfix (after #1) -- 实现连射多目标分配

零 XML 补丁, 零新 Def, 完全通过 C# 侧完成。

---

## 3. 具体实现

### 3.1 共享基础设施: Mod 检测门控

所有 VWE Heavy Weapons 补丁在 `V2CEPatchMod` 静态构造函数中按条件注册:

```
Source/V2CEPatch/V2CEPatchMod.cs:25-31
```

```csharp
if (ModDetection.VWEHeavyWeaponsActive)
{
    harmony.CreateClassProcessor(typeof(Patch_VerbLaunchProjectileCE_DurabilityLoss)).Patch();
    harmony.CreateClassProcessor(typeof(Patch_VerbLaunchProjectileCE_SwarmMissileHoming)).Patch();
    harmony.CreateClassProcessor(typeof(Patch_VerbLaunchProjectileCE_SwarmMissileRetarget)).Patch();
    Log.Message("[V2CEPatch] Applied VWE Heavy Weapons patches");
}
```

检测逻辑位于:

```
Source/V2CEPatch/Utility/ModDetection.cs:16
```

```csharp
VWEHeavyWeaponsActive = ModsConfig.IsActive("VanillaExpanded.VWEHW");
```

### 3.2 Hook 1: 逐发耐久扣除

**文件**: `Source/V2CEPatch/Harmony/Patch_VerbLaunchProjectileCE_DurabilityLoss.cs`

**挂载点**: `Verb_LaunchProjectileCE.TryCastShot` Postfix (line 9)

**工作流程**:

1. 仅在射击成功 (`__result == true`) 时执行 (line 18)
2. 首次调用时通过反射定位 `VEF.Weapons.HeavyWeapon` 类型及 `weaponHitPointsDeductionOnShot` 字段, 并缓存结果 (lines 25-28)
3. 从当前武器的 `modExtensions` 列表中查找 `HeavyWeapon` 实例 (line 33)
4. 读取扣除值, 从武器 HitPoints 中减去 (line 39)
5. 若 HP 降至 0: 以 `DestroyMode.Vanish` 销毁武器, 并停止持有者 Pawn 的所有 Job (lines 40-48)

**关键设计决策**:

- 使用反射而非硬引用 VEF 程序集: 避免在 VEF 未安装时产生 TypeLoadException
- 读取的是同一个 `weaponHitPointsDeductionOnShot` 字段: VEF 的 `DefsAlterer` 在启动时根据设置 UI 滑块修改该字段值, Postfix 在运行时读取修改后的值, 因此 VEF 设置 UI 仍然有效

### 3.3 Hook 2: 群蜂导弹追踪

**文件**: `Source/V2CEPatch/Harmony/Patch_VerbLaunchProjectileCE_SwarmMissileHoming.cs`

**挂载点**: `Verb_LaunchProjectileCE.SpawnProjectile` Postfix (line 21, 通过 `TargetMethod()` 指定)

**工作流程**:

1. 仅对 `VWE_Gun_SwarmMissileLauncher` 武器生效 (line 28)
2. 设置弹丸的 `homingAcceleration = 0.15f` (line 43)
3. 通过反射获取 `HomingBulletTrajectoryWorker.Instance` 单例 (lines 33-38)
4. 将该追踪 Worker 写入弹丸的 `forcedTrajectoryWorker` 字段 (line 47)
5. 将生成的弹丸引用存入 `lastSpawnedMissile` 供 Hook 3 消费 (line 50)

**关键设计决策**:

- 使用 CE 的**原生追踪系统** (`HomingBulletTrajectoryWorker`), 而非尝试移植 VEF 的 `CompGuidedProjectile`。CE 的追踪基于 `Vector3.RotateTowards` 平滑速度转向, 与 CE 弹道系统完全兼容。
- `homingAcceleration = 0.15f`: 单位为 rad/tick, 对应约 8.6 deg/tick 的最大航向修正能力。CE 追踪系统内置 3-tick 散布阶段, 发射后前 3 tick 不执行转向, 模拟火箭出膛惯性。经过 8-tick 斜升后达到全追踪能力。

### 3.4 Hook 3: 连射多目标分配

**文件**: `Source/V2CEPatch/Harmony/Patch_VerbLaunchProjectileCE_SwarmMissileRetarget.cs`

**挂载点**: `Verb_LaunchProjectileCE.TryCastShot` Postfix (line 11), 标注 `[HarmonyAfter("v2modpack.cepatch.durability")]` 确保在 Hook 1 之后执行 (line 12)

**工作流程**:

1. 仅对 `VWE_Gun_SwarmMissileLauncher` 武器生效 (line 22)
2. 从 Hook 2 存储的 `lastSpawnedMissile` 获取本发弹丸引用 (line 24)
3. 通过 `Dictionary<Thing, HashSet<Thing>>` 跟踪每个发射器已分配的目标集合 (line 15)
4. 若当前目标已被前序火箭占用, 调用 `AttackTargetFinder.BestAttackTarget` 搜索替代目标 (lines 40-44):
   - 搜索半径 = `range * 0.66`, 钳制在 `[2, 20]` 范围内 (lines 37-38)
   - 过滤条件: `NeedReachable | NeedThreat`, 排除已分配目标 (lines 42-43)
5. 若找到替代目标, 重写弹丸的 `intendedTarget` (line 48)
6. 将最终目标加入已分配集合 (lines 52-53)
7. 当 `burstShotsLeft <= 1` (最后一发) 时清除该发射器的跟踪状态, 防止内存泄漏 (lines 55-57)

**关键设计决策**:

- 搜索半径使用 `range * 0.66` 而非全射程: 导弹追踪有转向半径限制, 过远的替代目标可能超出追踪能力, 导致导弹飞偏后失效。0.66 系数保证替代目标在追踪可达范围内。
- 钳制范围 `[2, 20]`: 下限 2 防止近战距离退化为无效搜索, 上限 20 防止开阔地形下目标过于分散。

### 3.5 XML 补丁文件

```
Patches/VWEHeavyWeapons.xml
```

该文件是**纯文档桩** (documentation stub), 不包含任何实际 PatchOperation。文件仅作为索引, 引导读者查阅 C# 侧实现。所有修复逻辑均在 Harmony hooks 中完成。

### 3.6 武器耐久数值表

| 武器 | HP 扣除/射 | 默认 HP | 设计寿命 |
|:-----|:-----------|:--------|:---------|
| Autocannon | 1 | 100 | ~100 发 |
| Handheld Mortar | 20 | 100 | ~5 发 |
| Heavy Flamer | 10 | 100 | ~10 次射击 |
| Swarm Missile Launcher | 5 | 100 | ~20 发 |
| Uranium Slug Rifle | 10 | 100 | ~10 发 |

上述数值来自 VEF `HeavyWeapon` ModExtension 的默认配置。玩家可通过 VEF 设置 UI 调整滑块, `DefsAlterer` 会在启动时修改对应字段值, Postfix 在运行时读取修改后的值, 因此滑块调整始终生效。

### 3.7 追踪弹道参数

| 参数 | 值 | 说明 |
|:-----|:---|:-----|
| `homingAcceleration` | 0.15 rad/tick | 每 tick 最大转向角加速度 |
| 等效最大航向修正 | ~8.6 deg/tick | 经 8-tick 斜升后达到全追踪能力 |
| 散布阶段 | 3 tick | CE `HomingBulletTrajectoryWorker` 内置, 模拟出膛惯性 |
| 替代目标搜索半径 | `range * 0.66`, 钳制 [2, 20] | 保证替代目标在追踪可达范围内 |

---

## 4. 方案优势分析

### 4.1 耐久扣除: Postfix 读取原始字段 vs CE 内置概率损耗

最终方案直接读取 VEF 的 `weaponHitPointsDeductionOnShot` 字段, 在 CE 射击路径中执行确定性扣除。与 CE 内置 `weaponDeteriorationChance` 相比:

- **语义一致性**: VWE 的设计意图是精确倒计时 (Handheld Mortar 恰好 5 发后损坏), 而非概率事件。确定性扣除完全保留了这一设计意图。
- **配置维度一致**: VEF 的扣除值挂载在武器 Def 的 ModExtension 上, Postfix 从同一位置读取。若改用 CE 模型则需将配置迁移至弹药 Def 维度, 无法忠实还原逐武器的独立寿命设计。
- **设置 UI 兼容**: VEF 的 `DefsAlterer` 在启动时根据设置 UI 修改 `weaponHitPointsDeductionOnShot` 字段值。Postfix 读取的是同一字段的运行时值, 无需任何额外桥接逻辑即可保持 UI 滑块生效。

### 4.2 追踪系统: CE 原生 TrajectoryWorker vs VEF CompGuidedProjectile vs 自定义 ThingComp

最终方案使用 CE 的 `HomingBulletTrajectoryWorker` 和 `homingAcceleration` 字段, 利用 CE 弹道系统内置的追踪能力。

相比 VEF `CompGuidedProjectile`:

- **类型安全**: VEF 的制导组件通过 `FieldRef<Projectile, Vector3>` 操作原版弹丸, 在 CE 弹丸 (`ThingWithComps`) 上会产生类型不匹配异常。使用 CE 原生系统从根本上避免了这一问题。
- **弹道一致性**: CE 弹丸的飞行由 `TrajectoryWorker` 统一管理。通过 `forcedTrajectoryWorker` 字段指定追踪 Worker, 追踪行为与 CE 弹道系统协调工作, 不存在多系统竞争问题。

相比自定义 `ThingComp`:

- **无竞争风险**: 在 `CompTick` 中修改弹丸状态会与 `TrajectoryWorker` 的每帧计算竞争, 产生不可预测的弹丸行为。使用 CE 原生系统通过正规接口注入, 由 CE 自身调度转向计算, 不存在竞争。
- **维护成本低**: 无需维护自定义弹道计算逻辑。CE 的 `Vector3.RotateTowards` 平滑转向已经过充分测试, 直接复用可获得稳定的追踪表现。

### 4.3 架构层面

- **零 Def 引入**: 所有修复通过 Harmony hooks 完成, 不创建新的 ThingDef/AmmoSetDef/ProjectileDef。这避免了与 CE 手动补丁已有 Def 层修改产生冲突的风险。
- **条件注册**: 三个 hooks 仅在 `ModsConfig.IsActive("VanillaExpanded.VWEHW")` 返回 true 时注册, 对未安装 VWE Heavy Weapons 的用户零性能开销。
- **反射缓存**: 对 VEF 类型和字段的反射查询均在首次调用时执行并缓存 (`initialized` 标志), 后续调用直接使用缓存值, 将反射开销限制在每 hook 一次。
