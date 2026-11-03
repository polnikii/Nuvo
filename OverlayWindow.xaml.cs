using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace Nuvo;

public partial class OverlayWindow : Window
{
    readonly BitmapSource _shot;
    readonly double _scale, _w, _h;
    readonly Settings _cfg = Settings.Current;

    Rect _sel;
    Point _anchor;
    bool _selecting, _editing, _drawing;
    Point _strokeStart;
    string? _flyout;
    Image? _preview;

    enum Mode { Area, Window, Desktop }

    Mode _mode = Mode.Area;
    readonly List<Rect> _windows = new(); // DIPs, topmost first
    bool _hovering;

    internal OverlayWindow(BitmapSource shot, Drawing.Rectangle bounds, double scale, List<Drawing.Rectangle> windows)
    {
        InitializeComponent();
        _shot = shot;
        _scale = scale;
        _w = bounds.Width / scale;
        _h = bounds.Height / scale;

        var full = new Rect(0, 0, _w, _h);
        foreach (var r in windows)
        {
            var d = Rect.Intersect(new Rect((r.X - bounds.X) / scale, (r.Y - bounds.Y) / scale, r.Width / scale, r.Height / scale), full);
            if (!d.IsEmpty) _windows.Add(d);
        }

        AreaBtn.Checked += (_, _) => SetMode(Mode.Area);
        WindowBtn.Checked += (_, _) => SetMode(Mode.Window);
        DesktopBtn.Checked += (_, _) => SetMode(Mode.Desktop);
        SaveBtn.Click += (_, _) => SaveAndClose();

        // mode bar sits at the top centre of the primary monitor
        var p = Forms.Screen.PrimaryScreen!.Bounds;
        ModeBar.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Canvas.SetLeft(ModeBar, (p.X - bounds.X + p.Width / 2.0) / scale - ModeBar.DesiredSize.Width / 2);
        Canvas.SetTop(ModeBar, (p.Y - bounds.Y) / scale + 16);

        Left = bounds.X / scale; Top = bounds.Y / scale; Width = _w; Height = _h;
        Root.Width = _w; Root.Height = _h;
        Shot.Source = shot; Shot.Width = _w; Shot.Height = _h;
        UpdateDim();

        ArrowBtn.Checked += (_, _) => _tool = Tool.Arrow;
        RectBtn.Checked += (_, _) => _tool = Tool.Rect;
        LineBtn.Checked += (_, _) => _tool = Tool.Line;
        ColorBtn.Click += (_, _) => ToggleFlyout("color", BuildColorFlyout);
        BgBtn.Click += (_, _) => ToggleFlyout("bg", BuildBgFlyout);
        RefreshDots();

        Loaded += (_, _) => { Activate(); Focus(); };
    }

    Tool _tool = Tool.Arrow;

    // ---------- mouse ----------

    void SetMode(Mode m)
    {
        if (_editing) return;
        _mode = m;
        _hovering = false; _sel = Rect.Empty; UpdateSelection();
        if (m == Mode.Desktop) { _sel = new Rect(0, 0, _w, _h); EnterEdit(); }
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject d && (Toolbar.IsAncestorOf(d) || Flyout.IsAncestorOf(d) || ModeBar.IsAncestorOf(d))) return;
        CloseFlyout();
        var p = e.GetPosition(Root);

        if (!_editing && _mode == Mode.Window)
        {
            if (_hovering) EnterEdit();
        }
        else if (!_editing)
        {
            _anchor = p; _selecting = true;
            _sel = new Rect(p, p);
            CaptureMouse();
        }
        else if (_sel.Contains(p))
        {
            _drawing = true;
            _strokeStart = e.GetPosition(Layer);
            CaptureMouse();
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (_selecting)
        {
            _sel = new Rect(_anchor, Clamp(e.GetPosition(Root), new Rect(0, 0, _w, _h)));
            UpdateSelection();
        }
        else if (!_editing && _mode == Mode.Window)
        {
            var p = e.GetPosition(Root);
            var hit = _windows.FindIndex(r => r.Contains(p));
            _hovering = hit >= 0;
            _sel = _hovering ? _windows[hit] : Rect.Empty;
            UpdateSelection();
        }
        else if (_drawing)
        {
            Layer.Live = new Stroke(_tool, _strokeStart, Clamp(e.GetPosition(Layer), new Rect(0, 0, Layer.Width, Layer.Height)), StrokeColor());
            Layer.InvalidateVisual();
        }
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        if (_selecting)
        {
            _selecting = false; ReleaseMouseCapture();
            if (_sel.Width < 6 || _sel.Height < 6) { _sel = Rect.Empty; UpdateSelection(); return; }
            EnterEdit();
        }
        else if (_drawing)
        {
            _drawing = false; ReleaseMouseCapture();
            var s = Layer.Live;
            Layer.Live = null;
            if (s != null && (s.B - s.A).Length > 3) { Layer.Strokes.Add(s); Refresh(); }
            Layer.InvalidateVisual();
        }
    }

