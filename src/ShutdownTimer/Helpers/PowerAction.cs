using System;
using System.Collections.Generic;

namespace ShutdownTimer.Helpers
{
    /// <summary>
    /// 电源动作的稳定内部标识。这个枚举是唯一的真相来源：
    /// settings.json、CLI 参数、日志、执行分发全部使用它的 Key（英文），
    /// 只有界面显示才走 DisplayName（本地化）。
    /// 这样汉化界面不会破坏配置文件兼容性和命令行脚本。
    /// </summary>
    public enum PowerAction
    {
        Shutdown,
        Restart,
        Hibernate,
        Sleep,
        Logout,
        Lock,
        CustomCommand
    }

    /// <summary>
    /// ComboBox 的绑定项：Value 是稳定枚举，ToString() 给出本地化显示名。
    /// 用对象绑定而不是裸字符串，界面语言怎么切都不影响下游逻辑。
    /// </summary>
    public sealed class PowerActionOption
    {
        public PowerAction Value { get; }

        public PowerActionOption(PowerAction value)
        {
            Value = value;
        }

        public override string ToString() => Value.DisplayName();

        // 让 ComboBox.Items.Contains(option) 之类比较按语义生效
        public override bool Equals(object obj) => obj is PowerActionOption other && other.Value == Value;
        public override int GetHashCode() => (int)Value;
    }

    public static class PowerActions
    {
        /// <summary>下拉框里的展示顺序，与上游原版保持一致。</summary>
        public static readonly PowerAction[] All =
        {
            PowerAction.Shutdown,
            PowerAction.Restart,
            PowerAction.Hibernate,
            PowerAction.Sleep,
            PowerAction.Logout,
            PowerAction.Lock,
            PowerAction.CustomCommand
        };

        public static List<PowerActionOption> Options()
        {
            var list = new List<PowerActionOption>(All.Length);
            foreach (var a in All) { list.Add(new PowerActionOption(a)); }
            return list;
        }

        /// <summary>
        /// 把本地化后的动作选项填充进 ComboBox
        /// </summary>
        public static void BindTo(System.Windows.Forms.ComboBox box)
        {
            box.Items.Clear();
            foreach (var option in Options()) box.Items.Add(option);
        }

        /// <summary>
        /// 在已绑定的 ComboBox 中选中指定动作
        /// </summary>
        public static void Select(System.Windows.Forms.ComboBox box, PowerAction action)
        {
            for (int i = 0; i < box.Items.Count; i++)
            {
                if (box.Items[i] is PowerActionOption option && option.Value == action)
                {
                    box.SelectedIndex = i;
                    return;
                }
            }

            ExceptionHandler.Log($"Action option not found in ComboBox: {action.Key()}");
        }

        /// <summary>
        /// 稳定英文标识：写入 settings.json、匹配 CLI 参数、打进日志。
        /// 注意 CustomCommand 序列化成 "Custom Command"，与上游旧配置和 CLI 文档保持兼容。
        /// </summary>
        public static string Key(this PowerAction action)
        {
            switch (action)
            {
                case PowerAction.Shutdown: return "Shutdown";
                case PowerAction.Restart: return "Restart";
                case PowerAction.Hibernate: return "Hibernate";
                case PowerAction.Sleep: return "Sleep";
                case PowerAction.Logout: return "Logout";
                case PowerAction.Lock: return "Lock";
                case PowerAction.CustomCommand: return "Custom Command";
                default: return "Shutdown";
            }
        }

        /// <summary>界面显示名，跟随当前 UI 语言。</summary>
        public static string DisplayName(this PowerAction action)
        {
            switch (action)
            {
                case PowerAction.Shutdown: return Loc.T("Action.Shutdown");
                case PowerAction.Restart: return Loc.T("Action.Restart");
                case PowerAction.Hibernate: return Loc.T("Action.Hibernate");
                case PowerAction.Sleep: return Loc.T("Action.Sleep");
                case PowerAction.Logout: return Loc.T("Action.Logout");
                case PowerAction.Lock: return Loc.T("Action.Lock");
                case PowerAction.CustomCommand: return Loc.T("Action.CustomCommand");
                default: return Loc.T("Action.Shutdown");
            }
        }

        /// <summary>
        /// 只有这几个动作会强制关闭应用，因此只有它们能用优雅模式。
        /// 取代原先散在 Menu.cs 里的三段字符串比较。
        /// </summary>
        public static bool SupportsGraceful(this PowerAction action)
        {
            return action == PowerAction.Shutdown
                || action == PowerAction.Restart
                || action == PowerAction.Logout;
        }

        /// <summary>
        /// 宽容解析：接受稳定 Key（大小写不敏感）、任意语言的当前显示名，
        /// 以及历史遗留写法（Reboot / Logoff / Sign out）。
        /// 保证老用户的 settings.json 和 CLI 脚本在汉化后依然可用。
        /// </summary>
        public static bool TryParse(string raw, out PowerAction action)
        {
            action = PowerAction.Shutdown;
            if (string.IsNullOrWhiteSpace(raw)) { return false; }

            string value = raw.Trim();

            // 1) 先按稳定 Key 精确匹配（大小写不敏感），这是最可靠的路径
            foreach (var candidate in All)
            {
                if (string.Equals(candidate.Key(), value, StringComparison.OrdinalIgnoreCase))
                {
                    action = candidate;
                    return true;
                }
            }

            // 2) 再按所有已知语言的显示名匹配，兼容用户把中文界面值传进 CLI 的情况
            foreach (var candidate in All)
            {
                if (string.Equals(candidate.DisplayName(), value, StringComparison.OrdinalIgnoreCase))
                {
                    action = candidate;
                    return true;
                }
            }

            // 3) 历史别名兜底
            switch (value.ToLowerInvariant())
            {
                case "reboot": action = PowerAction.Restart; return true;
                case "logoff":
                case "sign out":
                case "signout": action = PowerAction.Logout; return true;
                case "customcommand":
                case "custom": action = PowerAction.CustomCommand; return true;
            }

            return false;
        }

        /// <summary>解析并保证有默认值，供 CLI 等不能失败的路径使用。</summary>
        public static PowerAction ParseOrDefault(string raw, PowerAction fallback = PowerAction.Shutdown)
        {
            return TryParse(raw, out var result) ? result : fallback;
        }
    }
}
