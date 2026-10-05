using ShutdownTimer.Helpers;
using Xunit;

namespace ShutdownTimer.Tests
{
    /// <summary>
    /// 资源解析测试。这些用例依赖当前 UI 文化，因此整个程序集已禁用并行执行
    /// （见 AssemblyInfo.cs），否则用例之间会互相踩 culture。
    /// </summary>
    public class LocalizationTests
    {
        [Fact]
        public void Chinese_resources_resolve()
        {
            Loc.ApplyLanguage(Loc.LangChinese);

            Assert.Equal("关机", PowerAction.Shutdown.DisplayName());
            Assert.Equal("自定义命令", PowerAction.CustomCommand.DisplayName());
            Assert.Equal("定时关机", Loc.T("App.Title"));
            Assert.Equal("确定", Loc.T("Common.OK"));
        }

        [Fact]
        public void English_resources_resolve()
        {
            Loc.ApplyLanguage(Loc.LangEnglish);

            Assert.Equal("Shutdown", PowerAction.Shutdown.DisplayName());
            Assert.Equal("Custom Command", PowerAction.CustomCommand.DisplayName());
            Assert.Equal("Shutdown Timer", Loc.T("App.Title"));
        }

        [Fact]
        public void Title_template_fixes_chinese_word_order()
        {
            Loc.ApplyLanguage(Loc.LangChinese);

            // 上游是 Timer.Action + " Timer"，中文会变成 "关机 Timer"
            string title = Loc.T("Countdown.TitleFormat", PowerAction.Shutdown.DisplayName());
            Assert.Equal("关机倒计时", title);

            Loc.ApplyLanguage(Loc.LangEnglish);
            Assert.Equal("Shutdown Timer", Loc.T("Countdown.TitleFormat", PowerAction.Shutdown.DisplayName()));
        }

        [Fact]
        public void Missing_key_is_visible_instead_of_silent()
        {
            Loc.ApplyLanguage(Loc.LangChinese);

            // 缺 key 必须显示成 [key]，漏翻要在界面上直接暴露
            Assert.Equal("[NoSuch.Key]", Loc.T("NoSuch.Key"));
        }

        [Fact]
        public void Placeholders_survive_formatting()
        {
            Loc.ApplyLanguage(Loc.LangChinese);

            string text = Loc.T("Countdown.ResetNotify", 1, 2, 3);
            Assert.Contains("1", text);
            Assert.Contains("2", text);
            Assert.Contains("3", text);
            Assert.DoesNotContain("{0}", text);
        }

        [Fact]
        public void Language_names_stay_self_describing()
        {
            // 中文界面里 English 仍要显示 English，否则切错语言读不回来
            Loc.ApplyLanguage(Loc.LangChinese);
            Assert.Equal("English", Loc.T("Language.En"));
            Assert.Equal("简体中文", Loc.T("Language.ZhCn"));

            Loc.ApplyLanguage(Loc.LangEnglish);
            Assert.Equal("English", Loc.T("Language.En"));
        }

        [Fact]
        public void Normalize_maps_unknown_to_auto()
        {
            Assert.Equal(Loc.LangAuto, Loc.Normalize(""));
            Assert.Equal(Loc.LangAuto, Loc.Normalize(null));
            Assert.Equal(Loc.LangAuto, Loc.Normalize("随便"));
            Assert.Equal(Loc.LangChinese, Loc.Normalize("zh-CN"));
            Assert.Equal(Loc.LangEnglish, Loc.Normalize("en"));
        }

        [Fact]
        public void Wired_up_gap_keys_resolve_in_both_languages()
        {
            // S2 接线：这些 key 曾"资源已备好但代码未引用"，现在必须双语都能取到真文案
            string[] keys =
            {
                "Countdown.ConfirmRestart", "Countdown.ConfirmRestartTitle",
                "Countdown.PasswordPromptLock", "Countdown.PasswordWrongTitle",
                "Countdown.PasswordTitle", "Countdown.Menu.Lock", "Countdown.LockedNotify",
                "Settings.CountdownSize", "Tray.Balloon.CountdownFinished",
                "Tray.Tooltip.Format", "Countdown.Err.CustomCommandTitle",
            };

            foreach (var lang in new[] { Loc.LangEnglish, Loc.LangChinese })
            {
                Loc.ApplyLanguage(lang);
                foreach (var key in keys)
                {
                    string value = Loc.T(key);
                    Assert.NotEqual("[" + key + "]", value);
                    Assert.False(string.IsNullOrWhiteSpace(value));
                }
            }
        }

        [Fact]
        public void Tray_tooltip_format_fits_notifyicon_limit()
        {
            // NotifyIcon.Text 超过 127 字符会抛异常，格式化后的悬浮提示必须留足余量
            Loc.ApplyLanguage(Loc.LangChinese);
            string text = Loc.T("Tray.Tooltip.Format", "999:59:59");
            Assert.Contains("999:59:59", text);
            Assert.True(text.Length <= 127);

            Loc.ApplyLanguage(Loc.LangEnglish);
            text = Loc.T("Tray.Tooltip.Format", "999:59:59");
            Assert.Contains("999:59:59", text);
            Assert.True(text.Length <= 127);
        }

        [Fact]
        public void Tray_theme_values_are_stable_across_languages()
        {
            Loc.ApplyLanguage(Loc.LangChinese);

            // 显示中文，但存盘值必须是上游那三个英文值
            foreach (var option in LocalizedOptions.TrayThemes)
            {
                Assert.Contains(option.Value, new[] { "Automatic", "Light", "Dark" });
                Assert.NotEqual(option.Value, option.ToString());
            }

            Loc.ApplyLanguage(Loc.LangEnglish);
        }
    }
}
