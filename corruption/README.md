# 腐化：铁甲战士形象修正

适用于 IroncladFX 魔焰铁甲 1.0.1、游戏 v0.111.0。仅把原腐化巨像的兜帽老人/白胡须改成铁甲战士的面具与向后飘动的白发，保留原配色、画风、骨架、握拳、火焰、布条、透明度曲线、动画时长和卡牌结算。

| 范围 | 实现 |
|---|---|
| 运行时修正 | 仅在 IroncladFx.Assets.Texture 返回 corruption/rig2/body.png 时替换头部合成结果 |
| 原图保留 | 从使用者自己的原 Mod 读取原图，原像素仅在内存里参与合成，不随插件分发 |
| 局部修改 | 只处理头部、头发、兜帽和旧胡须占据区域；其余躯干、垂下的手臂、衣料像素保持原样 |
| 动画保持 | 没有挂接或重写 CorruptionFx、RigInstance、Shader、Tween 或任何计时入口；眼睛对齐使用原定位点，画布仍为1001×1430 |
| 回滚 | 移出本插件目录即可恢复原人物，不修改作者 DLL/PNG、游戏本体或作者设置 |

新人物由内置 ImageGen 编辑生成，最终素材在 [assets/corruption-warrior-body-v2.png](assets/corruption-warrior-body-v2.png)，提示规格见 [assets/README.md](assets/README.md)。完整生成图仅作素材输入；运行时只采用头部及原胡须区域，其他区域继续使用使用者原图。

## 安装与测试

```powershell
./scripts/Deploy-CorruptionVfx.ps1 -GamePath 'X:\SteamLibrary\steamapps\common\Slay the Spire 2'
./scripts/Deploy-CorruptionVfx.ps1 -GamePath 'X:\SteamLibrary\steamapps\common\Slay the Spire 2' -Rollback
./scripts/Test-ElementalVfx.ps1 -Feature Corruption -Characters Ironclad -GamePath 'X:\SteamLibrary\steamapps\common\Slay the Spire 2'
```

需要启用原 IroncladFx。测试复用已有无保存视觉夹具，不触发火墙/岩甲回归。脚本拒绝和日常游戏同时运行，测试后移除探针、恢复玩家记录。不要把 verification 插件用于日常游戏。

## 2026-10-26 本机验证

| 批次 | 结果 |
|---|---|
| corruption-190706 | 首版局部合成比对通过；夹具错误地把手牌悬停当作作者预览入口，预览断言失败，未算整体通过；记录恢复成功 |
| corruption-192537 | 最终图：头部等区域有299144个变化像素，限定区域之外0个变化像素，画布尺寸相同；原 Ready.Show 指向预览入口、实际 Corruption 出牌、能力应用及退场截图通过；退出码0，记录恢复成功 |

已查看运行时合成图、选牌预览和出牌截图。预览入口是原作者 IroncladFx.Ready.Show，不是悬停触发测试。正常退出仍有当前多 Mod 环境的引擎资源未释放警告；未宣称所有 Mod 任意组合无问题。美术最终以用户实屏反馈为准。
