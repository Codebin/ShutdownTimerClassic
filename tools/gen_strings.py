#!/usr/bin/env python3
"""生成 Strings.resx（英文中性）与 Strings.zh-CN.resx（简体中文）。

单一数据源：STRINGS 表。这样两份资源文件的 key 必然对齐，
漏翻会在生成阶段暴露，而不是等到界面上出现 [key] 才发现。

用法:  python3 tools/gen_strings.py
输出:  src/ShutdownTimer/Strings.resx / Strings.zh-CN.resx
"""

import os
import xml.sax.saxutils as su

# key -> (英文, 简体中文)
STRINGS = {
    # ---------- 应用级 ----------
    "App.Title": ("Shutdown Timer", "定时关机"),
    "App.WindowTitle": ("Shutdown Timer", "定时关机"),

    # ---------- 通用 ----------
    "Common.OK": ("OK", "确定"),
    "Common.Cancel": ("Cancel", "取消"),
    "Common.Hours": ("Hours", "时"),
    "Common.Minutes": ("Minutes", "分"),
    "Common.Seconds": ("Seconds", "秒"),

    # ---------- 电源动作显示名 ----------
    "Action.Shutdown": ("Shutdown", "关机"),
    "Action.Restart": ("Restart", "重启"),
    "Action.Hibernate": ("Hibernate", "休眠"),
    "Action.Sleep": ("Sleep", "睡眠"),
    "Action.Logout": ("Logout", "注销"),
    "Action.Lock": ("Lock", "锁定"),
    "Action.CustomCommand": ("Custom Command", "自定义命令"),

    # ---------- 托盘图标主题 ----------
    "TrayTheme.Automatic": ("Automatic", "自动"),
    "TrayTheme.Light": ("Light", "浅色"),
    "TrayTheme.Dark": ("Dark", "深色"),

    # ---------- 界面语言 ----------
    "Language.Automatic": ("Auto (follow system)", "自动（跟随系统）"),
    "Language.En": ("English", "English"),
    "Language.ZhCn": ("简体中文", "简体中文"),

    # ---------- Menu 主界面 ----------
    "Menu.Title": ("Shutdown Timer", "定时关机"),
    "Menu.TimeGroup": ("When to do it?", "何时执行？"),
    "Menu.TimeOfDayMode": ("At a specific time of day", "在指定时刻"),
    "Menu.CountdownMode": ("After a specific timespan", "经过指定时长后"),
    "Menu.ActionGroup": ("What to do?", "执行什么操作？"),
    "Menu.PreventSleep": ("Prevent system from going to sleep", "阻止系统进入睡眠"),
    "Menu.Graceful": ("Graceful (do not force close apps)", "优雅模式（不强制关闭应用）"),
    "Menu.RunInBackground": ("Run in background", "在后台运行"),
    "Menu.SelectAction": ("Select an action:", "选择操作："),
    "Menu.Start": ("Start", "开始"),
    "Menu.StartRecommended": ("Start (with recommended settings)", "开始（使用推荐设置）"),

    # Menu 悬停提示
    "Menu.Tip.Graceful": (
        "Applications that do not exit when prompted automatically get terminated by default to ensure a successful shutdown."
        "\n\nA graceful shutdown, on the other hand, will wait for all applications to exit before continuing with the shutdown."
        "\nThis might result in an unsuccessful shutdown if one or more applications are unresponsive or require a user interaction to exit.",
        "默认情况下，收到退出指令后仍未退出的应用会被强制终止，以确保关机顺利完成。"
        "\n\n而优雅关机会等待所有应用退出后再继续关机流程。"
        "\n如果某个应用无响应，或退出时需要用户手动操作，关机可能会因此失败。"),
    "Menu.Tip.PreventSleep": (
        "Depending on the power settings of your system, it might go to sleep after a certain amount of time due to inactivity."
        "\nThis option will keep the system awake to ensure the timer can properly run and execute a shutdown.",
        "根据系统的电源设置，电脑可能在一段时间无操作后自动进入睡眠。"
        "\n启用此选项会让系统保持唤醒，确保计时器正常运行并执行关机。"),
    "Menu.Tip.RunInBackground": (
        "This will launch the countdown without a visible window but will show a tray icon in your taskbar.",
        "启动倒计时时不显示窗口，只在任务栏显示一个托盘图标。"),
    "Menu.Tip.CountdownMode": (
        "Will count down from the hours, minutes and seconds selected below,\nlike a countdown timer, and execute the power action when it reaches zero.",
        "按下面选择的时、分、秒进行倒计时，\n像普通计时器一样，归零时执行所选的电源操作。"),
    "Menu.Tip.TimeOfDayMode": (
        "In this mode you can select the target time of day (24h clock) for the power action.\nIf the time has already passed, it will roll over to tomorrow.\n\nWhen you press start, the appropriate countdown will be calculated.\n",
        "此模式下可以指定一天中的目标时刻（24 小时制）来执行电源操作。\n如果该时刻已经过去，则顺延到第二天。\n\n点击开始后，程序会自动计算所需的倒计时长度。\n"),

    # Menu 校验与弹窗
    "Menu.Err.InvalidAction": ("Please select a valid action from the dropdown menu!\n\n", "请从下拉菜单中选择一个有效的操作！\n\n"),
    "Menu.Err.ZeroTime": ("The timer cannot start at 0 when in countdown mode!\n\n", "倒计时模式下，时间不能为 0！\n\n"),
    "Menu.Err.TimeSpanConversion": (
        "TimeSpan conversion failed! Please check if your time values are within a reasonable range or represent a valid time of day, if you used the time of day mode.\n\n",
        "时间转换失败！请检查所填时间是否在合理范围内；若使用的是指定时刻模式，请确认它是一个有效的时刻。\n\n"),
    "Menu.Warn.HugeTimespan": (
        "Your chosen time equates to {0} days ({1} years)!\n"
        "It is highly discouraged to choose such an insane amount of time as either your hardware, operating system, or this app will fail *way* before you even come close to reaching the target!"
        "\n\nBut if you are actually going to do this, please tell me how long this app survived.",
        "你所选的时间相当于 {0} 天（{1} 年）！"
        "\n强烈不建议设置这么夸张的时长——在接近目标之前，你的硬件、操作系统或本程序大概率会先出问题！"
        "\n\n但如果你真的打算这么做，请务必告诉我这个程序撑了多久。"),
    "Menu.Err.StartFailed": (
        "The following error(s) occurred:\n\n{0}Please resolve the problem(s) and try again.",
        "发生以下错误：\n\n{0}请解决这些问题后重试。"),
    "Menu.Err.StartFailedTitle": ("There seems to be a problem!", "出现问题！"),
    "Menu.WarnTitle": ("Warning", "警告"),
    "Menu.CustomCommandTitle": ("Custom Command", "自定义命令"),
    "Menu.CustomCommandPrompt": (
        "Please enter the command you want to have executed. If you wish to launch a file, just enter the full file path.\n\n"
        "Note: Execution will use this user's permissions.",
        "请输入要执行的命令。如果想打开某个文件，直接填写它的完整路径即可。\n\n"
        "注意：命令将以当前用户的权限执行。"),
    "Menu.Err.EmptyCommand": (
        "Custom command field was empty. Please enter a valid command or use a different action!",
        "自定义命令为空。请输入有效的命令，或改用其他操作！"),
    "Menu.Err.EmptyCommandTitle": ("Invalid command!", "命令无效！"),
    "Menu.PasswordTitle": ("Password Protection", "密码保护"),
    "Menu.PasswordPrompt": (
        "Please set a password to enable password protection.\n\n"
        "You can disable this dialog in the settings under Advanced > Password Protection.",
        "请设置一个密码以启用密码保护。\n\n"
        "可在“设置 > 高级 > 密码保护”中关闭此对话框。"),
    "Common.AlreadyRunning": (
        "Another instance of this application is already running. To allow multiple instances, please check the \"Allow multiple instances\" option in the application settings.\n\nExiting...",
        "本程序已有另一个实例在运行。若允许多个实例，请在应用设置中勾选“允许多个实例”。\n\n即将退出..."),
    "Common.AlreadyRunningTitle": ("Application already running!", "程序已在运行！"),

    # ---------- Countdown 倒计时窗口 ----------
    "Countdown.Title": ("Countdown", "倒计时"),
    "Countdown.TitleFormat": ("{0} Timer", "{0}倒计时"),
    "Countdown.TitleFormatPaused": ("{0} Timer (paused)", "{0}倒计时（已暂停）"),
    "Countdown.Menu.Pause": ("Pause", "暂停"),
    "Countdown.Menu.Resume": ("Resume", "继续"),
    "Countdown.Menu.StopAndExit": ("Stop and exit", "停止并退出"),
    "Countdown.Menu.ResetTimer": ("Reset timer", "重置计时"),
    "Countdown.Menu.RestartApp": ("Restart application", "重启应用程序"),
    "Countdown.Menu.MoveToBackground": ("Move to background", "移到后台"),
    "Countdown.Menu.ShowCountdownWindow": ("Show countdown window", "显示倒计时窗口"),
    "Countdown.Menu.SetNewCountdown": ("Set a new countdown", "设置新的倒计时"),
    "Countdown.Menu.ToggleClickThrough": ("Toggle click-through", "切换鼠标穿透"),
    "Countdown.ClickThroughOn": ("Click-through is ON. The window ignores mouse input; use the tray icon menu to control the timer.",
                                 "鼠标穿透已开启：窗口不接收鼠标输入，请用托盘图标菜单控制计时器。"),

    # ---------- Settings 设置窗口 ----------
    "Settings.Title": ("Settings", "设置"),
    "Settings.WindowTitle": ("Shutdown Timer - Settings", "定时关机 - 设置"),
    "Settings.Footer": ("Made with love in Germany", "在德国用心制作"),
    "Settings.ViewOnGitHub": ("View on GitHub", "GitHub 主页"),
    "Settings.TabGeneral": ("General", "常规"),
    "Settings.TabAdvanced": ("Advanced", "高级"),
    "Settings.TabAbout": ("About", "关于"),
    "Settings.AppBehaviour": ("Application behaviour", "应用行为"),
    "Settings.AllowMultipleInstances": ("Allow multiple instances", "允许多个实例"),
    "Settings.RememberPosCountdown": ("Remember last screen position (Countdown)", "记住上次窗口位置（倒计时）"),
    "Settings.RememberPosMenu": ("Remember last screen position (Menu)", "记住上次窗口位置（主界面）"),
    "Settings.LanguageGroup": ("Language", "界面语言"),
    "Settings.LanguageLabel": ("UI language:", "界面语言："),
    "Settings.LanguageRestartHint": (
        "Changing the language takes effect after restarting the application.",
        "切换语言后需重启应用才会生效。"),
    "Settings.TrayIconGroup": ("System tray menu", "系统托盘菜单"),
    "Settings.TrayIconThemeLabel": ("Trayicon theme:", "托盘图标主题："),
    "Settings.ClearSettings": ("Clear Settings", "清除设置"),
    "Settings.TimerDefaults": ("Timer defaults", "定时器默认值"),
    "Settings.CustomDefaults": ("Custom defaults", "自定义默认值"),
    "Settings.RememberLastState": ("Remember last state", "记住上次状态"),

    "Settings.DeveloperOptions": ("Developer Options", "开发者选项"),
    "Settings.OpenAppData": ("Open appdata folder", "打开 appdata"),
    "Settings.SaveLogs": ("Save event logs to appdata on exit", "退出时将事件日志保存到 appdata"),
    "Settings.PasswordGroup": ("Password protection", "密码保护"),
    "Settings.PasswordLabel": (
        "Locks the countdown UI with a password you can set when starting a shutdown after clicking start.",
        "锁定倒计时界面，需要在点击开始后的启动过程中设置密码。"),
    "Settings.EnablePassword": ("Enable password protection", "启用密码保护"),
    "Settings.CountdownGroup": ("Countdown window", "倒计时窗口"),
    "Settings.TransparentWindow": ("Transparent countdown window", "透明倒计时窗口"),
    "Settings.AdaptiveTextSize": ("Adapt clock text to window size", "时钟文字随窗口大小自适应"),
    "Settings.SetBackgroundColor": ("(set background color)", "（设置背景颜色）"),
    "Settings.DisableNotifications": ("Disable notifications and confirmations", "禁用通知与确认对话框"),
    "Settings.DisableAnimations": ("Disable animations", "禁用动画"),
    "Settings.DisableAlwaysOnTop": ("Disable always on top behaviour", "禁用始终置顶行为"),
    "Settings.ForceFlagGroup": ("Force flag", "强制标志"),
    "Settings.ForceFlagDocs": ("Documentation", "文档"),
    "Settings.ForceFlagLabel": (
        "Changes how running applications are force-closed when\r\nusing a non-graceful shutdown.",
        "更改在非优雅关机时强制关闭\r\n正在运行应用的方式。"),

    "Settings.AboutGroup": ("About this app", "关于本应用"),
    "Settings.CreateLogfile": ("Create Logfile", "生成日志文件"),
    "Settings.Email": ("Email", "发送邮件"),
    "Settings.GitHubIssues": ("GitHub Issues", "GitHub Issues"),
    "Settings.AboutText": (
        "This app is a free open-source project by Lukas Langrock under the MIT license which means there is no warranty provided and I shall not be held liable for any kind of damage!\n"
        "After all you may lose unsaved work or mess up another running application. Use this with caution and common sense!\n\n"
        "The application is built to be reliable and should accurately measure and count time. I can't exactly guarantee this but I aim to get the application to be as stable and polished as possible.\n"
        "In the case you encounter an error or notice the application not working as it should, an issue on GitHub or an Email would be very appreciated so I can fix the mistake.\n\n"
        "If you enjoy using this free app, please consider writing a review on the Microsoft Store or starring the project on GitHub.\n"
        "Any contributions in the form of issues and PR's are very welcome.",
        "本应用是 Lukas Langrock 的免费开源项目，采用 MIT 许可证，因此不提供任何担保，本人也不会对任何形式的损害承担责任！\n"
        "毕竟你可能丢失未保存的工作，或影响其他正在运行的程序。请谨慎并凭常识使用！\n\n"
        "本应用以可靠为目标构建，应当能够准确计时。我无法做出绝对保证，但会尽力让它稳定、完善。\n"
        "如果你遇到错误，或发现程序行为不符合预期，欢迎在 GitHub 提交 issue 或发送邮件，以便我修复问题。\n\n"
        "如果你喜欢这款免费应用，欢迎在 Microsoft Store 写评价，或在 GitHub 上给项目加一颗星。\n"
        "任何形式的贡献（issue 与 PR）都非常欢迎。"),
    "Settings.LicenseGroup": ("Licenses && Source", "许可证与源码"),
    "Settings.SourceCode": ("Sourcecode", "源码"),
    "Settings.Application": ("Application:", "应用程序："),

    "Settings.HideTrayIcon": ("Hide tray icon (not recommended)", "隐藏托盘图标（不推荐）"),
    "Settings.HideTrayIconWarn": (
        "Disabling the tray icon will prevent you from interacting with the application when it's running in the background.\n"
        "Your only choice in such a case is to kill the application with Task Manager.\n\n"
        "Notifications will also stop working!\n\n"
        "Are you sure you want to enable this option?",
        "禁用托盘图标后，当应用在后台运行时你将无法与它交互。\n"
        "那种情况下你只能用任务管理器结束应用。\n\n"
        "通知也会一并失效！\n\n"
        "确定要启用这个选项吗？"),

    # ---------- 倒计时窗口新特性（本次改造） ----------
    "Settings.ClickThrough": ("Click-through (ignore mouse input)", "鼠标穿透（忽略鼠标输入）"),
    "Settings.ClickThroughHint": (
        "Mouse clicks pass through the countdown window to whatever is behind it. Use the tray menu to control the timer.",
        "鼠标点击会穿过倒计时窗口落到下层内容。请通过托盘菜单控制计时器。"),
    "Settings.CountdownSize": ("Window size:", "窗口大小："),
    "Settings.CountdownSizeReset": ("Reset size", "重置大小"),

    # ---------- 托盘气泡与其余弹窗 ----------
    "Tray.Balloon.CountdownStopped": ("The countdown was stopped.", "倒计时已停止。"),
    "Tray.Balloon.CountdownFinished": ("The countdown has finished.", "倒计时已结束。"),
    "Countdown.ConfirmStop": (
        "Do you really want to stop the countdown and exit the application?",
        "确定要停止倒计时并退出程序吗？"),
    "Countdown.ConfirmStopTitle": ("Stop countdown?", "停止倒计时？"),
    "Countdown.ConfirmRestart": (
        "Do you really want to restart the application? The current countdown will be lost.",
        "确定要重启应用程序吗？当前的倒计时将会丢失。"),
    "Countdown.ConfirmRestartTitle": ("Restart application?", "重启应用程序？"),
    "Countdown.ReLockPrompt": ("Would you like to re-lock the countdown?", "要重新锁定倒计时吗？"),
    "Countdown.PasswordWrong": (
        "The password you entered is incorrect. The countdown keeps running.",
        "你输入的密码不正确。倒计时仍在继续。"),
    "Countdown.PasswordWrongTitle": ("Wrong password", "密码错误"),
    "Countdown.PasswordTitle": ("Password Protection", "密码保护"),
    "Countdown.PasswordPromptUnlock": (
        "Enter the password to unlock the countdown controls.",
        "输入密码以解锁倒计时控件。"),
    "Countdown.PasswordPromptLock": (
        "Enter a password to lock the countdown controls.",
        "输入密码以锁定倒计时控件。"),
    "Countdown.Err.CustomCommand": (
        "There was an error executing your custom command.\n\nYour custom command: {0}\nError: {1}",
        "执行自定义命令时出错。\n\n你的命令：{0}\n错误：{1}"),
    "Countdown.Err.CustomCommandTitle": ("Countdown Update", "倒计时更新"),

    # ---------- 托盘气泡通知 ----------
    "Tray.Balloon.CountdownFinished": ("The countdown has finished.", "倒计时已结束。"),
    "Tray.Balloon.CountdownStopped": ("The countdown was stopped.", "倒计时已停止。"),

    # ---------- 取消/停止与剩余时间通知 ----------
    "Countdown.StoppedNotify": (
        "Your timer was canceled successfully!\nThe application will now close.",
        "定时器已成功取消！\n应用即将关闭。"),
    "Countdown.ResetNotify": (
        "Timer has been reset. Remaining time until power action will be executed is {0} hours, {1} minutes and {2} seconds.",
        "定时器已重置。距离执行电源操作还剩 {0} 小时 {1} 分 {2} 秒。"),
    "Countdown.MovedToBackground": (
        "Timer has been moved to the background. Right-click the tray icon for more info.",
        "定时器已移到后台。右键点击托盘图标可查看更多信息。"),
    "Countdown.Remaining.Hours2": (
        "2 hours remaining until the power action will be executed",
        "距离执行电源操作还剩 2 小时"),
    "Countdown.Remaining.Hours1": (
        "1 hour remaining until the power action will be executed.",
        "距离执行电源操作还剩 1 小时。"),
    "Countdown.Remaining.Minutes30": (
        "30 minutes remaining until the power action will be executed.",
        "距离执行电源操作还剩 30 分钟。"),
    "Countdown.Remaining.Minutes5": (
        "5 minutes remaining until the power action will be executed.",
        "距离执行电源操作还剩 5 分钟。"),
    "Countdown.Remaining.Seconds30": (
        "30 seconds remaining until the power action will be executed.",
        "距离执行电源操作还剩 30 秒。"),

    # ---------- 设置新倒计时 ----------
    "Countdown.UpdateTitle": ("Countdown Update", "倒计时更新"),
    "Countdown.NewTimeTitle": ("Set a new countdown", "设置新的倒计时"),
    "Countdown.NewTimePrompt": (
        "Enter new time for the countdown in the format of HH:mm:ss or HH:mm.\n\nThis will replace the current timer in place.",
        "请输入新的倒计时时间，格式为 HH:mm:ss 或 HH:mm。\n\n这会直接替换当前的定时器。"),
    "Countdown.Err.NoTime": (
        "Operation aborted: You have not supplied a new time value!",
        "操作已中止：你没有提供新的时间值！"),
    "Countdown.Err.InvalidTime": (
        "Operation aborted: You have not supplied a valid time value!",
        "操作已中止：你提供的时间值无效！"),
    "Countdown.Err.InternalError": (
        "Operation aborted: You have either not supplied a valid time value or there was an internal error outside the scope of your input while processing it.",
        "操作已中止：要么你提供的时间值无效，要么处理过程中出现了与输入无关的内部错误。"),

    # ---------- 密码解锁提示 ----------
    "Countdown.PasswordPromptByAction": (
        "This countdown has been protected with a password. Enter your password to release the lock.\n"
        "You can re-lock the countdown by clicking on the lock icon afterwards.",
        "此倒计时已受密码保护。输入密码即可解锁。\n"
        "之后点击锁形图标可以重新上锁。"),
    "Countdown.PasswordPromptPlain": (
        "Enter your password to unlock this countdown.\n\n"
        "You can re-lock the countdown by clicking on the lock icon afterwards.",
        "输入密码以解锁此倒计时。\n\n"
        "之后点击锁形图标可以重新上锁。"),

    # ---------- 异常处理对话框 ----------
    "Error.UnhandledTitle": (
        "Shutdown Timer Classic crashed and needs to be terminated!",
        "Shutdown Timer Classic 已崩溃，必须终止！"),
    "Error.UnhandledMessage": (
        "An unhandled exception occurred and the application needs to be terminated!\n\n"
        "A log file containing information about the process and the error has been saved to your desktop.\n"
        "Please create an issue on GitHub and include the contents of this log file to help identify and fix the issue.\n\n"
        "GitHub: github.com/lukaslangrock/ShutdownTimerClassic/issues\n"
        "Email: lukas.langrock@outlook.de",
        "发生未处理的异常，应用必须终止！\n\n"
        "一份包含进程与错误信息的日志文件已保存到桌面。\n"
        "请在 GitHub 上提交 issue，并附上该日志的内容，以便定位并修复问题。\n\n"
        "GitHub：github.com/lukaslangrock/ShutdownTimerClassic/issues\n"
        "邮箱：lukas.langrock@outlook.de"),
    "Error.ThreadTitle": ("Shutdown Timer Classic crashed!", "Shutdown Timer Classic 已崩溃！"),
    "Error.ThreadMessage": (
        "A thread exception occurred!\n\n"
        "A log file containing information about the process and the error has been saved to your desktop.\n"
        "Please create an issue on GitHub and include the contents of this log file to help identify and fix the issue.\n\n"
        "Log file location: {0}\n"
        "GitHub: github.com/lukaslangrock/ShutdownTimerClassic/issues\n"
        "Email: lukas.langrock@outlook.de\n\n"
        "The application experienced a critical error and may very well be broken. It is not recommended to keep using this instance of the application!\n"
        "Would you like to terminate the application?",
        "发生线程异常！\n\n"
        "一份包含进程与错误信息的日志文件已保存到桌面。\n"
        "请在 GitHub 上提交 issue，并附上该日志的内容，以便定位并修复问题。\n\n"
        "日志文件位置：{0}\n"
        "GitHub：github.com/lukaslangrock/ShutdownTimerClassic/issues\n"
        "邮箱：lukas.langrock@outlook.de\n\n"
        "应用发生了严重错误，很可能已不可用，不建议继续使用当前实例！\n"
        "是否要终止应用？"),
}

