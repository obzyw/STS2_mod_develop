# 猎人位移动画兼容（已验收、已启用）

日期：2026-10-23。目标：MeleeAttack（工坊3774274248，1.0.31）与 MoreSilentAnimations（3821353824）共存，后者负责猎人位移和形象；保留前者其他功能。测试共享 Mod/用户数据，不与日常游玩并行；只读分析和构建可以。

新基线：artifacts/baseline-20261023-200810，共54个清单。骨妹 necro-v1 必须保持启用。

## 代码机制

MeleeAttack.SimpleTeleportPatch.CreatureCmdTriggerAnimPatch.Prefix 在检查位移设置前执行 Toutou.OnAttackStart，后者 CancelFade 可将 Body.Modulate.A 恢复为1。位移关闭也会经过此处。更多猎人动画 DisplayLease.Acquire 将原本体 alpha 设为0，创建 MoreSilentDisplay 移动替身；HiddenBodyMeshes.CheckVisibility 在 alpha 恢复后重新启用原骨骼网格。因此即使关闭位移，前者仍可能使本体显现。

候选独立 Mod 在前者的那个 Prefix 上增加一个 Harmony 前缀：猎人本体时让它直接返回 true，放行原生动画链，绕过透明度重置和位移接管。非猎人不拦截；其他卡牌专用补丁不卸载。原工坊 DLL 不修改。初始化核对两个主 DLL 的已审计哈希，版本变化不强制应用。

## 代码、测试和回滚

- compatibility/：独立 SilentMovementCompat 的源码/工程/清单。
- scripts/Deploy-SilentCompat.ps1：构建部署；-Rollback 将兼容目录移到 artifacts 留档。
- verification/SilentProbe.cs：复用已有 no-save 诊断夹具创建猎人测试角色。按原始/候选 × 位移关/开，测试 Neutralize、PoisonedStab、Backstab，共12用例；逐帧记录替身可见、位移、原本体同时显现与实际伤害。
- Test-NativeDiagnostics.ps1 新增 -SilentProbe；结束时恢复骨妹 DLL 的进入时状态，不再一律撤销骨妹补丁。

## 最终验收：2026-10-23

已正式部署到游戏 mods/SilentMovementCompat，两个原 Mod DLL 均未修改。移除/停用此独立兼容 Mod 即可撤销。以更多猎人动画负责猎人通用攻击位移；不增加按牌动态回退或叠加选项。

| 项目 | 结果 |
|---|---|
| 实际角色 | Silent，真实原生渲染与 AutoPlay，无保存测试局 |
| 矩阵 | 原始/兼容 × 前者位移关/开 × 中和/带毒刺击/背刺，共12用例 |
| 原始组重复显现 | 6用例共239帧 |
| 兼容组重复显现 | 6用例共244个替身可见采样帧中，重复显现0帧 |
| 兼容组位移 | 六用例均大于640像素，最高约1189像素，替身持续可见 |
| 实际伤害 | 对照/兼容均为中和3、带毒刺击4、背刺11，开关两态一致 |
| 回滚/再部署/重复部署 | PASS；停用时归档兼容目录，不修改订阅 DLL |
| 既有 DLL 审计 | 65个既有 DLL 均与本次基线一致，含已启用骨妹补丁 |
| 设置/测试清理 | MeleeAttack_settings.json 哈希一致；临时 STS2CompatProbe 移除；测试进程退出 |

证据：artifacts/native-compatibility-20261023-201759/silent-validation.json、同目录 validation.json、artifacts/silent-summary.json、silent-dll-audit.json。

重复显现的定义：原 Body alpha>0.1，并且 MoreSilentDisplay 在场景中可见且 alpha>=0.1。每个 ProcessFrame 采样，使用全场景搜索与全局坐标，因为移动替身会重挂到敌人附近节点层。该方法验证渲染节点状态，不宣称做了所有卡牌的逐像素视频对比。保留其他角色/专用技能的代码路径，未穷尽联机和全部技能组合。

## 验证问题与解决

1. Player.CreateForNewRun 重载不唯一，夹具初始化失败；已精确限定非泛型、首参数 CharacterModel，失败目录201145。
2. 初始遗物重做要求开局选牌，阻止进入可出牌状态；仅在测试局移除初始遗物，所有 Mod 仍加载，不修改日常遗物配置，失败目录201249。
3. 角色子树采样漏掉已经重挂到敌人层的替身，位移被误记为0；已全场景跟踪可见 MoreSilentDisplay 并计算全局位移，最终12用例通过。旧失败目录201549不可当通过证据。

## 跨电脑操作

在工程根目录，传目标机器游戏路径：

```powershell
./scripts/Deploy-SilentCompat.ps1 -GamePath '<游戏目录>'
./scripts/Deploy-SilentCompat.ps1 -GamePath '<游戏目录>' -Rollback
./scripts/Test-NativeDiagnostics.ps1 -GamePath '<游戏目录>' -Card compatibility -Probe -SilentProbe
```

需要 .NET 9 SDK 和当前两个原 Mod。初始化/部署均验证主 DLL 哈希：MeleeAttack=7BB0DE0F078D97EDFEBB0D4494B5B0A1224CF75DA940971EC6106870F4AF663B；MoreSilentAnimations=95F6E7A7C003FBA2B9FA1C64E470373DCBF349100BB5EB5C0173B7ED5C3D23AC。作者更新后需重新审计，兼容不会强行应用到未知版本。

## 网络资料

作者工坊：https://steamcommunity.com/workshop/filedetails/?id=3774274248 ，确认有角色/怪物位移和独立开关。

视频：https://www.bilibili.com/video/BV1j4ez6oEp1/ 、https://www.bilibili.com/video/BV147Je6tEnH/ 。网页返回412或无法访问；只读到前者搜索摘要，未观看视频，不把摘要当视觉验收。
