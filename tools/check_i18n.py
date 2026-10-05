#!/usr/bin/env python3
"""校验本地化引用完整性。

检查四件事：
  1. 代码里 Loc.T("key") 引用的 key 是否都存在于资源表（漏 key 界面上会变成 [key]）
  2. 带占位符的调用（Loc.T("key", a, b)）在两种语言里是否都有对应的 {0}/{1}
  3. 占位符数量与实参数量是否匹配
  4. 资源表里有哪些 key 从未被引用（死 key 提示，不算错误）

退出码：0 = 通过，1 = 有缺失或占位符不匹配
"""

import os
import re
import sys
import xml.etree.ElementTree as ET

HERE = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.join(HERE, "..", "src", "ShutdownTimer")

RESX = {
    "en": os.path.join(SRC, "Strings.resx"),
    "zh-CN": os.path.join(SRC, "Strings.zh-CN.resx"),
}

CALL_HEAD = re.compile(r'Loc\.T\(\s*"([^"]+)"')
# LocalizedOption 的显示名 key 是第二个构造参数：new LocalizedOption(value, "Display.Key")。
# 只扫 Loc.T 会把 Language.* / TrayTheme.* 这 6 个 key 误报成死 key。
OPTION_HEAD = re.compile(r'new LocalizedOption\([^,]+,\s*"([^"]+)"')
PLACEHOLDER = re.compile(r"\{(\d+)\}")


def iter_calls(text):
    """扫描文本里的 Loc.T("key", a, b, ...) 调用，产出 (key, 实参个数)。

    用括号配对而不是纯正则：实参里可能嵌套 DisplayName() 这类调用，
    正则回溯会把后面的逗号算进来。
    """
    for m in CALL_HEAD.finditer(text):
        key = m.group(1)
        i = m.end()  # 位于第二个引号之后
        depth = 1
        commas = 0
        in_string = False
        escape = False
        while i < len(text) and depth > 0:
            ch = text[i]
            if in_string:
                if escape:
                    escape = False
                elif ch == "\\":
                    escape = True
                elif ch == '"':
                    in_string = False
            else:
                if ch == '"':
                    in_string = True
                elif ch in "([":
                    depth += 1
                elif ch in ")]":
                    depth -= 1
                    if depth == 0:
                        break
                elif ch == "," and depth == 1:
                    commas += 1
            i += 1

        if depth != 0:
            continue  # 调用没闭合，跳过

        # 实参之间用逗号分隔；无实参时 commas=0
        argc = commas  # key 之后的逗号数即实参数
        yield key, argc


def load(path):
    out = {}
    for node in ET.parse(path).getroot():
        if node.tag == "data":
            value = node.find("value")
            out[node.get("name")] = (value.text or "") if value is not None else ""
    return out


def main():
    tables = {lang: load(path) for lang, path in RESX.items()}
    en, zh = tables["en"], tables["zh-CN"]

    used = {}  # key -> 引用的实参个数
    cs_files = []
    for root, _, files in os.walk(SRC):
        for f in files:
            if f.endswith(".cs"):
                cs_files.append(os.path.join(root, f))

    for path in cs_files:
        with open(path, encoding="utf-8-sig") as fh:
            text = fh.read()
        for key, argc in iter_calls(text):
            used[key] = max(used.get(key, -1), argc)
        for m in OPTION_HEAD.finditer(text):
            used.setdefault(m.group(1), 0)

    problems = []

    # 1. 引用了但资源表没有
    for key in sorted(used):
        for lang, table in tables.items():
            if key not in table:
                problems.append(f"缺失 key: {key}  ({lang})")

    # 2/3. 占位符数量与实参数量匹配
    for key, argc in sorted(used.items()):
        if argc == 0:
            continue
        for lang, table in tables.items():
            if key not in table:
                continue
            found = set(int(x) for x in PLACEHOLDER.findall(table[key]))
            expected = set(range(argc))
            if found != expected:
                problems.append(
                    f"占位符不匹配: {key} ({lang}) 需要 {sorted(expected)}，实际 {sorted(found)}"
                )

    # 4. 死 key
    dead = sorted(set(en) - set(used))

    print(f"资源条目: en={len(en)} zh-CN={len(zh)}")
    print(f"代码引用: {len(used)} 个 key")

    if problems:
        print("\n问题:")
        for p in problems:
            print("  -", p)

    if dead:
        print(f"\n未被引用的 key（{len(dead)} 个）:")
        for k in dead:
            print("  ·", k)

    if problems:
        print("\n校验失败")
        return 1
    print("\n校验通过")
    return 0


if __name__ == "__main__":
    sys.exit(main())
