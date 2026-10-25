# 猎人三方特效与开关兼容

2026-10-24，Asia/Shanghai。**当前为 0.1.1；两张牌的新例外与增量验收详见 VISUAL_EXCEPTIONS.md。** 下方 52 用例、200 项来源及 18 次伤害对照为 0.1.0 的历史通过记录，本次没有重跑。初步审计及早期停止历史见 SILENT_EFFECTS_AUDIT.md。

## 规则与实际行为

| 项目 | 实现 |
|---|---|
| 普通重叠 | SilentFX > 更多猎人动画 > 动作与特效（MeleeAttack） |
| 华丽收场 GrandFinale | MeleeAttack 负责过场，MoreSilentAnimations 负责人物谢幕；前者关闭后回退后者整套效果，仍尊重各自开关 |
| 分阶段 | 恢复 SilentFX 的拖牌、瞄准预览；其实际出牌/命中/能力效果与其他来源重叠时，按优先级让位 |
| 独立作用域 | 不把所有设置名单当成出牌特效名单；手牌 UI、后续中毒状态与不同卡牌动作保留原有协作 |
| 上级来源关闭 | 查询三方当前内存配置；SilentFX 总开关或单卡关闭后，更多猎人动画可接管；两者均关闭后，允许打开了对应开关的 MeleeAttack |
| 刀刃陷阱 | SilentFX 负责选择预览，MoreSilentAnimations 负责出牌及其自动小刀；后者关闭时回退原 SilentFX 链。独立打出的小刀优先级不变 |
| 猎人位移 | 保留上一组兼容；仅由更多猎人动画负责的牌仍可位移。被 SilentFX 接管出牌阶段的共享牌让出更多猎人动画，包括相应动作 |
| 战斗逻辑 | 不改伤害、状态、抽牌或目标选择；仅限制三方视觉入口及读取卡牌调用上下文 |

SilentFX 的 MurderHeld/KnifeTrapHeld 都有预览到出牌的状态转交。谋杀仍保留完整 SilentFX 链；刀刃陷阱按用户新例外，在接管出牌前认领并结束预览实例，再交给 MoreSilentAnimations，避免预览实例泄漏或两套出牌特效重叠。

## 逐牌覆盖

以下使用类型名，方便跨语言和反编译文件核对。S=SilentFX，A=更多猎人动画，M=MeleeAttack。表格表示三方相关选项均打开时；主来源关闭可按上述规则回退。

| 卡牌类型 | 已审计的作用域 | 选择 |
|---|---|---|
| LeadingStrike | S 预览、生成返手小刀；A 攻击与回刀 | S |
| Slice | A 攻击；M 切割视口、升级命中通用特效 | A |
| PreciseCut | S 预览、连锁出牌；A 攻击 | S；测试补充 Guida 重做需要的可弃非技能牌 |
| Finisher | S 预览、出牌和多段命中；A 攻击 | S |
| EchoingSlash | S 出牌及命中；A 多目标斩击 | S |
| Predator | S 命中；A 攻击 | S |
| Pounce | S 出牌、命中；A 攻击 | S |
| PoisonedStab | S 预览、出牌/命中；A 攻击 | S |
| Neutralize | A 攻击；M 通用升级命中 | A |
| Assassinate | S 预览、出牌/命中；A 攻击 | S |
| Dash | S 出牌/命中；A 攻击 | S |
| SuckerPunch | S 出牌/命中；A 攻击 | S |
| Backstab | S 预览、出牌/命中；A 攻击 | S |
| Murder | S 预览、出牌、命中；A 动作；M 专属命中及通用命中 | S |
| Suppress | S 预览、出牌/命中；A 攻击 | S |
| TheHunt | S 预览、出牌/命中及能力效果；A 攻击 | S |
| BubbleBubble | S 预览、出牌；A 投掷/落点 | S |
| DeadlyPoison | S 预览、出牌及后续中毒；A 投掷/落点 | S |
| BouncingFlask | A 依赖原生瓶子；S 后续中毒 | 保留两作用域，当前 Guida 重做不生成瓶子，见下文 |
| Shiv | S 预览、投掷/命中；A 小刀动作/投掷；M 升级冲击、击退/染色 | S；同时遵循刀刃陷阱母牌上下文 |
| GrandFinale | A 动作/斩击；M 专属全屏场景/羽毛 | M 过场＋A 谢幕动作；不启动 A 位移斩击 |
| Snakebite | S 预览、出牌及中毒；A 专属动作 | S |
| Tracking | S 能力起始与标记；A 出牌标记 | S |
| KnifeTrap | S 预览到出牌转交、生成小刀；A 生成小刀；M 加速/小刀染色/升级命中 | S 预览＋A 出牌及子小刀（用户新例外） |
| StormOfSteel | S 预览、出牌和小刀生成；A 专属出牌/小刀表现 | S |

