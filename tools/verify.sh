#!/usr/bin/env bash
# 一条命令跑完本次改造的全部检查。改文案或改控件后先跑这个。
set -euo pipefail
cd "$(dirname "$0")/.."

echo "── 1/5 生成双语资源 ─────────────────────────"
python3 tools/gen_strings.py

echo "── 2/5 校验 key 与占位符 ─────────────────────"
python3 tools/check_i18n.py

echo "── 3/5 估算中文破版 ──────────────────────────"
python3 tools/check_layout.py

echo "── 4/5 编译（警告即失败）─────────────────────"
dotnet build src/ShutdownTimer/ShutdownTimer.csproj --nologo -warnaserror

echo "── 5/5 编译测试 ──────────────────────────────"
# WinForms 测试主机需要 Windows 上的 Microsoft.WindowsDesktop.App 运行时，
# 在 Linux/macOS 上只能编译。要真正执行用例，请在 Windows 上跑：
#   dotnet test tests/ShutdownTimer.Tests/ShutdownTimer.Tests.csproj
dotnet build tests/ShutdownTimer.Tests/ShutdownTimer.Tests.csproj --nologo

echo
echo "全部通过 ✅（测试仅编译；在 Windows 上执行 dotnet test）"
