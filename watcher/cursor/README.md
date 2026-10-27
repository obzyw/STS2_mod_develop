# 观者饼干头像光标

为 CookieCursor 0.7.0 补充观者头像，依赖用户本人订阅的 CookieCursor。采用当前美化观者的蓝灰头发、双金簪、闭眼表情、紫粉领饰，沿用作者的 Q 版糖霜饼干、米黄色底边和厚黑描边。美术待用户实屏评价。

| 项目 | 实现 |
|---|---|
| 角色 | Boninall 的 Watcher（watcher）及重塑独立 TheWatcher（thewatcher） |
| 接入 | CookieCursor.CacheCharCookies 后在内存缓存注册两项，作者文件不改写 |
| 点击 | 调用作者 RotateImageManually，保持正负5度倾斜 |
| 尺寸 | 作者原有64像素基准及 CursorScale；本模块不改用户设置 |
| 热点 | 基准(10,4)，随作者缩放一起变化 |
| 其他角色/菜单 | 沿用作者的角色切换与默认光标恢复 |

源图在 assets/watcher-cookie-v1.png，运行时导入256方形再交给原生旋转/缩放流程；透明通道保留。未复制作者头像或 DLL 到本模块。联机远程光标也读取同一缓存，但本轮不进行真实多人测试。

```powershell
./scripts/Deploy-WatcherCookieCursor.ps1 -GamePath 'X:\SteamLibrary\steamapps\common\Slay the Spire 2'
./scripts/Deploy-WatcherCookieCursor.ps1 -GamePath 'X:\SteamLibrary\steamapps\common\Slay the Spire 2' -Rollback
```

正常关闭游戏后安装/回滚，不需要放弃当前一局。目标游戏v0.111.0；初始化检查缓存和旋转入口，作者改动API后需要适配。未接入unified或portable正式快照。

验证入口：`scripts/Test-ElementalVfx.ps1 -Feature WatcherCursor -Characters Watcher`。无保存夹具，测试后恢复原记录并移除临时探针。最终结果待追加。

213257 首次夹具在读取扩展章节列表时遇到 `No default act for index 4`，尚未进入光标检查，不算通过。光标夹具改为仅取前三幕，原生 Launch 后直接检查，不进入无关战斗；不修改该扩展的设置或用户存档。

213447 最终通过并安装：实际 Watcher 角色启动时新图生效；两个别名各1/1.5倍尺寸为64/96、透明角、点击倾斜和热点缩放均通过；切铁甲保留原图、默认光标恢复通过。原生退出0，玩家记录恢复且探针移除。TheWatcher 别名素材已验证，未启动该独立角色，也未做真实多人验证。精选证据见 evidence/watcher-cookie-cursor-20261026.json。实际64像素图已目视检查。
