#!/usr/bin/env python3
"""项目进度仪表盘 —— 给人看的，不是给下一个会话看的。

真相源是 docs/STATUS.md 里的里程碑表（人可直接编辑），
本脚本负责三件事：
  1. 从 git / check_i18n.py 抓客观事实（commit 数、diff、是否已 push、key 缺口）
  2. 用事实校验表格里声称的状态，撒谎就退出码 1（--check 模式给 CI 用）
  3. 重写 STATUS.md 的「客观指标」节 + 重算三条轨道完成度

用法：
    python3 tools/progress.py            # 刷新 STATUS.md 并打印仪表盘
    python3 tools/progress.py --check    # 只校验不写文件，不一致则退出码 1
    python3 tools/progress.py --quiet    # 只输出问题，适合塞进 workflow
"""

from __future__ import annotations

import argparse
import json
import re
import subprocess
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent
STATUS = REPO / "docs" / "STATUS.md"
BASE_BRANCH = "master"          # fork 的基线，用来算「我们改了多少」
PUSH_BRANCH = "feat/i18n-zh"   # 应该存在于 origin 的分支

# 状态词 -> (显示, 计入完成的比例)
STATE = {
    "done":     ("✅ 完成",   1.0),
    "partial":  ("🟡 部分",   0.5),
    "todo":     ("⬜ 未开始", 0.0),
    "blocked":  ("🔴 阻塞",   0.0),
    "optional": ("⚪ 可选",   0.0),
}
EFFORT = {"S": 1, "M": 3, "L": 5}   # 工作量权重，用来加权算百分比

TABLE_RE = re.compile(r"^\|\s*([SD]\d)\s*\|")   # S=开发里程碑, D=交付里程碑


def sh(cmd: list[str]) -> str:
    r = subprocess.run(cmd, cwd=REPO, capture_output=True, text=True)
    return r.stdout.strip()


# ---------------------------------------------------------------- 客观事实

def git_facts() -> dict:
    ahead = sh(["git", "rev-list", "--count", f"{BASE_BRANCH}..HEAD"])
    shortstat = sh(["git", "diff", "--shortstat", f"{BASE_BRANCH}..HEAD"])
    m = re.search(r"(\d+) files? changed.*?(\d+) insertions?.*?(\d+) deletions?", shortstat)
    pushed = sh(["git", "rev-parse", "--verify", f"origin/{PUSH_BRANCH}"])
    # 看板文件自身变化不算「未提交的活」，否则刷新一次就自己报警一次
    dirty = sh(["git", "status", "--porcelain", "--", ".", ":(exclude)docs/STATUS.md"])
    return {
        "commits": int(ahead or 0),
        "files": int(m.group(1)) if m else 0,
        "insertions": int(m.group(2)) if m else 0,
        "deletions": int(m.group(3)) if m else 0,
        "pushed": bool(pushed),
        "dirty": bool(dirty),
        "last_commit": sh(["git", "log", "-1", "--format=%ci"]),
        "branch": sh(["git", "rev-parse", "--abbrev-ref", "HEAD"]),
    }


def i18n_facts() -> dict:
    """跑 check_i18n.py，抓 key 对齐数与未引用 key 数。"""
    out = sh(["python3", "tools/check_i18n.py"])
    en = re.search(r"en=(\d+)\s+zh-CN=(\d+)", out)
    unused = re.search(r"未被引用的 key（(\d+) 个", out)
    return {
        "en": int(en.group(1)) if en else -1,
        "zh": int(en.group(2)) if en else -1,
        "unused_keys": int(unused.group(1)) if unused else -1,
        "check_green": "校验通过" in out,
    }


def repo_slug() -> str:
    """从 origin 解析 owner/repo。
    坑：本仓库配了 upstream remote，gh 会把命令默认解析到**上游**仓库，
    于是 run list / pr list 看到的是 lukaslangrock 的数据。必须显式 --repo。
    """
    url = sh(["git", "remote", "get-url", "origin"])
    m = re.search(r"github\.com[:/]([^/]+/[^/.]+)", url)
    return m.group(1) if m else ""


def ci_facts() -> dict:
    """最近一次跑在我们分支上的 CI 结果 + Actions 开关状态。gh 不可用就返回 unknown。"""
    slug = repo_slug()
    if not slug:
        return {"available": False}
    raw = sh(["gh", "run", "list", "--repo", slug, "--branch", PUSH_BRANCH,
              "--limit", "1",
              "--json", "databaseId,status,conclusion,displayTitle,updatedAt"])
    try:
        runs = json.loads(raw)
    except (json.JSONDecodeError, TypeError):
        return {"available": False, "slug": slug, "raw": raw[:200]}
    run = runs[0] if runs else {}
    perm = sh(["gh", "api", f"repos/{slug}/actions/permissions"])
    try:
        actions_enabled = json.loads(perm).get("enabled")
    except (json.JSONDecodeError, TypeError):
        actions_enabled = None
    return {
        "available": True,
        "slug": slug,
        "run_id": run.get("databaseId"),
        "run_status": run.get("status"),
        "conclusion": run.get("conclusion"),
        "run_title": run.get("displayTitle"),
        "updated": run.get("updatedAt"),
        "actions_enabled": actions_enabled,
    }


