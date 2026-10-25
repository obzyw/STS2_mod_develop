# 交接与实施计划

**2026-10-25 打包交接优先说明：先读 [NEXT_SESSION.md](NEXT_SESSION.md)。稳定部署已恢复为旧四插件＋necro-v1，统一Mod试装已撤回；unified为未完成实验，不能自动部署。本次用户要求优先上传GitHub，不继续扩大整合开发或游戏测试。存档和进行中的对局未改动。仓库portable收录自有验收二进制，evidence收录精选报告；下文artifacts原始证据仅在原机器保留。**

更新日期：2026-10-25（Asia/Shanghai）。**最新为黑闪显示同步调整，用户反馈目前未再出现；观者姿态过渡及此前各组保留。** 亡灵契约师子任务 necro-v1 早已通过运行验收；该组完整范围见 VALIDATION.md。

## 当前交接检查点（优先阅读）

### 最新：2026-10-25 全屏黑闪再现，显示同步调整

用户确认只有全屏游戏闪，主菜单与选人即可发生。**已正式应用165 FPS / VSync on，保持D3D12、全屏、MSAA及所有Mod；用户随后反馈“目前未再出现黑闪”，当前方案保留并停止追加测试。不能称根因已找到或永久根治。** 先读 `DISPLAY_STABILITY.md`。本轮现场未见设备丢失/交换链失败或Display4101；主菜单顺序对比500/adaptive P95 19.387ms、165/enabled P95 6.180ms，不读回图像时测量，预热影响未排除。原生帧没有明确捕获黑块，实屏提交阶段无法由这些截图判断。后续若复发，再按新现场继续定位。

第一次长选人采样发生角色/焦点变化，最终骨架断言失败，`watcher-menu-20261025-002408` 只保留部分诊断依据。两张骤暗候选看过，是角色切换。正式改配置后短启动 `watcher-menu-20261025-002825` 成功，实际165/Enabled/Fullscreen/165.004Hz、原美化选人可见，三份偏好精确恢复到本次正式设置，临时探针归档、游戏退出。本轮没有重跑旧组。2026-10-25 用户在实屏验证问题中回复“目前未再出现黑闪”；未说明观察时长，不扩大为长期稳定性结论。

应用脚本 `scripts/Set-DisplayStability.ps1`，本机备份与撤销记录 `artifacts/display-settings-20261025-002736-000/report.json`；只改两个字段，其余设置字节保留。可用 -RollbackFrom 撤销，或复制新电脑后重新按其实际刷新率应用。诊断源码 `verification/DisplayObservation.cs`，可选入口 `watcher/scripts/Test-Selection.ps1 -DisplayObservation`，普通短菜单验证不带此开关。Windows窗口连接因kernel assets路径报错不可用，未使用其他前台自动化绕过。没有禁用Mod或修改NVIDIA/系统配置。

### 最新：2026-10-25 观者姿态过渡

**WatcherStanceTransitions 0.1.0 已独立部署。** 先读 `watcher/transitions/README.md`。美化 0.3.13 只给同姿态血量换档淡化；不同姿态直接切换，作者设置无相应开关。补丁仅钩 `RigPool.TryActivate`，旧/新原骨架短交叉淡化，按目标姿态分配蓝环舒展 0.58s、红弧蓄力 0.44s、金紫升华 0.68s、收光回落 0.52s。原模型、原光影、重塑卡牌特效和已有神格摊掌保留；动作占用时结束转场，不增加结算等待。

正常战斗仅跑一次：`artifacts/native-compatibility-20261025-000927/stance-transition-validation.json` success=true，四次目标切换均有中间帧且完成，神格原光环/新手臂有效，实际打击18伤害后恢复；看过实际截图，背景眼睛仍在。六组记录恢复，临时探针移除，游戏退出。没有重跑旧组、穷举12方向或扩大异常测试。快速原生骨架预览另有68帧和自包含 `artifacts/stance-transition-preview/preview.html`；最终预览无脚本或多边形错误。

部署哈希见 `artifacts/stance-transitions-deployment.json`，撤销 `watcher/scripts/Deploy-StanceTransitions.ps1 -Rollback` 只归档本插件，不撤销摊掌及其他兼容组。源码 `watcher/transitions/`；验证 `verification/StanceTransitionProbe.cs`；最小验证入口 `scripts/Test-NativeDiagnostics.ps1 -Card compatibility -Probe -StanceTransitionProbe -NativeRenderer`。不要仅为新会话再跑一遍已完成验证。残血共用骨架不换图的路径保留原表现。

### 最新：2026-10-24 神格摊掌姿态首版

