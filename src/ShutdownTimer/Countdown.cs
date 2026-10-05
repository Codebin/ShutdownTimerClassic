using ShutdownTimer.Helpers;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace ShutdownTimer
{
    public partial class Countdown : Form
    {
        public string Password { get; set; } // if value is not empty then a password will be required to change or disable the countdown
        public bool IsUserLaunched { get; set; } // false if launched from CLI
        public bool IsForegroundUI { get; set; } // disables form UI updates when set to false (used for running in background)
        public bool IsReadOnly { get; set; } // disables all UI controls and exit dialogs when true

        private TimeSpan lastStateUITimeSpan; // used to limit UI events that should only be executed once per second instead of once per update
        private bool ignoreClose; // true: cancel close events without asking | false: default behaviour (if ignoreClose == true, allowClose will be ignored)
        private bool allowClose; // true: accept close without asking | false: Ask user to confirm closing
        private int lockState = 0; // used for Password Protection free/locked/unlocked
        private int logTimerCounter = 0; // used to log events every 10,000 timer ticks

        public Countdown()
        {
            InitializeComponent();
        }

        public void UpdateExternal(TimeSpan ts)
        {
            this.Invoke(new Action(() => UpdateUI(ts)));
        }

        public void ExitExternal()
        {
            this.Invoke(new Action(() =>
            {
                ignoreClose = false;
                allowClose = true;
                this.Close();
            }));
        }

        #region "form events"

        // entrypoint
        private void Countdown_Load(object sender, EventArgs e)
        {
            ExceptionHandler.Log("Check multiple instances setting");

            if (!SettingsProvider.Settings.EnableMultipleInstances)
            {
                ExceptionHandler.Log("Multiple instances disabled");

                ExceptionHandler.Log("Checking for running instance");
                if (!ApplicationInstanceManager.IsSingleInstance())
                {
                    MessageBox.Show(Loc.T("Common.AlreadyRunning"),Loc.T("Common.AlreadyRunningTitle"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    ExceptionHandler.Log("Another instance detected; exiting");
                    Application.Exit();
                }
            }
            else
            {
                ExceptionHandler.Log("Multiple instances allowed");
            }

            ExceptionHandler.Log("Starting timer clock");
            Timer.Start();

            ExceptionHandler.Log("Preparing UI");

            // Set custom background color / transparency

            if (SettingsProvider.Settings.DisableAnimations)
            {
                if (SettingsProvider.Settings.BackgroundColor == Color.Transparent)
                {
                    BackColor = Color.Black;
                    TransparencyKey = Color.Black;
                    FormBorderStyle = FormBorderStyle.None;
                }
                else
                {
                    BackColor = SettingsProvider.Settings.BackgroundColor;
                }
            }

            // Set trayIcon icon to the opposite of the selected theme
            bool lighttheme;

            if (SettingsProvider.Settings.TrayIconTheme == "Light") { lighttheme = true; }
            else if (SettingsProvider.Settings.TrayIconTheme == "Dark") { lighttheme = false; }
            else { lighttheme = WindowsAPIs.SystemUsesLightTheme(); }

            // When the dark theme is selected we are using the light icon to generate contrast (and vice versa), you wouldn't want a white icon on a white background.
            notifyIcon.Icon = lighttheme ? Properties.Resources.icon_dark : Properties.Resources.icon_light;

            // Hide tray icon if enabled in settings
            notifyIcon.Visible = !SettingsProvider.Settings.HideTrayIcon;

            // Load password and set the lock state
            if (!String.IsNullOrEmpty(Password))
            {
                ExceptionHandler.Log("Password configured");
                ChangeLockState("locked");
            }

            /// Setup UI

            titleLabel.Text = Loc.T("Countdown.TitleFormat", Timer.Action.DisplayName());

            TopMost = !SettingsProvider.Settings.DisableAlwaysOnTop;

            // 本次改造新增：恢复窗口尺寸、应用鼠标穿透、给托盘菜单加开关、加运行时上锁入口
            ApplyCountdownSize();
            BuildClickThroughMenuItem();
            BuildLockMenuItem();

            // 字体按界面语言选择（中文需要 CJK 字形）
            this.Font = Loc.CreateUiFont(8.25f);

            if (!IsForegroundUI)
            {
                HideUI();
            }

            if (IsReadOnly)
            {
                ignoreClose = true;
                contextMenuStrip.Enabled = false;
            }

            if (!IsUserLaunched) // disable restart app menu button because it would also keep the CLI args so the countdown would restart so it's basically useless
            {
                appRestartMenuItem.Enabled = false;
            }

            // Check if the user has opted to remember the last Countdown screen position,
            // and if LastScreenPosition is available (not null), apply its X and Y coordinates to position the form.

            if (SettingsProvider.Settings.RememberLastScreenPositionCountdown && SettingsProvider.Settings.LastScreenPositionCountdown != null)
            {
                this.Location = new Point(SettingsProvider.Settings.LastScreenPositionCountdown.X, SettingsProvider.Settings.LastScreenPositionCountdown.Y);
            }

            ExceptionHandler.Log("UI prepared");

            ExceptionHandler.Log("Updating UI");
            UpdateUI(Timer.CountdownTimeSpan);

            ExceptionHandler.Log("Countdown sequence started");
        }

        /// <summary>
        /// Resize countdown text if enabled in settings
        /// </summary>
        private void Countdown_SizeChanged(object sender, EventArgs e)
        {
            if (SettingsProvider.Settings.AdaptiveCountdownTextSize)
            {
                int defaultWidth = 375;
                int defaultHeight = 185;
                int defaultFontSize = 24;

                float scaleX = Size.Width / defaultWidth;
                float scaleY = Size.Height / defaultHeight;
                float scaledSize = defaultFontSize * Math.Min(scaleX, scaleY);

                timeLabel.Font = new Font(timeLabel.Font.FontFamily, Math.Max(defaultFontSize, scaledSize), timeLabel.Font.Style);
            }
        }

        /// <summary>
        /// Clicking on the lock-icon will allow the user to change the lock state
        /// </summary>
        private void LockStatePictureBox_Click(object sender, EventArgs e)
        {
            ExceptionHandler.Log("Lock icon clicked");

            switch (lockState)
            {
                case 0: // lock state 'free': nothing needs to be done
                    ExceptionHandler.Log("Error: lockState = free, the user should not be able to click the icon as it should be invisible and deactivated");
                    break;

                case 1: // lock state 'locked': ask user for password to unlock
                    ExceptionHandler.Log("LockState=locked: requesting password");
                    UnlockUIByPassword();
                    break;

                case 2: // lock state 'unlocked': change lockstate to 'locked'
                    ExceptionHandler.Log("LockState=unlocked: confirming re-lock");
                    DialogResult result = MessageBox.Show(Loc.T("Countdown.ReLockPrompt"),Loc.T("Countdown.PasswordTitle"), MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (result == DialogResult.Yes)
                    {
                        ExceptionHandler.Log("User confirmed lock");
                        ChangeLockState("locked");
                    }
                    else { ExceptionHandler.Log("User kept UI unlocked"); }
                    break;
            }
        }

        /// <summary>
        /// Saves the last Countdown position
        /// </summary>
        private void SaveSettings()
        {
            ExceptionHandler.Log("Saving settings");

            // Only store last screen position if setting is enabled and countdown is not in background
            if (SettingsProvider.Settings.RememberLastScreenPositionCountdown && IsForegroundUI)
            {
                SettingsProvider.Settings.LastScreenPositionCountdown = new LastScreenPosition
                {
                    X = this.Location.X,
                    Y = this.Location.Y
                };
            }

            SettingsProvider.Save();

            ExceptionHandler.Log("Settings saved");
        }

        /// <summary>
        /// Logic to prevent or ignore unwanted close events and notify the user.
        /// </summary>
        private void Countdown_FormClosing(object sender, FormClosingEventArgs e)
        {
            ExceptionHandler.Log("Form closing event");
            if (ignoreClose) // Ignore closing event
            {
                e.Cancel = true;
                ExceptionHandler.Log("Close ignored (ignoreClose)");
                return;
            }
            else if (!allowClose && !SettingsProvider.Settings.DisableNotifications) // Ask user for confirmation
            {
                e.Cancel = true;
                ExceptionHandler.Log("Prompting user to confirm exit");
                string caption = Loc.T("Countdown.ConfirmStopTitle");
                string message = Loc.T("Countdown.ConfirmStop");
                DialogResult question = MessageBox.Show(message, caption, MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (question == DialogResult.Yes) { ExceptionHandler.Log("User confirmed exit"); ExitApplication(); return; }
            }
            else if (!allowClose) // Just close without asking first, as user has disabled confirmations in settings
            {
                e.Cancel = true;
                ExceptionHandler.Log("Exiting without confirmation (DisableNotifications=true)");
                ExitApplication();
                return;
            }
            // allowClose == true is not handled and if ignoreClose == false, the application will exit
            ExceptionHandler.Log("Form closing");
            //ExceptionHandler.CreateAutoLogIfApplicable();
        }

        #endregion

        #region "form actions / state changes"

        /// <summary>
        /// Stops the countdown, displays an exit message and closes exits the application.
        /// Notice: This logic allows for a graceful exit and is therefore initially called by Countdown_FormClosing before redirecting there again through Application.Exit();
        /// </summary>
        private void ExitApplication()
        {
            ExceptionHandler.Log("Exit requested");

            if (lockState == 1)
            {
                ExceptionHandler.Log("Exit halted by password protection");
                if (UnlockUIByPassword(true)) { ExceptionHandler.Log("Password accepted; resuming exit"); ExitApplication(); }
            }
            else
            {
                ExceptionHandler.Log("Exiting application");

                ignoreClose = false;
                allowClose = true;
                ExceptionHandler.Log("Pausing timer");
                Timer.Pause();
                ExceptionHandler.Log("Sending cancellation notification");
                SendNotification(Loc.T("Countdown.StoppedNotify")); // Show windows toast notification as confirmation
                ExceptionHandler.Log("Saving settings");
                SaveSettings();
                ExceptionHandler.Log("Exit");
                Application.Exit();
            }
        }

        /// <summary>
        /// Restarts the application.
        /// confirmed=false 时先让用户确认，避免误点托盘菜单丢掉当前倒计时；
        /// 密码解锁后的递归调用传 confirmed=true，不会二次确认。
        /// </summary>
        private void RestartApplication(bool confirmed = false)
        {
            ExceptionHandler.Log("Restart requested");
            if (!confirmed)
            {
                DialogResult answer = MessageBox.Show(Loc.T("Countdown.ConfirmRestart"), Loc.T("Countdown.ConfirmRestartTitle"), MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (answer != DialogResult.Yes)
                {
                    ExceptionHandler.Log("Restart aborted by user at confirmation");
                    return;
                }
            }

            if (lockState == 1)
            {
                ExceptionHandler.Log("Restart halted by password protection");
                if (UnlockUIByPassword(true)) { ExceptionHandler.Log("Password accepted; resuming restart"); RestartApplication(true); }
            }
            else
            {
                ExceptionHandler.Log("Restarting application");

                ignoreClose = false;
                allowClose = true;
                ExceptionHandler.Log("Pausing timer");
                Timer.Pause();
                ExceptionHandler.Log("Clearing EXECUTION_STATE flags");
                ExecutionState.SetThreadExecutionState(ExecutionState.EXECUTION_STATE.ES_CONTINUOUS); // Clear EXECUTION_STATE flags to allow the system to go to sleep if it's tired
                ExceptionHandler.Log("Saving settings");
                SaveSettings();
                ExceptionHandler.Log("Restart");
                Application.Restart();
            }
        }

        /// <summary>
        /// Resets the timer to initial values.
        /// </summary>
        private void ResetTimer()
        {

            ExceptionHandler.Log("Reset requested");
            if (lockState == 1)
            {
                ExceptionHandler.Log("Reset halted by password protection");
                if (UnlockUIByPassword(true)) { ExceptionHandler.Log("Password accepted; resuming reset"); ResetTimer(); }
            }
            else
            {
                ExceptionHandler.Log("Resetting timer");
                Timer.Reset();
                ExceptionHandler.Log("Update UI");
                UpdateUI(Timer.CountdownTimeSpan);
                if (this.WindowState == FormWindowState.Minimized) { SendNotification(Loc.T("Countdown.ResetNotify", Timer.CountdownTimeSpan.Hours, Timer.CountdownTimeSpan.Minutes, Timer.CountdownTimeSpan.Seconds)); }
            }
        }

        /// <summary>
        /// Pauses/Resumes the countdown timer
        /// </summary>
        private void PauseResumeTimer()
        {
            if (Timer.IsRunning())
            {
                Timer.Pause();
                contextMenuStrip.Items[0].Text = Loc.T("Countdown.Menu.Resume");
                titleLabel.Text = Loc.T("Countdown.TitleFormatPaused", Timer.Action.DisplayName());
            }
            else
            {
                Timer.Resume();
                contextMenuStrip.Items[0].Text = Loc.T("Countdown.Menu.Pause");
                titleLabel.Text = Loc.T("Countdown.TitleFormat", Timer.Action.DisplayName());
            }
        }

        /// <summary>
        /// Shows a Dialog for setting a new countdown time
        /// </summary>
        private void ShowSetNewCountdownDialog()
        {
            ExceptionHandler.Log("Prompting for new countdown time");
            using (var form = new InputBox())
            {
                form.Title = Loc.T("Countdown.NewTimeTitle");
                form.Message = Loc.T("Countdown.NewTimePrompt");
                TopMost = false;
                form.ShowDialog();
                TopMost = !SettingsProvider.Settings.DisableAlwaysOnTop;
                if (form.DialogResult == DialogResult.None || form.DialogResult == DialogResult.Cancel)
                {
                    return;
                }
                else if (form.ReturnValue == "" || form.ReturnValue == null)
                {
                    ExceptionHandler.Log("Update aborted: no input");
                    MessageBox.Show(Loc.T("Countdown.Err.NoTime"),Loc.T("Countdown.UpdateTitle"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                else
                {
                    ExceptionHandler.Log("Parsing new time input");
                    try
                    {
                        String[] values = form.ReturnValue.Split(':');
                        if (values.Length == 2) // HH:mm
                        {
                            int newHours = Convert.ToInt32(values[0]);
                            int newMinutes = Convert.ToInt32(values[1]);
                            Timer.CountdownTimeSpan = new TimeSpan(newHours, newMinutes, 0);
                            Timer.Reset();

                            ExceptionHandler.Log("Countdown updated (HH:mm)");
                        }
                        else if (values.Length == 3) // HH:mm:ss
                        {
                            int newHours = Convert.ToInt32(values[0]);
                            int newMinutes = Convert.ToInt32(values[1]);
                            int newSeconds = Convert.ToInt32(values[2]);
                            Timer.CountdownTimeSpan = new TimeSpan(newHours, newMinutes, newSeconds);
                            Timer.Reset();

                            ExceptionHandler.Log("Countdown updated (HH:mm:ss)");
                        }
                        else
                        {
                            ExceptionHandler.Log("Update aborted: malformed input");
                            MessageBox.Show(Loc.T("Countdown.Err.InvalidTime"),Loc.T("Countdown.UpdateTitle"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                    catch (Exception)
                    {
                        ExceptionHandler.Log("Update aborted: input processing error");
                        MessageBox.Show(Loc.T("Countdown.Err.InternalError"),Loc.T("Countdown.UpdateTitle"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        /// <summary>
        /// Hides countdown window
        /// </summary>
        private void HideUI()
        {
            ExceptionHandler.Log("Hiding UI");

            timerUIHideMenuItem.Enabled = false;
            timerUIShowMenuItem.Enabled = true;
            TopMost = false;
            ShowInTaskbar = false;
            WindowState = FormWindowState.Minimized;
            IsForegroundUI = false;
            ignoreClose = true; // Prevent closing (and closing dialog) after ShowInTaskbar changed
            Hide();

            SendNotification(Loc.T("Countdown.MovedToBackground"));
        }

        /// <summary>
        /// Shows countdown window
        /// </summary>
        private void ShowUI()
        {
            ExceptionHandler.Log("Showing UI");

            Show();
            timerUIHideMenuItem.Enabled = true;
            timerUIShowMenuItem.Enabled = false;
            TopMost = !SettingsProvider.Settings.DisableAlwaysOnTop;
            ShowInTaskbar = true;
            WindowState = FormWindowState.Normal;
            IsForegroundUI = true;
            ignoreClose = true; // Prevent closing (and closing dialog) after ShowInTaskbar changed

            UpdateUI(Timer.GetTimeRemaining());

            // Re-Enable close question after the main thread has moved on and the close event raised from the this. ShowInTaskbar has been ignored
            new Thread(() =>
            {
                Thread.CurrentThread.IsBackground = true;
                Thread.Sleep(100); // Waiting to make sure the main thread has moved on and successfully
                ignoreClose = false; // Re-Enable close question
            }).Start();
        }

        /// <summary>
        /// Lock / Unlock the UI and update the picturebox
        /// </summary>
        private void ChangeLockState(string newLockState)
        {
            ExceptionHandler.Log("Switching to lockState: " + newLockState);

            switch (newLockState)
            {
                case "free":
                    lockState = 0;
                    lockStatePictureBox.Image = null;
                    lockStatePictureBox.Visible = false;
                    lockStatePictureBox.Enabled = false;
                    break;

                case "locked":
                    lockState = 1;
                    lockStatePictureBox.Image = Properties.Resources.fa_lock_white;
                    lockStatePictureBox.Visible = true;
                    lockStatePictureBox.Enabled = true;
                    break;

                case "unlocked":
                    lockState = 2;
                    lockStatePictureBox.Image = Properties.Resources.fa_unlock_white;
                    lockStatePictureBox.Visible = true;
                    lockStatePictureBox.Enabled = true;
                    break;
            }

            // 同步托盘菜单"锁定倒计时"项：只有还没设密码（free）时可用
            foreach (ToolStripItem item in contextMenuStrip.Items)
            {
                if (item.Name == "lockCountdownMenuItem") item.Enabled = lockState == 0;
            }
        }

        private bool UnlockUIByPassword(bool reasonBecauseOfAction = false)
        {
            ExceptionHandler.Log("Unlock has been requested");

            string message;
            if (reasonBecauseOfAction)
            {
                message = Loc.T("Countdown.PasswordPromptByAction");
            }
            else
            {
                message = Loc.T("Countdown.PasswordPromptPlain");
            }

            using (var form = new InputBox())
            {
                form.Title = Loc.T("Countdown.PasswordTitle");
                form.Message = message;
                form.PasswordMode = true;
                TopMost = false;
                var result = form.ShowDialog();
                TopMost = !SettingsProvider.Settings.DisableAlwaysOnTop;
                if (form.ReturnValue == Password)
                {
                    ExceptionHandler.Log("Successfully unlocked");
                    ChangeLockState("unlocked");
                    return true;
                }
                else
                {
                    ExceptionHandler.Log("Unlock has failed");
                    MessageBox.Show(Loc.T("Countdown.PasswordWrong"),Loc.T("Countdown.PasswordWrongTitle"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }
            }
        }

        /// <summary>
        /// Sends a toast notification (or tray menu balloon tip on legacy systems) to the user, unless it was disabled in settings
        /// </summary>
        /// <param name="message">Message content to be displayed in notification</param>
        private void SendNotification(string message)
        {
            if (!SettingsProvider.Settings.DisableNotifications)
            {
                ExceptionHandler.Log("Sending notification: " + message);
                notifyIcon.BalloonTipText = message;
                notifyIcon.ShowBalloonTip(5000);
            }
        }

        #region "窗口尺寸与鼠标穿透（本次改造新增）"

        // Designer 里的默认客户区尺寸，作为未保存过尺寸时的回退
        private const int DefaultWidth = 359;
        private const int DefaultHeight = 146;
        private const int MinWidth = 200;
        private const int MinHeight = 90;
        private const int MaxWidth = 1400;
        private const int MaxHeight = 900;

        /// <summary>
        /// 恢复用户上次调整过的窗口尺寸；没有记录时保持 Designer 默认值
        /// </summary>
        private void ApplyCountdownSize()
        {
            int width = SettingsProvider.Settings.CountdownWidth;
            int height = SettingsProvider.Settings.CountdownHeight;

            if (width <= 0 || height <= 0)
            {
                ExceptionHandler.Log("No saved countdown size; using designer default");
                return;
            }

            width = Math.Max(MinWidth, Math.Min(MaxWidth, width));
            height = Math.Max(MinHeight, Math.Min(MaxHeight, height));

            ClientSize = new Size(width, height);
            ExceptionHandler.Log($"Restored countdown size {width}x{height}");
        }

        /// <summary>
        /// Ctrl + 滚轮缩放窗口。鼠标穿透开启时窗口收不到鼠标消息，直接跳过。
        /// 尺寸即时写入设置，重启后保持。
        /// </summary>
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);

            if (SettingsProvider.Settings.ClickThrough) return;
            if ((ModifierKeys & Keys.Control) != Keys.Control) return;

            ResizeCountdown(e.Delta > 0 ? 1 : -1);
        }

        private void ResizeCountdown(int direction)
        {
            int width = Math.Max(MinWidth, Math.Min(MaxWidth, ClientSize.Width + direction * 24));
            int height = Math.Max(MinHeight, Math.Min(MaxHeight, ClientSize.Height + direction * 14));

            if (width == ClientSize.Width && height == ClientSize.Height) return;

            ClientSize = new Size(width, height);
            SettingsProvider.Settings.CountdownWidth = width;
            SettingsProvider.Settings.CountdownHeight = height;
            SettingsProvider.Save();

            ExceptionHandler.Log($"Countdown resized to {width}x{height}");
        }

        /// <summary>
        /// 重置为默认尺寸
        /// </summary>
        public void ResetCountdownSize()
        {
            ClientSize = new Size(DefaultWidth, DefaultHeight);
            SettingsProvider.Settings.CountdownWidth = 0;
            SettingsProvider.Settings.CountdownHeight = 0;
            SettingsProvider.Save();
            ExceptionHandler.Log("Countdown size reset to default");
        }

        /// <summary>
        /// 托盘菜单里的鼠标穿透开关。穿透后窗口点不动，必须靠这个菜单或设置页关闭，
        /// 所以菜单项常驻并显示当前状态。
        /// </summary>
        private void BuildClickThroughMenuItem()
        {
            var item = new ToolStripMenuItem(Loc.T("Countdown.Menu.ToggleClickThrough"))
            {
                Name = "toggleClickThroughMenuItem",
                Checked = SettingsProvider.Settings.ClickThrough
            };
            item.Click += ToggleClickThroughMenuItem_Click;
            contextMenuStrip.Items.Add(item);

            if (SettingsProvider.Settings.ClickThrough)
            {
                WindowsAPIs.SetClickThrough(Handle, true);
            }
        }

        /// <summary>
        /// 供设置页调用：按当前设置立即切换穿透，并同步托盘菜单的勾选状态
        /// </summary>
        public void ApplyClickThroughSetting()
        {
            bool enabled = SettingsProvider.Settings.ClickThrough;
            WindowsAPIs.SetClickThrough(Handle, enabled);

            foreach (ToolStripItem item in contextMenuStrip.Items)
            {
                if (item is ToolStripMenuItem menuItem && item.Name == "toggleClickThroughMenuItem") menuItem.Checked = enabled;
            }
        }

        private void ToggleClickThroughMenuItem_Click(object sender, EventArgs e)
        {
            bool enabled = !SettingsProvider.Settings.ClickThrough;
            SettingsProvider.Settings.ClickThrough = enabled;
            SettingsProvider.Save();

            WindowsAPIs.SetClickThrough(Handle, enabled);

            if (sender is ToolStripMenuItem item) item.Checked = enabled;

            if (enabled && !SettingsProvider.Settings.DisableNotifications)
            {
                SendNotification(Loc.T("Countdown.ClickThroughOn"));
            }

            ExceptionHandler.Log("User toggled click-through to " + enabled);
        }

        #endregion

        #region "运行时上锁（本次改造新增）"

        /// <summary>
        /// 托盘菜单里的"锁定倒计时"项：给正在运行的倒计时补设密码并立即上锁。
        /// 上游只能在启动倒计时前设密码，运行中无法反悔加锁，这里补上这个流程。
        /// 已有密码（locked/unlocked）时禁用，避免覆盖现有密码。
        /// </summary>
        private void BuildLockMenuItem()
        {
            var item = new ToolStripMenuItem(Loc.T("Countdown.Menu.Lock"))
            {
                Name = "lockCountdownMenuItem",
                Enabled = lockState == 0
            };
            item.Click += LockMenuItem_Click;
            contextMenuStrip.Items.Add(item);
        }

        private void LockMenuItem_Click(object sender, EventArgs e)
        {
            if (lockState != 0)
            {
                ExceptionHandler.Log("Lock requested but a password is already set; ignoring");
                return;
            }

            ExceptionHandler.Log("User requested runtime lock");
            using (var form = new InputBox())
            {
                form.Title = Loc.T("Countdown.PasswordTitle");
                form.Message = Loc.T("Countdown.PasswordPromptLock");
                form.PasswordMode = true;
                TopMost = false;
                var result = form.ShowDialog();
                TopMost = !SettingsProvider.Settings.DisableAlwaysOnTop;

                if (result != DialogResult.OK || string.IsNullOrEmpty(form.ReturnValue))
                {
                    ExceptionHandler.Log("Runtime lock aborted: no password entered");
                    return;
                }

                Password = form.ReturnValue;
                ChangeLockState("locked");
                SendNotification(Loc.T("Countdown.LockedNotify"));
                ExceptionHandler.Log("Countdown locked at runtime");
            }
        }

        /// <summary>
        /// 供 Timer 在倒计时到点时调用：先弹结束通知再关窗。
        /// 窗口一关 notifyIcon 就随窗体销毁，通知必须在 ExitExternal 之前发。
        /// </summary>
        public void NotifyCountdownFinished()
        {
            this.Invoke(new Action(() => SendNotification(Loc.T("Tray.Balloon.CountdownFinished"))));
        }

        #endregion

        #endregion

        #region "tray menu events"

        /// <summary>
        /// Pause/Resume timer option in the tray menu
        /// 
        /// </summary>
        private void TimerPauseMenuItem_Click(object sender, EventArgs e)
        {
            ExceptionHandler.Log("Trying to pause/resume countdown");

            if (lockState == 1)
            {
                ExceptionHandler.Log("Attempt halted due to password protection");
                if (UnlockUIByPassword(true)) { ExceptionHandler.Log("Password protection passed"); PauseResumeTimer(); }
            }
            else
            {
                PauseResumeTimer();
            }
        }

        /// <summary>
        /// Update time option in the tray menu
        /// </summary>
        private void UpdateTimeMenuItem_Click(object sender, EventArgs e)
        {
            ExceptionHandler.Log("Trying to set a new countdown");

            if (lockState == 1)
            {
                ExceptionHandler.Log("Attempt halted due to password protection");
                if (UnlockUIByPassword(true)) { ExceptionHandler.Log("Password protection passed"); ShowSetNewCountdownDialog(); }
            }
            else
            {
                ShowSetNewCountdownDialog();
            }
        }

        /// <summary>
        /// Stop timer option in the tray menu
        /// </summary>
        private void TimerStopMenuItem_Click(object sender, EventArgs e)
        {
            ExitApplication();
        }

        /// <summary>
        /// Restart timer option in the tray menu
        /// </summary>
        private void TimerResetMenuItem_Click(object sender, EventArgs e)
        {
            ResetTimer();
        }

        /// <summary>
        /// Restart application option in the tray menu
        /// </summary>
        private void AppRestartMenuItem_Click(object sender, EventArgs e)
        {
            RestartApplication();
        }

        /// <summary>
        /// Hide countdown window option in the tray menu
        /// </summary>
        private void TimerUIHideMenuItem_Click(object sender, EventArgs e)
        {
            ExceptionHandler.Log("Trying to hide the UI");

            if (lockState == 1)
            {
                ExceptionHandler.Log("Attempt halted due to password protection");
                if (UnlockUIByPassword(true)) { ExceptionHandler.Log("Password protection passed"); HideUI(); }
            }
            else
            {
                HideUI();
            }
        }

        /// <summary>
        /// Show countdown window option in the tray menu
        /// </summary>
        private void TimerUIShowMenuItem_Click(object sender, EventArgs e)
        {
            ExceptionHandler.Log("Trying to show the UI");

            if (lockState == 1)
            {
                ExceptionHandler.Log("Attempt halted due to password protection");
                if (UnlockUIByPassword(true)) { ExceptionHandler.Log("Password protection passed"); ShowUI(); }
            }
            else
            {
                ShowUI();
            }
        }

        #endregion

        #region "ui updates"

        /// <summary>
        /// Updates the time label with current time left and applies the corresponding background color.
        /// </summary>
        private void UpdateUI(TimeSpan ts)
        {
            if (logTimerCounter <= 0)
            {
                ExceptionHandler.Log("Heartbeat: The countdown reads " + Numerics.ConvertTimeSpanToString(ts) + " with memory usage at " + Format.BytesToString(Process.GetCurrentProcess().PrivateMemorySize64) + ". Next heartbeat in 100,000 ticks.");
                logTimerCounter = 100000;
            }
            else
            {
                logTimerCounter--;
            }

            if (Math.Truncate(lastStateUITimeSpan.TotalSeconds) != Math.Truncate(ts.TotalSeconds)) // only if a full second passed between updates
            {
                // Save current ts to last state memory
                lastStateUITimeSpan = ts;

                // display every started second for one second instead of switching directly to the next lower second. makes for smoother display of time.
                if (ts.Milliseconds > 0)
                {
                    ts = ts.Add(TimeSpan.FromSeconds(1));
                }

                // Update time labels
                string elapsedTime = Numerics.ConvertTimeSpanToString(ts);
                timeLabel.Text = elapsedTime;
                timeMenuItem.Text = elapsedTime;

                // 托盘图标悬浮提示实时显示剩余时间（本次改造新增）
                notifyIcon.Text = Loc.T("Tray.Tooltip.Format", elapsedTime);

                if (IsForegroundUI) // UI for countdown window
                {
                    this.Text = Loc.T("Countdown.Title");

                    // Decide which color/animation to use
                    if (!SettingsProvider.Settings.DisableAnimations)
                    {
                        if (ts.Days > 0 || ts.Hours > 0 || ts.Minutes >= 30) { BackColor = Color.ForestGreen; }
                        else if (ts.Minutes >= 10) { BackColor = Color.DarkOrange; }
                        else if (ts.Minutes >= 1) { BackColor = Color.OrangeRed; }
                        else
                        {
                            if (ts.Seconds % 2 == 0) { BackColor = Color.Red; }
                            else { BackColor = Color.Black; }
                        }
                    }
                }
                else // UI for tray menu
                {
                    this.Text = Loc.T("Countdown.Title") + " - " + elapsedTime;

                    // Decide which tray message to show          
                    if (ts.Days == 0 && ts.Hours == 2 && ts.Minutes == 0 && ts.Seconds == 00) { SendNotification(Loc.T("Countdown.Remaining.Hours2")); }
                    else if (ts.Days == 0 && ts.Hours == 1 && ts.Minutes == 0 && ts.Seconds == 00) { SendNotification(Loc.T("Countdown.Remaining.Hours1")); }
                    else if (ts.Days == 0 && ts.Hours == 0 && ts.Minutes == 30 && ts.Seconds == 00) { SendNotification(Loc.T("Countdown.Remaining.Minutes30")); }
                    else if (ts.Days == 0 && ts.Hours == 0 && ts.Minutes == 5 && ts.Seconds == 00) { SendNotification(Loc.T("Countdown.Remaining.Minutes5")); }
                    else if (ts.Days == 0 && ts.Hours == 0 && ts.Minutes == 0 && ts.Seconds == 30) { SendNotification(Loc.T("Countdown.Remaining.Seconds30")); }
                }
            }

            // Correct window states if unexpected changes occur
            if (!IsForegroundUI && WindowState != FormWindowState.Minimized)
            {
                ExceptionHandler.Log("Application was set for background operation but form was visible, switching to foreground operation");
                ShowUI();
            }
            else if (IsForegroundUI && WindowState == FormWindowState.Minimized)
            {
                ExceptionHandler.Log("Application was set for foreground operation but form was minimized, switching to background operation");
                HideUI();
            }
        }
        #endregion
    }
}
