# 项目状态快照 · PROGRESS

> **这是给"下一个会话"看的，不是给人看的。** 想给人看进度，读 `docs/STATUS.md`（一页看板 + 三条轨道完成度），
> 刷新用 `python3 tools/progress.py`，防看板撒谎用 `--check`。
> 任何新会话开场只读这一个文件 + `docs/SESSIONS.md` 对应小节，不要重新读代码。
> 每次会话结束**必须**更新本文件的「最后更新」和「下一步」两节，然后 commit。
> 真相来源优先级：`git log` > 本文件 > 对话记忆。对话记忆不可靠，随时会因上下文爆掉而丢失。

---

## 最后更新

- 时间：2026-10-05（第二轮会话）
- 分支：`feat/i18n-zh`，**19 个 commit**，作者已统一为 `Codebin <Codebin@users.noreply.github.com>`
- 备份 tag：`backup/pre-author-fix` = 改写作者前的快照。`git diff backup/pre-author-fix HEAD` 为空，即只改作者、代码一字未动
- remote 已按标准 fork 工作流重排：
  - `origin` → `https://github.com/Codebin/ShutdownTimerClassic.git`（**已 fork**，parent = lukaslangrock，默认分支 master）
  - `upstream` → `https://github.com/lukaslangrock/ShutdownTimerClassic.git`
- **尚未 push**：沙箱外网 DNS 间歇性中断（`api.github.com` / `github.com` / `deb.debian.org` 同时解析失败，Docker 内部解析器 `127.0.0.11` 无响应）。fork 与 gh 设备码登录在此之前已完成。网络恢复后第一件事就是 push。
- gh 已登录 `Codebin`（token 存 `/data/.config/gh/hosts.yml`，不落聊天记录）
- 校验：`check_i18n.py`（`en=149 zh-CN=149`）/ `gen_strings.py` / `check_layout.py` / `release_notes.py` 全部退出码 0；三个 workflow 的 YAML 语法通过（沙箱无 actionlint）

## 已完成（相对上游 `37c955e`，45 文件 / +3779 −418）

| 块 | 状态 | 证据 |
|---|---|---|
| .NET Framework 4.8 → .NET 8 | ✅ | `0d389e5`，csproj 纯 SDK 风格，无旧 Reference 残留 |
| 本地化层（Loc / PowerAction / LocalizedOptions） | ✅ | `a5c9faf`，148 条双语，en/zh-CN 对齐，无空值 |
| 四窗体文本全量走资源 | ✅ | `ff8025f`，MessageBox / 托盘 / 通知均 `Loc.T` |
| 字体按语言选择（防中文方块） | ✅ | `04ed0ae`，`Loc.CreateUiFont()` |
| 鼠标穿透 + Ctrl+滚轮缩放 | ✅ | `ab23c75` |
| 语言切换 UI | ✅ | `a81e15c` |
| 兼容性契约测试 | ✅ | `79623f1`，`tests/`，已在 sln 内 |
| CI（Linux 编译检查 + Windows build/test/publish） | ✅ | `dotnet.yml`，Windows job 真跑 `dotnet test` |
| Release 发版 | ✅ | `f06b990`，tag `v*` 触发，双 RID × 双形态 |
| 文档（README.zh-CN / Structure / CHANGELOG） | ✅ | — |

**判定：核心编码已完成，无编译阻塞。** 剩余是收口项，见下。

## 待办（已定位到行号，无需重新调查）

### A. 中文界面会露英文 —— ✅ 已修（`fix(i18n): route tooltip title…`）
- `Menu.Designer.cs:288` `ToolTipTitle = "Help"` → `Loc.T("Menu.Tip.Title")`，新增该 key（双语条目 148 → 149）
- `Countdown.Designer.cs:85` `BalloonTipTitle = "Shutdown Timer"` → `Loc.T("App.Title")`
- 顺带清掉 `App.config` 两节死配置（`supportedRuntime` / WinForms `DpiAwareness`，.NET 8 均不读取）
- **仍待处理（建议级，非必修）**：`Settings.Designer.cs:812` `"Font Awesome:"`；4 处 `AccessibleName`/`AccessibleDescription`（仅屏幕阅读器可见）

### B. 资源已备好但未接线的 6 个 key —— 功能缺口
| key | 缺什么 | 接线点 |
|---|---|---|
| `Countdown.ConfirmRestart` / `ConfirmRestartTitle` | 重启前无确认弹窗 | `Countdown.cs:293-318 RestartApplication()` |
| `Countdown.PasswordPromptLock` | 无「输入密码以上锁」流程 | `Menu.cs:195-196` 附近 |
| `Countdown.PasswordWrongTitle` | 密码错误弹窗标题错用 `Menu.PasswordTitle` | `Countdown.cs:532` |
| `Settings.CountdownSize`（"窗口大小："） | 只有重置按钮，无标签 | `Settings.cs:117` 附近 |
| `Tray.Balloon.CountdownFinished` | 倒计时到点直接退出，无结束通知 | `Timer.cs:131-137` |
| `Countdown.PasswordPromptUnlock` | 真冗余（代码用 `…PromptByAction/Plain`） | 删 key 或接线，二选一 |