**WatcherDivinityPose 0.1.0 独立插件已启用。** 先读 `watcher/pose/README.md`。从新绘制素材只取手臂和衣袖；原人物的脸、身体、腿、飘带与法阵保留，不用整张 AI 图盖原模型。原法杖直立悬浮，原背景眼睛继续播放。正常/半血神格 Idle 生效，出牌等动作及退出姿态回退，残血共用骨架保留原动作。原作者文件、卡面和三开关配置没改。

用户最新要求：这是普通 Mod，只需正常游戏可用，不做银行安保级别防御测试；不要扩展旧测试、穷举异常退出、反复回滚验证。最终正常战斗 `artifacts/native-compatibility-20261024-233858` 三项通过：新姿态+原光环alpha1、攻击18伤害并回到新待机、退出神格恢复。已看实际游戏截图，背景眼睛在。六组 Rewind/Loadout 恢复，临时探针已移除，游戏已退出。

部署状态 `artifacts/divinity-pose-deployment.json`；DLL SHA256 `28CC0B900DCD39FF536332B5FF0C926AD0A929D18D31BC21F78531375D42E71C`，GD脚本 SHA256 `BAD692A36243657589AA8CC18FDA8682227EAE90FF38F38040E81B240698C325`。回滚 `watcher/scripts/Deploy-DivinityPose.ps1 -Rollback` 仅归档此插件。早期原骨架拉袖试摆不合格、C#素材导出器退出异常均不作为最终通过依据；原生 GDScript 素材预览已取代此导出器，旧源码仅在 artifacts 归档。

### 最新：2026-10-24 观者作者开关与选人场景

用户已自行设置“重塑”并确认主要目标基本解决，停止无必要的整套特效移植；神格动作仍最后做。本机 `WatcherReimagined/settings.json` 为 card_art=false、motion=false、effects=true。选人场景同样受 motion 控制，修改后旧背景不会主动刷新；切换角色或重启即可重建。真实原生渲染游戏验证 `artifacts/watcher-menu-20261024-222235/menu-result.json` 成功：美化专属选人骨架，实际可见，特效仍开启。未修改任何作者 DLL/PCK，正式设置/偏好恢复且核对通过。详情及清理中断/修复记录见 `watcher/README.md`；临时测试插件已经归档移出 mods，游戏已退出。此前为新订阅设置的 donor 隔离不是最终运行方案，用户现已自行启用，不可重新禁用。

### 最新：2026-10-24 储君原设置方案

**已应用，未新增生产 DLL。** 先读 REGENT_EFFECTS.md。黑洞缺失来自万象本地开关 false，不是对方整牌屏蔽；同时恢复创世纪、创世之柱。这三张与光谱偏移保留双方，彗星/新月长矛/星灭/七星/星界脉冲使用作者逐牌开关固定万象优先。用户要求“开关→作者/网上现成工具补丁→必要时自写代码”；此次最终只改两个配置文件的八个布尔值，临时候选源码归档，不可误部署。

| 项目 | 当前依据 |
|---|---|
| 最新验证 | artifacts/native-compatibility-20261024-102544：无新兼容 DLL，10 项实际出牌通过；四张能力含真实触发，五张攻击主场景及无重复 A 场景，另含低血量黑洞；main-scene-audit.json 逐项确认主特效，不把通用粒子当主效果 |
| 配置与回滚 | artifacts/regent-settings-deployment.json；最终撤销入口 artifacts/regent-settings-20261024-102855-961/report.json，两份配置先应用、精确回滚核对哈希、再重新应用 |
| 命令 | scripts/Set-RegentEffects.ps1；撤销加 -RollbackFrom '<上述 report.json>'；脚本只改原作者设置，不处理 Mod DLL |
| 边界 | 固定逐牌开关优先级，未新增自动回退；用户日后关闭万象某张攻击，需要自行重开 A 同名牌。其他独占牌、皮肤与以前兼容组保留。临时测试插件与游戏进程已退出 |

首次测试夹具快照错误及原型扩展断言失败保留记录，不能引用为通过。最终设置方案测试已恢复两份配置、六组记录；正式应用之后的八个开关变化是交付内容，不要恢复为测试前状态。

### 最新：2026-10-24 两张猎人牌分阶段混合

