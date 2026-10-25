# 实现、复现与验收

## 环境

Windows；脚本兼容 Windows PowerShell 5.1 和 PowerShell 7；.NET SDK 9 可用。
本机已获准下载并安装项目内 ILSpy 9.1.0.7988（针对已安装 .NET 8 运行时），无全局工具安装。

```powershell
dotnet tool install ilspycmd --version 9.1.0.7988 --tool-path ./tools
```

修改 DLL 使用 Mono.Cecil 0.11.5，本机从现有 VS 安装复制到 tools/Mono.Cecil.dll。迁移时复制整个工程的 tools 文件夹即可；若仅复制源码，需要准备相同库并通过 -CecilPath 指定。不要把 VS 的绝对路径写入项目脚本。
ILSpy 官方：https://github.com/icsharpcode/ILSpy
Mono.Cecil 官方：https://github.com/jbevain/cecil

## 阅读任意订阅 Mod

Inspect-Mods.ps1 记录所有工坊和本地 mods 清单，inventory.json 包含 manifest 路径和同目录 DLL 名称/哈希。通过对应目录定位 DLL，交给现有工具，而不是另写反编译器：

```powershell
./scripts/Read-ModCode.ps1 -DllPath '<本机订阅目录>/<Mod>.dll' -GamePath '<本机游戏目录>'
# 只读取特定类型：
./scripts/Read-ModCode.ps1 -DllPath '<DLL路径>' -TypeName 'NecrobinderVfx.Main' -GamePath '<游戏目录>'
```

输出位于 artifacts/decompiled，仅为反编译视图；作者源码仓库、注释、构建资源不一定能恢复。当前仅分析了本子任务相关实现，不声称已经审计全部 53 个 Mod。

## 根因与既有兼容

动画拓展 EndOfDaysVisualPatch.Transpiler 将原生 NLargeMagicMissileVfx.Create(Vector2, Color) 调用替换为自己的 Create(Vector2, Color, EndOfDays)。金卡 EndOfDaysPresentation.ReplaceMissileCall 仍要求恰好存在一次原生调用，故匹配数变成 0 后抛异常。Main.Initialize 的 catch 随即 UnpatchAll("jackt.NecrobinderVfx")，其他金卡动画一起失效。

动画拓展 MoreAnimationsCompatibility 通过反射发现更多骨妹程序集，向 AnimationSettings.Allows 和 AttackVisualPatch.Prefix 安装 Harmony 前缀；结合 AnimationSettings.RuntimeSources 决定是否使用骨妹来源。另有 MoreAnimationLayers 等协调动作层。当前实际配置已经多选来源，保持不变。

Harmony 官方说明：https://github.com/pardeike/Harmony/blob/master/Documentation/articles/patching-transpiler.md
transpiler 会在后续添加补丁时重新组成链执行；单纯交换加载顺序不足以保证两个严格匹配原生调用的改写器共存。

## necro-v1 方案

采用版本受限的离线 DLL 小范围修补。当前独立运行时补丁需要抢在金卡初始化前安装，并协调加载顺序；固定版本离线修补更直接，不增加游戏运行时依赖。

1. Main.Initialize 中三个安装调用改为 pop：EndOfDaysPresentation、NeurosurgeRelay、ReaperFormRelay。消费已压栈的 Harmony 参数，保持栈平衡。
2. Main.Supported 保留原来 26 个类型中的 21 个，排除 BansheesCry、Eradicate、EndOfDays、Neurosurge、ReaperForm。原 DLL 虽宣传 25 张，实际谓词包含 26 个类型，报告以代码为准。
3. Supported 是拖牌、出牌前后、受击、能力变化与异步场景回调的共同许可入口；精神过载和死神形态还有独立的能力恢复/回放路径，因此同时停止相应 Relay 安装。
4. 不改任何原生游戏 DLL、不改后两个 Mod 的 DLL 和用户配置。不改变原本的 21 个金卡效果实现。
5. 加载标记更换为明确的兼容构建标记，避免原 DLL 中过时的 0.2.93 提示造成误判。

固定配置要求三个 Mod 都已订阅且在游戏内启用。即使手动关闭后两个 Mod 的某张牌，这五张牌也不会自动退回金卡来源；本轮不实现动态回退。未来更改卡牌分配或版本必须重新审计。

**当前部署状态：已通过实际运行验收并正式启用。** 用户接受运行断言代替视觉逐帧对照。最终只有金卡 DLL 发生生产改动，其余 63 个 DLL、原有配置/玩家存档记录均一致；临时测试 Mod 已清理。最终证据见 VALIDATION.md 与 artifacts/deployment-state.json。

