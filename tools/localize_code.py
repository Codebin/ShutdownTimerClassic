#!/usr/bin/env python3
"""把代码里硬编码的英文用户可见文本换成 Loc.T(...) 取词。

映射表在 localize_map.txt，每行： <英文原文>\t<key>
原文里的换行/引号用 C# 转义写法（\\n、\\"），与源码字面量一致。

只处理"纯字面量拼接"的参数；含变量的调用（错误列表、日志路径等）跳过，
由人工单独处理，脚本会把这些列出来。
"""
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.join(HERE, "..", "src", "ShutdownTimer")
MAP_FILE = os.path.join(HERE, "localize_map.txt")

FILES = ["Menu.cs", "Countdown.cs", "Settings.cs", "Timer.cs",
         "InputBox.cs", "Helpers/ExceptionHandler.cs"]

# 需要本地化的第 1 个参数（MessageBox.Show 的第 1、2 个参数单独处理）
TARGETS = [
    ("MessageBox.Show(", 0),
    ("SendNotification(", 0),
    ("infoToolTip.SetToolTip(", 1),
]

# 赋值式： form.Title = "..." ; errMessages += "..." ; Items[0].Text = "..."
ASSIGN_PATTERNS = [
    re.compile(r'(?P<lhs>form\.Title)\s*=\s*(?P<lit>"(?:[^"\\]|\\.)*"(?:\s*\+\s*"(?:[^"\\]|\\.)*")*)\s*;'),
    re.compile(r'(?P<lhs>form\.Message)\s*=\s*(?P<lit>"(?:[^"\\]|\\.)*"(?:\s*\+\s*"(?:[^"\\]|\\.)*")*)\s*;'),
    re.compile(r'(?P<lhs>errMessages)\s*\+=\s*(?P<lit>"(?:[^"\\]|\\.)*"(?:\s*\+\s*"(?:[^"\\]|\\.)*")*)\s*;'),
    re.compile(r'(?P<lhs>warnMessages)\s*\+=\s*(?P<lit>"(?:[^"\\]|\\.)*"(?:\s*\+\s*"(?:[^"\\]|\\.)*")*)\s*;'),
    re.compile(r'(?P<lhs>caption)\s*=\s*(?P<lit>"(?:[^"\\]|\\.)*"(?:\s*\+\s*"(?:[^"\\]|\\.)*")*)\s*;'),
    re.compile(r'(?P<lhs>message)\s*=\s*(?P<lit>"(?:[^"\\]|\\.)*"(?:\s*\+\s*"(?:[^"\\]|\\.)*")*)\s*;'),
    re.compile(r'(?P<lhs>contextMenuStrip\.Items\[0\]\.Text)\s*=\s*(?P<lit>"(?:[^"\\]|\\.)*"(?:\s*\+\s*"(?:[^"\\]|\\.)*")*)\s*;'),
    re.compile(r'(?P<lhs>this\.Text)\s*=\s*(?P<lit>"(?:[^"\\]|\\.)*"(?:\s*\+\s*"(?:[^"\\]|\\.)*")*)\s*;'),
    re.compile(r'(?P<lhs>\w+\.Text)\s*=\s*(?P<lit>"(?:[^"\\]|\\.)*"(?:\s*\+\s*"(?:[^"\\]|\\.)*")*)\s*;'),
]

CS_ESCAPES = {"\\n": "\n", "\\t": "\t", "\\r": "\r", '\\"': '"', "\\\\": "\\"}


def unescape_csharp(text):
    """把映射文件里的 C# 风格转义（\\n、\\"）解析成真实字符，
    这样多行文本也能写在一行里，并与 eval_literal 的结果对齐。"""
    out, i = [], 0
    while i < len(text):
        if text[i] == "\\":
            two = text[i:i + 2]
            if two in CS_ESCAPES:
                out.append(CS_ESCAPES[two])
                i += 2
                continue
        out.append(text[i])
        i += 1
    return "".join(out)


def load_map():
    table = {}
    with open(MAP_FILE, encoding="utf-8") as fh:
        for line in fh:
            line = line.rstrip("\n")
            if not line or line.startswith("#") or "\t" not in line:
                continue
            text, key = line.split("\t", 1)
            table[unescape_csharp(text)] = key.strip()
    return table


def split_args(text, open_idx):
    """返回调用参数列表及其位置。字符串感知 + 括号配对。"""
    depth = 1
    i = open_idx + 1
    start = i
    args = []
    in_str = False
    esc = False
    while i < len(text):
        ch = text[i]
        if in_str:
            if esc:
                esc = False
            elif ch == "\\":
                esc = True
            elif ch == '"':
                in_str = False
        else:
            if ch == '"':
                in_str = True
            elif ch in "([{":
                depth += 1
            elif ch in ")]}":
                depth -= 1
                if depth == 0:
                    args.append(text[start:i])
                    return args, i
            elif ch == "," and depth == 1:
                args.append(text[start:i])
                start = i + 1
        i += 1
    return None, None