**SilentEffectsCompat 0.1.1 已启用。** `docs/VISUAL_EXCEPTIONS.md` 为最新依据。刀刃陷阱预览来自 SilentFX，出牌/子小刀来自 MoreSilentAnimations；华丽收场保留 MeleeAttack 过场，复用 MoreSilentAnimations 谢幕姿势和身体占用/恢复机制，不启动其整套位移斩击。正式 DLL SHA256=`2C180C01ACD5861DC2CFB2007F71E7C6047487ECE45739EE803557AF269381C7`。当前证据为 `artifacts/visual-exceptions-deployment.json`；下面第三组的 52/200/18 属于 0.1.0 历史记录，并非本次重跑。新增普通版六用例与升级版两用例通过，后者还进入商人并打开商品界面；两个测试均恢复了三份配置和六组记录。

黑影：本机显示器 165 Hz，正式设置仍为 500 FPS/adaptive。测试仅在隔离进程比较 165/on，没有持久修改。2026-10-24 用户实玩无复现，要求只简单测试，不继续排查偶发黑影；不得写成已经找到并修复根因。不要再启动旧组回归。

### 第三组：2026-10-23 猎人开关与三方特效

**已完成并正式启用。** 用户解除了一小时上限，并在完成本次验证后要求停止重复旧成果检查。完整交接、25 张牌的阶段/来源表、构建/回滚及边界见 SILENT_EFFECTS.md。

| 项目 | 最终状态 |
|---|---|
| 独立插件 | effects/SilentEffectsCompat → mods/SilentEffectsCompat；原三方 DLL 不改 |
| 规则 | SilentFX > MoreSilentAnimations > MeleeAttack；华丽收场例外优先 MeleeAttack，仍尊重开关；刀刃陷阱追踪小刀来源 |
| 全套验证 | native-compatibility-20261023-212049：52 项实际出牌、8 组配置的 200 项卡牌路由通过；含 Guida 重做 PreciseCut 的实际三段攻击 |
| 结算对照 | native-compatibility-20261023-212521：九张牌 × 原始/候选共18次，血量差向量逐项一致；六组记录及三份配置恢复核对通过 |
| 旧成果 | 骨妹回归 212854 已通过；位移重复回归 213204 按用户要求取消，已清理并恢复记录，不继续追加旧组验证 |
| 额外发现 | Guida 的 BouncingFlask 重做本来就不生成瓶子轨迹，两边均如此；保留其战斗逻辑和 SilentFX 中毒效果 |
| 交付证据 | artifacts/effects-deployment.json；DLL SHA256=1D33242F7CEBE0577F063546AF8B0E26FFA1551C1EC0B25346D8865DA0082C95，与最终全套通过构建相同 |
| 回滚 | scripts/Deploy-SilentEffectsCompat.ps1 -Rollback；只归档第三组插件。三份玩家配置不变，临时 STS2CompatProbe 已移除，无测试游戏进程 |

失败与取消历史完整保留在 SILENT_EFFECTS.md。取消时的退出进程竞争已处理、自动归档脚本已修正；不要把取消记录当成兼容失败，也不要再按旧一小时限制停止后续任务。

### 新子任务：2026-10-23 猎人位移

**已完成并启用** More Action & Effects（3774274248/MeleeAttack）与更多猎人动画（3821353824/MoreSilentAnimations）的双角色兼容。详见 SILENT_MOVEMENT.md。独立 mods/SilentMovementCompat，不改原 DLL。原始/兼容 × 位移关/开 × 三张牌的12用例通过：原始239帧重复显现，候选244个替身可见采样帧内重复0帧，位移保留，伤害一致。证据 native-compatibility-20261023-201759。65个既有DLL一致，用户位移设置不变，骨妹 necro-v1 继续启用，测试脚本已改为恢复其进入时状态。回滚 Deploy-SilentCompat.ps1 -Rollback 已实测。当前没有测试游戏进程，可正常游玩；后续测试仍禁止和日常游戏并行。

### 最终交接状态