## 构建、部署和回滚

在工程根目录执行。工程不在游戏目录内时，给各脚本传 -GamePath。

```powershell
./scripts/Build-NecroCompat.ps1 -InputDll '<原始NecrobinderVfx.dll>' -GamePath '<游戏目录>'
./scripts/Deploy-NecroCompat.ps1 -GamePath '<游戏目录>'
./scripts/Deploy-NecroCompat.ps1 -GamePath '<游戏目录>' -Rollback
```

构建只接受 SHA256 为 1C68A4699FDC0BB25BC0737FE811E9A368442A47D922F9A6B3F493DED28433AA 的原 DLL。部署另核对 v0.111.0 及两个伴随 Mod 的全部四个 DLL 哈希；版本变化必须重新审计。部署前必须关闭游戏；首次部署备份原文件，拒绝覆盖未知版本；重复部署无操作。构建再运行应使用备份原件，而非已修改的工坊 DLL。回滚不要求伴随 Mod 或游戏保持原版本。

当前部署位置是工坊订阅目录，会被 Steam 更新/验证覆盖。后续更新后应重新盘点和适配，禁止强行套旧补丁。备份、输出和报告都在 artifacts，应随工程一起迁移；Git 默认忽略这些第三方/本机文件。

## 2026-10-22 验证记录

| 检查 | 结果 | 证据 |
|---|---|---|
| 全部订阅清单 | PASS，53 个清单 | artifacts/baseline-* |
| Windows PowerShell 5.1 构建 | PASS | artifacts/build/necro-v1/build-report.json |
| 重新读取输出 DLL | PASS，21 个保留类型、5 个排除、3 个安装点停用 | 构建内置断言 |
| 原文件恢复、再次部署、重复部署 | PASS | 本轮命令输出及哈希校验 |
| Steam 游戏无界面启动 | PASS，Steam 初始化成功，三个 Mod 初始化，原来源选择器安装成功 | artifacts/headless-necro-v1-steam.log |
| 金卡加载 | PASS，新加载标记出现，未出现原初始化异常 | 同上，1811 行附近 |
| 实际打牌/效果归属/同场共存 | PASS | 九用例候选运行和原始环境对照，详见 VALIDATION.md |
| 出牌次数/敌人血量对照 | PASS | artifacts/differential-validation.json |
| 苦难实际伤害/状态/对象清理 | PASS | native-misery-20261022-232939 |
| 宣传视频逐帧外观/全场景极速 | 未覆盖，不作为当前验收方法 | 用户接受高效运行验证，未声明全覆盖 |

第一次无界面测试缺少 Steam AppID，未进入 Mod 加载；第二次只在测试进程环境设置 SteamAppId/SteamGameId=2868840 后成功。未在游戏目录添加 steam_appid.txt。
无界面渲染存在 shader 警告；另有其他 Mod 不支持 public-beta 的既有报错，均不能算本补丁已经解决。
测试没有开始新一局。完成初始化采样后关闭本次创建的无界面测试进程。

## 实际运行验收与复现

桌面控制工具当前始终报 `failed to write kernel assets: 系统找不到指定的路径。 (os error 3)`；已尝试重置后重连，仍失败。B 站网页工具无法读取用户提供的三个视频，未能对照。

后续已发现作者自带 NativeDiagnostics，可直接在真实游戏的无保存新局运行，导出渲染画面与断言。verification/ 的临时观察 Mod 复用它的夹具，补充五张共享牌的实际对象分配和原始环境对照，不依赖桌面连接。已按用户允许的方法完成本子任务验收，覆盖和局限见 VALIDATION.md。

Test-NativeDiagnostics.ps1 自动试部署、测试、回滚和清理临时 Mod；同时备份/恢复 Rewind 与 Loadout 的 profile 目录，保留测试写入产物在各测试目录的 userdata-after。对移动目录逐项验证绝对路径在指定用户游戏数据根和 artifacts 目标内，不删除原记录。

```powershell
./scripts/Test-NativeDiagnostics.ps1 -Card misery
./scripts/Test-NativeDiagnostics.ps1 -Card compatibility -Probe
./scripts/Test-NativeDiagnostics.ps1 -Card compatibility -Probe -Control
./scripts/Test-SurgicalDiff.ps1
```

对照组会使用原始金卡（原初始化冲突预期出现），只复用作者无保存夹具；不能将对照日志里的初始化警告误认为兼容构建回归。测试结束默认恢复原件；正式启用再执行 Deploy。存档快照含用户数据，按本地交接处理。
