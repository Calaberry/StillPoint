# StillPoint 架构说明

> 目标：用最少的机制拿到「透明、置顶、穿透、跨游戏」四件事，其余全部保持在普通 WPF 应用范围内。

## 1. 为什么是「独立透明窗口」而不是游戏集成

想要跨游戏生效，只有三种办法：

1. **改游戏**（插件、配置文件、官方 API）——需要逐游戏适配，跨游戏等于零；
2. **注入 / Hook**（DLL 注入、D3D 渲染管线 Hook）——能覆盖包括独占全屏在内的所有画面，但会被反作弊针对、风险高、还要求每种图形 API 分别实现；
3. **独立透明置顶窗口**——零游戏适配、零侵入、Windows 原生支持，代价是**覆盖不了独占全屏**（独占全屏绕过 DWM 合成）。

本项目选 3。这是一次明确的取舍：用「独占全屏不支持」换取「任何游戏都能用、绝不触碰游戏进程」。设置窗口里把这条限制写清楚，而不是靠注入去假装支持。

## 2. 模块划分

```
App.xaml.cs                     进程生命周期、单实例、全局装配
├─ SingleInstanceGuard.cs       命名互斥量 Local\StillPoint.SingleInstance.v1
├─ SettingsService.cs           JSON 读写 + 0.4 s 防抖 + Normalise
├─ OverlayController.cs         浮层窗口的创建/显示/隐藏/显示器重定位
│  └─ OverlayWindow.xaml(.cs)   透明置顶窗口 + 1.2 s 置顶校正
│     └─ OverlaySurface.cs      FrameworkElement 渲染宿主（缓存 DrawingGroup）
│        └─ DotRenderer.cs      画 16 个点（含光晕）
│           └─ DotLayout.cs     4×4 网格几何（全部按比例）
├─ Views/SettingsWindow.xaml   设置界面（自定义标题栏 + WindowChrome）
│  └─ Controls/DotPreview.cs    16:9 内嵌预览（复用 DotRenderer）
├─ Services/HotkeyService.cs    RegisterHotKey 全局快捷键（消息专用窗口）
├─ Services/TrayService.cs      NotifyIcon + 右键菜单
├─ Services/MonitorService.cs   EnumDisplayMonitors / GetMonitorInfoW
├─ Services/CompatibilityService.cs  当前前台窗口与「是否全屏」
├─ Services/Log.cs              %LOCALAPPDATA%\StillPoint\logs\stillpoint.log
└─ Interop/NativeMethods.cs     全部 Win32 P/Invoke 集中处
```

数据流只有一条：**设置模型（`OverlaySettings`）是唯一真相**。滑杆 → 属性 setter（夹取范围）→ `Changed` 事件 → 预览重绘 + 浮层重绘 + 0.4 s 防抖落盘。没有第二份状态，因此「预览」与「真实浮层」不可能不一致——它们调用的是同一个 `DotRenderer`，传的是同一个对象。

## 3. 浮层窗口的关键实现

### 3.1 窗口样式

`OverlayWindow` 是普通 WPF `Window`：`WindowStyle=None`、`AllowsTransparency=True`、`Background=Transparent`、`ShowInTaskbar=False`、`Topmost=True`、`ShowActivated=False`、`IsHitTestVisible=False`。

句柄创建后再补三项 Win32 扩展样式（`NativeMethods.MakeOverlayWindow`）：

| 样式 | 作用 |
| --- | --- |
| `WS_EX_LAYERED` | 允许逐像素透明（DWM 合成时整窗不参与命中测试） |
| `WS_EX_TRANSPARENT` | 鼠标点击穿透：点击直接落到下面的窗口 |
| `WS_EX_NOACTIVATE` | 点击/显示都不抢焦点 |
| `WS_EX_TOOLWINDOW` | 不出现在 Alt+Tab 与任务栏 |

`ShowActivated=False` + `WS_EX_NOACTIVATE` 双保险，实测点击前后前台窗口没变化。

### 3.2 置顶校正（而不是「设一次 Topmost 就完事」）

Windows 里 `HWND_TOPMOST` 并不永久：其它置顶窗口、全屏应用切前台、显示器切换都可能改变 Z 序。所以 `OverlayWindow` 有一个 `DispatcherTimer`，**1.2 秒**一次，做两件很轻的事：

1. 用 `GetWindowRect` 比对窗口矩形与目标显示器矩形，不一致就重新 `SetWindowPos` 定位；
2. 前台窗口变化时，把浮层重新插到 `HWND_TOPMOST` 之后（`SWP_NOMOVE|SWP_NOSIZE|SWP_NOACTIVATE`）。

为避免「设置窗口一打开就被浮层盖住」，浮层支持一个 `AnchorProvider`：设置窗口可见时，浮层把自己的 Z 序插到设置窗口**之下**（`SetWindowPos` 的 `hWndInsertAfter` 传设置窗口句柄）；设置窗口隐藏后回落到 `HWND_TOPMOST`。同时校正逻辑会跳过「前台是本进程窗口」的情况，避免自己和自己抢 Z 序。

### 3.3 坐标与 DPI

- 设置窗口用 **DIP**（WPF 默认单位）；
- 浮层窗口用 `VisualTreeHelper.GetDpi(this)` 拿到缩放比，把 DIP 换算成**物理像素**再 `SetWindowPos`，保证覆盖到显示器物理矩形的最后一行像素（这正是 125% 缩放下最容易错的地方）；
- 十六个点的位置由 `DotLayout` 按**屏幕比例**计算：水平边距 17.54%、垂直边距 7.71%、点径 0.4514% 屏高、光晕半径 1.85 倍点半径。这些常数是从参考截图 2560×1440 上逐像素测出来再归一化的，换分辨率自动等比缩放。

