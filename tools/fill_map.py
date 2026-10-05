#!/usr/bin/env python3
"""把 tools/map_draft.txt 的 TODO 按前缀规则填成资源 key，产出 localize_map.txt。

规则按声明顺序匹配，长前缀必须写在短前缀之前
（否则 "Countdown" 会抢走 "Countdown Update"）。
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
DRAFT = os.path.join(HERE, "map_draft.txt")
OUT = os.path.join(HERE, "localize_map.txt")

RULES = [
    # 通用 / 已在多处复用
    ("Another instance of this application", "Common.AlreadyRunning"),
    ("Application already running!", "Common.AlreadyRunningTitle"),

    # Menu 弹窗与标题
    ("There seems to be a problem!", "Menu.Err.StartFailedTitle"),
    ("Warning", "Menu.WarnTitle"),
    ("Custom command field was empty", "Menu.Err.EmptyCommand"),
    ("Invalid command!", "Menu.Err.EmptyCommandTitle"),
    ("Custom Command", "Menu.CustomCommandTitle"),
    ("Please enter the command you want", "Menu.CustomCommandPrompt"),
    ("Password Protection", "Menu.PasswordTitle"),
    ("Please set a password to enable", "Menu.PasswordPrompt"),
    ("Start (with recommended settings)", "Menu.StartRecommended"),

    # Menu 悬停提示
    ("Applications that do not exit when prompted", "Menu.Tip.Graceful"),
    ("Depending on the power settings", "Menu.Tip.PreventSleep"),
    ("This will launch the countdown without a visible window", "Menu.Tip.RunInBackground"),
    ("Will count down from the hours", "Menu.Tip.CountdownMode"),
    ("In this mode you can select the target time", "Menu.Tip.TimeOfDayMode"),

    # Menu 校验
    ("The timer cannot start at 0", "Menu.Err.ZeroTime"),
    ("TimeSpan conversion failed", "Menu.Err.TimeSpanConversion"),

    # Countdown 弹窗
    ("Would you like to re-lock", "Countdown.ReLockPrompt"),
    ("Operation aborted: You have not supplied a new time value", "Countdown.Err.NoTime"),
    ("Operation aborted: You have not supplied a valid time value", "Countdown.Err.InvalidTime"),
    ("Operation aborted: You have either not supplied", "Countdown.Err.InternalError"),
    ("Countdown Update", "Countdown.UpdateTitle"),
    ("Incorrect password!", "Countdown.PasswordWrong"),
    ("Set a new countdown", "Countdown.NewTimeTitle"),
    ("Enter new time for the countdown", "Countdown.NewTimePrompt"),
    ("Do you really want to cancel the timer?", "Countdown.ConfirmStop"),
    ("Are you sure?", "Countdown.ConfirmStopTitle"),
    ("This countdown has been protected", "Countdown.PasswordPromptByAction"),
    ("Enter your password to unlock", "Countdown.PasswordPromptPlain"),

    # Countdown 通知
    ("Your timer was canceled successfully", "Countdown.StoppedNotify"),
    ("Timer has been moved to the background", "Countdown.MovedToBackground"),
    ("2 hours remaining", "Countdown.Remaining.Hours2"),
    ("1 hour remaining", "Countdown.Remaining.Hours1"),
    ("30 minutes remaining", "Countdown.Remaining.Minutes30"),
    ("5 minutes remaining", "Countdown.Remaining.Minutes5"),
    ("30 seconds remaining", "Countdown.Remaining.Seconds30"),

    # 动态菜单项与窗体标题（长前缀在前）
    ("Resume", "Countdown.Menu.Resume"),
    ("Pause", "Countdown.Menu.Pause"),
    ("Countdown", "Countdown.Title"),

    # Settings
    ("Disabling the tray icon", "Settings.HideTrayIconWarn"),

    # 异常对话框
    ("Shutdown Timer Classic crashed and needs", "Error.UnhandledTitle"),
    ("Shutdown Timer Classic crashed!", "Error.ThreadTitle"),
    ("An unhandled exception occurred", "Error.UnhandledMessage"),
]


def main():
    rows = []
    unmatched = []

    with open(DRAFT, encoding="utf-8") as fh:
        for line in fh:
            line = line.rstrip("\n")
            if not line or "\t" not in line:
                continue
            text, _placeholder = line.split("\t", 1)
            key = None
            for prefix, candidate in RULES:
                if text.startswith(prefix):
                    key = candidate
                    break
            if key is None:
                unmatched.append(text[:70])
                key = "TODO"
            rows.append((text, key))

    with open(OUT, "w", encoding="utf-8") as fh:
        fh.write("# 由 tools/fill_map.py 从 map_draft.txt 生成；手工改规则后重跑\n")
        for text, key in rows:
            fh.write(f"{text}\t{key}\n")

    print(f"写入 {OUT}（{len(rows)} 条）")
    todo = [r for r in rows if r[1] == "TODO"]
    if todo:
        print(f"仍有 {len(todo)} 条未匹配:")
        for t in todo:
            print("  -", t[0][:70])
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