此外检查并实际出牌：BulletTime、Backflip、WraithForm、Afterimage、Pinpoint、Blur、Burst、Flechettes、DaggerThrow。前八张的相关播放/能力路径由 S 接管；DaggerThrow 在 S 中属于手牌处理而不是 CardFx 播放注册，故不据此阻止 M 的普通小刀动作，避免整牌误禁。

## 最小修补点

生产文件：`effects/Entry.cs`、`effects/FinaleBow.cs`、`SilentEffectsCompat.csproj`、`SilentEffectsCompat.json`，无新增外部包。

1. 读取 S 的 CardFxManager.Map、ShivFx.Styles、PowerFxManager.Factories，共得到 76 个播放来源；与 A 的 25 张登记牌独立核对，交集为 21 张。保留 S 作者的设置许可检查。
2. 对 A 的 AnimationSettings.Allows(string) 追加来源许可；其 SilentFxCompatibility.Owns 返回 false，解除原来覆盖预览在内的整牌否决。保留作者已有的移动锚点、虚体染色同步，不卸载整套兼容逻辑。
3. 对 M 的 IsSkillEnabled 追加共享牌优先级。为缺少开关查询的谋杀命中、通用升级命中、小刀、染色、击退、特殊动画、缩放等独立入口增加许可；不屏蔽其清理、状态复位钩子。
4. 追踪原生 OnPlayWrapper 的 AsyncLocal 卡牌链，用于刀刃陷阱自动打出小刀的来源识别，不替换卡牌本体实现。对返回 bool 的 M 原有 Harmony 前缀，退出时正确返回 true，让原生动画继续。
5. 按游戏 DLL 及三方 DLL 的 SHA256 限制适用版本。初始化出错只卸载本插件的 Harmony 补丁，不留下半套修改。

## 已查明的第四方影响

Guida 卡牌拓展的 `GuidasExpand.GuidasExpandCode.Patches.SilentBalanceComplex.BouncingFlaskOnPlayPrefix` 返回 false，替换原生出牌；自定义实现只重复施加中毒，不调用 NItemThrowVfx.Create。因此原始环境与候选中都没有瓶子轨迹，S 的中毒节点都存在。本轮保留 Guida 的玩法重做，不为补动画恢复原生出牌，避免改动其次数/目标/结算。

同一类还重做 PreciseCut，需要手牌中存在可弃的非技能牌才实际攻击。验证夹具已按这项前提补充卡牌，不把空手导致零伤害误判为兼容破坏。

证据：各测试目录的 effect-patch-map.json；本机反编译 `artifacts/decompiled/GuidasExpand/GuidasExpand.GuidasExpandCode.Patches.SilentBalanceComplex.decompiled.cs`。这两点来自实际订阅 DLL，不是视频推断。

## 构建、试验、部署及回滚

在 STS2ModCompat 目录执行；迁移电脑可加 `-GamePath '<本机游戏目录>'`。依赖项目本地现有 .NET 9 与游戏自带 Harmony/Godot，无联网安装。