    protected override void OnMouseRightButtonDown(MouseButtonEventArgs e) => Close();

    // ---------- keyboard ----------

    protected override void OnKeyDown(KeyEventArgs e)
    {
        bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
        if (e.Key == Key.Escape || e.Key == Key.Enter) Close();
        else if (_editing && ctrl && e.Key == Key.Z && Layer.Strokes.Count > 0)
        {
            Layer.Strokes.RemoveAt(Layer.Strokes.Count - 1);
            Layer.InvalidateVisual();
            Refresh();
        }
        else if (_editing && ctrl && e.Key == Key.S) SaveAndClose();
    }

    // ---------- selection / edit ----------

    static Point Clamp(Point p, Rect r) => new(Math.Clamp(p.X, r.Left, r.Right), Math.Clamp(p.Y, r.Top, r.Bottom));

    void UpdateDim()
    {
        var full = new RectangleGeometry(new Rect(0, 0, _w, _h));
        Dim.Data = _sel.IsEmpty || _sel.Width <= 0
            ? full
            : new CombinedGeometry(GeometryCombineMode.Exclude, full, new RectangleGeometry(_sel));
    }

    void UpdateSelection()
    {
        UpdateDim();
        bool has = !_sel.IsEmpty && _sel.Width > 0;
        SelBorder.Visibility = SizeLabel.Visibility = has ? Visibility.Visible : Visibility.Collapsed;
        if (!has) return;

        Canvas.SetLeft(SelBorder, _sel.X); Canvas.SetTop(SelBorder, _sel.Y);
        SelBorder.Width = _sel.Width; SelBorder.Height = _sel.Height;

        var px = PixelRect();
        SizeText.Text = $"{px.Width} × {px.Height}";
        SizeLabel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Canvas.SetLeft(SizeLabel, _sel.X);
        Canvas.SetTop(SizeLabel, _sel.Y - SizeLabel.DesiredSize.Height - 6 < 0 ? _sel.Y + 6 : _sel.Y - SizeLabel.DesiredSize.Height - 6);
    }

    Int32Rect PixelRect()
    {
        int x = Math.Max(0, (int)Math.Round(_sel.X * _scale)), y = Math.Max(0, (int)Math.Round(_sel.Y * _scale));
        int w = Math.Min((int)Math.Round(_sel.Width * _scale), _shot.PixelWidth - x);
        int h = Math.Min((int)Math.Round(_sel.Height * _scale), _shot.PixelHeight - y);
        return new Int32Rect(x, y, Math.Max(1, w), Math.Max(1, h));
    }

    void EnterEdit()
    {
        _editing = true;
        ModeBar.Visibility = Visibility.Collapsed;
        UpdateSelection(); // for desktop mode the selection was set directly
        Cursor = Cursors.Arrow;
        SelBorder.Stroke = Brushes.Transparent; // keep the frame invisible once the area is final
        Canvas.SetLeft(Layer, _sel.X); Canvas.SetTop(Layer, _sel.Y);
        Layer.Width = _sel.Width; Layer.Height = _sel.Height;
        Layer.Visibility = Visibility.Visible;

        Toolbar.Visibility = Visibility.Visible;
        Toolbar.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var t = Toolbar.DesiredSize;
        double x = Math.Max(8, Math.Min(_sel.Right - t.Width, _w - t.Width - 8));
        double y = _sel.Bottom + 10;
        if (y + t.Height > _h - 8) y = _sel.Top - t.Height - 10;
        if (y < 8) y = _sel.Bottom - t.Height - 10;
        Canvas.SetLeft(Toolbar, x); Canvas.SetTop(Toolbar, y);

        Refresh();
    }

