# 会话拆分执行方案 · SESSIONS

> 前提认知：**卡死不是模型笨，是上下文被喂了不该喂的东西。**
> 本项目 4 个窗体 `.resx` 合计 854 KB / 9830 行（全是 base64 图片），`Countdown.resx` 单个 332 KB ≈ 8–10 万 token。
> 一次 `read_file` 读它，会话就废了。所以拆分的核心不是"拆任务"，是**拆上下文**。

---

## 一、防卡死六条铁律（每个会话开场先贴这段）

1. **状态外置**：真相 = `git log` + `docs/PROGRESS.md`。不靠对话记忆。会话开场只读这两个，禁止"我们之前说过…"。
2. **大文件黑名单**：以下文件**禁止全量 `read_file`**，只允许 `grep -n` 定位 + `sed -n '起,止p'` 定点读（±20 行）：
   ```
   src/ShutdownTimer/*.resx        # 除 Strings.resx / Strings.zh-CN.resx
   src/ShutdownTimer/*.Designer.cs # 975 行的 Settings.Designer.cs 也禁止全读
   ```
3. **改 Designer 优先走工具**：`tools/localize_designer.py` 做机械替换，别手改 Designer（会破坏 VS Designer）。
4. **子 agent 只回结论**：给子 agent 的 prompt 末尾必写「输出 ≤60 行，只给结论 + `文件:行号`，不要贴代码」。审计/搜索类活全部丢子 agent，主会话只收摘要。
5. **单会话单职责 + 结束即 commit**：一个会话只做一个 S 节。commit 是检查点，卡死了也能从上一个 commit 复活。
6. **上下文预算**：单会话全量 `read_file` 次数 ≤ 5，且单次 ≤ 400 行。超了就说明任务拆粗了，停下来再拆。

## 二、会话开场模板（复制即用）

```
读 docs/PROGRESS.md 和 docs/SESSIONS.md 的 <Sx> 节，只做这一节。
禁止全量读取：src/ShutdownTimer/*.resx（Strings*.resx 除外）、*.Designer.cs。
需要看这些文件时用 grep -n 定位 + sed -n 'a,bp' 只读 ±20 行。
改完跑验收命令，把结果贴出来，然后 git add -A && git commit（不要 push）。
最后更新 docs/PROGRESS.md 的「最后更新」「下一步」两节，一并 commit。
```

## 三、拆分表

| 会话 | 职责 | 改动文件 | 验收 | 预估 |
|---|---|---|---|---|
| **S1** | 修 2 处硬编码英文 + 补文档 | `Menu.Designer.cs:288`、`Countdown.Designer.cs:85`、`Structure.md`、`CHANGELOG.md:36` | `check_i18n.py` 绿 + `grep -n 'ToolTipTitle\|BalloonTipTitle'` 无英文 | 小 |
| **S2** | 接线 6 个孤儿 key（真功能缺口） | `Countdown.cs`、`Menu.cs`、`Settings.cs`、`Timer.cs`、`Strings*.resx`（经 `gen_strings.py`） | `check_i18n.py` 未引用 key 数下降；`dotnet build -warnaserror` | 中 |
| **S3** | 修 `check_i18n.py` 假阳性 | `tools/check_i18n.py` | 6 个 `Language.*`/`TrayTheme.*` 不再被报死 key | 小 |
| **S4** | CI/发布收口 | `.github/workflows/local-build.yml`（提交+修 4 缺陷）、`release.yml`（`python3`→`python`、resx 守卫、notes 裁剪）、版本号单一真相源 | `actionlint` 或 yamllint + 一次手动 `workflow_dispatch` | 中 |
| **S5** | **Windows 真机验证**（必须在真 Windows / self-hosted runner） | 无改动 | `dotnet test` 全绿 + 目视：字体渲染、鼠标穿透、缩放不破版 | 中 |
| **S6** | 打包链路（可选，低优先） | `vdproj`（只能 VS 手工重建）、`wapproj` 实测 | 出可安装 MSI / MSIX | 大，人工为主 |

**依赖顺序**：S1 → S2 → S3 可并行；S4 独立；**S5 必须在 S1–S3 之后**（否则测的是旧代码）；S6 最后，且可与前面完全解耦。

**当前沙箱没有 dotnet SDK**，所以 S1–S4 在这里只能做静态检查；S2/S4/S5 的编译与测试环节要在 Windows 上补。

## 四、每个会话的启动 prompt（直接粘）

### S1 · 硬编码英文收口
```
只做 S1。目标：中文界面不再露英文 + 补文档缺口。
1) src/ShutdownTimer/Menu.Designer.cs:288 的 ToolTipTitle = "Help"
2) src/ShutdownTimer/Countdown.Designer.cs:85 的 BalloonTipTitle = "Shutdown Timer"
两处改为走资源：先在 tools/gen_strings.py 的双语源表里加 key，跑 gen_strings.py 重生成 resx，
再在代码里用 Loc.T 引用（Designer 里不能调 Loc，所以在 Menu.cs / Countdown.cs 构造后覆盖）。
禁止全量读 Designer.cs 和 *.resx，用 sed -n '280,300p' 这类定点读。
3) Structure.md 补 Timer.cs 条目（静态类，倒计时状态机，见 PROGRESS.md F 节）
4) CHANGELOG.md:36 描述与 App.config:8-10 实际不符，二选一改正
验收：python3 tools/check_i18n.py && python3 tools/check_layout.py 退出码 0
commit: fix(i18n): route tooltip title and tray balloon title through resources
```

