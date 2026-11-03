using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace Nuvo;

public partial class SettingsWindow : Window
{
    readonly Settings _cfg = Settings.Current;

    internal SettingsWindow(Action capture)
    {
        InitializeComponent();

        for (int i = 0; i < Backgrounds.Presets.Length; i++)
        {
            int idx = i;
            BgPanel.Children.Add(Ui.Swatch(Backgrounds.Preset(i), () => { _cfg.Background = idx; _cfg.Save(); RefreshPreview(); }));
        }
        BgPanel.Children.Add(Ui.Swatch(new SolidColorBrush(Color.FromRgb(0xF2, 0xF2, 0xF7)), () =>
        {
            if (Ui.PickCustomColor(new WindowInteropHelper(this).Handle, _cfg)) RefreshPreview();
        }));

        foreach (var hex in Ui.Palette)
        {
            var c = hex;
            ColorPanel.Children.Add(Ui.Swatch(new SolidColorBrush(Ui.Hex(c)), () => { _cfg.StrokeColor = c; _cfg.Save(); }));
        }

        AutostartBox.IsChecked = App.IsAutostart();
        AutostartBox.Click += (_, _) => App.SetAutostart(AutostartBox.IsChecked == true);
        CaptureBtn.Click += (_, _) => { Close(); capture(); };

        RefreshPreview();

        var wa = SystemParameters.WorkArea; // bottom-right, next to the tray
        Left = wa.Right - Width - 12;
        Loaded += (_, _) =>
        {
            Top = wa.Bottom - ActualHeight - 12;
            int dark = 1; // dark title bar
            DwmSetWindowAttribute(new WindowInteropHelper(this).Handle, 20, ref dark, sizeof(int));
        };
    }

    void RefreshPreview() => PreviewBg.Background = Backgrounds.Current(_cfg);

    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
}
