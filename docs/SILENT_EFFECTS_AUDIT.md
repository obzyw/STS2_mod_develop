# 猎人三组特效：初步审计与停止记录

2026-10-23，Asia/Shanghai。**最新状态：用户明确解除一小时限制后，已完成并正式部署独立 SilentEffectsCompat。52 项出牌及 18 次伤害对照通过，最终结果见 SILENT_EFFECTS.md。** 下文停止记录是此前历史，本文件的静态发现不等于最终验收。

## 用户要求

| 项目 | 要求 |
|---|---|
| 设置 | 有效化 MeleeAttack 作者提供的开关，包括全局特效关闭 |
| 范围 | 检查全部重叠牌；谋杀、刀刃陷阱只是例子 |
| 分阶段共存 | 时间、位置完全错开的效果保留；需要审查选牌、瞄准、出牌、命中、持续状态 |
| 重叠优先级 | SilentFX > MoreSilentAnimations > MeleeAttack |
| 例外 | 华丽收场（GrandFinale）优先 MeleeAttack；这不表示强制打开玩家关闭的开关 |
| 工时 | 原一小时上限已由用户明确解除；本轮继续完成实施 |
| 安全 | 保留此前骨妹兼容、猎人位移兼容；可回滚、可迁移，不改原生战斗结算 |

## 已确认的代码事实

反编译根目录：`artifacts/decompiled/`。以下路径相对此目录，使用现有项目内 ILSpy 读取本机订阅 DLL，未联网安装新工具。

| 发现 | 直接证据 | 含义及边界 |
|---|---|---|
| 设置查询本身包含总开关 | `MeleeAttack/MeleeAttack/SettingsUI.cs:1276`，IsSkillEnabled 先查询 IsGlobalSkillsEnabled | 已读取这些帮助方法；没有把用户设置切换流程做运行验证 |
| 谋杀的攻击动画入口检查开关，但命中特效另走一条无开关路径 | `MeleeAttack/MeleeAttack/MurderEffect.cs:69`；`MeleeAttack/MeleeAttack/MurderSettlementEffect.cs:18` 的 MurderBeforeDamagePatch.Postfix | 后者生成“谋杀02.tscn”、目标染色和径向模糊；可解释关闭仍出特效，尚未用新测试重现 |
| 升级猎人牌存在通用命中特效入口 | `MeleeAttack/MeleeAttack/SimpleTeleportPatch.cs:209` 和 `:1228` | GrandFinaleDamageReceivedPatch 和 PlayGrandFinaleEffect 实际接受 Silent 池升级牌或升级小刀，并非只处理华丽收场；两处都没有设置检查 |
| 升级小刀还有独立命中特效 | `MeleeAttack/MeleeAttack/ShivUpgradedEffectPatch.cs:17` | 播放 vfx_grand_finale_impact，不查询开关；需与上条及生成小刀的牌一起检查，不能仅按类名归属 |
| 刀刃陷阱自身的加速入口有开关 | `MeleeAttack/MeleeAttack/KnifeTrapFastForward.cs` | 未确认用户观察到的额外视觉究竟来自哪条链路，不能直接宣称已找到刀刃陷阱全部根因 |
| 更多猎人动画主动压制 SilentFX 的共享牌效果 | `MoreSilentAnimations/MoreSilentAnimations/SilentFxCompatibility.cs:67`、`:128`、`:146`、`:154` | 对 CanShow 安装结果过滤，对 StartSession 安装前缀，并处理小刀等；不仅仅是加载顺序。能解释预览消失，但本轮未检查实际运行时补丁安装状态 |
| SilentFX 谋杀含预览和出牌后效果 | `SilentFX/SilentFX.Cards/MurderHeld.cs:88`；`MurderFx.cs:123` | Held 标记会在出牌时转交，不能把整个 Held 会话简单等同于“仅打出前” |
| SilentFX 刀刃陷阱含预览和出牌后效果 | `SilentFX/SilentFX.Cards/KnifeTrapHeld.cs:37`、`:63`；`KnifeTrapFx.cs:278`、`:296` | 出牌处理接收 Held 的 Trap 实例；切开两阶段可能破坏状态转交，需要针对该路径验证 |
| 华丽收场有独立的更多猎人动画入口 | `MoreSilentAnimations/MoreSilentAnimations/FinalePresentationPatch.cs:48`；MeleeAttack 的 `SequenceScreenEffect.cs` | 例外需覆盖这套动作与特效，不能仅调整普通攻击的来源 |

