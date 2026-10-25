# 观者：作者开关方案与选人界面

2026-10-25 最新：**姿态过渡插件已启用**，见 `transitions/README.md`。四种姿态按目标分别加入蓝环舒展、红弧蓄力、金紫升华与收光回落，约 0.44～0.68 秒；短淡化衔接原人物，不阻塞结算。一次正常战斗四次切换及神格攻击通过，原光环、眼睛背景和已有摊掌动作保留。独立回滚 `scripts/Deploy-StanceTransitions.ps1 -Rollback`。

2026-10-24 最新：**神格摊掌姿态首版已启用**，见 `pose/README.md`。原美化模型只补新手臂/衣袖，保留脸、身体、原神格法阵、背景眼睛及现有特效；法杖独立直立悬浮。正常/半血神格待机已覆盖，残血共用骨架保留原动作。一次正常战斗三项验证通过，不继续扩展防御测试。独立 Mod，可通过 `scripts/Deploy-DivinityPose.ps1 -Rollback` 撤销。

用户此前已自行使用“重塑”的三个作者开关，确认最初的特效移植需求基本解决。此方案继续保留，不创建整套特效移植 Mod。以下为作者开关与选人场景说明。

| 选项 | 当前值 | 用途 |
|---|---|---|
| 观者：重制卡面 | 关闭 | 保留原有卡面 |
| 观者：重制人物动作 | 关闭 | 保留原有人物；此项同时控制选人场景、商人、休息点等画面覆盖 |
| 观者：重制特效 | 开启 | 使用重塑的特效 |

作者配置为 `user://WatcherReimagined/settings.json`，Windows 通常对应 `%APPDATA%/SlayTheSpire2/WatcherReimagined/settings.json`。本轮读取到 `card_art=false, motion=false, effects=true`，不需要新增第四个设置。

## 选人界面残留的判断

旧游戏日志显示这次启动时三个开关全开，后来才关闭卡面、人物动作。重塑的 `BoninallScenes.Active` 在动作关闭且原版骨架可用时返回 false，停止接管原观者的选人画面；但设置变更没有重新创建当前背景的处理，仅卡面监听了 `WatcherSettings.Changed`。

因此，改开关后先切换到其他角色，再切回观者；或者完整退出并重启游戏。原生 `NCharacterSelectScreen.SelectCharacter` 会移除旧背景、创建新的背景，美化的 `SelectionArt` 随后应用其选人骨架。不要为一次未刷新的背景改写作者 DLL。

若重启后仍异常，需要检查 `MotionAvailable`（原版骨架加载失败会令动作覆盖被强制开启）、皮肤管理器当前是否选中 YUWatcherForms，以及实际背景骨架路径，不能只凭初始化日志判断画面正确。

## 只读检查与恢复

`scripts/Test-Selection.ps1 -GamePath '<游戏目录>'` 使用临时测试插件启动真实原生渲染，仅进入选人界面，不出发、不放弃现有对局、不解锁角色。断言 WatcherSpine 实际资源为：

`res://YUWatcherForms/animations/character_select/watcher/characterselect_watcher_skel_data.tres`

同时要求人物实际可见、卡面/动作关闭、特效开启。证据在 `artifacts/watcher-menu-<时间>/`；测试退出后将临时插件移出游戏目录并归档，精确恢复进入时已有的原生设置和角色偏好。该检查不能证明 86 张牌的特效、音效或冲突优先级全部正确。

本机实际验证：`artifacts/watcher-menu-20261024-222235/menu-result.json` 的 `success=true`，骨架路径与上面完全一致，人物可见，另有原生渲染 `selection.png`。三份原生设置/角色偏好已精确恢复，见 `restored.json`；测试插件归档于同目录 `probe-not-deployed`，不在游戏 `mods` 下。第一次退出时 DLL 短暂占用导致清理脚本中断，随后手动完成恢复与归档；脚本已改为先恢复偏好、等待退出，再重试归档，不能把这次运行描述为原始脚本全流程无错误。

`scripts/Set-DonorIsolation.ps1` 是前一阶段防止新订阅自动覆盖的工具，不是最终运行方案；用户后来已经自行启用重塑并配置开关，不可再次无故禁用或恢复到初次订阅前的设置。

未修改任何作者 DLL、PCK 或正式配置。新增人物动作与转场分别在独立 WatcherDivinityPose、WatcherStanceTransitions 中。四套原始文件备份、素材与生成过程见 `assets/divinity/v001/README.md`。
