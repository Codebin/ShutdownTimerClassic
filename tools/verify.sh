#!/usr/bin/env bash
# 一条命令跑完本次改造的全部检查。改文案或改控件后先跑这个。
set -euo pipefail
cd "$(dirname "$0")/.."

echo "── 1/4 生成双语资源 ─────────────────────────"
python3 tools/gen_strings.py

echo "── 2/4 校验 key 与占位符 ─────────────────────"
python3 tools/check_i18n.py

echo "── 3/4 估算中文破版 ──────────────────────────"
python3 tools/check_layout.py

echo "── 4/4 编译（警告即失败）─────────────────────"
dotnet build src/ShutdownTimer/ShutdownTimer.csproj --nologo -warnaserror

echo
echo "全部通过 ✅"