# ---------------------------------------------------------------- 解析看板

def parse_status(text: str) -> list[dict]:
    rows = []
    for line in text.splitlines():
        if not TABLE_RE.match(line):
            continue
        cells = [c.strip() for c in line.strip().strip("|").split("|")]
        if len(cells) < 6:
            continue
        rows.append({
            "id": cells[0],
            "name": cells[1],
            "track": cells[2],
            "state": cells[3].lower(),
            "effort": cells[4].upper(),
            "owner": cells[5],
            "evidence": cells[6] if len(cells) > 6 else "",
        })
    return rows


# ---------------------------------------------------------------- 校验撒谎

def audit(rows: list[dict], g: dict, i: dict, ci: dict) -> list[str]:
    """看板声称的状态与客观事实不符 -> 报警。手工看板必然腐烂，靠这个兜底。"""
    problems: list[str] = []
    by_id = {r["id"]: r for r in rows}

    # S2 接线孤儿 key：只要还有真缺口，就不许标 done
    s2 = by_id.get("S2")
    if s2 and s2["state"] == "done" and i["unused_keys"] > 8:
        problems.append(
            f"S2 标 done，但 check_i18n 仍报 {i['unused_keys']} 个未引用 key（阈值 8）")

    # S3 修工具盲区：Language.* / TrayTheme.* 还在报告里就是没修
    if "S3" in by_id and by_id["S3"]["state"] == "done":
        out = sh(["python3", "tools/check_i18n.py"])
        if "Language.Automatic" in out or "TrayTheme.Dark" in out:
            problems.append("S3 标 done，但 check_i18n 仍误报 Language.*/TrayTheme.*")

    # S5 真机验证：标 done 必须有证据，且 CI 必须真的绿
    s5 = by_id.get("S5")
    if s5 and s5["state"] == "done":
        if not s5["evidence"].strip():
            problems.append("S5 标 done 但无证据（需 dotnet test 输出或截图记录）")
        if ci.get("available") and ci.get("conclusion") not in (None, "success"):
            problems.append(f"S5 标 done，但分支最近一次 CI 是 {ci['conclusion']}")

    # D1 push：看板说 done 但 origin 上没分支 -> 撒谎
    d1 = by_id.get("D1")
    if d1 and d1["state"] == "done" and not g["pushed"]:
        problems.append("D1 标 done，但 origin 上没有分支")
    if g["commits"] > 0 and not g["pushed"]:
        problems.append(
            f"{g['commits']} 个 commit 未 push 到 origin/{PUSH_BRANCH} —— 交付轨道无法推进")

    # D2 Actions：看板说 done 但 Actions 实际是关的
    d2 = by_id.get("D2")
    if d2 and d2["state"] == "done" and ci.get("available") and ci.get("actions_enabled") is False:
        problems.append("D2 标 done，但 Actions permissions 显示 enabled=false")

    if g["dirty"]:
        problems.append("工作区有未提交改动 —— 进度数字不可信")

    if not i["check_green"]:
        problems.append("check_i18n.py 未通过 —— 看板不应显示绿色")

    if i["en"] != i["zh"]:
        problems.append(f"双语条目不对齐：en={i['en']} zh-CN={i['zh']}")

    return problems


# ---------------------------------------------------------------- 计算与渲染

def bar(pct: float, width: int = 14) -> str:
    filled = round(pct / 100 * width)
    return "█" * filled + "░" * (width - filled)


def track_scores(rows: list[dict]) -> dict[str, float]:
    """按工作量加权算每条轨道完成度。optional 不计入分母。"""
    acc: dict[str, list[float]] = {}
    for r in rows:
        if r["state"] not in STATE:
            continue
        w = EFFORT.get(r["effort"], 1)
        if r["state"] == "optional":
            continue
        acc.setdefault(r["track"], [0.0, 0.0])
        acc[r["track"]][0] += w * STATE[r["state"]][1]
        acc[r["track"]][1] += w
    return {t: (v[0] / v[1] * 100 if v[1] else 0.0) for t, v in acc.items()}


