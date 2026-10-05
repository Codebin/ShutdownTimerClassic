# 更新日志

本仓库是 [ShutdownTimerClassic](https://github.com/lukaslangrock/ShutdownTimerClassic) 的中文汉化与改造分支。
上游的变更历史见其仓库；本文件只记录**本分支相对上游所做的改动**。

格式参考 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/)，版本号遵循语义化版本。

---

## 1.3.3（本分支，基于上游 `37c955e`）

> 版本号刻意保持纯数字 `1.3.3`，**不要**加 `-zh.1` 之类的预发布标签。
> `Helpers/Settings.cs` 的迁移逻辑会把 `Application.ProductVersion` 按 `.` 切分成 4 段整数，
> `"3-zh"` 解析失败会被填成 0，从而误判为 v1.3.0 并强制改写用户的 `DefaultTimer.CountdownMode`。
> 区分本分支请靠 `CHANGELOG.md` 与仓库来源，而不是版本号。

### 新增

- **简体中文界面**：全部可见文本走资源文件，共 151 条双语条目
- **界面语言切换**：设置 → 常规 → 界面语言，支持 `自动（跟随系统）` / `English` / `简体中文`；切换后重启生效
- **鼠标穿透**：倒计时窗口可忽略鼠标输入（`WS_EX_TRANSPARENT` + `WS_EX_LAYERED`）；托盘菜单新增常驻开关，开启时弹提示
- **窗口大小可调**：`Ctrl + 鼠标滚轮` 缩放倒计时窗口，尺寸持久化并在下次启动恢复；设置页提供"重置大小"
- **.NET 8 交叉编译支持**：开启 `EnableWindowsTargeting`，Linux 上也能编译，用于快速 CI 检查
- **CI**：新增 Linux 编译检查（`-warnaserror`）+ 本地化校验 + 自包含便携版产物
- **工具链**：`tools/` 下的资源生成、文本替换、key 校验、破版估算、发布说明抽取与一键验证脚本
- **兼容性契约测试**：`tests/ShutdownTimer.Tests/` 锁住老 `settings.json` 与 CLI 脚本所依赖的行为（解析规则、Key 拼写、优雅模式判断、资源解析与占位符）。需在 Windows 上执行，CI 的 Windows job 已接入
- **发布产物**：`tools/publish.ps1` 产出按 RID 自包含的便携 ZIP；CI 的 Windows job 同时上传该产物
- **Release 发版**：打 tag `v*` 自动产出 4 个 ZIP（`win-x64` / `win-arm64` × self-contained / framework-dependent）。`tools/release_notes.py` 只取 CHANGELOG 中对应版本的段落作为发布说明——内部「已知问题 / 待办」不会被打到公开页面上
- **文档**：`README.zh-CN.md` 中文文档；`Structure.md` 补充本地化架构说明
- **运行时上锁**：托盘菜单新增"锁定倒计时"项，可对正在运行的倒计时补设密码并立即上锁（上游只能在点击开始后的启动流程里设密码，运行中无法反悔加锁）；已有密码时该项自动禁用，防止覆盖现有密码
- **托盘悬浮提示显示剩余时间**：托盘图标的悬停 tooltip 每秒刷新为"定时关机：剩余 HH:MM:SS"，不用点开窗口也能看倒计时
- **重启应用前确认**：托盘菜单"重启应用程序"先弹确认框，防止误点丢掉当前倒计时（密码解锁后的续跑不会二次确认）
- **倒计时结束通知**：到点执行电源操作前先发托盘气泡"倒计时已结束"（此前直接静默退出；`DisableNotifications` 时跳过）
- **设置页"窗口大小："标签**：重置按钮左侧补上说明标签，与 `Ctrl+滚轮` 缩放说明呼应

### 变更

