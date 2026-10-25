# 猎人两张牌混合效果与黑影检查

交接日期：2026-10-24（Asia/Shanghai）。测试目录日期保留执行时记录的 2026-10-23。

## 当前行为

| 卡牌 | 选择/瞄准 | 出牌 | 回退 |
|---|---|---|---|
| 刀刃陷阱 KnifeTrap | SilentFX 刃雨毒潮预览 | 更多猎人动画，包括母牌自动打出的小刀；不叠加 SilentFX 出牌/命中特效 | 更多猎人动画该牌或总开关关闭时，回退原 SilentFX 链 |
| 华丽收场 GrandFinale | 保持原行为 | MeleeAttack 全屏过场＋更多猎人动画人物谢幕；不启动更多猎人动画位移/斩击 | MeleeAttack 该牌关闭后回退更多猎人动画整套；更多猎人动画关闭则只留 MeleeAttack |

独立打出的小刀仍遵循原三方优先级。其他牌的规则与前两组兼容不改。没有修改作者 DLL、游戏本体、存档或玩家的特效设置。

`Entry.cs` 在 SilentFX `CardFxManager.OnCardPlayed` 针对刀刃陷阱认领并结束 held 预览，阻止该来源出牌；仅在 KnifeTrap 异步母子调用期间屏蔽 SilentFX 小刀许可。`FinaleBow.cs` 复用作者 `DisplayLease`、`NativePose.LoadFinale/SampleFinaleGhost` 的谢幕姿势，结束或提前离场时释放 lease 恢复原身体，不复制作者动画资源，不重写骨骼渲染。

## 本次验证

使用正常 D3D12 Forward+，保留全部订阅 Mod，复用无保存战斗夹具；没有重复骨妹或旧位移组回归。

| 记录目录（artifacts 下） | 验证与结果 |
|---|---|
| native-compatibility-20261023-220436 | 旧已部署版本：帧率比较及六张牌采样，未发现结束后残留黑遮罩；用于定位，不是新效果验收 |
| native-compatibility-20261023-220846 | 新构建六张牌实际出牌通过：Slice、Murder、KnifeTrap、GrandFinale、WraithForm、Shiv。刀刃陷阱产生两个预览容器、三次 A 飞刀，无 S 出牌/小刀命中；华丽收场一个 M 过场、一个谢幕身体，无 A 整套位移，最终身体恢复 |
| native-compatibility-20261023-221150 | 两张升级牌通过：刀刃陷阱三刀总伤害 18；华丽收场每敌人 75。随后进入商人并打开商品界面，采样未见异常全屏黑影。该数字只是夹具实际结果，不冒充本次伤害 A/B 对照 |

后两目录的 `effects-validation.json`、PNG、`isolation.json` 为证据。三份配置与六组 Rewind/Loadout 目录恢复匹配，临时插件已清理。新 DLL 的 SHA256 为 `2C180C01ACD5861DC2CFB2007F71E7C6047487ECE45739EE803557AF269381C7`，与验收候选相同。旧 52/200/18 套件断言已同步例外规则，但按用户要求没有重跑，不能将其历史结果算作新构建测试数量。

## 黑影结论和停止边界

本机显示器 165 Hz，正式游戏设置始终保持 500 FPS、adaptive VSync。兼容生产代码没有 FPS/VSync 设置。诊断夹具原有 60 FPS/off 只影响测试进程；本次帧率比较也只在测试进程内切换，没有写入玩家设置。

首轮干净待机比较：500/adaptive 帧间隔中位 5.114 ms、95 分位 7.015 ms；165/on 为 6.062 ms、6.316 ms。GPU 读回和 PNG 保存会严重降低诊断采样时的帧率，不能把截图阶段的 FPS 当正常游戏性能。采样没有稳定复现用户所说黑闪，不能把帧间隔改善称为黑影根因或已修复。2026-10-24 用户自行游玩未再发现，要求只简单检查并转后两项，因此停止扩展，不改显示设置。

## 部署、回滚和迁移

从工程目录执行，路径自动推导；另一台电脑可传 `-GamePath`。保留相同目标游戏/三方 DLL 的哈希门禁，没有新增依赖。

```powershell
# 本次范围验证：两张升级牌＋商人；临时替换已部署插件，finally 恢复原版本。
./scripts/Test-NativeDiagnostics.ps1 -Card compatibility -Cases visual-confirm -Probe -EffectsProbe -TrialEffects -NativeRenderer
# 已验收构建更新：先归档已部署版，再部署源码构建。
./scripts/Deploy-SilentEffectsCompat.ps1 -Rollback
./scripts/Deploy-SilentEffectsCompat.ps1
# 完全停用猎人三方特效兼容（保留其他两组、原作者 Mod）。
./scripts/Deploy-SilentEffectsCompat.ps1 -Rollback
```

只撤销这次两张牌新例外、恢复 0.1.0：先执行上一条回滚，再将 `artifacts/before-visual-fixes-20261023/installed` 整个目录复制为游戏目录的 `mods/SilentEffectsCompat`。该备份含旧 DLL 与 manifest，旧 DLL SHA256=`1D33242F7CEBE0577F063546AF8B0E26FFA1551C1EC0B25346D8865DA0082C95`。不要直接以当前源码重新构建来恢复旧行为。所有部署/回滚均要求先关闭游戏。

当前部署及通过测试的哈希关联见 `artifacts/visual-exceptions-deployment.json`。迁移时整体保留工程 tools/artifacts，这些本地备份通常不跟随 Git。