def render(rows: list[dict], g: dict, i: dict, ci: dict, scores: dict[str, float]) -> str:
    lines = ["<!-- BEGIN METRICS —— 由 tools/progress.py 生成，勿手改 -->"]
    lines.append(f"生成时间：{sh(['git', 'log', '-1', '--format=%ci'])}（最后一次提交）")
    lines.append("")
    lines.append("| 指标 | 数值 |")
    lines.append("|---|---|")
    lines.append(f"| 分支 | `{g['branch']}` |")
    lines.append(f"| 领先 {BASE_BRANCH} | {g['commits']} commits |")
    lines.append(f"| 改动规模 | {g['files']} 文件 / +{g['insertions']} −{g['deletions']} |")
    lines.append(f"| 是否已 push | {'✅ 是' if g['pushed'] else '❌ 否（交付轨道 0%）'} |")
    lines.append(f"| 工作区 | {'⚠️ 有未提交改动' if g['dirty'] else '✅ 干净'} |")
    lines.append(f"| 双语条目 | en={i['en']} / zh-CN={i['zh']} {'✅' if i['en'] == i['zh'] else '❌'} |")
    lines.append(f"| 未接线 key | {i['unused_keys']} 个（真缺口 6，假阳性 6，冗余 1，其余 M5 用） |")
    lines.append(f"| check_i18n | {'✅ 通过' if i['check_green'] else '❌ 失败'} |")
    if ci.get("available"):
        mark = {"success": "✅", "failure": "❌", "cancelled": "⚠️"}.get(ci.get("conclusion"), "⏳")
        lines.append(f"| Actions 开关 | {'✅ 已启用' if ci.get('actions_enabled') else '❌ 未启用'} |")
        lines.append(f"| 分支最近 CI | {mark} {ci.get('conclusion') or ci.get('run_status')} "
                     f"(run `{ci.get('run_id')}`) |")
    else:
        lines.append("| 分支最近 CI | ❓ gh 不可用，未校验 |")
    lines.append("")
    lines.append("**轨道完成度**")
    lines.append("")
    for t in ("编码", "收口", "交付"):
        if t in scores:
            lines.append(f"- {t}：`{bar(scores[t])}` {scores[t]:.0f}%")
    lines.append("<!-- END METRICS -->")
    return "\n".join(lines)


def splice(text: str, block: str) -> str:
    if "BEGIN METRICS" in text and "END METRICS" in text:
        pre = text.split("<!-- BEGIN METRICS")[0]
        post = text.split("END METRICS -->", 1)[1]
        return pre + block + post
    return text.rstrip() + "\n\n---\n\n" + block + "\n"


def console(rows, g, i, ci, scores, problems) -> None:
    print(f"\n{'=' * 62}")
    print(f"  {g['branch']}  ·  {g['commits']} commits ahead of {BASE_BRANCH}"
          f"  ·  {g['files']} files +{g['insertions']}/−{g['deletions']}")
    print("=" * 62)
    for t in ("编码", "收口", "交付"):
        if t in scores:
            print(f"  {t:<4} {bar(scores[t])} {scores[t]:5.0f}%")
    print("-" * 62)
    for r in rows:
        label = STATE.get(r["state"], ("❓ " + r["state"], 0))[0]
        print(f"  {r['id']:<4} {r['name']:<26} {label:<10} {r['effort']}  {r['owner']}")
    print("-" * 62)
    if ci.get("available"):
        print(f"  CI: {ci.get('conclusion') or ci.get('run_status')}"
              f"  ·  Actions enabled={ci.get('actions_enabled')}"
              f"  ·  run {ci.get('run_id')}")
    if problems:
        print("  ⚠️  看板与事实不符 / 阻塞：")
        for p in problems:
            print(f"     · {p}")
    else:
        print("  ✅ 看板与 git 事实一致")
    print()


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true", help="只校验，不写文件")
    ap.add_argument("--quiet", action="store_true", help="只打印问题")
    args = ap.parse_args()

    if not STATUS.exists():
        print("缺少 docs/STATUS.md —— 里程碑看板不存在", file=sys.stderr)
        return 2

    rows = parse_status(STATUS.read_text(encoding="utf-8"))
    if not rows:
        print("docs/STATUS.md 里没解析到里程碑行（格式：| S1 | 名称 | 轨道 | 状态 | 工作量 | 谁动 | 证据 |）",
              file=sys.stderr)
        return 2

    g, i, ci = git_facts(), i18n_facts(), ci_facts()
    scores = track_scores(rows)
    problems = audit(rows, g, i, ci)

    if not args.quiet:
        console(rows, g, i, ci, scores, problems)
        print(json.dumps({"tracks": {k: round(v) for k, v in scores.items()},
                          "problems": problems}, ensure_ascii=False))
    elif problems:
        # CI 里只给退出码没用，必须把原因打出来
        for p in problems:
            print(f"STATUS 看板与事实不符：{p}", file=sys.stderr)

    if not args.check:
        STATUS.write_text(splice(STATUS.read_text(encoding="utf-8"),
                                 render(rows, g, i, ci, scores)), encoding="utf-8")
        if args.quiet:
            print(f"docs/STATUS.md 已刷新")

    return 1 if problems and args.check else 0


if __name__ == "__main__":
    sys.exit(main())
