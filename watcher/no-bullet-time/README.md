# 观者：关闭全局子弹时间

用户反馈诸神之黄昏后整款游戏进入慢动作，要求取消子弹时间。此模块依赖观者·重塑，仅在内存中拦截 `WatcherMod.Vfx.Fx.BulletTime(double,int)`，返回已完成的 Task，不启动全局减速、计时器或恢复钩子。重塑的其他卡牌调用同一入口，也一起关闭该减速。

保留落雷、闪屏、震屏、粒子及音效；不改卡牌伤害和段数，不强制每帧锁定 Engine.TimeScale，不改 SpeedX 设置或作者文件。作者目前只有卡面、动作、特效三类开关，没有单独的子弹时间开关；本机 SpeedX 的 hitStopDisabled=true 也没有阻止此入口。

若加载了动作与特效 MeleeAttack，还使用作者现有 `HitStopController.ExcludedHitStopCards` 排除名单，在内存中加入 WATCHER_RAGNAROK，关闭这张升级牌每一段攻击附加的全局停顿。其他牌的动作与特效规则保留。游戏重启且撤回本插件后，作者内存名单自然恢复。

## 安装与回滚

正常关闭游戏，不必放弃进行中的一局。需要 .NET 9 和本人订阅的 WatcherReimagined。

```powershell
./scripts/Deploy-WatcherNoBulletTime.ps1 -GamePath 'X:\SteamLibrary\steamapps\common\Slay the Spire 2'
./scripts/Deploy-WatcherNoBulletTime.ps1 -GamePath 'X:\SteamLibrary\steamapps\common\Slay the Spire 2' -Rollback
```

目标版本：游戏 v0.111.0，WatcherReimagined 1.5.4。若作者 API 改变，初始化会明确报错。

## 必要验证

使用 `scripts/Test-ElementalVfx.ps1 -Feature WatcherTime -Characters Watcher` 的无保存夹具；与日常游戏互斥。测试结束移除临时探针、恢复原记录，并核对哈希。不将其他 Mod 的退出警告算作完整兼容通过。

首次 205625：直接嵌套减速调用在 1/5 倍下不再改速度；普通诸神之黄昏通过，但后续升级版仍检测到速度变化，整批失败。205805 临时 setter 追踪确认写入来自 MeleeAttack.HitStop.Apply，调用者为 UpgradeCardHitStopPatch.Postfix，倍率反复在 0 和 1 之间变化。两套全局时间控制器交叠后有把对方临时倍率当恢复值的风险；没有重演用户原局，不将其表述成对原现场的完整复现。已利用作者名单排除该牌，最终验证结果待追加。

最终 210002 批次通过，已安装：

| 检查 | 结果 |
|---|---|
| 普通/升级诸神之黄昏，1倍连续出牌 | 分别281/294帧，倍率始终1；重塑减速调用被拦截 |
| 普通/升级诸神之黄昏，5倍连续出牌 | 分别309/305帧，倍率始终5；重塑减速调用被拦截 |
| 原生伤害 | 普通25，升级36 |
| 嵌套减速请求 | 1/5倍各128/129帧，任务立即完成，速度保持原值 |
| setter 追踪 | 仅夹具主动设置倍率，没有其他代码写入全局倍率 |
| 清理 | 原生退出0；1346个原记录文件恢复并核对；移除临时探针 |

精选证据见 `evidence/watcher-no-bullet-time-20261026.json`。未单独测试菜单退场动画或所有观者卡牌；多 Mod 原有初始化/退出警告不计为本修复的验收。没有测试或改动进行中的存档。
