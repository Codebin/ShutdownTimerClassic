using System;
using System.Drawing;
using System.Globalization;
using System.Resources;
using System.Threading;

namespace ShutdownTimer.Helpers
{
    /// <summary>
    /// 取词器：所有用户可见文本统一从这里读取，按当前 UI 语言解析卫星资源。
    /// 资源文件：Strings.resx（英文，中性语言 / 兜底）、Strings.zh-CN.resx（简体中文）。
    /// 内部日志、CLI 参数名、settings.json 的键值一律不走这里，保持英文稳定。
    /// </summary>
    public static class Loc
    {
        public const string LangAuto = "Auto";
        public const string LangEnglish = "en";
        public const string LangChinese = "zh-CN";

        private static ResourceManager _manager;

        private static ResourceManager Manager
        {
            get
            {
                if (_manager == null)
                {
                    _manager = new ResourceManager("ShutdownTimer.Strings", typeof(Loc).Assembly);
                }
                return _manager;
            }
        }

        /// <summary>当前生效的语言代码（en / zh-CN）。</summary>
        public static string ActiveLanguage { get; private set; } = LangEnglish;

        public static bool IsChinese => ActiveLanguage == LangChinese;

        /// <summary>
        /// 按当前语言创建界面字体。
        /// 上游把 "Microsoft Sans Serif" 硬编码在 Menu.cs / Countdown.cs 里，并注释说明
        /// "本应用只有英文，不需要显示非拉丁字符"——该字体没有中文字形，中文会渲染成方块。
        /// 中文界面因此换成微软雅黑：优先 "Microsoft YaHei UI"（Win 8.1+ 的界面优化版），
        /// 不存在则退回 "Microsoft YaHei"，再不行用系统默认字体。
        /// </summary>
        public static Font CreateUiFont(float sizeInPoints)
        {
            if (!IsChinese)
            {
                return new Font("Microsoft Sans Serif", sizeInPoints, FontStyle.Regular, GraphicsUnit.Point, 0);
            }

            foreach (var candidate in new[] { "Microsoft YaHei UI", "Microsoft YaHei", "Malgun Gothic", "Segoe UI" })
            {
                if (FontFamilyExists(candidate))
                {
                    return new Font(candidate, sizeInPoints, FontStyle.Regular, GraphicsUnit.Point, 0);
                }
            }

            // 交给系统默认字体，至少不会拿到一个没有中文字形的字体族
            ExceptionHandler.Log("No CJK-capable font found; falling back to the system default font");
            return new Font(SystemFonts.DefaultFont.FontFamily, sizeInPoints, FontStyle.Regular, GraphicsUnit.Point, 0);
        }

        private static bool FontFamilyExists(string name)
        {
            try
            {
                using (var family = new FontFamily(name))
                {
                    return !string.IsNullOrEmpty(family.Name);
                }
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        /// <summary>
        /// 取一条本地化文本。缺 key 时返回 "[key]"，方便在界面上直接看出漏翻，
        /// 而不是静默回退成英文导致问题被掩盖。
        /// </summary>
        public static string T(string key)
        {
            try
            {
                string value = Manager.GetString(key, CultureInfo.CurrentUICulture);
                if (string.IsNullOrEmpty(value))
                {
                    ExceptionHandler.Log("Missing localization key: " + key);
                    return "[" + key + "]";
                }
                return value;
            }
            catch (Exception ex)
            {
                ExceptionHandler.Log("Localization lookup failed for '" + key + "': " + ex.Message);
                return "[" + key + "]";
            }
        }

        /// <summary>带占位符的取词，内部用 string.Format。</summary>
        public static string T(string key, params object[] args)
        {
            try { return string.Format(CultureInfo.CurrentUICulture, T(key), args); }
            catch (FormatException) { return T(key); }
        }

        /// <summary>
        /// 根据设置项应用 UI 语言。Auto 时跟随系统显示语言（中文系统→简体中文，其余→英文）。
        /// 必须在任何窗体构造之前调用，否则 Designer 里的取词会用到错误的文化。
        /// </summary>
        public static void ApplyLanguage(string preference)
        {
            if (string.IsNullOrWhiteSpace(preference)) { preference = LangAuto; }

            string target;
            if (preference.Equals(LangAuto, StringComparison.OrdinalIgnoreCase))
            {
                // 用机器的用户界面语言判断，而不是当前区域性
                string systemUi = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
                target = systemUi.Equals("zh", StringComparison.OrdinalIgnoreCase) ? LangChinese : LangEnglish;
                ExceptionHandler.Log("Language set to Auto; resolved from system UI culture '" + systemUi + "' to " + target);
            }
            else if (preference.Equals(LangChinese, StringComparison.OrdinalIgnoreCase))
            {
                target = LangChinese;
            }
            else
            {
                target = LangEnglish;
            }

            var culture = new CultureInfo(target);
            CultureInfo.CurrentUICulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;
            ActiveLanguage = target;

            ExceptionHandler.Log("UI language applied: " + target);
        }

        /// <summary>把设置里的语言值规范化成可比较的形式。</summary>
        public static string Normalize(string preference)
        {
            if (string.IsNullOrWhiteSpace(preference)) { return LangAuto; }
            if (preference.Equals(LangChinese, StringComparison.OrdinalIgnoreCase)) { return LangChinese; }
            if (preference.Equals(LangEnglish, StringComparison.OrdinalIgnoreCase)) { return LangEnglish; }
            return LangAuto;
        }
    }
}
