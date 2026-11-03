using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace Nuvo;

/// <summary>Controls shared by the overlay flyouts and the settings window.</summary>
static class Ui
{
    public static readonly string[] Palette = { "#FF3B30", "#FF9500", "#FFCC00", "#34C759", "#0A84FF", "#FFFFFF", "#1C1C1E" };

    public static Color Hex(string s) => (Color)ColorConverter.ConvertFromString(s);

    public static Button Swatch(Brush fill, Action onClick)
    {
        var b = new Button
        {
            Style = (Style)Application.Current.FindResource("PlainBtn"),
            Content = new Ellipse { Width = 20, Height = 20, Fill = fill, Stroke = new SolidColorBrush(Color.FromArgb(0x33, 255, 255, 255)) },
        };
        b.Click += (_, _) => onClick();
        return b;
    }

    /// <summary>Asks for a custom background color; on OK stores it in <paramref name="cfg"/>.</summary>
    public static bool PickCustomColor(IntPtr owner, Settings cfg)
    {
        using var dlg = new Forms.ColorDialog { FullOpen = true };
        var c = Hex(cfg.CustomColor);
        dlg.Color = Drawing.Color.FromArgb(c.R, c.G, c.B);
        if (dlg.ShowDialog(new Hwnd(owner)) != Forms.DialogResult.OK) return false;

        cfg.CustomColor = $"#{dlg.Color.R:X2}{dlg.Color.G:X2}{dlg.Color.B:X2}";
        cfg.Background = -1;
        cfg.Save();
        return true;
    }

    sealed class Hwnd : Forms.IWin32Window
    {
        public Hwnd(IntPtr h) { Handle = h; }
        public IntPtr Handle { get; }
    }
}
