using System;
using System.Collections.Generic;

namespace ShutdownTimer.Helpers
{
    /// <summary>
    /// 通用"稳定取值 + 本地化显示名"下拉选项。
    /// 与 PowerActionOption 同一思路：存英文 Key，显示走资源。
    /// </summary>
    public sealed class LocalizedOption
    {
        public string Value { get; }
        public string DisplayKey { get; }

        public LocalizedOption(string value, string displayKey)
        {
            Value = value;
            DisplayKey = displayKey;
        }

        public override string ToString() => Loc.T(DisplayKey);

        public override bool Equals(object obj) => obj is LocalizedOption other && other.Value == Value;
        public override int GetHashCode() => Value?.GetHashCode() ?? 0;
    }

    public static class LocalizedOptions
    {
        // ---------- 界面语言 ----------
        // 语言名一律自描述：English 在中文界面里也显示 English，
        // 否则切错语言后用户看不懂选项、切不回来。
        public static readonly LocalizedOption[] Languages =
        {
            new LocalizedOption(Loc.LangAuto, "Language.Automatic"),
            new LocalizedOption(Loc.LangEnglish, "Language.En"),
            new LocalizedOption(Loc.LangChinese, "Language.ZhCn"),
        };

        // ---------- 托盘图标主题 ----------
        public static readonly LocalizedOption[] TrayThemes =
        {
            new LocalizedOption("Automatic", "TrayTheme.Automatic"),
            new LocalizedOption("Light", "TrayTheme.Light"),
            new LocalizedOption("Dark", "TrayTheme.Dark"),
        };

        public static void BindTo(System.Windows.Forms.ComboBox box, LocalizedOption[] options)
        {
            box.Items.Clear();
            foreach (var option in options) box.Items.Add(option);
        }

        public static void Select(System.Windows.Forms.ComboBox box, LocalizedOption[] options, string value)
        {
            if (string.IsNullOrWhiteSpace(value)) value = options[0].Value;

            foreach (var option in options)
            {
                if (string.Equals(option.Value, value, StringComparison.OrdinalIgnoreCase))
                {
                    box.SelectedItem = option;
                    return;
                }
            }

            // 兼容历史值：认不出来就退回第一项，避免下拉框空白
            ExceptionHandler.Log($"Unrecognized option '{value}' for {box.Name}; falling back to {options[0].Value}");
            box.SelectedItem = options[0];
        }

        public static string SelectedValue(System.Windows.Forms.ComboBox box, LocalizedOption[] options)
        {
            if (box.SelectedItem is LocalizedOption option) return option.Value;

            // 用户可能通过自动完成输入了显示名，这里反查回稳定取值
            foreach (var candidate in options)
            {
                if (string.Equals(candidate.ToString(), box.Text, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(candidate.Value, box.Text, StringComparison.OrdinalIgnoreCase))
                {
                    return candidate.Value;
                }
            }

            return options[0].Value;
        }
    }
}
