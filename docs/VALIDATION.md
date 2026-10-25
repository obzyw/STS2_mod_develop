# 亡灵契约师兼容验收

日期：2026-10-22。适用游戏 v0.111.0，金卡 0.3.2、动画拓展 1.0.3、更多骨妹 0.1.26；部署脚本核对实际 DLL 哈希。

## 结论与范围

用户允许用高效的实际运行断言替代逐帧视频对照。当前候选补丁已在完整订阅环境中通过加载、真实打牌、特效实例归属、同场共存、结算对照与作用域清理检查。

| 验收项 | 结果 | 证据 |
|---|---|---|
| 金卡初始化恢复 | PASS，新兼容标记，无原初始化失败 | headless-necro-v1-steam.log、候选实际运行日志 |
| 五张重叠牌让位 | PASS，均无金卡共享效果实例，goldCasts=0；伴随特效实际创建 | native-compatibility-20261022-232341/compatibility-probe.json |
| 原有双来源保留 | PASS，末日降临和精神过载均观测到两 Mod 的实际对象 | 同上 effectAllocations |
| 独占金卡恢复播放 | PASS，Hang、SoulStorm、Squeeze 各一次 goldCast 和对应实例 | 同上 |
| 死神形态与独占金卡同场 | PASS，骨妹死神形态后打 Hang，金卡正常创建一次 | 同上 reuseCombat=true |
| 三种苦难场景 | PASS，原生打牌/伤害/状态传播/效果清理断言 | native-misery-20261022-231258/validation.json |
| 全部 26 个卡牌类型路由 | PASS，对实际注册 ModelDb 模型执行编译后的 Supported 方法 | 实际运行探针，失败会中止套件 |
| 原始环境对照 | PASS，九个用例的开始/结束事件、敌人血量前后值完全相同 | differential-validation.json |
| 修改范围 | PASS，16,336 个方法体中仅 Main.Initialize 和 Main.Supported 两个变化 | build/necro-v1/surgical-diff.json |
| 备份与恢复 | PASS，原件恢复后 SHA256 一致，重复部署无操作 | 部署脚本与 DLL 审计 |
| 自动记录恢复复测 | PASS，恢复脚本完整执行；原有存档/配置/检查点/Loadout 无差异 | native-misery-20261022-232939、最终用户数据审计 |
| 最终生产部署 | 已启用，只改变金卡 DLL，其余 63 个 DLL 一致；无临时 Mod | deployment-state.json、final-dll-audit.json |

以上证据路径均相对于 `artifacts/`。

## 重叠牌的实际来源

| 卡牌 | 观测到的来源 | 金卡对应实例 |
|---|---|---|
| 女妖之嚎 BansheesCry | 更多骨妹 BansheeEffect / ClipPlayer | 0 |
| 根除 Eradicate | 动画拓展 EradicateSequenceV1 | 0 |
| 末日降临 EndOfDays | 更多骨妹 EndOfDaysEffect + 动画拓展 EndOfDaysCarrier | 0 |
| 精神过载 Neurosurge | 更多骨妹 ClipPlayer + 动画拓展 NeuroSequenceV5 | 0 |
| 死神形态 ReaperForm | 更多骨妹 ReaperFormEffect / ClipPlayer | 0 |

这是在当前用户来源配置下的观测结果，未强行更换后两个 Mod 的设置。用户说明“冲突时忽略”是简便性偏好；本轮实践表明继续由原有两 Mod 负责这五张牌更简单，因此没有再添加金卡来源叠加。

## 方法与局限

- 启动真实渲染游戏、使用作者自带无保存诊断新局。不是只检查日志，也不是模拟特效。
- 临时 `verification/` Mod 观察实际特效构造及原生 Hook 开始/完成事件，并复用作者的场景/引用清理断言；测试结束卸下它。
- 对照组使用未经修改的金卡 DLL，其原始冲突导致金卡补丁停用；仅启用其无保存诊断夹具，保留另外两个 Mod 与其他订阅。使用相同种子、卡牌和调用顺序。
- 对照范围是九个用例的出牌次数和敌人血量，不宣称穷尽全部能力/回合/能量/联机语义。AutoPlay 本身不扣能量。
- 实际播放检查覆盖五张共享牌、四张独占牌（含苦难）。剩余独占牌的入口路由已运行验证、内部方法体保持原样，但没有逐张完成全部玩法场景测试。
- 导出过真实 PNG；第三方公告遮挡部分画面，未用 B 站视频做逐帧外观一致性验收。用户已接受更高效的运行验证。
- 游戏退出的引擎资源警告不等同于本补丁对象清理失败；作用域清理断言通过，不声称修复了所有订阅 Mod 的资源管理问题。

## 已处理的验证问题

1. 广泛观察第三方构造函数会触发现有 Harmony 对 ReadOnlySpan IL 的处理错误。已缩小到相关类型，后续探针完整通过；失败记录 native-compatibility-20261022-231643 不作通过证据。
2. 作者 SoulStorm 扩展诊断读取 CalculatedDamage 键，在当前组合运行模型中该键不存在。该旧诊断不适合当前组合；保留失败报告 native-soul-storm-20261022-232016，不修改第三方卡牌规则以迎合断言。SoulStorm 的实际特效创建和原始环境血量对照已由独立探针通过。
3. 游戏进程退出后仍持有进程句柄，曾导致回滚脚本将其判为运行中。已在测试后释放句柄，并将运行检查限定为 HasExited=false。
4. 原生 no-save 保护不阻止 Rewind/Loadout 自己写记录。已将测试前快照恢复，测试脚本也加入这两个目录的备份/恢复；测试产物另存 artifacts，不删除原始记录。

## 复验

从工程根目录运行，迁移后可传 `-GamePath`。

```powershell
./scripts/Test-SurgicalDiff.ps1
./scripts/Test-NativeDiagnostics.ps1 -Card misery
./scripts/Test-NativeDiagnostics.ps1 -Card compatibility -Probe
./scripts/Test-NativeDiagnostics.ps1 -Card compatibility -Probe -Control
./scripts/Compare-ProbeResults.ps1 -CandidateReport '<候选目录>/compatibility-probe.json' -ControlReport '<对照目录>/compatibility-probe.json'
```

测试脚本每次结束恢复原始金卡。正式启用必须再单独执行 Deploy-NecroCompat.ps1。迁移时整个复制工程包含 tools/artifacts，不能只复制 Git 已跟踪文件。
