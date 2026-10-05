#!/usr/bin/env python3
"""从 CHANGELOG.md 里抽取指定版本的段落，作为 GitHub Release 的说明。

为什么需要这个脚本：release.yml 原先直接 --notes-file CHANGELOG.md，
会把「已知问题 / 待办」整段公开到 Release 页面——那是内部施工记录，
不该出现在给别人看的发布说明里。

用法:  python3 tools/release_notes.py v1.3.3
输出:  该版本的 CHANGELOG 段落（已剔除「已知问题」小节），写到 stdout
退出码: 0 = 找到并输出；1 = 找不到该版本段落
"""

import re
import sys
import os

CHANGELOG = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "CHANGELOG.md")

# 版本标题形如： "## 1.3.3（本分支，基于上游 37c955e）"
HEADING = re.compile(r"^##\s+(?P<ver>\d+(?:\.\d+)*)")

# 要剔除的小节标题关键词（内部记录，不对外）
SKIP_SECTIONS = ("已知问题", "待办", "Known issues", "TODO")


def extract(version: str) -> str:
    """返回 version 对应的段落正文；找不到返回空串。"""
    with open(CHANGELOG, encoding="utf-8") as fh:
        lines = fh.read().splitlines()

    out = []
    in_section = False
    skipping = False
    seen_subheading = False

    for line in lines:
        m = HEADING.match(line)
        if m:
            if in_section:
                break  # 到了下一个版本段落，收工
            if m.group("ver") == version:
                in_section = True
            continue

        if not in_section:
            continue

        # 三级标题：判断是否属于要剔除的小节
        if line.startswith("### "):
            seen_subheading = True
            title = line[4:]
            skipping = any(k in title for k in SKIP_SECTIONS)
            if skipping:
                continue

        if skipping:
            continue

        # 第一个三级标题之前的引用块是给维护者的注记
        # （例如"版本号为何不能带 -zh.1 预发布标签"），对普通用户无意义，剔除。
        if not seen_subheading and line.startswith(">"):
            continue

        out.append(line)

    return "\n".join(out).strip()


def main():
    if len(sys.argv) != 2:
        print("用法: python3 tools/release_notes.py <tag>  例如 v1.3.3", file=sys.stderr)
        return 2

    tag = sys.argv[1].strip()
    version = tag[1:] if tag.startswith("v") else tag

    body = extract(version)
    if not body:
        print(f"错误：CHANGELOG.md 里找不到版本 {version} 的段落（tag={tag}）。", file=sys.stderr)
        print("提示：标题必须形如 '## 1.3.3' 或 '## 1.3.3（…）'，且与 tag 去掉 v 后一致。", file=sys.stderr)
        return 1

    print(body)
    return 0


if __name__ == "__main__":
    sys.exit(main())