HEADER = '''<?xml version="1.0" encoding="utf-8"?>
<root>
  <xsd:schema id="root" xmlns="" xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:msdata="urn:schemas-microsoft-com:xml-msdata">
    <xsd:import namespace="http://www.w3.org/XML/1998/namespace" />
    <xsd:element name="root" msdata:IsDataSet="true">
      <xsd:complexType>
        <xsd:choice maxOccurs="unbounded">
          <xsd:element name="metadata">
            <xsd:complexType>
              <xsd:sequence>
                <xsd:element name="value" type="xsd:string" minOccurs="0" />
              </xsd:sequence>
              <xsd:attribute name="name" use="required" type="xsd:string" />
              <xsd:attribute name="type" type="xsd:string" />
              <xsd:attribute name="mimetype" type="xsd:string" />
              <xsd:attribute ref="xml:space" />
            </xsd:complexType>
          </xsd:element>
          <xsd:element name="assembly">
            <xsd:complexType>
              <xsd:attribute name="alias" type="xsd:string" />
              <xsd:attribute name="name" type="xsd:string" />
            </xsd:complexType>
          </xsd:element>
          <xsd:element name="data">
            <xsd:complexType>
              <xsd:sequence>
                <xsd:element name="value" type="xsd:string" minOccurs="0" msdata:Ordinal="1" />
                <xsd:element name="comment" type="xsd:string" minOccurs="0" msdata:Ordinal="2" />
              </xsd:sequence>
              <xsd:attribute name="name" type="xsd:string" use="required" msdata:Ordinal="1" />
              <xsd:attribute name="type" type="xsd:string" msdata:Ordinal="3" />
              <xsd:attribute name="mimetype" type="xsd:string" msdata:Ordinal="4" />
              <xsd:attribute ref="xml:space" />
            </xsd:complexType>
          </xsd:element>
          <xsd:element name="resheader">
            <xsd:complexType>
              <xsd:sequence>
                <xsd:element name="value" type="xsd:string" minOccurs="0" msdata:Ordinal="1" />
              </xsd:sequence>
              <xsd:attribute name="name" type="xsd:string" use="required" />
            </xsd:complexType>
          </xsd:element>
        </xsd:choice>
      </xsd:complexType>
    </xsd:element>
  </xsd:schema>
  <resheader name="resmimetype"><value>text/microsoft-resx</value></resheader>
  <resheader name="version"><value>2.0</value></resheader>
  <resheader name="reader"><value>System.Resources.ResXResourceReader, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value></resheader>
  <resheader name="writer"><value>System.Resources.ResXResourceWriter, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value></resheader>
'''


