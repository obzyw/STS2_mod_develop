# 储君双动画兼容

2026-10-24。当前采用**作者原有设置**实现分域共存与固定优先级，不新增生产兼容 DLL；验收与配置应用记录见末尾。

## 审计范围与规则

实际订阅名为“万象辉星[RegentFX]”（用户称万象星辉），工坊 3747497501，0.5.1；另一方 RegentAnimations储君动画，3820682341，0.44.0。保留储君皮肤美化（三阶段）3819703767，0.3.19，以及原来三个兼容组。

两套代码没有发现针对对方的整牌禁用逻辑。黑洞缺失的直接原因是本机 `%LOCALAPPDATA%/SlayTheSpire2/RegentFX/ritsu_interop_state.json` 的 `power_BlackHole=false`；同文件 `power_Genesis`、`power_PillarOfCreation` 也关闭。不是初始化失败，日志显示 RegentFX 32 个场景预加载成功。

| 双方共有卡牌 | 万象辉星作用域 | RegentAnimations 作用域 | 策略 |
|---|---|---|---|
| 黑洞 BlackHole | 角色 VfxSpawnPosition 周围的常驻黑洞、获得/消耗星星后的爆发 | 角色前上方，默认身体偏移 (+290,-345)，独立球体及命中 | 共存；实测中心距离，保留各自清理 |
| 创世纪 Genesis | 角色中心出牌时约 4 秒的开场、音效和曝光 | 后上方默认 (-65,-350) 星体，后续能量重置时生星 | 共存 |
| 创世之柱 PillarOfCreation | 角色后方 (-130,-50) 柱体，生成卡牌时发光 | 从身体锚点右侧 (+420,+162) 的独立柱体，生成牌获甲反馈 | 共存 |
| 光谱偏移 SpectrumShift | 出牌时约 3 秒的开场 | 后续 BeforeHandDraw 生成无色牌时的法阵与卡牌反馈 | 共存；不关闭后续触发 |
| 彗星 Comet | 替换出牌并在敌人附近落彗星、虚弱/易伤 | 原生导弹包装、飞行与同一目标命中 | 轨迹/命中重叠，开启万象单卡，关闭 A 单卡 |
| 新月长矛 CrescentSpear | 角色至目标的长矛、命中前等待 | 同一攻击的起手、长矛及命中 | 同上 |
| 星灭 DyingStar | AOE 主场景、曝光及命中 | 同一 AOE 主场景、地面岩石和命中 | 同上 |
| 七星 SevenStars | 多段星体 AOE | 同一多段事件扇面/星流/命中 | 同上 |
| 星界脉冲 AstralPulse | 角色中心向外脉冲、全屏扭曲/曝光 | 同一攻击向敌方传播的波与命中 | 同上 |

其他独占牌保留原行为。此处 A 指 RegentAnimations。特效粒子会经过邻近区域，不将每一像素相交都视为必须禁用；以相同的攻击阶段、主要轨迹/命中区域、角色占用为依据。

## 先用现成功能

用户明确要求先检查开关、现成工具和补丁，代码修改放最后。已查看 [RegentFX 作者工坊页](https://steamcommunity.com/sharedfiles/filedetails/?id=3747497501)、[RegentAnimations 作者工坊页](https://steamcommunity.com/sharedfiles/filedetails/?id=3820682341) 和针对两者的兼容补丁搜索，没有找到专门解决该组合的公开补丁；不能据此断言全网不存在。作者现成的逐牌开关足以配置本次需求，因此不安装补丁、不引入自动仲裁逻辑。

`scripts/Set-RegentEffects.ps1` 只调整两个作者配置文件，修改前备份，检查游戏及两个作者 DLL 的哈希；不改任何 DLL。四张能力双方均开，五张攻击保留万象、关闭 A。当前实际差异应为万象三个 false→true 与 A 五个 true→false；万象“打击”“战火铸就”等无关设置保留。

这是**固定开关组合**，不提供运行时自动回退。如果日后手动关闭某张万象攻击，需要在 RegentAnimations 设置里重新打开对应牌才能使用后者；不将自动回退作为未要求功能增加代码。游戏内入口：A 在“选项→通用→RegentAnimations储君动画→设置”；万象使用已安装 RitsuLib 的模组设置。

早期曾编写临时候选，现已归档 `artifacts/regent-prototype-not-deployed-20261024`，不部署到正式 mods。只保留无保存测试观察器作为项目验证工具；其不参与日常游戏。

## 复现、部署、回滚

从工程目录运行，可加 `-GamePath` 指定另一台电脑的游戏目录。沿用已有 .NET 9、ILSpy、Harmony，无新增外部包。

```powershell
# 十个仅针对储君的设置方案用例；内存临时切换作者选项，finally 恢复。
./scripts/Test-NativeDiagnostics.ps1 -Card compatibility -Probe -RegentProbe -NativeRenderer -TimeoutSeconds 600
# 通过后应用作者设置，不部署新 DLL。
./scripts/Set-RegentEffects.ps1
# 使用上一条输出的备份 report.json 撤销；迁移电脑保留 artifacts。
./scripts/Set-RegentEffects.ps1 -RollbackFrom './artifacts/regent-settings-<时间>/report.json'
```

测试不能和用户日常游戏同时运行。皮肤保留加载；黑洞普通血量与低血量都检查两处中心距离及实际渲染。最终测试覆盖四张能力出牌与真实触发、五张攻击万象实际场景和 A 没有重复场景，以及低血量黑洞。初始化或许可谓词正确不能冒充播放成功。回滚若文件未再改变则逐字节恢复；如果之后调整了其他选项，则只撤销本次仍保持应用值的字段，不覆盖其他新偏好。

## 最终验收记录

**已应用并通过验证。** 无新增生产 DLL。前面猎人/骨妹成果没有重复测试。

| 证据 | 结果 |
|---|---|
| artifacts/native-compatibility-20261024-102544/regent-validation.json | settingsOnly=true，10/10 实际出牌通过；完整作者环境和皮肤保留 |
| 同目录 main-scene-audit.json | 十次均确认本张牌的万象主场景（七星为 Blade1），没有把通用命中粒子冒充主特效 |
| 同目录 PNG | 已查看黑洞、光谱偏移与低血量截图；黑洞两处明确分离，10/75 HP 时仍共存 |
| 同目录 isolation.json | 两份设置与六组 Rewind/Loadout 目录恢复匹配；临时测试插件已清理 |
| artifacts/regent-settings-deployment.json | 正式方案为 author-settings-only，万象 3 项 false→true、A 的 5 项 true→false |
| artifacts/regent-settings-20261024-102855-961/report.json | 最终配置备份与撤销入口；先应用、撤销并核对原配置逐字节哈希、再应用，rollbackVerifiedExactBytes=true |

本机撤销命令（从工程目录执行，关闭游戏后）：

```powershell
./scripts/Set-RegentEffects.ps1 -RollbackFrom './artifacts/regent-settings-20261024-102855-961/report.json'
```

历史失败保留：`native-compatibility-20261024-102030` 为测试快照错误（ConcurrentDictionary 枚举类型不匹配），未执行出牌；已修正测试。`102209` 为临时候选的扩展回退断言失败：彗星实际回退动画已出现，但测试把万象通用命中粒子误算为彗星主特效。该候选未交付，也不将部分完成当整套通过；最终改用作者开关，不需要继续开发自动回退补丁。
