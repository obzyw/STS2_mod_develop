# 观者姿态过渡 · WatcherStanceTransitions 0.1.0

2026-10-25：独立插件已部署，正常战斗验证通过。给“观者形态与美化”补齐姿态之间的起势、融合与收势；继续使用美化原人物，并与已有神格摊掌插件共存。

## 设计

| 目标姿态 | 过程 | 时长 |
|---|---|---|
| 平静 | 轻收身体，蓝色细环舒展，柔和落定 | 0.58 秒 |
| 愤怒 | 短促蓄力，红色弧光划过，微前倾收住 | 0.44 秒 |
| 神格 | 金紫双环升起，身体轻升，接上摊掌待机 | 0.68 秒 |
| 普通 | 紫色光环收敛，身体回落，接回持杖待机 | 0.52 秒 |

所有来源姿态共用目标姿态的过渡；离开神格回普通时回落略明显。旧、新骨架在过渡前半段短暂交叉淡化，以免直接换图；这段可能看见短暂的半透明轮廓重叠，结束后只保留新姿态。没有重绘原人物或新增逐骨骼变身动画。

参考 [Godot 的插值](https://docs.godotengine.org/en/stable/tutorials/math/interpolation.html)与 [Tween 缓动及避免同属性动画互相覆盖的说明](https://docs.godotengine.org/en/stable/classes/class_tween.html)。本实现直接使用 Godot 内置 `smoothstep`，无需下载转场库、引入新框架或额外纹理。

## 为什么需要独立插件

已阅读 YUWatcherForms 0.3.13 的原有实现：同姿态的血量外观切换有 0.22 秒淡化；不同姿态则直接停放旧骨架、显示新骨架。当前作者设置没有姿态转场选项。现成全屏转场着色器不适合局部人物，也无法单独处理其骨架池，因此只在原作者 `RigPool.TryActivate` 前后补充局部过渡。

`Entry.cs` 记录切换前后的原骨架；`StanceTransition.gd` 负责两者的短淡化、轻微位移/缩放与细环、星点。原神格法阵、背景眼睛、卡牌和场景特效继续由各自作者代码绘制，不替换材质，也不加全屏遮罩。

姿态结算立即生效，动画不新增等待。出牌、受击或死亡动作开始时，会结束当前过渡并交还原动作；连续切姿态先收好上一段，避免残留半透明旧人物。同姿态血量换档仍使用作者原有淡化。残血共用同一骨架且没有换图时，保留作者原有表现。

## 运行证据与边界

本次只运行一次正常战斗，未重跑此前兼容组：

| 项目 | 结果 |
|---|---|
| 普通→平静→愤怒→神格→普通 | 四次均捕获中间帧，完成后新人物可见且不透明 |
| 神格光影与新手臂 | 原光环 alpha > 0.9，新摊掌姿态有效；实际截图可见背景眼睛 |
| 神格中实际打击 | 造成 18 伤害，攻击后恢复摊掌待机 |
| 测试清理 | 六组 Rewind/Loadout 记录恢复，临时探针移除，游戏进程退出 |

证据：`../../artifacts/native-compatibility-20261025-000927/stance-transition-validation.json`、同目录八张截图与 `isolation.json`。这不代表穷举了十二个方向、所有血量组合或联网情境。

分帧预览：`../../artifacts/stance-transition-preview/preview.html`，包含美化原骨架及已安装的神格新手臂。预览独立加载角色资源，不含实际战斗中的背景眼睛、重塑/美化的场景级特效；最终游戏效果以正常战斗截图为准。

## 迁移、部署与回滚

复制整个 `STS2ModCompat` 工程，准备 .NET 9 SDK、游戏及 YUWatcherForms 0.3.13。脚本从游戏目录发现 Steam 工坊位置；无需当前电脑盘符。神格新手臂另由 WatcherDivinityPose 提供，不是此插件的必需依赖。

在本工程根目录、游戏关闭时执行：

```powershell
# 默认工程位于游戏目录下；位于其他位置时指定 -GamePath
& ./watcher/scripts/Deploy-StanceTransitions.ps1 -GamePath 'X:\SteamLibrary\steamapps\common\Slay the Spire 2'

# 仅撤销本次过渡，原美化、摊掌、卡牌特效及此前兼容插件继续保留
& ./watcher/scripts/Deploy-StanceTransitions.ps1 -GamePath 'X:\SteamLibrary\steamapps\common\Slay the Spire 2' -Rollback
```

回滚将独立的 `mods/WatcherStanceTransitions` 移到工程 `artifacts/stance-transitions-rollback-<时间>`；未修改作者 DLL、PCK、正式设置或存档。版本更新前先回滚再部署。部署脚本只接受已阅读的 YU DLL 哈希；作者更新后先确认切换入口，不能直接删除版本检查绕过。状态与哈希见 `../../artifacts/stance-transitions-deployment.json`。

若日后修改本插件，最小正常战斗验证命令为：

```powershell
& ./scripts/Test-NativeDiagnostics.ps1 -Card compatibility -Probe -StanceTransitionProbe -NativeRenderer -TimeoutSeconds 240
```

它会临时加载本项目探针并启动实际游戏，不与日常游戏同时运行。无需因交接再执行已经完成的旧测试。
