using ShutdownTimer.Helpers;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace ShutdownTimer
{
    public partial class Menu : Form
    {
        public bool ApplyArgumentValues { get; set; }
        public int ArgTimeH { get; set; }
        public int ArgTimeM { get; set; }
        public int ArgTimeS { get; set; }
        public string ArgAction { get; set; }
        public string ArgMode { get; set; }
        public string ArgPassword { get; set; }
        public bool ArgGraceful { get; set; }
        public bool ArgPreventSleep { get; set; }
        public bool ArgBackground { get; set; }
        public bool ArgUseTimeOfDay { get; set; }

        private string password; // used for password protection
        private string command; // used for custom command
        private bool isConcluded = false; // true after form has concluded its function

        public Menu()
        {
            InitializeComponent();

            // 动作下拉框改为对象绑定：界面显示本地化名，内部始终是 PowerAction 枚举
            PowerActions.BindTo(actionComboBox);
            PowerActions.Select(actionComboBox, PowerAction.Shutdown);
        }

        #region "form events"

        private void Menu_Load(object sender, EventArgs e)
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

            ExceptionHandler.Log("Initializing form");

            versionLabel.Text = "v" + Application.ProductVersion.Remove(Application.ProductVersion.LastIndexOf(".")); // Display current version
#if DEBUG
            versionLabel.Text += "_debug";
#endif

            infoToolTip.SetToolTip(gracefulCheckBox,Loc.T("Menu.Tip.Graceful"));
            infoToolTip.SetToolTip(preventSleepCheckBox,Loc.T("Menu.Tip.PreventSleep"));
            infoToolTip.SetToolTip(backgroundCheckBox,Loc.T("Menu.Tip.RunInBackground"));
            infoToolTip.SetToolTip(countdownModeRadioButton,Loc.T("Menu.Tip.CountdownMode"));
            infoToolTip.SetToolTip(timeOfDayModeRadioButton,Loc.T("Menu.Tip.TimeOfDayMode"));

            // 字体按界面语言选择：中文必须有 CJK 字形，写死 Microsoft Sans Serif 会让中文渲染成方块
            this.Font = Loc.CreateUiFont(8.25f);

            ExceptionHandler.Log("Form initialized");
        }

        private void Menu_Shown(object sender, EventArgs e)
        {
            ExceptionHandler.Log("Form shown");
            // Check for startup arguments
            if (ApplyArgumentValues)
            {
                // Apply given setting
                ExceptionHandler.Log("Applying CLI arguments");
                LoadArgs();
            }
            else
            {
                // Load settings
                ExceptionHandler.Log("Loading settings");
                Application.DoEvents();
                LoadSettings();
            }
        }

        private void Menu_FormClosing(object sender, FormClosingEventArgs e)
        {
            ExceptionHandler.Log("Form closing");

            // Only save settings when form hasn't concluded it's function.
            if (!isConcluded)
            {
                SaveSettings();
            }
        }

        /// <summary>
        /// 读取下拉框当前选中的动作。既支持选中项（对象绑定），也支持用户手输文本，
        /// 且中英文显示名与英文 Key 都能识别。
        /// </summary>
        private bool TryGetSelectedAction(out PowerAction action)
        {
            if (actionComboBox.SelectedItem is PowerActionOption option)
            {
                action = option.Value;
                return true;
            }

            return PowerActions.TryParse(actionComboBox.Text, out action);
        }

        private void ActionComboBox_TextChanged(object sender, EventArgs e)
        {
            // 只有会强制关闭应用的动作才支持优雅模式；按枚举判断，与界面语言无关
            gracefulCheckBox.Enabled = TryGetSelectedAction(out var action) && action.SupportsGraceful();
        }

        private void SettingsButton_Click(object sender, EventArgs e)
        {
            ExceptionHandler.Log("Showing settings form");
            Settings settings = new Settings();
            settings.ShowDialog();
        }

        private void StartButton_Click(object sender, EventArgs e)
        {
            ExceptionHandler.Log("Start requested");

            (bool allChecksPassed, string listOfErrorsFound, string listOfWarningsFound) = RunChecks();

            if (!allChecksPassed)
            {
                ExceptionHandler.Log("Start aborted: failing checks");
                MessageBox.Show(Loc.T("Menu.Err.StartFailed", listOfErrorsFound), Loc.T("Menu.Err.StartFailedTitle"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (!listOfWarningsFound.Equals(""))
            {
                if (MessageBox.Show(listOfWarningsFound,Loc.T("Menu.WarnTitle"), MessageBoxButtons.OK, MessageBoxIcon.Exclamation) != DialogResult.OK)
                {
                    ExceptionHandler.Log("User cancelled due to warnings");
                    return;
                }
            }

            if (TryGetSelectedAction(out var selectedAction) && selectedAction == PowerAction.CustomCommand)
            {
                ExceptionHandler.Log("Custom command requested");
                using (var form = new InputBox())
                {
                    form.Title = Loc.T("Menu.CustomCommandTitle");
                    form.Message = Loc.T("Menu.CustomCommandPrompt");

                    ExceptionHandler.Log("Prompting for custom command");
                    var result = form.ShowDialog();

                    if (result != DialogResult.OK)
                    {
                        ExceptionHandler.Log("User cancelled custom command input; aborting");
                        return;
                    }

                    if (String.IsNullOrWhiteSpace(form.ReturnValue))
                    {
                        ExceptionHandler.Log("Invalid custom command input; aborting");
                        MessageBox.Show(Loc.T("Menu.Err.EmptyCommand"),Loc.T("Menu.Err.EmptyCommandTitle"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    ExceptionHandler.Log("Accepted command: \"" + form.ReturnValue + "\".");
                    command = form.ReturnValue;
                }
            }

            if (!String.IsNullOrEmpty(ArgPassword))
            {
                password = ArgPassword;
            }
            else if (SettingsProvider.Settings.PasswordProtection)
            {
                ExceptionHandler.Log("Password protection enabled");
                using (var form = new InputBox())
                {
                    form.Title = Loc.T("Menu.PasswordTitle");
                    form.Message = Loc.T("Menu.PasswordPrompt");
                    form.PasswordMode = true;
                    ExceptionHandler.Log("Prompting for password");
                    var result = form.ShowDialog();
                    ExceptionHandler.Log("Password set");
                    password = form.ReturnValue;
                }
            }

            StartCountdown();
        }

        #endregion

        /// <summary>
        /// Checks user input before further processing
        /// </summary>
        /// <returns>Report of all failed checks</returns>
        private (bool allChecksPassed, string listOfErrorsFound, string ListOfWarningsFound) RunChecks()
        {
            ExceptionHandler.Log("Running checks...");

            string errMessages = ""; // error messages will append to this
            string warnMessages = ""; // warning messages will append to this

            // Check if chosen action is a valid option
            if (!TryGetSelectedAction(out _))
            {
                errMessages += Loc.T("Menu.Err.InvalidAction");
            }

            // Check if all time values are zero when in countdown mode
            if (hoursNumericUpDown.Value == 0 && minutesNumericUpDown.Value == 0 && secondsNumericUpDown.Value == 0 && countdownModeRadioButton.Checked)
            {
                errMessages += Loc.T("Menu.Err.ZeroTime");
            }

            // Respective check for either countdown or timeOfDay mode
            try
            {
                TimeSpan ts = Numerics.CalculateCountdownTimeSpan(hoursNumericUpDown.Value, minutesNumericUpDown.Value, secondsNumericUpDown.Value, timeOfDayModeRadioButton.Checked);

                // Sanity check
                if (ts.TotalDays > 100)
                {
                    warnMessages += Loc.T("Menu.Warn.HugeTimespan",
                        Math.Round(ts.TotalDays), Math.Round(ts.TotalDays / 365, 2));
                }
            }
            catch
            {
                errMessages += Loc.T("Menu.Err.TimeSpanConversion");
            }

            if (errMessages.Equals(""))
            {
                ExceptionHandler.Log("Ran all checks, no errors found.");
                return (true, errMessages, warnMessages);
            }
            else
            {
                ExceptionHandler.Log("Ran all checks, the following errors have been found:\n" + errMessages);
                return (false, errMessages, warnMessages);
            }
        }

        /// <summary>
        /// Load UI element data from args
        /// </summary>
        private void LoadArgs()
        {
            PowerActions.Select(actionComboBox, PowerActions.ParseOrDefault(ArgAction));
            gracefulCheckBox.Checked = ArgGraceful;
            preventSleepCheckBox.Checked = ArgPreventSleep;
            backgroundCheckBox.Checked = ArgBackground;
            hoursNumericUpDown.Value = ArgTimeH;
            minutesNumericUpDown.Value = ArgTimeM;
            secondsNumericUpDown.Value = ArgTimeS;
            countdownModeRadioButton.Checked = !ArgUseTimeOfDay;
            timeOfDayModeRadioButton.Checked = ArgUseTimeOfDay;

            if (ArgMode.Equals("Lock"))
            {
                ExceptionHandler.Log("Setting 'Lock' mode");
                startButton.Text = Loc.T("Menu.StartRecommended");
                settingsButton.Enabled = false;
                actionGroupBox.Enabled = false;
                timeGroupBox.Enabled = false;
            }
        }

        /// <summary>
        /// Load UI element data from settings
        /// </summary>
        private void LoadSettings()
        {
            PowerActions.Select(actionComboBox, PowerActions.ParseOrDefault(SettingsProvider.Settings.DefaultTimer.Action));
            gracefulCheckBox.Checked = SettingsProvider.Settings.DefaultTimer.Graceful;
            preventSleepCheckBox.Checked = SettingsProvider.Settings.DefaultTimer.PreventSleep;
            backgroundCheckBox.Checked = SettingsProvider.Settings.DefaultTimer.Background;
            countdownModeRadioButton.Checked = SettingsProvider.Settings.DefaultTimer.CountdownMode;
            timeOfDayModeRadioButton.Checked = !SettingsProvider.Settings.DefaultTimer.CountdownMode;
            hoursNumericUpDown.Value = SettingsProvider.Settings.DefaultTimer.Hours;
            minutesNumericUpDown.Value = SettingsProvider.Settings.DefaultTimer.Minutes;
            secondsNumericUpDown.Value = SettingsProvider.Settings.DefaultTimer.Seconds;

            // Check if the user has opted to remember the last UI screen position,
            // and if LastScreenPosition is available (not null), apply its X and Y coordinates to position the form.
            if (SettingsProvider.Settings.RememberLastScreenPositionUI && SettingsProvider.Settings.LastScreenPositionUI != null)
            {
                this.Location = new Point(SettingsProvider.Settings.LastScreenPositionUI.X, SettingsProvider.Settings.LastScreenPositionUI.Y);
            }
        }

        /// <summary>
        /// Saves current timer settings as default settings if activated in settings
        /// and the last UI position
        /// </summary>
        private void SaveSettings()
        {
            if (SettingsProvider.SettingsLoaded)
            {
                ExceptionHandler.Log("Saving settings...");

                if (SettingsProvider.Settings.RememberLastState)
                {
                    // 存稳定 Key，不存显示名：换语言或系统语言不同都不会让旧配置失效
                    SettingsProvider.Settings.DefaultTimer.Action =
                        (TryGetSelectedAction(out var chosenAction) ? chosenAction : PowerAction.Shutdown).Key();
                    SettingsProvider.Settings.DefaultTimer.Graceful = gracefulCheckBox.Checked;
                    SettingsProvider.Settings.DefaultTimer.PreventSleep = preventSleepCheckBox.Checked;
                    SettingsProvider.Settings.DefaultTimer.Background = backgroundCheckBox.Checked;
                    SettingsProvider.Settings.DefaultTimer.CountdownMode = countdownModeRadioButton.Checked;
                    SettingsProvider.Settings.DefaultTimer.Hours = Convert.ToInt32(hoursNumericUpDown.Value);
                    SettingsProvider.Settings.DefaultTimer.Minutes = Convert.ToInt32(minutesNumericUpDown.Value);
                    SettingsProvider.Settings.DefaultTimer.Seconds = Convert.ToInt32(secondsNumericUpDown.Value);
                }

                if (SettingsProvider.Settings.RememberLastScreenPositionUI)
                {
                    SettingsProvider.Settings.LastScreenPositionUI = new LastScreenPosition
                    {
                        X = this.Location.X,
                        Y = this.Location.Y
                    };
                }

                SettingsProvider.Save();
                ExceptionHandler.Log("Settings saved");
            }
            else
            {
                ExceptionHandler.Log("Ignoring SaveSettings() call as no settings are loaded");
            }
        }

        /// <summary>
        /// Starts the countdown with values from UI
        /// </summary>
        private void StartCountdown()
        {
            ExceptionHandler.Log("Preparing countdown");
            startButton.Enabled = false;
            actionGroupBox.Enabled = false;
            timeGroupBox.Enabled = false;
            isConcluded = true; // mark form as concluded to prevent further logic on form closing events
            SaveSettings();

            ExceptionHandler.Log("Countdown initiated");
            this.Hide();

            ExceptionHandler.Log("Starting countdown...");

            // Calculate TimeSpan
            ExceptionHandler.Log("Calculating timespan");
            TimeSpan timeSpan = Numerics.CalculateCountdownTimeSpan(hoursNumericUpDown.Value, minutesNumericUpDown.Value, secondsNumericUpDown.Value, timeOfDayModeRadioButton.Checked);

            Timer.CountdownTimeSpan = timeSpan;
            Timer.Action = TryGetSelectedAction(out var timerAction) ? timerAction : PowerAction.Shutdown;
            Timer.Graceful = gracefulCheckBox.Checked;
            Timer.PreventSystemSleep = preventSleepCheckBox.Checked;
            Timer.Command = command;

            // Show countdown window
            ExceptionHandler.Log("Starting timer");
            Timer.Start(password, !backgroundCheckBox.Checked);
        }
    }
}