    // ---------- output ----------

    /// <summary>Composes the current result and puts it on the clipboard.</summary>
    void Refresh()
    {
        var img = Composer.Compose(_shot, PixelRect(), Layer.Strokes, _scale, Backgrounds.Current(_cfg));
        try { Composer.Copy(img); }
        catch (System.Runtime.InteropServices.ExternalException) { /* clipboard held by another app; next change retries */ }
        if (_preview != null) _preview.Source = img;
    }

    void SaveAndClose()
    {
        var img = Composer.Compose(_shot, PixelRect(), Layer.Strokes, _scale, Backgrounds.Current(_cfg));
        var dir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Nuvo");
        Directory.CreateDirectory(dir);
        using (var f = File.Create(System.IO.Path.Combine(dir, $"Nuvo {DateTime.Now:yyyy-MM-dd HH-mm-ss}.png")))
            Composer.Encode(img, f);
        Close();
    }

    // ---------- toolbar / flyouts ----------

    Color StrokeColor() => (Color)ColorConverter.ConvertFromString(_cfg.StrokeColor);

    void RefreshDots()
    {
        ColorDot.Fill = new SolidColorBrush(StrokeColor());
        BgDot.Fill = Backgrounds.Current(_cfg);
    }

    void ToggleFlyout(string kind, Func<UIElement> build)
    {
        if (_flyout == kind) { CloseFlyout(); return; }
        CloseFlyout();
        _flyout = kind;
        FlyoutHost.Children.Add(build());
        Flyout.Visibility = Visibility.Visible;
        Flyout.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var f = Flyout.DesiredSize;
        double tx = Canvas.GetLeft(Toolbar), ty = Canvas.GetTop(Toolbar);
        double x = Math.Max(8, Math.Min(tx + Toolbar.DesiredSize.Width - f.Width, _w - f.Width - 8));
        double y = ty - f.Height - 8;
        if (y < 8) y = ty + Toolbar.DesiredSize.Height + 8;
        Canvas.SetLeft(Flyout, x); Canvas.SetTop(Flyout, y);
        if (kind == "bg") Refresh(); // fills the preview
    }

    void CloseFlyout()
    {
        _flyout = null; _preview = null;
        FlyoutHost.Children.Clear();
        Flyout.Visibility = Visibility.Collapsed;
    }

    UIElement BuildColorFlyout()
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var hex in Ui.Palette)
        {
            var c = hex;
            row.Children.Add(Ui.Swatch(new SolidColorBrush(Ui.Hex(c)), () =>
            {
                _cfg.StrokeColor = c; _cfg.Save(); RefreshDots(); CloseFlyout();
            }));
        }
        return row;
    }

    UIElement BuildBgFlyout()
    {
        var grid = new WrapPanel { Width = 4 * 32 };
        for (int i = 0; i < Backgrounds.Presets.Length; i++)
        {
            int idx = i;
            grid.Children.Add(Ui.Swatch(Backgrounds.Preset(i), () => { _cfg.Background = idx; _cfg.Save(); RefreshDots(); Refresh(); }));
        }
        grid.Children.Add(Ui.Swatch(new SolidColorBrush(Color.FromRgb(0xF2, 0xF2, 0xF7)), PickCustomColor));

        _preview = new Image { MaxWidth = 4 * 32, MaxHeight = 150, Stretch = Stretch.Uniform, Margin = new Thickness(0, 8, 0, 2) };

        var panel = new StackPanel();
        panel.Children.Add(new TextBlock { Text = "Фон", Foreground = new SolidColorBrush(Color.FromRgb(0x8E, 0x8E, 0x93)), Margin = new Thickness(4, 0, 0, 4) });
        panel.Children.Add(grid);
        panel.Children.Add(_preview);
        return panel;
    }

    void PickCustomColor()
    {
        if (!Ui.PickCustomColor(new WindowInteropHelper(this).Handle, _cfg)) return;
        RefreshDots();
        Refresh();
    }
}
