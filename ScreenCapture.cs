using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace Nuvo;

static class ScreenCapture
{
    /// <summary>Whole virtual desktop (all monitors) in physical pixels.</summary>
    public static (BitmapSource Image, Drawing.Rectangle Bounds) Grab()
    {
        var b = Forms.SystemInformation.VirtualScreen;
        using var bmp = new Drawing.Bitmap(b.Width, b.Height, Drawing.Imaging.PixelFormat.Format32bppRgb);
        using (var g = Drawing.Graphics.FromImage(bmp))
            g.CopyFromScreen(b.Location, Drawing.Point.Empty, b.Size);

        var data = bmp.LockBits(new Drawing.Rectangle(0, 0, b.Width, b.Height),
            Drawing.Imaging.ImageLockMode.ReadOnly, Drawing.Imaging.PixelFormat.Format32bppRgb);
        try
        {
            var src = BitmapSource.Create(b.Width, b.Height, 96, 96, PixelFormats.Bgr32, null,
                data.Scan0, data.Stride * b.Height, data.Stride);
            src.Freeze();
            return (src, b);
        }
        finally { bmp.UnlockBits(data); }
    }

    /// <summary>Visible top-level windows in physical screen pixels, topmost first.</summary>
    public static List<Drawing.Rectangle> Windows()
    {
        var list = new List<Drawing.Rectangle>();
        var cls = new StringBuilder(64);
        EnumWindows((h, _) =>
        {
            if (!IsWindowVisible(h) || IsIconic(h)) return true;
            if (DwmGetWindowAttribute(h, 14, out int cloaked, 4) == 0 && cloaked != 0) return true; // other virtual desktop, suspended UWP
            int ex = GetWindowLong(h, -20);
            if ((ex & 0x80) != 0 && (ex & 0x40000) == 0) return true; // tool window
            cls.Clear(); GetClassName(h, cls, 64);
            if (cls.ToString() is "Progman" or "WorkerW") return true; // desktop itself
            if (DwmGetWindowAttribute(h, 9, out Rect r, 16) != 0) return true; // frame without the invisible resize border
            if (r.Right - r.Left > 1 && r.Bottom - r.Top > 1)
                list.Add(Drawing.Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom));
            return true;
        }, IntPtr.Zero);
        return list;
    }

    delegate bool EnumProc(IntPtr h, IntPtr l);
    [StructLayout(LayoutKind.Sequential)] struct Rect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc p, IntPtr l);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr h, int i);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr h, StringBuilder s, int n);
    [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr h, int a, out int v, int size);
    [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr h, int a, out Rect v, int size);

    /// <summary>Physical pixels per WPF DIP (the app is system-DPI aware, so this is constant).</summary>
    public static double Scale()
    {
        using var g = Drawing.Graphics.FromHwnd(IntPtr.Zero);
        return g.DpiX / 96.0;
    }
}
