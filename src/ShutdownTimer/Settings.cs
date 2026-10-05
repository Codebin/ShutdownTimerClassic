using ShutdownTimer.Helpers;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace ShutdownTimer
{
    public partial class Settings : Form
    {
        private ComboBox languageComboBox;   // 运行时创建，避免改动 Designer 生成代码
        private CheckBox clickThroughCheckBox;
        private Button resetSizeButton;
        private string loadedLanguage;       // 打开设置时的语言，用于判断是否需要重启提示

        public Settings()
        {
            InitializeComponent();

            // 与 Menu 窗体一致：动作下拉框绑定 PowerAction 选项，显示名走本地化资源
            PowerActions.BindTo(actionComboBox);

            // 托盘主题同样改为"稳定取值 + 本地化显示名"
            LocalizedOptions.BindTo(trayiconThemeComboBox, LocalizedOptions.TrayThemes);
        }

        /// <summary>
        /// 取下拉框选中的动作；未选中时回退到默认动作
        /// </summary>
        private PowerAction GetSelectedAction()
        {
            if (actionComboBox.SelectedItem is PowerActionOption option) return option.Value;
            return PowerActions.ParseOrDefault(actionComboBox.Text);
        }

        /// <summary>
        /// 动态插入"界面语言"分组。
        /// 语言选项必须在 LoadSettings 之前建好，才能把当前设置选中。
        /// 这里不用 Designer 加控件：那个文件是 VS 生成的，手改容易被重新生成覆盖。
        /// </summary>
        private void BuildLanguageControls()
        {
            if (languageComboBox != null) return;

            var group = new GroupBox
            {
                Text = Loc.T("Settings.LanguageGroup"),
                Location = new Point(6, 331),
                Size = new Size(284, 62),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Name = "languageGroupBox"
            };

            var label = new Label
            {
                Text = Loc.T("Settings.LanguageLabel"),
                Location = new Point(9, 22),
                AutoSize = true,
                Name = "languageLabel"
            };

            languageComboBox = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(112, 19),
                Size = new Size(160, 23),
                Name = "languageComboBox"
            };
            LocalizedOptions.BindTo(languageComboBox, LocalizedOptions.Languages);

            var hint = new Label
            {
                Text = Loc.T("Settings.LanguageRestartHint"),
                Location = new Point(9, 42),
                AutoSize = true,
                ForeColor = SystemColors.GrayText,
                Name = "languageHintLabel"
            };

            group.Controls.Add(label);
            group.Controls.Add(languageComboBox);
            group.Controls.Add(hint);
            tabPage1.Controls.Add(group);

            // 给新分组腾位置：下方控件与窗体整体下移
            const int shift = 68;
            trayiconGroupBox.Top += shift;
            clearSettingsButton.Top += shift;
            ClientSize = new Size(ClientSize.Width, ClientSize.Height + shift);
        }

        /// <summary>
        /// 在"倒计时窗口"分组里追加鼠标穿透开关与尺寸重置按钮。
        /// 同样用代码创建，不动 Designer 生成代码。
        /// </summary>
        private void BuildCountdownExtras()
        {
            if (clickThroughCheckBox != null) return;

            const int rowHeight = 23;
            int top = hideTrayIconCheckBox.Bottom + 3;
            int originalBottom = countdownGroupBox.Bottom;

            clickThroughCheckBox = new CheckBox
            {
                Name = "clickThroughCheckBox",
                Text = Loc.T("Settings.ClickThrough"),
                Location = new Point(6, top),
                AutoSize = true
            };

            var tip = new ToolTip();
            tip.SetToolTip(clickThroughCheckBox, Loc.T("Settings.ClickThroughHint"));

            resetSizeButton = new Button
            {
                Name = "resetSizeButton",
                Text = Loc.T("Settings.CountdownSizeReset"),
                Location = new Point(countdownGroupBox.Width - 96, top - 2),
                Size = new Size(90, 23),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            resetSizeButton.Click += ResetSizeButton_Click;

            // 重置按钮左侧的说明标签（Ctrl+滚轮缩放窗口）
            var sizeLabel = new Label
            {
                Name = "countdownSizeLabel",
                Text = Loc.T("Settings.CountdownSize"),
                Location = new Point(countdownGroupBox.Width - 180, top),
                Size = new Size(78, 23),
                TextAlign = ContentAlignment.MiddleRight,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };

            countdownGroupBox.Controls.Add(clickThroughCheckBox);
            countdownGroupBox.Controls.Add(sizeLabel);
            countdownGroupBox.Controls.Add(resetSizeButton);
            countdownGroupBox.Height += rowHeight;

            // 把该分组下方的同级控件整体下移，避免重叠
            foreach (Control sibling in tabPage2.Controls)
            {
                if (sibling != countdownGroupBox && sibling.Top >= originalBottom)
                {
                    sibling.Top += rowHeight;
                }
            }
        }

        private void ResetSizeButton_Click(object sender, EventArgs e)
        {
            // 倒计时窗口开着的话立即重置，否则只清掉保存的尺寸
            foreach (Form form in Application.OpenForms)
            {
                if (form is Countdown countdown) countdown.ResetCountdownSize();
            }

            SettingsProvider.Settings.CountdownWidth = 0;
            SettingsProvider.Settings.CountdownHeight = 0;
            ExceptionHandler.Log("User requested countdown size reset from settings");
        }

        private void Settings_Load(object sender, EventArgs e)
        {
            appLabel.Text = Application.ProductName + "@v" + Application.ProductVersion.Remove(Application.ProductVersion.LastIndexOf("."));
#if DEBUG
            appLabel.Text += "_debug";
#endif

            // 字体按界面语言选择（中文需要 CJK 字形）
            this.Font = Loc.CreateUiFont(8.25f);

            BuildLanguageControls();
            BuildCountdownExtras();
            LoadSettings();
        }

        private void Settings_FormClosing(object sender, FormClosingEventArgs e)
        {
            SaveSettings();
        }

        private void RememberStateCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (rememberStateCheckBox.Checked)
            {
                SettingsProvider.Settings.RememberLastState = true;
                customDefaultsGroupBox.Enabled = false;
            }
            else
            {
                SettingsProvider.Settings.RememberLastState = false;
                customDefaultsGroupBox.Enabled = true;
            }
        }

        private void ClearSettingsButton_Click(object sender, EventArgs e)
        {
            SettingsProvider.ClearSettings();
            LoadSettings();
        }

        private void LoadSettings()
        {
            // general controls
            rememberStateCheckBox.Checked = SettingsProvider.Settings.RememberLastState;
            LocalizedOptions.Select(trayiconThemeComboBox, LocalizedOptions.TrayThemes, SettingsProvider.Settings.TrayIconTheme);
            LocalizedOptions.Select(languageComboBox, LocalizedOptions.Languages, SettingsProvider.Settings.Language);
            loadedLanguage = SettingsProvider.Settings.Language;
            rememberLastScreenPositionUI.Checked = SettingsProvider.Settings.RememberLastScreenPositionUI;
            rememberLastScreenPositionCountdown.Checked = SettingsProvider.Settings.RememberLastScreenPositionCountdown;
            enableMultipleInstances.Checked = SettingsProvider.Settings.EnableMultipleInstances;

            // default timer
            PowerActions.Select(actionComboBox, PowerActions.ParseOrDefault(SettingsProvider.Settings.DefaultTimer.Action));
            gracefulCheckBox.Checked = SettingsProvider.Settings.DefaultTimer.Graceful;
            preventSleepCheckBox.Checked = SettingsProvider.Settings.DefaultTimer.PreventSleep;
            backgroundCheckBox.Checked = SettingsProvider.Settings.DefaultTimer.Background;
            countdownModeRadioButton.Checked = SettingsProvider.Settings.DefaultTimer.CountdownMode;
            timeOfDayModeRadioButton.Checked = !SettingsProvider.Settings.DefaultTimer.CountdownMode;
            hoursNumericUpDown.Value = SettingsProvider.Settings.DefaultTimer.Hours;
            minutesNumericUpDown.Value = SettingsProvider.Settings.DefaultTimer.Minutes;
            secondsNumericUpDown.Value = SettingsProvider.Settings.DefaultTimer.Seconds;

            // advanced settings
            forceIfHungFlagRadioButton.Checked = !SettingsProvider.Settings.ForceIfHungFlag;
            forceFlagRadioButton.Checked = SettingsProvider.Settings.ForceIfHungFlag;
            disableAlwaysOnTopCheckBox.Checked = SettingsProvider.Settings.DisableAlwaysOnTop;
            disableAnimationsCheckBox.Checked = SettingsProvider.Settings.DisableAnimations;
            setBackgroundColorLinkLabel.Enabled = disableAnimationsCheckBox.Checked;
            disableNotificationsCheckBox.Checked = SettingsProvider.Settings.DisableNotifications;
            passwordCheckBox.Checked = SettingsProvider.Settings.PasswordProtection;
            enableAdaptiveCountdownTextSizeCheckBox.Checked = SettingsProvider.Settings.AdaptiveCountdownTextSize;
            hideTrayIconCheckBox.Checked = SettingsProvider.Settings.HideTrayIcon;
            if (clickThroughCheckBox != null) clickThroughCheckBox.Checked = SettingsProvider.Settings.ClickThrough;
            if (SettingsProvider.Settings.BackgroundColor == Color.Transparent) { transparentWindowCheckBox.Checked = true; }
            saveLogsCheckBox.Checked = SettingsProvider.Settings.SaveEventLogOnExit;
        }

        private void SaveSettings()
        {
            // general controls
            SettingsProvider.Settings.RememberLastState = rememberStateCheckBox.Checked;
            SettingsProvider.Settings.TrayIconTheme = LocalizedOptions.SelectedValue(trayiconThemeComboBox, LocalizedOptions.TrayThemes);

            // 语言：写入选择值；与打开设置时不同则提示需要重启
            if (languageComboBox != null)
            {
                string chosen = LocalizedOptions.SelectedValue(languageComboBox, LocalizedOptions.Languages);
                SettingsProvider.Settings.Language = chosen;

                if (!string.Equals(chosen, loadedLanguage, StringComparison.OrdinalIgnoreCase)
                    && !SettingsProvider.Settings.DisableNotifications)
                {
                    MessageBox.Show(Loc.T("Settings.LanguageRestartHint"), Loc.T("Menu.WarnTitle"),
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            SettingsProvider.Settings.RememberLastScreenPositionUI = rememberLastScreenPositionUI.Checked;
            SettingsProvider.Settings.RememberLastScreenPositionCountdown = rememberLastScreenPositionCountdown.Checked;
            SettingsProvider.Settings.EnableMultipleInstances = enableMultipleInstances.Checked;

            // default timer
            if (!SettingsProvider.Settings.RememberLastState)
            {
                SettingsProvider.Settings.DefaultTimer.Action = GetSelectedAction().Key();
                SettingsProvider.Settings.DefaultTimer.Graceful = gracefulCheckBox.Checked;
                SettingsProvider.Settings.DefaultTimer.PreventSleep = preventSleepCheckBox.Checked;
                SettingsProvider.Settings.DefaultTimer.Background = backgroundCheckBox.Checked;
                SettingsProvider.Settings.DefaultTimer.CountdownMode = countdownModeRadioButton.Checked;
                SettingsProvider.Settings.DefaultTimer.Hours = Convert.ToInt32(hoursNumericUpDown.Value);
                SettingsProvider.Settings.DefaultTimer.Minutes = Convert.ToInt32(minutesNumericUpDown.Value);
                SettingsProvider.Settings.DefaultTimer.Seconds = Convert.ToInt32(secondsNumericUpDown.Value);
            }

            // advanced settings
            SettingsProvider.Settings.ForceIfHungFlag = forceFlagRadioButton.Checked;
            SettingsProvider.Settings.DisableAlwaysOnTop = disableAlwaysOnTopCheckBox.Checked;
            SettingsProvider.Settings.DisableAnimations = disableAnimationsCheckBox.Checked;
            SettingsProvider.Settings.DisableNotifications = disableNotificationsCheckBox.Checked;
            SettingsProvider.Settings.AdaptiveCountdownTextSize = enableAdaptiveCountdownTextSizeCheckBox.Checked;
            SettingsProvider.Settings.HideTrayIcon = hideTrayIconCheckBox.Checked;

            if (clickThroughCheckBox != null)
            {
                bool enabled = clickThroughCheckBox.Checked;
                if (enabled != SettingsProvider.Settings.ClickThrough)
                {
                    SettingsProvider.Settings.ClickThrough = enabled;

                    // 倒计时窗口开着就立刻生效，不用等下一次启动
                    foreach (Form form in Application.OpenForms)
                    {
                        if (form is Countdown countdown) countdown.ApplyClickThroughSetting();
                    }
                }
            }
            SettingsProvider.Settings.PasswordProtection = passwordCheckBox.Checked;
            SettingsProvider.Settings.SaveEventLogOnExit = saveLogsCheckBox.Checked;

            SettingsProvider.Save();
        }

        private void GithubLinkLabel_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            System.Diagnostics.Process.Start("https://github.com/lukaslangrock/ShutdownTimerClassic");
        }

        private void AppLicenseLinkLabel_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            System.Diagnostics.Process.Start("https://github.com/lukaslangrock/ShutdownTimerClassic/blob/master/LICENSE");
        }

        private void AppSourceLinkLabel_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            System.Diagnostics.Process.Start("https://github.com/lukaslangrock/ShutdownTimerClassic");
        }

        private void FALicenseLinkLabel_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            System.Diagnostics.Process.Start("https://fontawesome.com/license/free");
        }

        private void FASourceLinkLabel_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            System.Diagnostics.Process.Start("https://github.com/FortAwesome/Font-Awesome");
        }

        private void GithubButton_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start("https://github.com/lukaslangrock/ShutdownTimerClassic/issues");
        }

        private void EmailButton_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start("mailto:lukas.langrock@outlook.de");
        }

        private void ForceFlagDocsLinkLabel_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            System.Diagnostics.Process.Start("https://docs.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-exitwindowsex#parameters");
        }

        private void LogButton_Click(object sender, EventArgs e)
        {
            ExceptionHandler.CreateManualLog();
        }

        private void SetBackgroundColorLinkLabel_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            ColorDialog colorDialog = new ColorDialog
            {
                Color = SettingsProvider.Settings.BackgroundColor,
                FullOpen = true
            };
            DialogResult result = colorDialog.ShowDialog();
            if (result == DialogResult.OK)
            {
                SettingsProvider.Settings.BackgroundColor = colorDialog.Color;
            }
        }

        private void TransparentWindowCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (transparentWindowCheckBox.Checked)
            {
                SettingsProvider.Settings.BackgroundColor = Color.Transparent;
            }
            else
            {
                SettingsProvider.Settings.BackgroundColor = Color.Blue;
            }
        }

        private void DisableAnimationsCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            setBackgroundColorLinkLabel.Enabled = disableAnimationsCheckBox.Checked;
            transparentWindowCheckBox.Enabled = disableAnimationsCheckBox.Checked;
        }

        private void openAppDataLinkLabel_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            System.Diagnostics.Process.Start(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\Shutdown Timer Classic");
        }

        private void hideTrayIconCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (hideTrayIconCheckBox.Checked)
            {
                // hiding the trayicon prevents notifications
                disableNotificationsCheckBox.Enabled = false;

                // ensure user understands implications from this action
                if (hideTrayIconCheckBox.Focused)
                {
                    DialogResult result = MessageBox.Show(Loc.T("Settings.HideTrayIconWarn"),Loc.T("Menu.WarnTitle"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                    if (result != DialogResult.Yes)
                    {
                        hideTrayIconCheckBox.Checked = false;
                    }
                }
            } else
            {
                // hiding the trayicon prevents notifications
                disableNotificationsCheckBox.Enabled = true;
            }
        }
    }
}
