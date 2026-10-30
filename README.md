# StillPoint

**Comfort Overlay — 一个给 Windows 游戏用的 16 点「防晕动」浮层。**

长时间玩第一人称 / 高速 3D 游戏容易产生眩晕，业界常用做法是在画面上叠一层固定的「舒适点」：让视野里始终有静止的参照物，大脑对运动的冲突感会明显下降。StillPoint 把这件事做成一个独立、轻量、跨游戏的小工具——不需要任何游戏本体支持，不注入、不 Hook、不改游戏文件。

![截图：设置窗口](https://cdn.jsdelivr.net/gh/Calaberry/StillPoint@main/docs/images/settings-top.png)

> 图片走 jsDelivr CDN，避免 raw.githubusercontent.com 在部分网络下不可达；离线看可用仓库里的 `docs/images/` 原图。


---

## 灵感来源

- **《使命召唤》(Call of Duty)** 里内置的「防晕动点阵」：在画面上铺一圈静止的参照点，缓解第一人称视角快速旋转带来的冲突感。StillPoint 的默认点阵（4×4；中间 8 点纯白，左右两列 8 点带极淡的柔和光晕）就是照着游戏内 2560×1440 截图逐个像素量出来的——点径、行列间距、边距、光晕半径与透明度都写在 [`src/StillPoint/Models/DotLayout.cs`](src/StillPoint/Models/DotLayout.cs) 里，注释中附了实测数值。
- **iOS / iPadOS「设置」** 的界面语言：克制的留白、分组卡片、分段控件、滑杆与开关，「一个参数一行、右端显示数值」的排版。设置窗口按这套思路组织，但控件全部是 Windows 原生的 WPF 样式与系统字体，没有照搬 Apple 的素材、图标或品牌元素。

这两条都只是「借鉴方向」，代码与素材均为本项目原创。

---

## 快速开始

1. 从 [Releases](https://github.com/Calaberry/StillPoint/releases) 下载 `StillPoint-0.1.0-win-x64.zip`，解压后双击 `StillPoint.exe`（自包含单文件，**不需要安装 .NET**）。
2. 程序启动后先出现设置窗口，同时浮层已经按默认参数开始运行。
3. 直接进游戏：把游戏设为 **窗口化** 或 **无边框全屏**（Borderless / Windowed Fullscreen）。
4. 关掉设置窗口即可，浮层继续工作；需要再调时双击托盘图标。

首次运行不会写入任何开机启动项。配置文件在 `%LOCALAPPDATA%\StillPoint\settings.json`，日志在 `%LOCALAPPDATA%\StillPoint\logs\stillpoint.log`。删除该目录即等于卸载。

### 三种打包方式（自己构建时可选）

| 目录 | 体积 | 需求 | 实测冷启动 | 实测常驻内存 | 说明 |
| --- | --- | --- | --- | --- | --- |
| `dist/StillPoint` | 139 MB | 无 | 0.88 s | ≈151 MB（空闲时 142–162 MB） | **推荐**：自包含、启动快、内存最低（Release 里发的就是这个） |
| `dist/StillPoint-small` | 68 MB | 无 | 1.07 s | ≈272 MB | 下载体积小，代价是单文件压缩后程序集常驻内存 |
| `dist/StillPoint-lite` | 1.0 MB | .NET 8 桌面运行时 | 0.69 s | ≈148 MB | 体积最小；机器上没装 .NET 8 时装不上 |

三者功能完全一致，只是打包方式不同。`dist/` 不进版本库，Release 附件与上表第一个目录内容一致。

---

## 它做什么 / 它不做什么

| 做 | 不做 |
| --- | --- |
| 用一个**独立的透明置顶窗口**在桌面最上层画 16 个点 | 不注入 DLL、不 Hook 渲染管线、不改游戏文件 |
| 鼠标点击**完全穿透**，键盘焦点不被抢走 | 不读取 / 不修改游戏内存 |
| 支持窗口化、无边框全屏 | 不截图、不录屏、不做任何屏幕采集 |
| 参数本地保存，下次启动自动恢复 | 不需要账号、不联网 |

技术路线就是最朴素的 Win32 + WPF：`WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW` + `HWND_TOPMOST`，每 1.2 秒做一次极轻量的「置顶校正」，其余时间完全空闲（实测空闲 CPU ≈0.1%，常驻内存 ≈150–175 MB）。

---

## 点阵样式

16 个点固定为 4×4 等距网格，和参考画面（《使命召唤》内置舒适点）一致：

- **中间两列共 8 个点**：纯白的干净圆点，边缘只做 1 px 左右的柔化，不加任何光晕；
- **最左、最右两列共 8 个点**：白点外侧带一层很淡的柔和光晕（默认强度 13%，半径约 1.85 倍点半径），越往外越淡。

间距、点径、光晕都按**屏幕比例**计算，而不是写死像素：2560×1440 下默认点径约 6.5 px、水平间距 554 px、垂直间距 406 px，换到其它分辨率会等比缩放，形状不变形。

---

## 设置项

| 分组 | 内容 |
| --- | --- |
| 顶栏 | 产品名、副标题、运行状态、总开关、最小化 / 关闭（关闭只是隐藏窗口，浮层继续跑） |
| 预览 | 与实际浮层**同一套参数与渲染代码**的 16:9 实时预览，可切换「游戏场景 / 浅色 / 深色」背景 |
| 外观 | 6 个预设色 + 自选颜色（内置 HSV 取色器：明度/饱和度面板 + 色相条 + 十六进制输入，拖动即时生效）+ 恢复默认；点阵大小、光晕强度、不透明度三个滑杆（实时生效并显示数值）；界面浅色 / 深色主题（原地切换，不用重启） |
| 布局 | 整体缩放、间距（默认水平垂直联动，可拆开分别调）；恢复默认布局 |
| 运行与兼容性 | 浮层状态与覆盖显示器、隐藏/显示点阵、全局快捷键（可改、可关）、当前前台窗口与窗口模式、三条兼容性说明 |
| 底栏 | 版本号、重置全部设置、退出 StillPoint |

![自选颜色：内置 HSV 取色器](https://cdn.jsdelivr.net/gh/Calaberry/StillPoint@main/docs/images/colour-picker.png)

深色主题（`外观 → 界面 → 深色`，原地切换并记住选择）：

![深色主题](https://cdn.jsdelivr.net/gh/Calaberry/StillPoint@main/docs/images/settings-dark.png)

所有滑杆都是即时生效：拖动的同时预览与真实浮层一起变，松手后 0.4 秒防抖写入磁盘。

**全局快捷键**默认 `Ctrl+Alt+D`，只在设置窗口之外生效，避开游戏常用键位；点快捷键按钮后按任意组合键即可改（要求至少一个修饰键，`Esc` 取消）。

---

## 兼容性一览

| 场景 | 结果 |
| --- | --- |
| 窗口化游戏 | ✅ 浮层覆盖整个桌面，稳定显示在游戏窗口之上 |
| 无边框全屏（Borderless） | ✅ 实测覆盖，鼠标点击穿透，焦点不被抢 |
| 独占全屏（Exclusive Fullscreen） | ⚠️ **不覆盖**。Windows 的独占全屏绕过桌面合成，任何普通置顶窗口都画不上去（除非用注入类做法，本工具不做）。请把游戏改成「无边框全屏」或「窗口化全屏」 |
| 独占全屏时 | 浮层不会消失、不会报错，切回桌面/无边框后自动重新出现 |

详细测试环境、方法与逐项证据见 [docs/COMPATIBILITY.md](docs/COMPATIBILITY.md)，架构说明见 [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)。

![无边框全屏](https://cdn.jsdelivr.net/gh/Calaberry/StillPoint@main/docs/images/fullscreen-borderless.png)

---

## 已知限制

- **独占全屏不覆盖**（见上）。这是 Windows 桌面合成机制的限制，不是 bug，也不是「还没做」。
- 目前固定覆盖**主显示器**；数据结构里已预留 `MonitorDevice`，多显示器选择留到后续版本。
- 只在 **2560×1440 / 125% DPI** 的机器上实测过；其它分辨率与 100% / 150% 缩放理论上等比正确（坐标全部是比例 + DPI 换算，不含写死像素），但未逐项实测。
- 单文件 exe **没有代码签名**，首次运行可能被 SmartScreen 拦一次，选「更多信息 → 仍要运行」。
- 托盘图标默认在 Windows 11 的「隐藏的图标」溢出区里，需要的话从溢出面板拖到任务栏固定。
- 没有开机自启（按需求「可选、默认不开」处理，本版未做）。

---

## 从源码构建

需要 .NET 8 SDK（[下载](https://dotnet.microsoft.com/download/dotnet/8.0)）。在仓库根目录执行：

```powershell
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'; $env:DOTNET_NOLOGO='1'
dotnet build 'src\StillPoint\StillPoint.csproj' -c Release

# 自包含单文件（目标机器无需 .NET）
dotnet publish 'src\StillPoint\StillPoint.csproj' `
    -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -o dist\StillPoint
```

调试运行时如果机器上只有 .NET 6/7，需要把 `DOTNET_ROOT` 指向 SDK 目录，否则 apphost 会提示「You must install or update .NET」：

```powershell
$env:DOTNET_ROOT='C:\Program Files\dotnet'
& 'src\StillPoint\bin\Debug\net8.0-windows\StillPoint.exe'
```

---

## 目录结构

```
StillPoint/
├─ README.md                  本文件
├─ LICENSE                    MIT
├─ docs/
│  ├─ COMPATIBILITY.md        兼容性测试报告（环境、方法、逐项结果、证据截图）
│  ├─ ARCHITECTURE.md         模块划分与关键实现决策
│  └─ images/                 文档用截图
├─ src/StillPoint/
│  ├─ Models/                 OverlaySettings（设置模型）、DotLayout（16 点几何）
│  ├─ Rendering/              DotRenderer（画点/光晕）、OverlaySurface（渲染宿主）
│  ├─ Views/                  SettingsWindow（设置窗口）、OverlayWindow（浮层窗口）
│  ├─ Controls/               DotPreview（设置窗口里的 16:9 预览）、ColorPicker（HSV 取色器）
│  ├─ Services/               OverlayController / SettingsService / HotkeyService /
│  │                          MonitorService / TrayService / CompatibilityService /
│  │                          ThemeManager / Log
│  ├─ Interop/                NativeMethods（Win32 P/Invoke）
│  ├─ Styles/                 Theme.xaml、ThemeDark.xaml（配色/字体）、Controls.xaml（控件样式）
│  └─ Resources/              StillPoint.ico / .png（应用与托盘图标）
├─ tools/                     兼容性测试工具（FakeGame 无边框全屏、ExclusiveGame 独占全屏、
│                             capture.ps1 截图、clicktest.ps1 真实点击注入）
└─ analysis/                  开发过程中的测量脚本（点阵定位、截图比对、控件遍历）
```

`dist/`、`.dotnet/`、`.tools/`、`bin/`、`obj/` 与测量截图都不进版本库，见 `.gitignore`。

---

## 版本

`0.1.0` — 首个可交付版本：浮层 / 设置 / 预览 / 持久化 / 托盘 / 快捷键 / 深色主题 / 兼容性实测 / 打包与文档。

---

## 许可证

[MIT](LICENSE) © 2026 Calaberry

参考截图来自《使命召唤》游戏画面，仅用于说明点阵样式，其版权归 Activision 所有，不在本仓库分发。
