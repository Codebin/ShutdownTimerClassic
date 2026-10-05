using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using ShutdownTimer;
using ShutdownTimer.Helpers;

// Renders the app's own WinForms UI to PNG at the Simplified-Chinese locale.
//
// Two capture paths are used:
//  * Forms (Menu, Settings) are shown off-screen with Opacity=0 so their full render
//    pipeline runs (DrawToBitmap alone leaves GroupBox child text unpainted), then
//    painted into a bitmap. They are never closed, so no FormClosing/SaveSettings runs.
//  * The Countdown form is captured with CreateControl only — Show() would fire
//    Countdown_Load, which starts the real timer and could execute a power action.
// No power action is ever triggered and no settings file is written (TemporaryMode).
static class ScreenshotProgram
{
    // Exact background colors sampled from the existing English screenshots, so the
    // Chinese countdown shots match their color scheme 1:1.
    static readonly Color Green = Color.FromArgb(34, 139, 34);   // ForestGreen  (> 30 min)
    static readonly Color Yellow = Color.FromArgb(255, 140, 0);  // DarkOrange   (30–10 min)
    static readonly Color Orange = Color.FromArgb(255, 69, 0);   // OrangeRed    (10–1 min)
    static readonly Color Red = Color.FromArgb(255, 0, 0);       // Red          (< 1 min, even sec)
    static readonly Color Black = Color.FromArgb(0, 0, 0);       // Black        (< 1 min, odd sec)

    [STAThread]
    static void Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        // Render at the 96-DPI baseline so output dimensions track the Designer layout
        // and AutoScale doesn't overflow Countdown's anchored controls on a high-DPI host.
        Application.SetHighDpiMode(HighDpiMode.DpiUnaware);

        Loc.ApplyLanguage("zh-CN");
        SettingsProvider.TemporaryMode = true; // no settings.json reads/writes
        SettingsProvider.Load();
        // Skip the single-instance guard in Menu_Load/Countdown_Load so Show() can never
        // raise a "already running" MessageBox.
        SettingsProvider.Settings.EnableMultipleInstances = true;

        string outDir = args.Length > 0 ? args[0] : ".";
        Directory.CreateDirectory(outDir);

        RenderMenu(Path.Combine(outDir, "Menu.png"));
        RenderMenuExpanded(Path.Combine(outDir, "Menu2.png"));
        RenderSettings(Path.Combine(outDir, "Settings.png"));
        RenderCountdown(Path.Combine(outDir, "CountdownGreen.png"), Green, "01:00:00");
        RenderCountdown(Path.Combine(outDir, "CountdownYellow.png"), Yellow, "00:20:00");
        RenderCountdown(Path.Combine(outDir, "CountdownOrange.png"), Orange, "00:05:00");
        RenderCountdown(Path.Combine(outDir, "CountdownRed.png"), Red, "00:00:30");
        RenderCountdown(Path.Combine(outDir, "CountdownBlack.png"), Black, "00:00:29");
        RenderTrayMenu(Path.Combine(outDir, "TrayMenu.png"));
        RenderRightClickMenu(Path.Combine(outDir, "RightClickMenu.png"));

