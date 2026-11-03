using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;

namespace Nuvo;

static class Composer
{
    /// <summary>
    /// Crops <paramref name="rect"/> out of the screenshot, burns in the strokes and puts the result
    /// on a rounded background with a rounded, shadowed card. Corners outside the background stay transparent.
    /// </summary>
    public static BitmapSource Compose(BitmapSource shot, Int32Rect rect, IReadOnlyList<Stroke> strokes, double scale, Brush background)
    {
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            dc.DrawImage(new CroppedBitmap(shot, rect), new Rect(0, 0, rect.Width, rect.Height));
            dc.PushTransform(new ScaleTransform(scale, scale));
            foreach (var s in strokes) Annotations.Draw(dc, s);
            dc.Pop();
        }
        var flat = new RenderTargetBitmap(rect.Width, rect.Height, 96, 96, PixelFormats.Pbgra32);
        flat.Render(dv);

        double pad = 56 * scale, w = rect.Width + 2 * pad, h = rect.Height + 2 * pad;
        var card = new Border
        {
            Width = rect.Width,
            Height = rect.Height,
            CornerRadius = new CornerRadius(12 * scale),
            Background = new ImageBrush(flat) { Stretch = Stretch.Fill },
            // blur + depth must stay below pad or the shadow gets cut at the image edge
            Effect = new DropShadowEffect { BlurRadius = 36 * scale, ShadowDepth = 12 * scale, Direction = 270, Opacity = 0.35 },
        };
        var root = new Border { Width = w, Height = h, Background = background, Child = card };
        root.Measure(new Size(w, h));
        root.Arrange(new Rect(0, 0, w, h));

        var result = new RenderTargetBitmap((int)System.Math.Ceiling(w), (int)System.Math.Ceiling(h), 96, 96, PixelFormats.Pbgra32);
        result.Render(root);
        result.Freeze();
        return result;
    }

    /// <summary>
    /// Puts the image on the clipboard as PNG (keeps transparent corners in browsers/messengers)
    /// plus a bitmap flattened on white for apps that ignore alpha, like Paint.
    /// </summary>
    public static void Copy(BitmapSource img)
    {
        var ms = new MemoryStream();
        Encode(img, ms);

        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, img.PixelWidth, img.PixelHeight));
            dc.DrawImage(img, new Rect(0, 0, img.PixelWidth, img.PixelHeight));
        }
        var white = new RenderTargetBitmap(img.PixelWidth, img.PixelHeight, 96, 96, PixelFormats.Pbgra32);
        white.Render(dv);

        var data = new DataObject();
        data.SetData("PNG", ms);
        data.SetImage(white);
        // WPF has no built-in retry; the clipboard is often briefly locked by another app
        for (int i = 0; ; i++)
        {
            try { Clipboard.SetDataObject(data, true); return; }
            catch (System.Runtime.InteropServices.COMException) when (i < 9) { System.Threading.Thread.Sleep(50); }
        }
    }

    public static void Encode(BitmapSource img, Stream to)
    {
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(img));
        enc.Save(to);
    }
}
