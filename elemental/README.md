# 火焰屏障与岩石铠甲（开发中）

独立的 ElementalDefenseVfx 视觉插件，面向游戏 v0.111.0。没有接入 unified，也没有改变旧四插件。画面仍待用户验收，不是工坊正式发行版。

| 卡牌 | 选牌 | 出牌与持续 | 结束 |
|---|---|---|---|
| 火焰屏障 | 持牌者周围的火焰流向内汇聚 | 朝敌人的一侧，在人物前方留出距离形成火墙；每次原生反伤回调向实际攻击者射出一道火焰 | FlameBarrierPower 移除后淡出 |
| 岩石铠甲 | 灰蓝岩片环绕实际持牌者 | 岩片汇入身体，人物原有胸腹与四肢网格显示岩甲纹理，随骨骼动作和 PlatingPower 持续 | PlatingPower 移除后淡出，归还原材质 |

选择、拖拽、取消和自动出牌均使用实际卡牌持有者。完全格挡的攻击仍触发反弹，多段攻击逐段触发；非攻击伤害不触发。不改伤害、数值、存档或结算等待。

## 角色和其他特效

| 机制 | 处理与边界 |
|---|---|
| 跨角色获得卡牌 | 使用 CardModel.Owner.Creature，不限制铁甲战士 |
| 动作 | 持续岩甲使用人物自身变形网格与原绘制顺序，不把独立岩片贴在固定屏幕坐标上 |
| 皮肤与姿态 | 支持 Spine 和 Godot Skeleton2D；观察可见骨架及整体 Visuals 替换；数字储君皮肤按结构避开王座/随从，低语义数字身体按位置与骨长回退，避开头部带/过长武器 |
| 临时动作替身 | 观察共享 DisplayLease 和实际可见网格，识别“原骨架仍在但网格被隐藏”的嵌套/兄弟动作人物；在绘制前重新绑定并刷新冻结网格，归还时恢复 |
| 动作专用附件 | 不只绑定首次覆甲时非空的附件；提前绑定有身体命名的空槽，持续补入新启用的身体表面，避免施法/攻击/受击等动作换附件时漏甲 |
| 虚影与材质 | 可组合 ShaderMaterial 先保留原片元处理，再混合岩石颜色，保留其输出透明度、消散和变形；同步原 uniform，支持依赖默认 COLOR 的材质。无法安全组合的材质保留作者效果，可能不显示岩甲 |
| 盾牌与贴身特效 | 火墙在朝敌人的前方向外留出间距，不移动或隐藏其他作者特效 |
| 菜单与界面 | 独立特效根节点不继承皮肤抬高的 Z，跟随原生 CombatVfxContainer 的战斗深度；火墙、环绕碎片和反弹火焰不再越过暂停/退出菜单 |
| 恢复 | 仅归还自己仍持有的材质，不覆盖别的 Mod 后来接管的材质 |

火墙移除后按约0.65秒的真实时间平滑淡出，不使用会被 Engine.time_scale 压缩的游戏时间。淡出过程在普通速度和5倍时间缩放下分别验证。动作切换的验证在渲染后逐帧记录实际提交的覆甲网格，不再仅凭绑定数量判断通过。

未知自定义角色、无骨架外观和特殊着色器不保证完整覆盖，所有 Mod 的任意组合也未穷尽。实机结果见 [验证说明](../docs/ELEMENTAL_DEFENSE_VFX.md)。

## 构建、安装与回滚

需要 .NET 9 SDK 和本机游戏引用，不下载或分发游戏文件。

```powershell
dotnet build ./elemental/ElementalDefenseVfx.csproj -c Release '-p:GamePath=X:\SteamLibrary\steamapps\common\Slay the Spire 2'
./scripts/Deploy-ElementalVfx.ps1 -GamePath 'X:\SteamLibrary\steamapps\common\Slay the Spire 2'
./scripts/Deploy-ElementalVfx.ps1 -GamePath 'X:\SteamLibrary\steamapps\common\Slay the Spire 2' -Rollback
```

安装内容为自有 DLL、manifest、GDScript、着色器和 assets。更新和回滚将旧插件目录保留到本机 artifacts。脚本拒绝游戏运行时安装，不改原作者 DLL。

| 文件 | 用途 |
|---|---|
| Entry.cs | 卡牌、原生能力和实际角色绑定，观察显示替身 |
| DefenseFx.gd | 岩甲材质组合、骨架绑定、火墙帧与火焰反弹 |
| StoneCoat.gdshader / FlameTrail.gdshader | 岩甲与反弹火焰材质 |
| assets/README.md | ImageGen 原创素材与提示规格 |
| ../verification/elemental | 无保存战斗夹具，不是玩家存档 |
| ../scripts/Test-ElementalVfx.ps1 | 拒绝与日常游戏并行，临时装探针，结束移除并恢复玩家记录 |
