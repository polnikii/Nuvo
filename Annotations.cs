using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace Nuvo;

enum Tool { Arrow, Rect, Line }

/// <summary>Coordinates are DIPs relative to the selection's top-left corner.</summary>
record Stroke(Tool Tool, Point A, Point B, Color Color);

static class Annotations
{
    const double Thickness = 4;

    public static void Draw(DrawingContext dc, Stroke s)
    {
        var brush = new SolidColorBrush(s.Color);
        var pen = new Pen(brush, Thickness) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };

        switch (s.Tool)
        {
            case Tool.Line:
                dc.DrawLine(pen, s.A, s.B);
                break;
            case Tool.Rect:
                dc.DrawRoundedRectangle(null, pen, new Rect(s.A, s.B), 2, 2);
                break;
            case Tool.Arrow:
                var v = s.B - s.A;
                double len = v.Length;
                if (len < 1) break;
                v /= len;
                var n = new Vector(-v.Y, v.X);
                double head = Math.Min(len, 18), half = 8;
                var tail = s.B - v * head;
                dc.DrawLine(pen, s.A, tail);
                var g = new StreamGeometry();
                using (var c = g.Open())
                {
                    c.BeginFigure(s.B, true, true);
                    c.LineTo(tail + n * half, true, false);
                    c.LineTo(tail - n * half, true, false);
                }
                dc.DrawGeometry(brush, null, g);
                break;
        }
    }
}

/// <summary>Live drawing surface over the selection; the same <see cref="Annotations.Draw"/> is used for export.</summary>
sealed class AnnotationLayer : FrameworkElement
{
    public List<Stroke> Strokes { get; } = new();
    public Stroke? Live { get; set; }

    public AnnotationLayer() { ClipToBounds = true; }

    protected override void OnRender(DrawingContext dc)
    {
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(RenderSize)); // hit-testable
        foreach (var s in Strokes) Annotations.Draw(dc, s);
        if (Live != null) Annotations.Draw(dc, Live);
    }
}