- **运行时**：.NET Framework 4.8 → **.NET 8**（`net8.0-windows`，SDK 风格工程文件）
- **电源动作解耦**（关键架构修正）：上游把下拉框的显示文本当业务标识使用。现在内部统一用 `PowerAction` 枚举，`settings.json`、CLI 参数与日志继续存英文 Key，界面显示本地化名。
  - 兼容性：读取配置与解析 CLI 时**同时接受**英文 Key（大小写不敏感）、当前语言的显示名，以及历史别名 `Reboot` / `Logoff`。**老配置文件与脚本无需修改。**
- **字体按语言选择**：上游在四个窗体与三个控件上硬编码了无中文字形的字体（`Microsoft Sans Serif` / `Consolas`），中文会渲染成方块。现在由 `Loc.CreateUiFont()` 按语言返回字体（中文优先微软雅黑）。
- **DPI 配置方式**：`App.config` 的 `DpiAwareness` 与 `app.manifest` 的 `dpiAware` 改为代码里的 `Application.SetHighDpiMode(PerMonitorV2)`（.NET 8 不再读取前者）
- **倒计时窗口标题**：由 `Action + " Timer"` 字符串拼接改为资源模板 `{0} Timer` / `{0}倒计时`，修正中文语序
- **CI 构建方式**：`msbuild + nuget restore` → `dotnet build`
- **版本号单一真相源**：程序集元数据（版本、标题、公司、Guid、`NeutralResourcesLanguage` 等）从手写 `Properties/AssemblyInfo.cs` 收口到 csproj 的 `<Version>1.3.3.0</Version>` 等属性，由 SDK `GenerateAssemblyInfo` 生成；手写文件只剩 `SupportedOSPlatform`。`IncludeSourceRevisionInInformationalVersion=false` 防止 `ProductVersion` 混入 git 提交号——那会让 `Helpers/Settings.cs` 的版本迁移解析失败并误改写用户的 `CountdownMode`

### 修复

- 移除 `Settings.resx` 中已迁移到 `Strings.resx` 的 `aboutRichTextBox.Text` 死条目，避免同一文案存在两份真相
- 移除 `Settings.Designer.cs` 中托盘主题下拉的英文 `Items` 死代码（运行时已由代码绑定）
- 补齐两处漏网的硬编码英文：`Menu.Designer.cs` 的 `infoToolTip.ToolTipTitle = "Help"`（中文界面下提示正文已翻译、标题栏仍是英文）与 `Countdown.Designer.cs` 的 `notifyIcon.BalloonTipTitle = "Shutdown Timer"`。前者新增 key `Menu.Tip.Title`，后者复用 `App.Title`
- 移除 `App.config` 里 .NET Framework 4.8 遗留的 `supportedRuntime` 与 WinForms `DpiAwareness` 两节：.NET 8 均不读取，留着只会让人去改一个不生效的开关
- 密码错误弹窗标题由泛用的"密码保护"改为专用的"密码错误"（`Countdown.PasswordWrongTitle`），倒计时窗体内的密码对话框统一改用 `Countdown.PasswordTitle`
- 自定义命令执行失败的弹窗标题由误用的"倒计时更新"改为"自定义命令错误"（`Countdown.Err.CustomCommandTitle`）
- 删除冗余资源 key `Countdown.PasswordPromptUnlock`（解锁流程实际使用 `…PromptByAction` / `…PromptPlain`，该 key 从未被引用）

### 已知问题 / 待办

- **`WindowsInstallerPackaging.vdproj` 已失效**：仍写死 `.NETFramework,Version=v4.8` 与 `bin\Release\ShutdownTimerClassic.exe`，迁移后框架与产物路径均不匹配。需要在 Visual Studio 的 Installer Projects 扩展中重建才能出 MSI。在此之前，分发以 CI 产出的自包含便携 ZIP 为准（或本地跑 `tools/publish.ps1`）。
- **`WindowsApplicationPackaging.wapproj`（Microsoft Store 包）未经验证**：通过 ProjectReference 挂接主项目，理论上仍可用，但未在 .NET 8 下实测。
- **界面运行时行为需在 Windows 上目视确认**：字体渲染、鼠标穿透手感、缩放后的布局，均只做了静态检查。
