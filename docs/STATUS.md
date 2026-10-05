# 项目进度看板 · STATUS

> **这个文件是给人看的。** 想掌握进度，只读这一页。
> 刷新数字：`python3 tools/progress.py`（会重写文末「客观指标」节，并重算三条轨道完成度）。
> 防看板腐烂：`python3 tools/progress.py --check` —— 表格里的状态与 git 事实不符就退出码 1。
> 给下一个会话看的细节在 `docs/PROGRESS.md`，会话怎么拆在 `docs/SESSIONS.md`。

---

## 一句话结论

**代码写完、已 push、CI 编译与测试全绿。** 分支 `feat/i18n-zh` 已在 origin 上，
GitHub Windows runner 真跑 `dotnet test`：**31/31 通过**。
剩下的是收口项（S2 接线 key、S3 工具盲区、S4 版本号双源）和只有人眼能做的 S5 目视验证。

---

## 里程碑（真相源，可直接编辑）

状态词只有五个：`done` / `partial` / `todo` / `blocked` / `optional`。改状态=改这个词。

| ID | 里程碑 | 轨道 | 状态 | 工作量 | 谁动 | 证据 |
|---|---|---|---|---|---|---|
| S0 | .NET 8 迁移 + 本地化层 + 四窗体 + 字体 + 穿透缩放 + 语言切换 + 契约测试 | 编码 | done | L | 小马 | `5d13cd9`…`3b4bb0c`，50 文件 +4265 |
| S1 | 硬编码英文收口（tooltip / 托盘气泡标题） | 编码 | done | S | 小马 | `23f2de3` `2228604` |
| S2 | 接线 6 个孤儿 key（重启确认、上锁密码、结束通知等真功能缺口） | 编码 | todo | M | 小马 | `check_i18n.py` 报 17 个未引用 key |
| S3 | 修 `check_i18n.py` 的 `LocalizedOption` 盲区（6 个假阳性） | 收口 | todo | S | 小马 | `tools/check_i18n.py:26` |
| S4 | CI / 发布收口（版本号单一真相源未做） | 收口 | partial | M | 小马 | `49480c8` `1f0eea7`；`csproj:29` 与 `AssemblyInfo.cs:38-39` 仍双源 |
| D1 | push 分支到 origin | 交付 | done | S | 小马 | `fe4d4f3` = `origin/feat/i18n-zh` |
| D2 | 启用 fork 的 Actions | 交付 | done | S | — | 实测 `{"enabled":true}`、`dotnet.yml` active；原先无需手动开，此前判断有误 |
| S5 | Windows 真机 `dotnet test` + 目视验证 | 交付 | partial | M | 主人 | run `37340271554`：`Passed: 31, Failed: 0`（net8.0, Windows runner）；目视清单未做 |
| D3 | 打 tag `v1.3.3` 出 Release | 交付 | todo | S | 小马 | 依赖 S2/S3/S4 收口 + S5 目视通过 |
| S6 | MSI / MSIX 打包链路 | 交付 | optional | L | 主人 | `vdproj` 只能在 VS 里手工重建 |

**依赖顺序**：~~D1 → D2~~ 已完成 → `(S2 / S3 / S4 收尾)` → `S5 目视` → `D3`。S6 全程可解耦，最后再说。

---

## 现在卡在哪（按谁能解开排序）

| 卡点 | 谁能解 | 怎么解 |
|---|---|---|
| S2 六个孤儿 key 未接线 | **小马** | 按 `SESSIONS.md` 的 S2 节，单会话干完 |
| S3 `check_i18n.py` 假阳性 | **小马** | 加 `LocalizedOption(` 引用扫描 |
| 版本号有两个真相源 | 小马 | `csproj:29` 与 `AssemblyInfo.cs:38-39` 合一，`GenerateAssemblyInfo` 一并调整 |
| S5 目视验证（方块字 / 破版 / 穿透手感） | **主人** | 下载 CI artifact 里的 exe，本机跑一遍看效果 |
| 是否给上游提 PR | **主人** | 公开动作，需明确决策 |

---

## 关键风险（发版前必须知道）

1. **tag 必须是纯数字 `v1.3.3`，不能加 `-zh.1`。** 版本迁移代码按 `.` 切 4 段整数，解析失败会误判成 v1.3.0 并**强制改写用户已有的 `CountdownMode` 设置**。详见 `CHANGELOG.md:12-15`。
2. **沙箱没有 dotnet SDK**，所以 S0–S4 全部只做过静态检查，**一行代码都没编译验证过**。CI 的 Windows job 会真跑 `dotnet test`——这是第一次真实编译，可能红。
3. `dotnet build` 整个 sln 在非 AnyCPU 平台会 MSB4278 硬失败（`wapproj` 的 `Build.0`）。CI 只 build csproj 所以没踩到，主人在 VS 里手动 build sln 会踩。
4. `vdproj` 已失效（写死 .NET Framework 4.8、指旧产物、缺 zh-CN 卫星程序集），**别指望它能装出可用的安装包**。

---

## 验收：什么叫做完

- ✅ `dotnet build -warnaserror` 与 `dotnet test` 在 Windows 上全绿（CI 已达成：31/31）
- 中文界面无方块字、无残留英文；语言切 English 能全回英文
- 倒计时窗口 Ctrl+滚轮缩放不破版
- `check_i18n.py` 未引用 key 数 ≤ 8
- GitHub Release 页面出现 v1.3.3 的双 RID × 双形态产物

---

<!-- BEGIN METRICS —— 由 tools/progress.py 生成，勿手改 -->
生成时间：2026-10-05 16:33:03 +0000（最后一次提交）

| 指标 | 数值 |
|---|---|
| 分支 | `feat/i18n-zh` |
| 领先 master | 22 commits |
| 改动规模 | 52 文件 / +4679 −426 |
| 是否已 push | ✅ 是 |
| 工作区 | ⚠️ 有未提交改动 |
| 双语条目 | en=149 / zh-CN=149 ✅ |
| 未接线 key | 17 个（真缺口 6，假阳性 6，冗余 1，其余 M5 用） |
| check_i18n | ✅ 通过 |
| Actions 开关 | ✅ 已启用 |
| 分支最近 CI | ✅ success (run `37341704208`) |

**轨道完成度**

- 编码：`█████████░░░░░` 67%
- 收口：`█████░░░░░░░░░` 38%
- 交付：`████████░░░░░░` 58%
<!-- END METRICS -->
-- END METRICS -->
