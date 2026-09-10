# 桌面蟑螂 / Desktop Roach

Windows 桌面生态小游戏，首版 v0.1。C# + WPF，使用系统自带二维绘图，无 NuGet 依赖、无服务器、无需管理员权限。

## 运行

Windows 10 / 11，.NET Framework 4.8。双击 `bin\DesktopRoach.exe`，或解压便携包运行其中的 exe。

程序先打开演练场，立即开始生态模拟。点击「开启桌面入侵」后，控制台最小化，桌面出现透明覆盖层和悬浮工具栏。

- 日常办公：覆盖层鼠标穿透，可继续操作正常应用。
- 蟑螂拍：点击拍打，冷却 0.28 秒，留下残骸。
- 杀虫喷雾：范围中毒消杀，冷却 2.5 秒，可按住连续使用。
- 蟑螂药：放置持续 90 秒的诱饵，最多同时存在 5 份。
- 扫把：按住清理食物、卵鞘、残骸。
- 拖把：按住清理油污，降低污染度。
- `Esc`：退出清理模式，恢复鼠标穿透。
- `Ctrl+Shift+Q`：紧急暂停、移除覆盖层并打开控制台。
- 托盘菜单：打开控制台、暂停、隐藏或退出。关闭控制台即完整退出。

选用清理工具时，覆盖层会接收桌面点击。请先按 Esc 恢复日常办公，再点击其他应用。工具提示也可通过悬停查看。

## 已实现

随机入侵、觅食、进食后产卵、42 秒孵化、幼虫 60 秒成长、仿真桌面物件拖动、污染累积、五种对抗工具、三档难度、统计、暂停、数量限制和本地存档。幼虫接触会让游戏工具的虚拟光标短暂减速；系统鼠标设置保持原值。

透明覆盖层会出现在应用窗口上方。支持全屏应用前台时自动隐藏并暂停，锁屏及系统休眠时暂停，显示器配置变化后退出覆盖层。污染覆盖最大约 23% 不透明度。快捷键注册失败时不允许启动覆盖层。

## 存档

路径：`%LOCALAPPDATA%\DesktopRoach\ecosystem.xml`。每 30 秒和正常退出时保存，保留 `.bak` 备份。主存档无效时尝试恢复备份；离线期间不继续繁殖。`--demo` 和截图模式不会覆盖存档。

## 构建与验证

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
$test = Start-Process .\bin\DesktopRoach.exe -ArgumentList '--self-test' -Wait -PassThru
Get-Content .\bin\test-results.txt
$test.ExitCode
powershell -NoProfile -ExecutionPolicy Bypass -File .\package.ps1
```

`build.ps1` 使用 Windows 内置 .NET Framework C# 编译器，不需要下载 SDK。`DesktopRoach.csproj` 可供安装了 .NET Framework 4.8 开发组件的 Visual Studio 使用。

导出演练场截图：

```powershell
.\bin\DesktopRoach.exe --snapshot artifacts\preview.png
.\bin\DesktopRoach.exe --compact --snapshot artifacts\preview-compact.png
```

自检涵盖繁殖、成长、消杀、清扫、冷却、诱饵数量及过期、物件移动、暂停、存档、异常数据及一小时加速模拟。

## 首版边界

- 桌面图标是游戏内仿真物件，尚未接入 Explorer 真实快捷方式移动。
- 多屏采用相同生态映射到各显示器，尚未实现跨屏独立生态或混合 DPI 精确交互验证。
- 尚未提供仅位于桌面壁纸之上的层级模式、自动更新、安装程序、签名和开机启动设置。
- 本轮已通过模型自检及两种窗口尺寸的 WPF 渲染检查；原生 UI 自动化因环境审批服务 503 被阻断，真实点击穿透、全屏隐藏、全局快捷键和多屏仍需实机交互验证。

## 代码结构

- `src/Simulation.cs`：与窗口无关的生态状态及游戏规则。
- `src/Scene.cs`：二维绘制、动画与工具交互。
- `src/ControlWindow.xaml`：中文控制台。
- `src/DesktopApp.cs`：透明窗口、托盘、热键、生命周期和存档调度。
- `src/SelfTests.cs`：无需测试框架的确定性核心自检。
