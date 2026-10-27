# 刀刃陷阱分阶段兼容

2026-10-27后续：本机已撤回此独立模块，完整SilentEffectsCompat 0.1.2已包含同一例外。当前迁移请使用Restore-SilentSetup.ps1，不再单独安装本模块。以下保留局部修复历史。

本机缺少旧 SilentEffectsCompat；MoreSilentAnimations 已更新到0.1.33，旧兼容包的0.1.31版本哈希限制不能直接放行。当前作者默认兼容按整张牌屏蔽SilentFX，连选牌预览也被关闭。不是仅靠调整两方单卡开关就能实现分阶段。

KnifeTrapStages 仅恢复刀刃陷阱的既定例外：选择/瞄准用刃雨毒潮，出牌及其自动小刀用更多猎人动画。出牌前认领并结束预览，母牌异步调用期间屏蔽SilentFX子小刀及较低优先级的部分MeleeAttack视觉入口。独立小刀不改规则；更多猎人动画该牌或总开关关闭时回退SilentFX。读取作者开关，不写作者DLL、配置或存档。没有恢复旧整套猎人兼容或华丽收场等其他例外。

目标：游戏v0.111.0、SilentFX 0.72.15、MoreSilentAnimations 0.1.33。本机已安装。

```powershell
./scripts/Deploy-KnifeTrapStages.ps1 -GamePath 'X:\SteamLibrary\steamapps\common\Slay the Spire 2'
./scripts/Deploy-KnifeTrapStages.ps1 -GamePath 'X:\SteamLibrary\steamapps\common\Slay the Spire 2' -Rollback
```

必要验证已完成（224406批次）：普通/升级各出现真实KnifeTrapHeld预览，三次KnifeTrapVisual，SilentFX出牌/命中零叠加，伤害12/18。关闭更多猎人动画时回退SilentFX出牌与三次命中，伤害12。独立小刀许可不受母牌上下文泄漏影响。原生退出0，1349个原记录恢复，探针移除。没有重跑旧整组或真实多人测试。详见 evidence/knife-trap-stages-20261027.json。
