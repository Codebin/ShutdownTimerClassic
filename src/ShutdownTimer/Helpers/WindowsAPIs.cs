using Microsoft.Win32;
using System;
using System.Runtime.InteropServices;

namespace ShutdownTimer.Helpers
{
    public static class WindowsAPIs
    {
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_LAYERED = 0x00080000;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        /// <summary>
        /// 开关鼠标穿透。开启后点击会穿过窗口落到下层内容，
        /// 窗口本身不再接收鼠标输入（因此只能靠托盘菜单关闭）。
        /// WS_EX_TRANSPARENT 需要配合 WS_EX_LAYERED 才生效。
        /// </summary>
        public static void SetClickThrough(IntPtr hwnd, bool enabled)
        {
            if (hwnd == IntPtr.Zero)
            {
                ExceptionHandler.Log("SetClickThrough skipped: window handle is zero");
                return;
            }

            try
            {
                int style = GetWindowLong(hwnd, GWL_EXSTYLE);
                int updated = enabled
                    ? style | WS_EX_LAYERED | WS_EX_TRANSPARENT
                    : style & ~WS_EX_TRANSPARENT;

                SetWindowLong(hwnd, GWL_EXSTYLE, updated);
                ExceptionHandler.Log("Click-through " + (enabled ? "enabled" : "disabled"));
            }
            catch (Exception ex)
            {
                ExceptionHandler.Log("Failed to toggle click-through: " + ex.Message);
            }
        }
        /// <summary>
        /// Checks if the system currently uses the light theme or not
        /// </summary>
        public static bool SystemUsesLightTheme()
        {
            ExceptionHandler.Log("Getting windows theme");

            bool lighttheme = false; // default if all checks fail (may happen when not on Windows 10)

            try // Get actual default Windows theme which (the same as the taskbar)
            {
                int key = (int)Registry.GetValue("HKEY_CURRENT_USER\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize", "SystemUsesLightTheme", null);
                if (key == 0) { lighttheme = false; }
                else if (key == 1) { lighttheme = true; }
            }
            catch (Exception) { ExceptionHandler.Log("Failed to read registry theme value"); }

            return lighttheme;
        }
    }
}