LITERAL = re.compile(r'^"(?:[^"\\]|\\.)*"(?:\s*\+\s*"(?:[^"\\]|\\.)*")*$')


def eval_literal(expr):
    """把 "a" + "b" 求值成实际文本；含变量则返回 None。"""
    expr = expr.strip()
    if not LITERAL.match(expr):
        return None
    parts = re.findall(r'"((?:[^"\\]|\\.)*)"', expr)
    out = []
    for p in parts:
        buf, i = [], 0
        while i < len(p):
            if p[i] == "\\":
                two = p[i:i + 2]
                if two in CS_ESCAPES:
                    out.append(CS_ESCAPES[two])
                    i += 2
                    continue
                out.append(two)
                i += 2
                continue
            out.append(p[i])
            i += 1
    return "".join(out)


def main():
    table = load_map()
    used, hits, misses = set(), [], []

    for name in FILES:
        path = os.path.join(SRC, name)
        if not os.path.exists(path):
            print(f"找不到文件: {path}")
            continue

        with open(path, encoding="utf-8-sig") as fh:
            src = fh.read()

        # 1) 调用式替换
        for prefix, arg_index in TARGETS:
            pos = 0
            edits = []
            while True:
                idx = src.find(prefix, pos)
                if idx < 0:
                    break
                open_idx = idx + len(prefix) - 1
                args, end = split_args(src, open_idx)
                if args is None or len(args) <= arg_index:
                    pos = idx + len(prefix)
                    continue

                raw = args[arg_index]
                value = eval_literal(raw)
                if value is not None and value in table:
                    key = table[value]
                    used.add(key)
                    new = f'Loc.T("{key}")'
                    a_start = open_idx + 1 + sum(len(a) + 1 for a in args[:arg_index])
                    a_end = a_start + len(raw)
                    edits.append((a_start, a_end, new, value[:50]))
                elif value is not None:
                    misses.append(f"{name}: {prefix[:-1]} -> 未映射: {value[:70]!r}")

                pos = idx + len(prefix)

            for a_start, a_end, new, label in reversed(edits):
                src = src[:a_start] + new + src[a_end:]
                hits.append(f"{name}: {prefix[:-1]}({label!r})")

        # 2) MessageBox 的标题（第 2 个参数）
        pos = 0
        edits = []
        while True:
            idx = src.find("MessageBox.Show(", pos)
            if idx < 0:
                break
            open_idx = idx + len("MessageBox.Show(") - 1
            args, end = split_args(src, open_idx)
            if args and len(args) >= 2:
                raw = args[1]
                value = eval_literal(raw)
                if value is not None and value in table:
                    key = table[value]
                    used.add(key)
                    a_start = open_idx + 1 + sum(len(a) + 1 for a in args[:1])
                    edits.append((a_start, a_start + len(raw), f'Loc.T("{key}")', value[:40]))
                elif value is not None:
                    misses.append(f"{name}: MessageBox 标题未映射: {value[:60]!r}")
            pos = idx + 10

        for a_start, a_end, new, label in reversed(edits):
            src = src[:a_start] + new + src[a_end:]
            hits.append(f"{name}: MessageBox 标题({label!r})")

        # 3) 赋值式
        for pat in ASSIGN_PATTERNS:
            edits = []
            for m in pat.finditer(src):
                value = eval_literal(m.group("lit"))
                if value is None:
                    continue
                if value in table:
                    key = table[value]
                    used.add(key)
                    edits.append((m.start("lit"), m.end("lit"), f'Loc.T("{key}")'))
                else:
                    misses.append(f"{name}: {m.group('lhs')} 未映射: {value[:60]!r}")
            for s, e, new in reversed(edits):
                src = src[:s] + new + src[e:]
                hits.append(f"{name}: 赋值")

        with open(path, "w", encoding="utf-8") as fh:
            fh.write(src)

    print(f"已替换 {len(hits)} 处")
    if misses:
        print(f"\n未命中 {len(misses)} 处（需要补映射表或人工处理）:")
        for m in misses:
            print("  -", m)
    unused = sorted(set(table.values()) - used)
    if unused:
        print(f"\n映射表里没用到的 key（{len(unused)}）:")
        for k in unused:
            print("  ·", k)
    return 1 if misses else 0


if __name__ == "__main__":
    sys.exit(main())
