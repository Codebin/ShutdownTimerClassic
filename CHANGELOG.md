# 更新日志

本仓库是 [ShutdownTimerClassic](https://github.com/lukaslangrock/ShutdownTimerClassic) 的中文汉化与改造分支。
上游的变更历史见其仓库；本文件只记录**本分支相对上游所做的改动**。

格式参考 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/)，版本号遵循语义化版本。

---

## 1.3.3-zh.1（本分支）

基于上游 `37c955e`。

### 新增

- **简体中文界面**：全部可见文本走资源文件，共 148 条双语条目
- **界面语言切换**：设置 → 常规 → 界面语言，支持 `自动（跟随系统）` / `English` / `简体中文`；切换后重启生效
- **鼠标穿透**：倒计时窗口可忽略鼠标输入（`WS_EX_TRANSPARENT` + `WS_EX_LAYERED`）；托盘菜单新增常驻开关，开启时弹提示
- **窗口大小可调**：`Ctrl + 鼠标滚轮` 缩放倒计时窗口，尺寸持久化并在下次启动恢复；设置页提供"重置大小"
- **.NET 8 交叉编译支持**：开启 `EnableWindowsTargeting`，Linux 上也能编译，用于快速 CI 检查
- **CI**：新增 Linux 编译检查（`-warnaserror`）+ 本地化校验 + 自包含便携版产物
- **工具链**：`tools/` 下的资源生成、文本替换、key 校验、破版估算与一键验证脚本
- **文档**：`README.zh-CN.md` 中文文档；`Structure.md` 补充本地化架构说明

### 变更

- **运行时**：.NET Framework 4.8 → **.NET 8**（`net8.0-windows`，SDK 风格工程文件）
- **电源动作解耦**（关键架构修正）：上游把下拉框的显示文本当业务标识使用。现在内部统一用 `PowerAction` 枚举，`settings.json`、CLI 参数与日志继续存英文 Key，界面显示本地化名。
  - 兼容性：读取配置与解析 CLI 时**同时接受**英文 Key（大小写不敏感）、当前语言的显示名，以及历史别名 `Reboot` / `Logoff`。**老配置文件与脚本无需修改。**
- **字体按语言选择**：上游在四个窗体与三个控件上硬编码了无中文字形的字体（`Microsoft Sans Serif` / `Consolas`），中文会渲染成方块。现在由 `Loc.CreateUiFont()` 按语言返回字体（中文优先微软雅黑）。
- **DPI 配置方式**：`App.config` 的 `DpiAwareness` 与 `app.manifest` 的 `dpiAware` 改为代码里的 `Application.SetHighDpiMode(PerMonitorV2)`（.NET 8 不再读取前者）
- **倒计时窗口标题**：由 `Action + " Timer"` 字符串拼接改为资源模板 `{0} Timer` / `{0}倒计时`，修正中文语序
- **CI 构建方式**：`msbuild + nuget restore` → `dotnet build`

### 修复

- 移除 `Settings.resx` 中已迁移到 `Strings.resx` 的 `aboutRichTextBox.Text` 死条目，避免同一文案存在两份真相
- 移除 `Settings.Designer.cs` 中托盘主题下拉的英文 `Items` 死代码（运行时已由代码绑定）

### 已知问题 / 待办

- **`WindowsInstallerPackaging.vdproj` 已失效**：仍写死 `.NETFramework,Version=v4.8` 与 `bin\Release\ShutdownTimerClassic.exe`，迁移后框架与产物路径均不匹配。需要在 Visual Studio 的 Installer Projects 扩展中重建才能出 MSI。在此之前，分发以 CI 产出的自包含便携 ZIP 为准（或本地跑 `tools/publish.ps1`）。
- **`WindowsApplicationPackaging.wapproj`（Microsoft Store 包）未经验证**：通过 ProjectReference 挂接主项目，理论上仍可用，但未在 .NET 8 下实测。
- **界面运行时行为需在 Windows 上目视确认**：字体渲染、鼠标穿透手感、缩放后的布局，均只做了静态检查。