### S2 · 接线孤儿 key
```
只做 S2。目标：把 PROGRESS.md B 节 6 个 key 接上或删掉（逐个决策，别一律接）。
注意 Countdown.PasswordPromptUnlock 是冗余，删 key 即可；Tray.Balloon.CountdownFinished
需要新增结束通知路径（Timer.cs:131-137 到点即退，要想清楚通知在退出前还是退出后发）。
文案改动一律改 tools/gen_strings.py 的源表，跑 gen_strings.py 重生成，不要手改 resx。
禁止全量读 Countdown.cs（858 行）——用 grep -n 定位后 sed -n 定点读。
验收：check_i18n.py 的「未被引用 key」从 17 降到 ≤8；有 dotnet 时跑 dotnet build -warnaserror
commit: feat(i18n): wire up restart confirmation, lock prompt and countdown-finished notice
```

### S3 · 工具修盲区
```
只做 S3。tools/check_i18n.py:26 的 CALL_HEAD 只匹配 Loc.T("…")，
漏了 Helpers/LocalizedOptions.cs:34-36,42-44 的 new LocalizedOption(value, "key") 形式，
导致 Language.* 与 TrayTheme.* 共 6 个 key 被误报为死 key。
加一条引用模式（或把 key 提取规则改成扫所有字符串字面量再交叉比对），
让报告只列真冗余/真缺口。补一条注释说明为什么需要两种模式。
验收：python3 tools/check_i18n.py 不再列出这 6 个 key，退出码仍 0
commit: fix(tools): recognize LocalizedOption key references in i18n check
```

### S4 · CI/发布收口
```
只做 S4。按 PROGRESS.md D 节逐条修：
1) 提交 .github/workflows/local-build.yml，并修 4 个缺陷：
   pwsh catch 语义（:38）、publish 前清空 artifacts\publish（:58）、
   加 actions/setup-dotnet pin 8.0.x、加 timeout-minutes
2) release.yml:33-37 的 python3 → python（或加 actions/setup-python）
3) release.yml 补 dotnet.yml:28-34 的 resx 已提交守卫
4) release.yml:71 的 --notes-file 会公开「已知问题/待办」→ 改为只截取 CHANGELOG 对应版本段
5) 版本号单一真相源：让 csproj 的 <Version>/<AssemblyVersion>/<FileVersion> 成为唯一来源，
   删掉 Properties/AssemblyInfo.cs:38-39 的重复定义（注意 GenerateAssemblyInfo=false 的现状要一并调整）
6) actions/checkout@v6 → v7
不要 push。改完用 yamllint 或 actionlint 自检（没有就跳过并说明）。
commit: ci: fix local-build runner pitfalls and unify release version source
```

### S5 · Windows 真机验证（换机器/换会话）
```
只做 S5。这台是 Windows，有 .NET 8 SDK。
1) dotnet build src/ShutdownTimer/ShutdownTimer.csproj -c Release --nologo -warnaserror
2) dotnet test tests/ShutdownTimer.Tests/ShutdownTimer.Tests.csproj -c Release
3) 目视清单（逐项截图或明确说 OK/不 OK）：
   - 中文界面有无方块字；字体是否微软雅黑
   - 倒计时窗口 Ctrl+滚轮缩放后有无破版；重启后尺寸是否恢复
   - 鼠标穿透开关手感；托盘菜单中文
   - 语言切到 English 后是否全部回英文；切回中文重启是否生效
   - S1/S2 新接的弹窗与通知是否显示正确
4) 把结果写进 docs/PROGRESS.md，未通过项列成 issue 清单
commit: test: record windows runtime verification results for v1.3.3
```

## 五、卡死之后的恢复流程（一定会再发生，提前准备）

```bash
git log --oneline -8          # 上一个 commit 就是断点
git status --short            # 有没有半截改动
git stash list                # 有没有未提交的活
```
新会话开场：读 `docs/PROGRESS.md` + `docs/SESSIONS.md` 对应节 → 继续。**不要试图回忆之前会话。**

半截改动若无法判断状态：`git checkout -- .` 回到上一个 commit 重做该会话，比修补更便宜。

## 六、什么时候用子 agent，什么时候用新会话

| 情况 | 用什么 |
|---|---|
| 找东西 / 审计 / 读大文件回答问题（不改代码） | **子 agent**，主会话只收 ≤60 行摘要 |
| 要改代码、要 commit、要跑验收 | **新会话**，一个 S 节一个 |
| 多个互不依赖的审计 | **并行多个子 agent**（本次就是这么做的） |
| 需要 Windows 环境 | **新会话 + 换机器**，环境差异不能靠子 agent 跨过去 |
