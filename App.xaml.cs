using System;
using System.Threading;
using System.Windows;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace Nuvo;

public partial class App : Application
{
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    Mutex? _mutex;
    Forms.NotifyIcon? _tray;
    HotKey? _hotKey;
    OverlayWindow? _overlay;
    SettingsWindow? _settings;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _mutex = new Mutex(true, "Nuvo.SingleInstance", out bool first);
        if (!first) { Shutdown(); return; }

        var autostart = new Forms.ToolStripMenuItem("Автозапуск") { CheckOnClick = true, Checked = IsAutostart() };
        autostart.CheckedChanged += (_, _) => SetAutostart(autostart.Checked);

        var menu = new Forms.ContextMenuStrip
        {
            Renderer = new Forms.ToolStripProfessionalRenderer(new DarkColors()) { RoundedEdges = false },
            BackColor = Drawing.Color.FromArgb(0x2C, 0x2C, 0x2E),
            ForeColor = Drawing.Color.White,
        };
        menu.Items.Add("Настройки", null, (_, _) => ShowSettings());
        menu.Items.Add("Сделать скриншот", null, (_, _) => Capture());
        menu.Items.Add(autostart);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Выход", null, (_, _) => Shutdown());

        using var iconStream = GetResourceStream(new Uri("pack://application:,,,/Assets/Nuvo.ico")).Stream;
        _tray = new Forms.NotifyIcon
        {
            Icon = new Drawing.Icon(iconStream),
            Text = "Nuvo — PrtScr",
            ContextMenuStrip = menu,
            Visible = true,
        };
        _tray.MouseClick += (_, a) => { if (a.Button == Forms.MouseButtons.Left) ShowSettings(); };

        _hotKey = new HotKey(0x2C); // VK_SNAPSHOT
        _hotKey.Pressed += Capture;
        if (Array.IndexOf(e.Args, "--settings") >= 0) ShowSettings();
        if (!_hotKey.Registered)
            _tray.ShowBalloonTip(5000, "Nuvo",
                "Не удалось перехватить PrtScr. Перезапустите Nuvo.",
                Forms.ToolTipIcon.Warning);
    }

    void ShowSettings()
    {
        if (_settings != null) { _settings.Activate(); return; }
        _settings = new SettingsWindow(CaptureDelayed);
        _settings.Closed += (_, _) => _settings = null;
        _settings.Show();
        _settings.Activate();
    }

    // gives the closing settings window time to disappear before the screen is grabbed
    async void CaptureDelayed()
    {
        await System.Threading.Tasks.Task.Delay(250);
        Capture();
    }

    void Capture()
    {
        if (_overlay != null) return;
        // both must happen before the overlay appears
        var (shot, bounds) = ScreenCapture.Grab();
        var windows = ScreenCapture.Windows();
        _overlay = new OverlayWindow(shot, bounds, ScreenCapture.Scale(), windows);
        _overlay.Closed += (_, _) => _overlay = null;
        _overlay.Show();
        _overlay.Activate();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _hotKey?.Dispose();
        if (_tray != null) { _tray.Visible = false; _tray.Dispose(); }
        _mutex?.Dispose();
        base.OnExit(e);
    }

    internal static bool IsAutostart()
    {
        using var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKey);
        return k?.GetValue("Nuvo") != null;
    }

    internal static void SetAutostart(bool on)
    {
        using var k = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(RunKey);
        if (on) k.SetValue("Nuvo", $"\"{Environment.ProcessPath}\"");
        else k.DeleteValue("Nuvo", false);
    }

    sealed class DarkColors : Forms.ProfessionalColorTable
    {
        static readonly Drawing.Color Bg = Drawing.Color.FromArgb(0x2C, 0x2C, 0x2E), Hover = Drawing.Color.FromArgb(0x48, 0x48, 0x4A);
        public override Drawing.Color ToolStripDropDownBackground => Bg;
        public override Drawing.Color ImageMarginGradientBegin => Bg;
        public override Drawing.Color ImageMarginGradientMiddle => Bg;
        public override Drawing.Color ImageMarginGradientEnd => Bg;
        public override Drawing.Color MenuBorder => Drawing.Color.FromArgb(0x48, 0x48, 0x4A);
        public override Drawing.Color MenuItemBorder => Hover;
        public override Drawing.Color MenuItemSelected => Hover;
        public override Drawing.Color MenuItemSelectedGradientBegin => Hover;
        public override Drawing.Color MenuItemSelectedGradientEnd => Hover;
        public override Drawing.Color SeparatorDark => Hover;
        public override Drawing.Color SeparatorLight => Bg;
        public override Drawing.Color CheckBackground => Hover;
        public override Drawing.Color CheckSelectedBackground => Hover;
        public override Drawing.Color CheckPressedBackground => Hover;
    }
}
