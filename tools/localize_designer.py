#!/usr/bin/env python3
"""把 Designer 文件里硬编码的控件文本替换为 Loc.T("<key>")。

映射表是唯一真相来源：脚本会报告任何"没在映射表里、也没在跳过清单里"的
.Text 赋值，避免汉化时漏掉控件。

用法:  python3 tools/localize_designer.py
"""

import os
import re
import sys

# 控件名 -> 资源 key
MAP = {
    "Menu.Designer.cs": {
        "titleLabel": "App.Title",
        "hoursLabel": "Common.Hours",
        "minutesLabel": "Common.Minutes",
        "secondsLabel": "Common.Seconds",
        "timeGroupBox": "Menu.TimeGroup",
        "timeOfDayModeRadioButton": "Menu.TimeOfDayMode",
        "countdownModeRadioButton": "Menu.CountdownMode",
        "actionGroupBox": "Menu.ActionGroup",
        "preventSleepCheckBox": "Menu.PreventSleep",
        "gracefulCheckBox": "Menu.Graceful",
        "backgroundCheckBox": "Menu.RunInBackground",
        "actionLabel": "Menu.SelectAction",
        "startButton": "Menu.Start",
    },
    "Countdown.Designer.cs": {
        "titleLabel": "App.Title",
        "notifyIcon": "App.Title",
        "timerPauseMenuItem": "Countdown.Menu.Pause",
        "timerStopMenuItem": "Countdown.Menu.StopAndExit",
        "timerResetMenuItem": "Countdown.Menu.ResetTimer",
        "appRestartMenuItem": "Countdown.Menu.RestartApp",
        "timerUIHideMenuItem": "Countdown.Menu.MoveToBackground",
        "timerUIShowMenuItem": "Countdown.Menu.ShowCountdownWindow",
        "updateTimeMenuItem": "Countdown.Menu.SetNewCountdown",
    },
    "InputBox.Designer.cs": {
        "okButton": "Common.OK",
        "cancelButton": "Common.Cancel",
    },
    "Settings.Designer.cs": {
        "titleLabel": "Settings.Title",
        "footerLabel": "Settings.Footer",
        "githubLinkLabel": "Settings.ViewOnGitHub",
        "tabPage1": "Settings.TabGeneral",
        "groupBox1": "Settings.AppBehaviour",
        "enableMultipleInstances": "Settings.AllowMultipleInstances",
        "rememberLastScreenPositionCountdown": "Settings.RememberPosCountdown",
        "rememberLastScreenPositionUI": "Settings.RememberPosMenu",
        "trayiconGroupBox": "Settings.TrayIconGroup",
        "trayiconThemeLabel": "Settings.TrayIconThemeLabel",
        "clearSettingsButton": "Settings.ClearSettings",
        "defaultSettingsGroupBox": "Settings.TimerDefaults",
        "customDefaultsGroupBox": "Settings.CustomDefaults",
        "timeOfDayModeRadioButton": "Menu.TimeOfDayMode",
        "countdownModeRadioButton": "Menu.CountdownMode",
        "secondsLabel": "Common.Seconds",
        "hoursLabel": "Common.Hours",
        "minutesLabel": "Common.Minutes",
        "preventSleepCheckBox": "Menu.PreventSleep",
        "gracefulCheckBox": "Menu.Graceful",
        "backgroundCheckBox": "Menu.RunInBackground",
        "actionLabel": "Menu.SelectAction",
        "rememberStateCheckBox": "Settings.RememberLastState",
        "tabPage2": "Settings.TabAdvanced",
        "developerGroupBox": "Settings.DeveloperOptions",
        "openAppDataLinkLabel": "Settings.OpenAppData",
        "saveLogsCheckBox": "Settings.SaveLogs",
        "passwordGroupBox": "Settings.PasswordGroup",
        "passwordLabel": "Settings.PasswordLabel",
        "passwordCheckBox": "Settings.EnablePassword",
        "countdownGroupBox": "Settings.CountdownGroup",
        "transparentWindowCheckBox": "Settings.TransparentWindow",
        "enableAdaptiveCountdownTextSizeCheckBox": "Settings.AdaptiveTextSize",
        "setBackgroundColorLinkLabel": "Settings.SetBackgroundColor",
        "disableNotificationsCheckBox": "Settings.DisableNotifications",
        "disableAnimationsCheckBox": "Settings.DisableAnimations",
        "disableAlwaysOnTopCheckBox": "Settings.DisableAlwaysOnTop",
        "forceFlagGroupBox": "Settings.ForceFlagGroup",
        "forceFlagDocsLinkLabel": "Settings.ForceFlagDocs",
        "forceFlagLabel": "Settings.ForceFlagLabel",
        "tabPage3": "Settings.TabAbout",
        "aboutGroupBox": "Settings.AboutGroup",
        "logButton": "Settings.CreateLogfile",
        "emailbutton": "Settings.Email",
        "githubButton": "Settings.GitHubIssues",
        "aboutRichTextBox": "Settings.AboutText",
        "licenseGroupBox": "Settings.LicenseGroup",
        "faSourceLinkLabel": "Settings.SourceCode",
        "appSourceLinkLabel": "Settings.SourceCode",
        "appInfoLabel": "Settings.Application",
        "hideTrayIconCheckBox": "Settings.HideTrayIcon",
    },
}