def emit(path, index, note):
    parts = [HEADER]
    parts.append(f"  <!-- {note} -->\n")
    for key, pair in STRINGS.items():
        value = pair[index]
        # 换行在 resx 里直接以真实换行保存，配合 xml:space="preserve"
        escaped = su.escape(value)
        parts.append(f'  <data name="{key}" xml:space="preserve"><value>{escaped}</value></data>\n')
    parts.append("</root>\n")
    with open(path, "w", encoding="utf-8") as fh:
        fh.write("".join(parts))
    print(f"写入 {path}（{len(STRINGS)} 条）")


def main():
    here = os.path.dirname(os.path.abspath(__file__))
    target = os.path.join(here, "..", "src", "ShutdownTimer")

    # 先做一致性检查：每条都必须有中英两份
    missing = [k for k, v in STRINGS.items() if not v[0] or not v[1]]
    if missing:
        raise SystemExit("以下 key 缺少翻译: " + ", ".join(missing))

    emit(os.path.join(target, "Strings.resx"), 0, "中性语言资源（英文），同时作为缺译时的兜底")
    emit(os.path.join(target, "Strings.zh-CN.resx"), 1, "简体中文资源")

    # 输出 key 清单，供 Designer 替换脚本和人工核对使用
    listing = os.path.join(here, "keys.txt")
    with open(listing, "w", encoding="utf-8") as fh:
        fh.write("\n".join(STRINGS.keys()) + "\n")
    print(f"key 清单写入 {listing}")


if __name__ == "__main__":
    main()