- 生产环境仅工坊金卡 NecrobinderVfx.dll 已替换为兼容构建；SHA256=88C0BA2ABB78D8EEA9A4983972449EE3E73A3AB96B8DEE14261DA8F1F8EE1647。原件备份不变，Deploy-NecroCompat.ps1 -Rollback 可恢复。
- 九用例候选实际运行：native-compatibility-20261022-232341；原始环境对照：native-compatibility-20261022-232650。两者成功，出牌次数及敌人血量逐项完全相同，见 artifacts/differential-validation.json。
- 五张共享牌无金卡对应实例，实际由另两 Mod 创建效果；Hang/SoulStorm/Squeeze 各一个金卡实例；骨妹死神形态同场 Hang 通过。26 个注册模型的编译后路由通过。
- 苦难三场景通过，最终自动恢复流程复测：native-misery-20261022-232939/validation.json。原生伤害/状态传播/对象清理断言通过。
- 16,336 个方法体对比仅 Initialize 和 Supported 改动，见 artifacts/build/necro-v1/surgical-diff.json。
- 最终 DLL 审计只有金卡变化，其余 63 个 DLL 一致；配置、原有存档/检查点/Loadout 记录与测试前快照一致。第三方工坊更新/遥测缓存有 5 项正常启动元数据变化，不属于玩家存档。见 artifacts/final-dll-audit.json、final-userdata-differences.json、deployment-state.json。
- 临时 mods/STS2CompatProbe 已清理，游戏测试进程已退出。无待完成的本子任务验收项；未逐牌穷尽所有能力/联机/外观场景，边界见 VALIDATION.md。
- 用户最新说明：忽略冲突牌是简便性原则，不是绝对禁令。本轮实践选择五张共享牌继续由原两 Mod 负责更简单，不新增叠加。
- 本工程可整体迁移（包括 tools/artifacts）；只搬 Git 跟踪文件不会包含工具、构建和备份。新机器使用 GamePath 参数，先检查游戏/三 Mod 版本及哈希。
- 下一组 Mod 必须先确定具体症状和覆盖范围，再建立自己的基线；不要重写本组已通过方案或为其他组改变本组来源配置。

以下均为过程历史；“待验收/已回滚”等旧描述不得覆盖以上最终状态。

### 后续推进：2026-10-22 23:20 左右

用户允许采用高效的非视觉验证。已发现并复用金卡自带 NativeDiagnostics，启动真实渲染游戏和无保存测试局，完成以下运行检查：

- `artifacts/native-misery-20261022-231258/validation.json`：PASS，三种苦难场景，原生出牌/伤害/传播/清理断言；导出真实 PNG。第三方公告遮挡画面，因此不把截图当成完整外观验收。
- `artifacts/native-compatibility-20261022-231745/compatibility-probe.json`：PASS，五张重叠牌均由后两者实际创建效果实例，金卡共享效果构造与 goldCasts 为零；Hang、SoulStorm、Squeeze 各有一个金卡实例和一次 goldCast。八张牌均恰好一个原生开始和结束事件；26 个真实注册 CardModel 的 Supported 谓词运行检查通过。
- 对应 validation.json：PASS，包括原诊断套件的效果节点/引用清理断言。
- `verification/` 是测试专用小型 Mod，替换诊断 compatibility 测试入口并观察实际对象构造；不修改生产版金卡 DLL。`scripts/Test-NativeDiagnostics.ps1 -Card compatibility -Probe` 自动构建、临时部署并在 finally 中清理它、恢复金卡原件。
- 本机 NuGet 有指向不存在 D 盘目录的全局 fallback 配置，测试工程本地 NuGet.Config 清空继承的源和 fallback；测试无 PackageReference，未下载额外包。
- 首次探针对过多第三方构造函数加观察导致 Harmony 的 ReadOnlySpan 处理报错；已缩小到实际相关的 15 个类型，复测通过。失败记录在 native-compatibility-20261022-231643，不可当通过证据。
- 部署脚本已补充两个伴随主 DLL 的哈希限制，工坊版本变化必须重新审计。

现在正在继续运行 SoulStorm 的伤害/升级/护甲/取消预览回归；测试结束自动回滚。最终根据通过的测试结果更新 README/IMPLEMENTATION 和最终部署状态，不能只引用早期“未验收”的文字。

实现细节、复现命令、限制和验收记录见 `IMPLEMENTATION.md`。`scripts/Build-NecroCompat.ps1` 构建补丁，`Deploy-NecroCompat.ps1 -Rollback` 恢复原件，`Read-ModCode.ps1` 可阅读任意订阅 DLL。

已获准下载 ILSpy 并完成项目内安装；另外复用了本机现成 Mono.Cecil 0.11.5，复制在 tools 中。原始 DLL 备份在 artifacts/backups/1C68A4699FDC0BB25BC0737FE811E9A368442A47D922F9A6B3F493DED28433AA/。

已确认根因并完成最小修补：五张重叠牌（BansheesCry、Eradicate、EndOfDays、Neurosurge、ReaperForm）让位，其他 21 个类型保留；停用 EndOfDaysPresentation、NeurosurgeRelay、ReaperFormRelay 的安装点。恢复、再次部署和重复部署已通过。

安全核对：基线中的所有 Mod DLL 在试部署时仅金卡一项发生变化；后两个实际用户配置哈希未变。回滚后金卡 SHA256 与原件完全一致。审计证据在 artifacts/dll-audit-before-rollback.json 和 dll-audit-after-rollback.json。后续再次测试先备份、试部署、验证，未完成验收时恢复原件；禁止改动其他组 Mod 以绕开本组问题。