### 3.4 渲染

`DotRenderer.Render(DrawingContext, width, height, settings, opacityMultiplier, radiusScale)`：

- 16 个点分两类：`column == 0 || column == 3` 为外圈（带光晕），其余为内圈（纯白点）；
- 光晕用 `RadialGradientBrush`，alpha 从 0.62 递减到 0，颜色取点色并乘以 `GlowOpacity`；外圈点的光晕**向外侧偏移**一点（不是正圆对称），符合参考画面的观感；
- 可选一层黑色柔影（`ShadowOpacity`，默认 0.10），让白点在浅色画面上也不糊边；
- 全部画进一个**冻结的 `DrawingGroup`**，按「尺寸 + 参数版本」缓存，参数不变时 `OnRender` 只做一次 `DrawDrawing`，不重算几何。

### 3.5 预览

`DotPreview` 把 16:9 的「虚拟屏幕」按比例缩放到预览区里（letterbox），再调用同一个 `DotRenderer`。因为预览只有约 400 px 宽，直接按屏高比例算出来的点会小到看不见，所以传一个 `radiusScale` 把点**视觉放大**到约 4 px —— 只影响预览的观感，布局比例仍与真实浮层完全一致（预览里两列之间的水平/垂直间距比与实机相同）。

### 3.6 主题切换

- `Styles/Theme.xaml`（浅色）和 `Styles/ThemeDark.xaml`（深色）是两份**键完全相同**的资源字典（各 23 个键，`analysis/check_keys.py` 校验差集为空），排版相关的 `UiFontFamily / DisplayFontFamily / BodyFontSize / CaptionFontSize` 也放在里面。
- 界面全部改用 `{DynamicResource …}`：`ThemeManager.Apply(dark)` 只是把 `Application.Current.Resources.MergedDictionaries[0]` 换成另一份字典，WPF 会自动让所有引用重新解析 —— 不需要重建窗口，也不会丢用户当前输入。
- `App` 在加载设置之后立即 `Apply(_settings.DarkTheme)`，并在 `OnSettingsChanged` 里比对前后值再切换，所以「界面点深色 / 重置全部设置 / 启动加载」三条路径都由同一处逻辑负责。
- 用 `DrawingContext` 直接画的元素（`DotPreview` 的取景框）收不到动态资源通知，所以它订阅 `ThemeManager.Changed` 并调用 `InvalidateVisual()`。
- 两个踩过的坑写在这里防止回退：① 深色字典必须带字体键，否则 `{DynamicResource UiFontFamily}` 解析为 null，`HwndSource` 在应用布局时抛 `Value cannot be null (Parameter 'fontFamily')`；② WPF 颜色格式是 `#AARRGGBB`，写成 CSS 的 `#RRGGBBAA` 会被解析成「近透明的黄」，表现为卡片出现亮黄描边。

## 4. 设置与持久化

- 模型：`OverlaySettings`（`INotifyPropertyChanged`），所有 setter 都做范围夹取；`DotSize / LayoutScale / Spacing / GlowOpacity / GlowRadius / DotOpacity / ShadowOpacity / Hotkey / DarkTheme …` 各有上下限，加载时 `Normalise()` 再走一遍 setter，坏数据自动回落默认值。
- 存储：`%LOCALAPPDATA%\StillPoint\settings.json`，写入方式是「先写 `.tmp` 再 `File.Move(overwrite)`」，避免掉电写坏；滑杆拖动期间 0.4 s 防抖，退出时 `Flush()` 强制落盘。
- 这个目录是唯一落点：删掉即卸载干净，没有任何注册表写入、没有服务、没有开机项。

## 5. 全局快捷键

`RegisterHotKey` 需要一个窗口句柄来接收 `WM_HOTKEY`，所以 `HotkeyService` 创建了一个 **message-only 窗口**（`HwndSource`，`ParentWindow = HWND_MESSAGE`），不显示、不占任务栏。默认 `Ctrl+Alt+D`：`HotkeyParser.TryParse` 要求至少一个修饰键、只允许一个普通键，避免误占游戏常用键；注册失败会在设置窗口显示原因（例如被别的程序占用），不会静默失败。

## 6. 兼容性判定

`CompatibilityService.DescribeForeground()` 读取前台窗口的标题、类名、进程名，再用 `GetWindowRect` 与 `MonitorFromWindow` 的显示器矩形比对（±2 px）判断窗口是否铺满整屏，从而给出「桌面 / 窗口模式 / 全屏（无边框）」的提示。它**不判断**独占全屏是否被覆盖——那由实测结论说话（不覆盖），界面直接给出建议。

## 7. 有意不做的事

- 不做注入、不做 D3D Hook、不做 Overlay 图层共享；
- 不读游戏内存、不读游戏窗口内容、不截图（包括不做「自动隐藏于截图」这类需要采集的功能）；
- 不做开机自启（需求是「可选、默认不开」，本版未实现，避免留下静默驻留项）；
- 不做多显示器选择 UI（`OverlaySettings.MonitorDevice` 字段与 `MonitorService.ResolveDisplay` 已就绪，界面留待后续版本）。

## 8. 性能取舍

浮层只在「参数变化 / 尺寸变化」时重绘；空闲时整个应用只有 1.2 秒一次的两次窗口 API 调用。实测空闲 45 秒累计 CPU 0.047 s、常驻内存约 173 MB（其中绝大部分是 WPF 自身的渲染与字体开销）。代价是内存不算小，换来的是「无需引入 Direct2D 互操作、代码量小、行为可预测」。