        Console.WriteLine("done: " + outDir);
    }

    // ---- Menu (main window, default state) ----
    static void RenderMenu(string path)
    {
        using (var menu = new Menu())
        {
            SetChecked(menu, "countdownModeRadioButton", true);
            Save(menu, path, show: true);
        }
    }

    // ---- Menu with the action combo dropped down (DrawToBitmap can't paint the popup,
    //      so we overlay the list ourselves below the combo) ----
    static void RenderMenuExpanded(string path)
    {
        using (var menu = new Menu())
        {
            SetChecked(menu, "countdownModeRadioButton", true);
            var bmp = CaptureForm(menu, show: true);

            var combo = (ComboBox)GetField(menu, "actionComboBox");
            var group = (GroupBox)GetField(menu, "actionGroupBox");
            int x = combo.Left + group.Left;
            int y = combo.Top + group.Top + combo.Height;
            int w = combo.Width;
            int rowH = combo.Height;

            var items = new List<string>();
            foreach (var a in PowerActions.All) items.Add(a.DisplayName());

            using (var g = Graphics.FromImage(bmp))
            {
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                var drop = new Rectangle(x, y, w, items.Count * rowH);
                g.FillRectangle(Brushes.White, drop);
                g.DrawRectangle(Pens.DarkGray, drop);
                using (var font = Loc.CreateUiFont(9f))
                {
                    for (int i = 0; i < items.Count; i++)
                    {
                        var r = new Rectangle(x, y + i * rowH, w, rowH);
                        if (i == 0) // Shutdown is the selected item
                        {
                            g.FillRectangle(SystemBrushes.Highlight, r);
                            g.DrawString(items[i], font, SystemBrushes.HighlightText, r.X + 4, r.Y + 3);
                        }
                        else
                        {
                            g.DrawString(items[i], font, Brushes.Black, r.X + 4, r.Y + 3);
                        }
                    }
                }
            }
            bmp.Save(path, ImageFormat.Png);
            Console.WriteLine(Path.GetFileName(path) + " " + bmp.Width + "x" + bmp.Height);
        }
    }

    // ---- Settings (general tab, with the runtime-added language + click-through groups) ----
    static void RenderSettings(string path)
    {
        using (var s = new Settings())
        {
            SetChecked(s, "countdownModeRadioButton", true);
            PowerActions.Select((ComboBox)GetField(s, "actionComboBox"), PowerAction.Shutdown);
            LocalizedOptions.Select((ComboBox)GetField(s, "trayiconThemeComboBox"), LocalizedOptions.TrayThemes, "Automatic");
            ((TabControl)GetField(s, "settingsTabControl")).SelectedIndex = 0;
            Save(s, path, show: true);
        }
    }

    // ---- Countdown window in one of the four color states (CreateControl only) ----
    static void RenderCountdown(string path, Color bg, string time)
    {
        using (var cd = new Countdown())
        {
            cd.Font = Loc.CreateUiFont(8.25f);
            SetText(cd, "titleLabel", Loc.T("Countdown.TitleFormat", PowerAction.Shutdown.DisplayName()));
            ((Label)GetField(cd, "timeLabel")).Text = time;
            cd.BackColor = bg;
            cd.CreateControl();
            var bmp = new Bitmap(cd.ClientSize.Width, cd.ClientSize.Height);
            cd.DrawToBitmap(bmp, new Rectangle(0, 0, bmp.Width, bmp.Height));
            bmp.Save(path, ImageFormat.Png);
            Console.WriteLine(Path.GetFileName(path) + " " + bmp.Width + "x" + bmp.Height);
        }
    }

    // ---- Tray / right-click context menu, rendered on its own ----
    static void RenderTrayMenu(string path)
    {
        using (var cd = new Countdown())
        {
            cd.Font = Loc.CreateUiFont(8.25f);
            cd.CreateControl();
            Invoke(cd, "BuildClickThroughMenuItem");
            Invoke(cd, "BuildLockMenuItem");
            var cms = (ContextMenuStrip)GetField(cd, "contextMenuStrip");
            cms.Font = Loc.CreateUiFont(9f);
            cms.CreateControl();
            var b = new Bitmap(cms.Width, cms.Height);
            cms.DrawToBitmap(b, new Rectangle(0, 0, b.Width, b.Height));
            b.Save(path, ImageFormat.Png);
            Console.WriteLine(Path.GetFileName(path) + " " + b.Width + "x" + b.Height);
        }
    }

    // ---- Right-click menu shown over the green countdown window ----
    static void RenderRightClickMenu(string path)
    {
        using (var cd = new Countdown())
        {
            cd.Font = Loc.CreateUiFont(8.25f);
            SetText(cd, "titleLabel", Loc.T("Countdown.TitleFormat", PowerAction.Shutdown.DisplayName()));
            ((Label)GetField(cd, "timeLabel")).Text = "00:20:00";
            cd.BackColor = Green;
            cd.CreateControl();
            Invoke(cd, "BuildClickThroughMenuItem");
            Invoke(cd, "BuildLockMenuItem");
            var cms = (ContextMenuStrip)GetField(cd, "contextMenuStrip");
            cms.Font = Loc.CreateUiFont(9f);
            cms.CreateControl();

            var bg = new Bitmap(cd.ClientSize.Width, cd.ClientSize.Height);
            cd.DrawToBitmap(bg, new Rectangle(0, 0, bg.Width, bg.Height));
            var menu = new Bitmap(cms.Width, cms.Height);
            cms.DrawToBitmap(menu, new Rectangle(0, 0, menu.Width, menu.Height));

            using (var g = Graphics.FromImage(bg))
            {
                int mx = Math.Max(0, bg.Width - menu.Width - 8);
                int my = Math.Max(0, (bg.Height - menu.Height) / 2);
                g.DrawImage(menu, mx, my);
            }
            bg.Save(path, ImageFormat.Png);
            Console.WriteLine(Path.GetFileName(path) + " " + bg.Width + "x" + bg.Height);
        }
    }

    // ---- capture helpers ----
    static void Save(Form f, string path, bool show)
    {
        var bmp = CaptureForm(f, show);
        bmp.Save(path, ImageFormat.Png);
        Console.WriteLine(Path.GetFileName(path) + " " + bmp.Width + "x" + bmp.Height);
    }

    static Bitmap CaptureForm(Form f, bool show)
    {
        if (show)
        {
            f.ShowInTaskbar = false;
            f.StartPosition = FormStartPosition.Manual;
            f.Location = new Point(-32000, -32000);
            f.Opacity = 0;
            f.Show();
            Application.DoEvents();
        }
        else
        {
            f.CreateControl();
        }
        var bmp = new Bitmap(f.ClientSize.Width, f.ClientSize.Height);
        f.DrawToBitmap(bmp, new Rectangle(0, 0, bmp.Width, bmp.Height));
        return bmp;
    }

    static object GetField(object o, string name)
        => o.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(o);

    static void SetText(object o, string name, string value)
    {
        var ctl = GetField(o, name) as Control;
        if (ctl != null) ctl.Text = value;
    }

    static void SetChecked(object o, string name, bool value)
    {
        var cb = GetField(o, name) as CheckBox;
        if (cb != null) { cb.Checked = value; return; }
        var rb = GetField(o, name) as RadioButton;
        if (rb != null) rb.Checked = value;
    }

    static void Invoke(object o, string name)
        => o.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(o, null);
}