# 明确不汉化的控件：运行时由代码赋值、纯数字、或 Windows API 常量名
SKIP = {
    "Menu.Designer.cs": {"versionLabel", "actionComboBox"},
    "Countdown.Designer.cs": {"timeLabel"},
    "InputBox.Designer.cs": {"titleLabel", "messageLabel", "$this"},
    "Settings.Designer.cs": {
        "appLabel", "actionComboBox",
        "forceFlagRadioButton", "forceIfHungFlagRadioButton",
        "faLicenseLinkLabel", "faInfoLabel", "appLicenseLinkLabel",
    },
}

# 窗体自身的标题：写法是 this.Text = "...";，与控件不同，单独处理
FORM_KEYS = {
    "Menu.Designer.cs": "Menu.Title",
    "Countdown.Designer.cs": "Countdown.Title",
    "Settings.Designer.cs": "Settings.WindowTitle",
}

# 匹配 this.<ctrl>.Text = <字符串字面量拼接>;  或  = resources.GetString("...");
PATTERN = re.compile(
    r'(?P<indent>[ \t]*)this\.(?P<ctrl>\w+)\.Text\s*=\s*'
    r'(?:"(?:[^"\\]|\\.)*"(?:\s*\+\s*"(?:[^"\\]|\\.)*")*|resources\.GetString\("[^"]*"\));'
)

# 匹配窗体自身的 this.Text = <字符串字面量拼接>;
PATTERN_FORM = re.compile(
    r'(?P<indent>[ \t]*)this\.Text\s*=\s*'
    r'(?:"(?:[^"\\]|\\.)*"(?:\s*\+\s*"(?:[^"\\]|\\.)*")*);'
)


def localize(path, name):
    with open(path, encoding="utf-8-sig") as fh:
        src = fh.read()

    # 幂等保护：已经汉化过的文件不再重复处理，否则"未命中"检查会误报
    if "Loc.T(" in src:
        print(f"{name}: 已汉化，跳过")
        return [], []

    mapping = MAP[name]
    skip = SKIP.get(name, set())
    replaced, untouched = [], []

    def sub(match):
        ctrl = match.group("ctrl")
        if ctrl in mapping:
            replaced.append(ctrl)
            return f'{match.group("indent")}this.{ctrl}.Text = Loc.T("{mapping[ctrl]}");'
        untouched.append(ctrl)
        return match.group(0)

    out = PATTERN.sub(sub, src)

    # 窗体自身的标题（this.Text = "...";）
    form_key = FORM_KEYS.get(name)
    if form_key:
        hits = len(PATTERN_FORM.findall(out))
        if hits:
            out = PATTERN_FORM.sub(
                lambda m: f'{m.group("indent")}this.Text = Loc.T("{form_key}");', out)
            replaced.append("<form>")

    # 补 using，让 Designer 能用短名 Loc.T
    if replaced and "using ShutdownTimer.Helpers;" not in out:
        out = "using ShutdownTimer.Helpers;\n\n" + out

    with open(path, "w", encoding="utf-8") as fh:
        fh.write(out)

    return replaced, untouched


def main():
    here = os.path.dirname(os.path.abspath(__file__))
    src = os.path.join(here, "..", "src", "ShutdownTimer")

    total_replaced = 0
    problems = []

    for name in MAP:
        path = os.path.join(src, name)
        if not os.path.exists(path):
            problems.append(f"缺少文件: {name}")
            continue

        replaced, untouched = localize(path, name)

        # 跳过已汉化的文件，不做后续一致性检查
        if not replaced and not untouched:
            continue

        total_replaced += len(replaced)

        unexpected = [c for c in untouched if c not in SKIP.get(name, set())]
        print(f"{name}: 替换 {len(replaced)} 处")
        if unexpected:
            problems.append(f"{name}: 未映射的控件 {sorted(set(unexpected))}")

        # 映射表里存在但文件里没匹配到的 key，说明上游改过或名字写错
        matched = set(replaced)
        missing = [c for c in MAP[name] if c not in matched]
        if missing:
            problems.append(f"{name}: 映射表有但文件未命中 {sorted(missing)}")

    print(f"\n合计替换 {total_replaced} 处")
    if problems:
        print("\n需要你确认:")
        for p in problems:
            print("  -", p)
        sys.exit(1)
    print("全部覆盖，无遗漏")


if __name__ == "__main__":
    main()
