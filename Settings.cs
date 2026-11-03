using System;
using System.IO;
using System.Text.Json;
using System.Windows.Media;

namespace Nuvo;

public sealed class Settings
{
    /// <summary>Index into <see cref="Backgrounds.Presets"/>; -1 means <see cref="CustomColor"/>.</summary>
    public int Background { get; set; } = 0;
    public string CustomColor { get; set; } = "#1C1C1E";
    public string StrokeColor { get; set; } = "#FF3B30";

    static readonly string FilePath =
        System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Nuvo", "settings.json");

    public static Settings Current { get; } = Load();

    static Settings Load()
    {
        try { return JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath)) ?? new Settings(); }
        catch { return new Settings(); } // missing or corrupt file: start from defaults
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this));
        }
        catch { /* settings are a convenience, never fail a screenshot over them */ }
    }
}

static class Backgrounds
{
    // macOS-wallpaper-like two-stop gradients, top-left to bottom-right.
    public static readonly (string From, string To)[] Presets =
    {
        ("#667EEA", "#764BA2"),
        ("#FF9A9E", "#FECFEF"),
        ("#F6D365", "#FDA085"),
        ("#43E97B", "#38F9D7"),
        ("#4FACFE", "#00F2FE"),
        ("#30CFD0", "#330867"),
        ("#232526", "#5A5D60"),
        ("#FFFFFF", "#DADCE0"),
    };

    public static Brush Preset(int i)
    {
        var (a, b) = Presets[i];
        var br = new LinearGradientBrush((Color)ColorConverter.ConvertFromString(a), (Color)ColorConverter.ConvertFromString(b),
            new System.Windows.Point(0, 0), new System.Windows.Point(1, 1));
        br.Freeze();
        return br;
    }

    public static Brush Current(Settings s)
    {
        if (s.Background >= 0 && s.Background < Presets.Length) return Preset(s.Background);
        var br = new SolidColorBrush((Color)ColorConverter.ConvertFromString(s.CustomColor));
        br.Freeze();
        return br;
    }
}