```powershell
# 52 个实际出牌用例，8 组配置共 200 项卡牌来源检查；试部署在 finally 中归档退出。
./scripts/Test-NativeDiagnostics.ps1 -Card compatibility -Probe -EffectsProbe -TimeoutSeconds 900
# 九张牌，各以原始/候选运行，比较真实敌人血量差。
./scripts/Test-NativeDiagnostics.ps1 -Card compatibility -Probe -EffectsProbe -Cases effects-differential -TimeoutSeconds 900
# 可选：保持第三组试部署，回归骨妹九用例。用户当前不要求重复旧成果回归。
./scripts/Test-NativeDiagnostics.ps1 -Card compatibility -Probe -EffectsCompat
# 正式启用（必须先通过验收）
./scripts/Deploy-SilentEffectsCompat.ps1
# 关闭第三组兼容；整个插件归档保留，不删除作者 Mod 或前两组兼容。
./scripts/Deploy-SilentEffectsCompat.ps1 -Rollback
```

退出所有日常游戏后测试；测试与日常游玩不能同时进行。测试只在内存切换选项，不持久修改玩家偏好；脚本另外备份三份配置，并备份/恢复 Rewind、Loadout 记录。无保存诊断禁止原生保存和重放写入。工具、第三方反编译文件、原始备份与证据都在 tools/artifacts，换电脑应整体复制，本地审计材料不默认公开分发。

部署位置为独立 `mods/SilentEffectsCompat`；不覆盖任何工坊 DLL。再次部署相同构建无操作；更新本插件前先回滚。游戏或作者 Mod 更新后拒绝应用旧补丁，需要重新审计。

当前 0.1.1 DLL SHA256：`2C180C01ACD5861DC2CFB2007F71E7C6047487ECE45739EE803557AF269381C7`。当前部署证据为 `artifacts/visual-exceptions-deployment.json`；`artifacts/effects-deployment.json` 保留 0.1.0 历史验收。测试脚本新增 `-TrialEffects` 可临时替换已安装插件，结束时恢复原 DLL；`-UseInstalledEffects` 仅观察现有版本，`-NativeRenderer` 使用游戏默认 D3D12 而非旧 GL 测试配置。

## 验证记录与边界

| 记录 | 状态 |
|---|---|
| native-compatibility-20261023-210329 | 失败：虚体回调名称错误；所有自身补丁自动撤销，已修正 |
| native-compatibility-20261023-210502 | 失败：夹具预期有瓶子轨迹，后查明 Guida 已替换出牌 |
| native-compatibility-20261023-210929 | 失败：夹具错误要求未登记的 DaggerThrow 具有 A 动画；已限制断言范围 |
| native-compatibility-20261023-211455 | 首次 52 用例全部通过，含 200 项卡牌配置路由；PNG 已人工查看 |
| native-compatibility-20261023-212049 | 最终构建 52 用例全部通过；含 Guida 重做 PreciseCut 的三段实际攻击 |
| native-compatibility-20261023-212521 | 九张牌各原始/候选，18 次实际出牌，伤害向量逐项一致；六组检查点/Loadout目录及三份配置恢复核对通过 |
| native-compatibility-20261023-212854 | 骨妹九用例回归通过，新插件共存；六组记录及三份配置恢复核对通过 |
| native-compatibility-20261023-213204 | 用户要求停止重复旧成果检查，位移回归已取消，不作为验收条件；临时插件和记录已清理/恢复，见 cancellation.json |

取消时进程正退出，部署脚本的进程存在性检查曾短暂阻止自动归档；已等待该进程退出并完成恢复，脚本已改为只拒绝仍在运行的进程。未继续扩展旧成果的验证。旧 SilentProbe 的三牌假设对应上一组来源；当前新优先级接管部分牌，不把该旧套件直接作为第三组验收。

验收覆盖单机真实渲染、拖牌瞄准的真实 HeldSession、出牌/命中/能力节点与具体来源调用；不是仅检查启动日志。运行测试不等于逐帧复刻 B 站视频。尚未穷尽联机、所有皮肤、跨牌持续能力组合和所有升级变体；设置关闭会阻止新触发，已经播放的节点仍按作者的清理周期结束。