## 覆盖清单：不以两个例子代替全部牌

`MoreSilentAnimations/MoreSilentAnimations/SettingsState.cs:29` 列出 25 张牌。与 `SilentFX/SilentFX.Core/CardFxSettings.cs:24` 的卡牌设置名称交集，**精确结果为 21 张**（早期口头估计 22 张不准确）。这只是静态覆盖候选，尚未逐牌证明实际时间或空间重叠。

| 分类 | 卡牌代码名 |
|---|---|
| 两者设置范围共同覆盖，21 张 | LeadingStrike、PreciseCut、Finisher、EchoingSlash、Predator、Pounce、PoisonedStab、Assassinate、Dash、SuckerPunch、Backstab、Murder、Suppress、TheHunt、BubbleBubble、DeadlyPoison、Shiv、Snakebite、Tracking、KnifeTrap、StormOfSteel |
| 其余更多猎人动画登记牌，4 张 | Slice、Neutralize、BouncingFlask、GrandFinale |
| 上述 25 张之外还需检查的 MeleeAttack / SilentFX 交集候选 | BulletTime、Backflip、WraithForm、Afterimage、Pinpoint、Blur、Burst、Flechettes、DaggerThrow；本行不是完整穷举 |
| 不受固定名单限制的路径 | 升级猎人牌通用命中、小刀与生成小刀的卡牌、通用攻击缩放/位移、能力/状态持续效果 |

Tracking 在 SilentFX 有 PowerFx 路径；Shiv、LeadingStrike 有小刀专用路径。没有出现在 CardFxManager.Register<T> 中，不代表没有特效。其余 4 张不在 SilentFX 的这份设置名单，也不等于已证明无任何间接特效。

因此，**全覆盖的触发阶段/位置矩阵尚未完成**，目前不能安全地把 21 张全部禁用某一来源，也不能把剩余牌全部判定为可叠加。

## 工时判断与停止边界

完整实施预计 **2–4 小时**，是剩余工作的粗估，不是已经消耗的时间。原因是：需要追踪独立命中入口和持续状态，改写现有反向来源屏蔽，处理 Held 到 OnPlay 的状态转交，单独落实华丽收场例外，并补充真实游戏的选牌/取消/出牌/升级/多段/持续状态测试。此前只验证位移的 12 个用例不能覆盖这些要求。

按用户要求停止本计划。没有以“先只修两张牌”替代完整目标，没有宣称这批问题已修复，也没有启动新游戏测试。若将来用户明确恢复，可先缩小成“修复设置绕过”一个可独立验收的阶段；这只是后续建议，尚未启动。

## 停止时生产状态核对

本轮没有写入 Mod DLL、玩家配置、存档。仅新增反编译审计材料和本交接文档。

| 文件 | 当前 SHA256 |
|---|---|
| MeleeAttack.dll | 7BB0DE0F078D97EDFEBB0D4494B5B0A1224CF75DA940971EC6106870F4AF663B |
| MoreSilentAnimations.dll | 95F6E7A7C003FBA2B9FA1C64E470373DCBF349100BB5EB5C0173B7ED5C3D23AC |
| SilentFX.dll | F0FEF25F3E01F7827F03FA394D366511D09D13826B20689318F474622366CCC7 |
| 已部署 NecrobinderVfx.dll | 88C0BA2ABB78D8EEA9A4983972449EE3E73A3AB96B8DEE14261DA8F1F8EE1647 |
| 已部署 SilentMovementCompat.dll | 61638A911582BE0156DA41FEAD0ADEE77D6F9633C512B9A6CA2081C9C487A982 |

停止时没有 SlayTheSpire2 游戏进程。已有两组兼容保持部署状态；本轮没有新兼容需要回滚。
