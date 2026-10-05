#!/usr/bin/env python3
"""导出代码里所有"纯字面量"的用户可见文本，输出成映射表骨架。

用法: python3 tools/dump_literals.py > tools/map_draft.txt
每行: <C# 转义写法的原文>\t<TODO>
人工把 TODO 换成资源 key 后另存为 localize_map.txt。
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from localize_code import FILES, SRC, TARGETS, ASSIGN_PATTERNS, split_args, eval_literal


def to_csharp(text):
    out = []
    for ch in text:
        if ch == "\\":
            out.append("\\\\")
        elif ch == '"':
            out.append('\\"')
        elif ch == "\n":
            out.append("\\n")
        elif ch == "\r":
            out.append("\\r")
        elif ch == "\t":
            out.append("\\t")
        else:
            out.append(ch)
    return "".join(out)


def main():
    found = []
    for name in FILES:
        path = os.path.join(SRC, name)
        if not os.path.exists(path):
            continue
        with open(path, encoding="utf-8-sig") as fh:
            src = fh.read()

        for prefix, arg_index in TARGETS:
            pos = 0
            while True:
                idx = src.find(prefix, pos)
                if idx < 0:
                    break
                open_idx = idx + len(prefix) - 1
                args, _ = split_args(src, open_idx)
                if args and len(args) > arg_index:
                    v = eval_literal(args[arg_index])
                    if v is not None:
                        found.append((name, to_csharp(v)))
                    if prefix == "MessageBox.Show(":
                        for extra in (1,):
                            if len(args) > extra:
                                c = eval_literal(args[extra])
                                if c is not None:
                                    found.append((name, to_csharp(c)))
                pos = idx + len(prefix)

        for pat in ASSIGN_PATTERNS:
            for m in pat.finditer(src):
                v = eval_literal(m.group("lit"))
                if v is not None:
                    found.append((name, to_csharp(v)))

    seen = set()
    for name, text in found:
        if text in seen:
            continue
        seen.add(text)
        print(f"{text}\tTODO")
    print(f"# 共 {len(seen)} 条", file=sys.stderr)


if __name__ == "__main__":
    main()
