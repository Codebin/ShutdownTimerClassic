#!/usr/bin/env python3
"""静态估算汉化后的控件宽度，找出可能破版的地方。

思路：Designer 里的 Size 是英文 + Microsoft Sans Serif 下量出来的。汉化后
  1) 文本换成中文（字符数通常变少）
  2) 字体换成 Microsoft YaHei UI（中文字符约 1 em 宽，明显宽于英文平均字宽）
两者叠加后 AutoSize 控件的实际宽度可能超过父容器可用宽度。

这里用粗略的字宽模型估算，只用于发现"明显会溢出"的控件，不追求像素级精确。
最终仍需主人在 Windows 上目视确认。
"""
import os
import re
import sys
import xml.etree.ElementTree as ET

HERE = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.join(HERE, "..", "src", "ShutdownTimer")

FILES = ["Menu.Designer.cs", "Settings.Designer.cs", "Countdown.Designer.cs", "InputBox.Designer.cs"]

# 8.25pt 下的粗略字宽（px, 96dpi）
CJK_W = 12        # 中日韩全角字符
ASCII_W = 6       # 拉丁字母/数字/标点（雅黑略宽于 Sans Serif）
PAD = 22          # CheckBox/RadioButton 的方框 + 内边距

AUTOSIZE = re.compile(r'this\.(\w+)\.AutoSize = true;')
LOC = re.compile(r'this\.(\w+)\.Location = new System\.Drawing\.Point\((-?\d+),\s*(-?\d+)\);')
SIZE = re.compile(r'this\.(\w+)\.Size = new System\.Drawing\.Size\((\d+),\s*(\d+)\);')
TEXT = re.compile(r'this\.(\w+)\.Text = Loc\.T\("([^"]+)"\);')
ADD = re.compile(r'this\.(\w+)\.Controls\.Add\(this\.(\w+)\);')
TYPE = re.compile(r'this\.(\w+)\s*=\s*new System\.Windows\.Forms\.(\w+)\(\);')
CLIENT = re.compile(r'this\.ClientSize = new System\.Drawing\.Size\((\d+),\s*(\d+)\);')


def load(path):
    out = {}
    for node in ET.parse(path).getroot():
        if node.tag == "data":
            v = node.find("value")
            out[node.get("name")] = (v.text or "") if v is not None else ""
    return out


def width_of(text):
    w = 0
    for line in text.split("\n"):
        line_w = 0
        for ch in line:
            line_w += CJK_W if ord(ch) > 0x2E80 else ASCII_W
        w = max(w, line_w)
    return w


def main():
    zh = load(os.path.join(SRC, "Strings.zh-CN.resx"))

    problems = []

    for name in FILES:
        path = os.path.join(SRC, name)
        with open(path, encoding="utf-8-sig") as fh:
            src = fh.read()

        autosize = set(AUTOSIZE.findall(src))
        types = dict(TYPE.findall(src))
        loc = {k: (int(a), int(b)) for k, a, b in LOC.findall(src)}
        size = {k: (int(a), int(b)) for k, a, b in SIZE.findall(src)}
        text = dict(TEXT.findall(src))
        parent = {}
        for p, c in ADD.findall(src):
            parent[c] = p

        form_w = None
        m = CLIENT.search(src)
        if m:
            form_w = int(m.group(1))

        for ctrl, key in text.items():
            if ctrl not in autosize:
                continue
            value = zh.get(key, "")
            if not value:
                continue

            kind = types.get(ctrl, "")
            pad = PAD if kind in ("CheckBox", "RadioButton") else 4
            est = width_of(value) + pad
            where = parent.get(ctrl)
            limit = None
            if where and where in size:
                limit = size[where][0]
            elif where is None and form_w:
                limit = form_w

            if limit is None:
                continue

            x = loc.get(ctrl, (0, 0))[0]
            available = limit - x - 8  # 留一点内边距
            if est > available:
                problems.append(
                    f"{name}: {ctrl} (父容器 {where or '窗体'}) 需要约 {est}px，可用 {available}px -> {value!r}"
                )

    if problems:
        print(f"可能破版 {len(problems)} 处：")
        for p in problems:
            print("  -", p)
        return 1
    print("未发现明显溢出（仍需 Windows 上目视确认）")
    return 0


if __name__ == "__main__":
    sys.exit(main())