游戏 headless 启动时同时发现三个 Mod；日志 `artifacts/headless-necro-v1-steam.log` 出现新的成功标记和原来源选择器成功标记。完整游戏画面尚未验证，不得将加载成功当成完整验收。测试进程已经关闭。

阻碍：桌面连接重置后仍报无法写入 kernel assets；B 站网页无法读取。因此下一步优先恢复桌面连接再执行 IMPLEMENTATION.md 的逐牌测试。工程代码和当前补丁可继续离线阅读，不依赖这项连接。

下方为早期基线与历史计划，若与本检查点不同，以本检查点和 IMPLEMENTATION.md 为准。

## 用户约束

中文交流；代码可跨 Windows 电脑迁移；文档支持新会话继续；优先复用已有工具与后两个 Mod 的兼容机制；金卡特效优先级最低；不无故等待确认；联网安装需询问。无需同一张牌叠加全部特效。可启动游戏调试，但避免影响现有存档。

## 阶段

| 阶段 | 工作 | 完成标准 |
|---|---|---|
| 1 基线 | 自动发现路径、枚举所有订阅 Mod、记录 DLL 哈希及配置 | 清单脚本在本机运行通过 |
| 2 分析 | 反编译三个 DLL，整理卡牌覆盖和 Harmony 补丁入口；阅读后两者兼容实现 | 找到初始化失败根因和最小修改点 |
| 3 实现 | 优先独立兼容补丁；若必须修补 DLL，严格限制版本与哈希 | 可重复构建、备份部署、回滚；静态检查通过 |
| 4 验收 | 启动游戏，检查初始化，逐类测试独占/重叠牌及极速模式 | 日志、卡牌测试表和视觉证据齐全 |

## 已确认事实

- 当前游戏 release_info.json：v0.111.0，日志 Steam 分支 public-beta。
- 工坊根位于当前 Steam 库的 steamapps/workshop/content/2868840；不要硬编码 D 盘。
- 最新基线日志 `%APPDATA%/SlayTheSpire2/logs/godot.log` 的 1154 行附近：金卡 DLL 开始初始化，动画拓展重新应用 EndOfDays 补丁，然后报 `Expected exactly one native EndOfDays missile call.`，金卡全部补丁被禁用。结尾的 `Finished mod initialization` 不代表成功，必须检查内部警告。
- 原因候选：两个改写同一 IL 调用的 transpiler 不兼容；需读 DLL 后确认，不能把加载顺序当成已验证修复。
- 工坊 `特效设置.json` 的兼容开关为 false，但不代表用户实际设置。
- 实际 `%APPDATA%/SlayTheSpire2/mod_configs/DebilitateRedKnife.animations.json` SchemaVersion=2，EndOfDays、Scourge、Neurosurge 等已选择多个来源。用户确认后两个可同时播放，应保留。
- 更多骨妹实际配置在 `mod_settings/MoreNecrobinderAnimations.json`。
- 当前安装 .NET SDK 9.0.315；尚未找到 ilspycmd。已询问是否允许项目内下载官方工具，回复前不要下载。
- 三个工坊目录当前没有源码，只有 DLL、清单、资源等。反编译所得不等于作者原始工程。

## 外部参考

- 金卡：https://www.bilibili.com/video/BV1VTJe6NEMD/
- 动画拓展：https://www.bilibili.com/video/BV1PFpb6jELf/
- 更多骨妹：https://www.bilibili.com/video/BV1LGp46bEGW/
- 工坊：https://steamcommunity.com/sharedfiles/filedetails/?id=3816316338 （已能读取作者卡牌列表）
- Harmony 官方 transpiler 说明：https://github.com/pardeike/Harmony/blob/master/Documentation/articles/patching-transpiler.md
  - 新增 transpiler 会让同一方法的 transpiler 链再次运行，因此顺序与 IL 匹配方式需检查。

三个 B 站链接网页工具均访问失败，尚未观看视频。桌面工具初次启动报 `failed to write kernel assets: 系统找不到指定的路径。 (os error 3)`；需排查连接，不能声称已验证动画。

## 下一步

1. 运行清单脚本保存基线；保留实际用户配置快照。
2. 检查本机已有反编译工具搜索结果；获得下载许可后使用项目本地 ILSpy。
3. 阅读金卡 Initialize、EndOfDays transpiler 和所有特效触发入口，以及动画拓展的 source selector。
4. 形成覆盖表后实现最小兼容方案，不直接覆盖 Steam 文件而缺少备份与回滚。