### C. 假阳性（不是 bug，是工具盲区）
`check_i18n.py:26` 只扫 `Loc.T("…")`，漏了 `new LocalizedOption(value, "key")` 形式 →
`Language.Automatic/En/ZhCn`、`TrayTheme.Automatic/Light/Dark` 这 6 个 key **实际在用**（`Helpers/LocalizedOptions.cs:34-36,42-44`）。
修法：给 `check_i18n.py` 增加 `LocalizedOption(` 的引用扫描，别再让人查一遍。

### D. CI / 发布缺口 —— ✅ 大部分已修（`ci(release): …` / `ci(local-build): …`）

已修：
- **`release.yml` 的 `python3`**：`windows-latest` 上 Python 官方安装包只创建 `python.exe`/`py.exe`，没有 `python3` → 原先这一步必然失败，**tag 推上去也出不来 Release**。现改为显式 `actions/setup-python@v5` + 统一 `python`。
- `release.yml` 补上 resx 已提交守卫（与 `dotnet.yml` 对齐）
- Release 说明改用 `tools/release_notes.py`：只取对应版本段落，剔除「已知问题 / 待办」与维护者注记；取不到段落就失败，宁可不出 Release
- `local-build.yml` **已提交**，并修掉 4 个缺陷：pwsh `catch`+`Write-Error` 语义反了（改 `Get-Command` 探测 + step output）、publish 前不清空 `artifacts\publish`、缺 `setup-dotnet` pin、缺 `timeout-minutes`；另补 resx 守卫

**仍未处理**：
- 版本号双真相源：`csproj:29 <Version>1.3.3</Version>`（只被 `publish.ps1:28` 读）vs `Properties/AssemblyInfo.cs:38-39`（真正写进 exe）；`vdproj:157` 还写着过期的 `1.3.2.0`
- `actions/checkout@v6` 落后一个大版本（最新 v7）——非 bug，本轮刻意不动以缩小改动面

### E. 打包链路（低优先，AI 帮不上多少）
- `WindowsInstallerPackaging.vdproj` 已失效：`:85,88,117,120` 写死 `.NETFramework,Version=v4.8`；`:145` 安装条件同；`:166,206,226` 指旧产物 `bin\Release\ShutdownTimerClassic.exe`；`:257` 只带 `Newtonsoft.Json.dll`，缺 .NET 8 新增依赖与 `zh-CN` 卫星程序集。**只能在 Visual Studio Installer Projects 扩展里手工重建。**
- `WindowsApplicationPackaging.wapproj` 不引用 4.8，靠 `ProjectReference`（`:112`）挂主项目，理论上可用但从未实测。
- 注意：`dotnet build` 整个 sln 在 **非 AnyCPU 平台会 MSB4278 硬失败**（wapproj 有 `Build.0`，`:53-75`）；Release\|Any CPU 下 vdproj 刷 MSB4078 警告。CI 只 build csproj，所以没踩到——本地手动 build sln 会踩。

### F. 文档
- `Structure.md` 漏记 `Timer.cs`（上游原有文件，`e500f4e` 引入；静态倒计时状态机，被 Menu/Countdown/Program 广泛引用）
- `CHANGELOG.md:36` 称 `App.config` 的 `DpiAwareness` 已移除，实际 `App.config:8-10` 仍在（`app.manifest` 侧确实已移除）

## 下一步

1. **网络恢复后先 push**：`git push -u origin feat/i18n-zh`，然后 `gh run list` 盯 Actions。
2. **fork 的 Actions 默认是关闭的**——不启用则 workflow 根本不跑，不是配置问题。仓库页 Actions → *I understand my workflows, go ahead and enable them*，或 `gh api -X PUT repos/Codebin/ShutdownTimerClassic/actions/workflows/dotnet.yml/enable`。
3. 然后按 `docs/SESSIONS.md` 做 **S2**（接线 6 个孤儿 key）与 **S3**（修 `check_i18n.py` 的 `LocalizedOption` 盲区）。
4. **S5 必须在 S2/S3 之后**：Windows 真机 `dotnet test` + 目视验证（字体、穿透手感、缩放破版）。沙箱无 dotnet，这一步只能靠 self-hosted runner 或主人本机。
5. 全部通过后才打 tag `v1.3.3`。**tag 用纯数字，勿加 `-zh.1`**——原因见 `CHANGELOG.md:12-15`（版本迁移代码按 `.` 切 4 段整数，解析失败会误判 v1.3.0 并强制改写用户的 `CountdownMode`）。
6. 若要把上游后续改动合进来：`git fetch upstream && git rebase upstream/master`（本分支尚未 push 过，改写历史安全；已 push 之后改用 merge）。

## 环境约束（重要）

- 当前沙箱**没有 dotnet SDK**（`dotnet: not found`），只有 `python3`。
  → 任何需要编译/测试的会话必须在 Windows 真机或 self-hosted runner 上跑，本地只能做静态检查。
- WinForms 测试只能在真 Windows 上执行（Linux 只能编译）。
