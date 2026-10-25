# 全屏黑影闪烁：显示同步调整

2026-10-25：用户报告黑闪再次出现，主菜单、选人界面即可触发，且只有全屏游戏画面闪，桌面/其他窗口不闪。**已应用165 FPS＋VSync on并验证游戏实际读取；用户随后实屏反馈“目前未再出现黑闪”。当前方案保留，停止追加测试。** 根因尚未直接复现，不应将本记录表述为已经证明某个 Mod 导致或已经彻底根治；若以后复发，再以新现场继续定位。

## 本次正式改动

| 项目 | 修改前 | 修改后 |
|---|---|---|
| 游戏 FPS 上限 | 500 | 165，匹配本机显示器 |
| 游戏 VSync | adaptive | on（始终开启） |
| 全屏、MSAA、分辨率 | 原设置 | 保留 |
| 渲染后端 | D3D12 / Forward+ | 保留 |
| Mod、角色模型和特效 | 已有配置 | 全部保留 |

只改活动 Steam 账户 `settings.save` 中 `fps_limit` 与 `vsync` 两个字段，其余内容按字节保留。没有新增生产 DLL，没有更新驱动、改系统刷新率、停用 Mod 或修改 NVIDIA 全局配置。此前兼容代码未设置正式游戏 FPS；旧诊断夹具的限帧只影响其测试进程，本次是首次正式应用显示稳定设置。

[Godot 4.5 的 VSync 定义](https://docs.godotengine.org/en/4.5/classes/class_displayserver.html#enum-displayserver-vsyncmode)说明 adaptive 在帧率低于刷新率时会关闭同步，enabled 则持续按垂直消隐提交画面。本次针对不稳定的全屏帧输出先采用后者。此机制支持调整方向，不能证明当前黑闪就是该机制引起。

## 检查与实际验证

本机 NVIDIA RTX 3060 Laptop GPU，驱动 560.94，显示器实际约 165.004 Hz；当前引擎 MegaDot 4.5.1.m.14、游戏 0.111.0、D3D12。最新现场日志未发现设备丢失/交换链提交失败，也没有同期 Windows Display 4101 驱动重置事件。战斗日志另有 `Task` 到 Godot Variant 转换错误，发生于出牌时，与本次主菜单黑闪没有建立因果关系，未为此改动无关 Mod。

| 证据 | 结论 |
|---|---|
| `artifacts/display-baseline-20261025-002408` | 修改前原始日志、设置、显卡信息备份 |
| `artifacts/watcher-menu-20261025-002408/display-result.json` | 同进程主菜单原设置帧间隔中位 3.026ms / P95 19.387ms；同步设置中位 6.058ms / P95 6.180ms。原设置超显示刷新率且波动明显，同步设置接近稳定165Hz。顺序测试也受资源预热影响，不是严格因果或性能基准 |
| 同目录原生像素采样 | 每场景/设置180帧；主菜单及原设置选人采样没有大范围骤暗候选。最后选人阶段两张候选经查看为角色切换，不是黑块；该阶段角色变化且失焦，最终骨架断言失败，不能计为验收通过。偏好已恢复，失败记录完整保留 |
| `artifacts/watcher-menu-20261025-002825/menu-result.json` | 正式设置写入后另启动一次短验证，success=true：实际 Engine.MaxFps=165、VSync=Enabled、Fullscreen、165.004Hz、焦点正常；美化选人骨架实际可见，重塑依旧仅特效开启 |
| 同目录 `restored.json` | 三份设置/偏好恢复并逐项匹配；恢复的是本次165/on正式设置，不是旧500/adaptive。测试插件移出mods，游戏进程已退出 |

原生像素读取会干扰帧率，因此帧间隔在独立、不读回图片的5秒区间测量。像素检测只是一种相邻帧亮度骤降启发式，可能漏过短暂/较小/亮度较低的黑影；图像位于显示提交之前，无法验证显示器、VRR或扫描输出阶段的闪烁。Windows 窗口连接本次连续失败（kernel assets路径错误），未以桌面截图冒充实屏验证。自动验证确认设置生效、画面正常加载和帧输出更稳定；随后用户实屏确认目前未再黑闪。该反馈没有指定观察时长，不扩大为长期稳定性结论。

网上检索到的 [Godot #118216](https://github.com/godotengine/godot/issues/118216)实际是 RTX5080/Vulkan/帧生成案例，与本机3060/D3D12不同，不据此修改帧生成、驱动或宣称同一根因。游戏自带 `launch_vulkan.bat`、`launch_opengl.bat` 可供后续必要时对比，本轮未切换后端，以免额外改变特效渲染。

## 迁移、撤销

从工程根目录、游戏关闭时：

```powershell
# 自动识别唯一 Steam 账户的设置，以及显卡报告的唯一有效刷新率
& ./scripts/Set-DisplayStability.ps1
# 多账户或多刷新率时显式指定；路径使用新电脑本机值
& ./scripts/Set-DisplayStability.ps1 -SettingsPath '<活动账户的settings.save>' -FpsLimit 165

# 撤销本机本次设置；保留后来修改的其他选项
& ./scripts/Set-DisplayStability.ps1 -RollbackFrom './artifacts/display-settings-20261025-002736-000/report.json'
```

脚本不依赖固定盘符/账户数字 ID。备份与记录保存在本工程；另一台电脑应用后应使用其自己生成的回滚记录，不套用本机的500/adaptive历史值。正式应用记录 `artifacts/display-settings-20261025-002736-000/report.json`，原设置完整备份同目录 `settings.before.json`。没有必要反复运行回滚验证或重跑旧兼容组。

若后续实屏仍闪，先区分屏幕输出闪烁与游戏确实绘制黑块，再按证据对比显示模式/自带其他渲染后端。不要直接撤销观者转场、停用全部Mod、清空缓存或重装游戏。
