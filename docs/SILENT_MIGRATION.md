# 当前猎人配置与迁移入口

2026-10-27。此前换电脑后的问题不是只缺一份开关文件：本机缺少旧 SilentMovementCompat 和 SilentEffectsCompat，只有作者 Mod 与新增特效。作者 Mod 订阅不会同步本工程的自有兼容 DLL。

本次优先检查开关：MoreSilentAnimations 总开关为 true；仅 StormOfSteel、Tracking 被关闭，保留这两个选择。恢复经过当前0.1.33适配的 SilentMovementCompat，阻止 MeleeAttack 在位移开关关闭时仍重置猎人本体透明度。随后恢复完整 SilentEffectsCompat 0.1.2：按实际播放注册、手牌预览和能力效果分域，冲突时 SilentFX > MoreSilentAnimations > MeleeAttack；尊重作者游戏内开关，上级关闭后回退。刀刃陷阱和华丽收场旧例外包含在完整模块，独立 KnifeTrapStages 已撤回归档，避免双重接管。

## 换电脑恢复

下载完整项目，订阅作者的 MoreSilentAnimations、MeleeAttack、SilentFX，正常关闭游戏。安装 .NET 9，然后运行：

```powershell
./scripts/Restore-SilentSetup.ps1 -GamePath 'X:\SteamLibrary\steamapps\common\Slay the Spire 2'
```

此入口安装当前位移＋完整特效两项猎人兼容，撤回冗余的独立 KnifeTrapStages，并恢复 `profiles/silent-setup.json` 的作者视觉设置；替换前备份当地设置到 artifacts。不依赖骨妹插件或其旧离线补丁，不部署 unified，不改存档。若希望修改以后迁移时使用的单卡开关，编辑该项目内的配置文件。

只检查安装与总开关：

```powershell
./scripts/Restore-SilentSetup.ps1 -GamePath 'X:\SteamLibrary\steamapps\common\Slay the Spire 2' -CheckOnly
```

缺少兼容 DLL、安装旧清单版本或关闭总开关会明确报错。原作者更新后仍可能需要重新适配，不能保证任意未来版本永久兼容。当前恢复的是猎人位移与完整三方视觉路由；不是整个工程所有角色的一键整合。旧 `Install-Portable.ps1` 是旧版本快照入口，不能替代本页的当前猎人恢复入口。
