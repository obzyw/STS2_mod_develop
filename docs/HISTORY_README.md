# 杀戮尖塔 2 Mod 兼容工程

本文件是换电脑、新会话的第一入口。项目可整个复制，不依赖当前电脑盘符。

## 当前目标

2026-10-25 最新显示问题：正式配置由500/adaptive改为165/on，其他选项及全部Mod保留；新启动确认实际165 FPS / VSync Enabled / 全屏及美化选人可见。用户随后实屏确认“目前未再出现黑闪”，保留当前方案，停止追加测试；未直接复现根因，不宣称永久根治。见 `docs/DISPLAY_STABILITY.md`，回滚入口 `scripts/Set-DisplayStability.ps1 -RollbackFrom '<本机report.json>'`。

2026-10-25 最新：**观者姿态过渡已启用**（独立 WatcherStanceTransitions 0.1.0）。为平静/愤怒/神格/普通添加各自的起势、短淡化与收势，0.44～0.68 秒，与结算并行。一次正常战斗四次切换及神格实际攻击通过，已有摊掌和原光影保留。见 `watcher/transitions/README.md`；预览 `artifacts/stance-transition-preview/preview.html`；回滚 `watcher/scripts/Deploy-StanceTransitions.ps1 -Rollback`。

2026-10-24 最新：**神格摊掌首版已启用**（独立 WatcherDivinityPose 0.1.0）。只补新手臂/衣袖，脸、躯干、腿及原动态光影继续由美化绘制；法杖旁侧悬浮。普通神格→实际攻击→退出神格三项通过，原光环和眼睛背景可见。正常/半血覆盖，残血共用骨架保留原动作。按用户要求停止穷举式检查。见 `watcher/pose/README.md`；回滚 `watcher/scripts/Deploy-DivinityPose.ps1 -Rollback`。

2026-10-24 观者作者开关：用户已用“重塑”作者开关解决主要需求，无需完整移植。当前卡面关、人物动作关、特效开。选人场景也由人物动作开关控制；在已经创建背景后关闭开关，需要切换角色重建背景或完整重启。真实游戏菜单验证已确认重启后加载 YUWatcherForms 专属选人骨架且可见，未修改作者 DLL/PCK。见 `watcher/README.md`；神格新动作和转场随后另行完成，以上方最新记录为准。

2026-10-24 最新：储君组已通过**作者原有设置**完成，不新增生产 DLL。恢复万象辉星被关闭的黑洞、创世纪、创世之柱；这三张及光谱偏移与 RegentAnimations 共存。五张共享攻击按万象优先，关闭 RegentAnimations 对应逐牌开关。10 项实际出牌及精确配置回滚通过；见 `docs/REGENT_EFFECTS.md`、`artifacts/regent-settings-deployment.json`。应用/回滚脚本 `scripts/Set-RegentEffects.ps1`，前面三组保留。用户要求以后先查开关与现成方案，再考虑自写兼容代码。

2026-10-24：独立猎人兼容插件更新至 **0.1.1**。刀刃陷阱改为 SilentFX 瞄准预览＋更多猎人动画出牌；华丽收场改为 MeleeAttack 过场＋更多猎人动画人物谢幕。普通及升级版实际出牌均通过。详见 `docs/VISUAL_EXCEPTIONS.md`（优先于下方历史记录）。黑影没有稳定复现，用户实玩也未再遇到，按要求停止扩展测试；正式 FPS/VSync 未修改。

2026-10-23：第三组“猎人开关与三方特效”已完成并启用，独立 `mods/SilentEffectsCompat`，不修改三个作者 DLL。修复开关绕过，按 SilentFX > 更多猎人动画 > MeleeAttack 选择重叠播放来源；华丽收场优先 MeleeAttack，刀刃陷阱覆盖母子牌来源。52 项实际出牌、200 项来源检查和18次伤害对照通过。详情见 `docs/SILENT_EFFECTS.md`，回滚 `scripts/Deploy-SilentEffectsCompat.ps1 -Rollback`。此前两组保持启用。用户要求不再重复旧成果回归，本轮已停止这类扩展检查；测试进程及临时插件已退出/移除，可正常游玩。

2026-10-23：第二组猎人位移兼容已完成并启用，详见 `docs/SILENT_MOVEMENT.md`。独立兼容 Mod 由后者负责猎人攻击位移，12用例通过；不修改两个原 DLL。回滚用 `scripts/Deploy-SilentCompat.ps1 -Rollback`。第一组骨妹兼容仍启用。

先兼容三个亡灵契约师特效 Mod；未来按具体冲突逐组扩展，不预先开发通用框架。

| 工坊 ID | Mod ID | 用途 |
|---|---|---|
| 3821345574 | NecrobinderVfx | 更多金卡特效，冲突时优先级最低 |
| 3816255707 | DebilitateRedKnife | 卡牌动画拓展，保留现有兼容 |
| 3816316338 | MoreNecrobinderAnimations | 更多骨妹动画，保留现有兼容 |

验收：金卡特效初始化成功；三个 Mod 共存；重叠卡牌让位给后两者，独占卡牌保留金卡效果；不改变原生战斗结算。暂不要求同牌三套叠加。

## 目录与迁移

- `scripts/`：发现环境、清单、构建和部署脚本。
- `docs/`：计划、分析与交接状态。
- `artifacts/`：本机证据、清单、验证结果；可能含本机绝对路径，不作为代码输入。
- `tools/`：项目本地工具（获准后下载），不要求全局安装。

换电脑先安装 Steam、订阅目标 Mod，准备 .NET SDK，再运行：

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Inspect-Mods.ps1
# 自动发现失败时显式提供本机路径，不改代码：
powershell -ExecutionPolicy Bypass -File .\scripts\Inspect-Mods.ps1 -GamePath 'X:\SteamLibrary\steamapps\common\Slay the Spire 2'
```

第三方 DLL、反编译文件仅供本地分析，不默认获得再分发许可。不要将游戏文件、用户存档或完整第三方代码公开发布。

当前：亡灵契约师子任务 necro-v1 已通过实际游戏运行验收，并正式启用。五张共享牌保留原有来源；金卡恢复加载，继续覆盖其余 21 个类型。用户接受运行断言代替逐帧视频对照。验收范围与证据见 `docs/VALIDATION.md`，新会话先读 `docs/HANDOFF.md`。

仅金卡 DLL 有生产改动，其余 63 个 DLL 未变。原件、配置和存档记录已核验；测试 Mod 已移除。

回滚（在本工程目录执行，先关闭游戏）：

```powershell
powershell -ExecutionPolicy Bypass -File ./scripts/Deploy-NecroCompat.ps1 -Rollback
```

工程不在游戏目录时加 `-GamePath '<本机游戏目录>'`。构建、部署和版本限制详见 `docs/IMPLEMENTATION.md`。
