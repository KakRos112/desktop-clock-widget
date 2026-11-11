using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Media;
using Microsoft.Win32;

namespace ClockWidget;

public enum ClockStyle { Minimal, Analog, Flip }
public enum AnimationKind { Fade, Slide, Flip, None }
public enum Anchor { TopLeft, Top, TopRight, Left, Center, Right, BottomLeft, Bottom, BottomRight, Custom }
public enum LayerMode { Topmost, Desktop }

public class Settings
{
    public ClockStyle Style { get; set; } = ClockStyle.Minimal;
    public AnimationKind Animation { get; set; } = AnimationKind.Fade;
    public int ColorIndex { get; set; }
    public bool ShowDate { get; set; } = true;
    public int TimeFormatIndex { get; set; }
    public int DateFormatIndex { get; set; }
    public Anchor Position { get; set; } = Anchor.TopRight;
    /// <summary>Monitor untuk posisi tetap; -1 berarti monitor utama.</summary>
    public int MonitorIndex { get; set; } = -1;
    public double CustomX { get; set; }
    public double CustomY { get; set; }
    public LayerMode Layer { get; set; } = LayerMode.Desktop;
    public double Scale { get; set; } = 1.0;
    public double Opacity { get; set; } = 1.0;

    public static readonly (string Name, Color Color)[] Palette =
    {
        ("Putih", Color.FromRgb(0xFF, 0xFF, 0xFF)),
        ("Biru langit", Color.FromRgb(0x4F, 0xC3, 0xF7)),
        ("Hijau mint", Color.FromRgb(0x69, 0xF0, 0xAE)),
        ("Kuning amber", Color.FromRgb(0xFF, 0xC9, 0x47)),
        ("Merah muda", Color.FromRgb(0xFF, 0x6F, 0x91)),
        ("Merah", Color.FromRgb(0xFF, 0x52, 0x52)),
        ("Oranye", Color.FromRgb(0xFF, 0x8A, 0x3D)),
        ("Ungu", Color.FromRgb(0xB3, 0x88, 0xFF)),
        ("Toska", Color.FromRgb(0x2D, 0xD4, 0xBF)),
        ("Lime", Color.FromRgb(0xC6, 0xFF, 0x00)),
    };

    // Jam diformat dengan InvariantCulture supaya ":" dan AM/PM tidak diubah oleh budaya id-ID.
    public static readonly string[] TimeFormats =
    {
        "HH:mm", "HH:mm:ss", "hh:mm tt", "hh:mm:ss tt", "HH.mm", "HH.mm.ss",
    };

    // Tanggal diformat dengan budaya id-ID (nama hari dan bulan bahasa Indonesia).
    public static readonly string[] DateFormats =
    {
        "dddd, d MMMM yyyy", "d MMMM yyyy", "ddd, d MMM yyyy", "dd'/'MM'/'yyyy", "yyyy-MM-dd",
    };

    /// <summary>Warna pilihan sendiri; dipakai saat ColorIndex bernilai -1.</summary>
    public string CustomColor { get; set; } = "#FFFFFFFF";

    [JsonIgnore]
    public Color CustomColorValue
    {
        get
        {
            try { return (Color)ColorConverter.ConvertFromString(CustomColor); }
            catch { return Colors.White; }
        }
    }

    [JsonIgnore]
    public Color Accent => ColorIndex < 0
        ? CustomColorValue
        : Palette[Math.Min(ColorIndex, Palette.Length - 1)].Color;
    [JsonIgnore]
    public string TimeFormat => TimeFormats[Math.Clamp(TimeFormatIndex, 0, TimeFormats.Length - 1)];
    [JsonIgnore]
    public string DateFormat => DateFormats[Math.Clamp(DateFormatIndex, 0, DateFormats.Length - 1)];

    static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ClockWidget", "settings.json");

    public static Settings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath)) ?? new Settings();
        }
        catch
        {
            // File rusak: pakai nilai bawaan.
        }
        return new Settings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // Gagal simpan tidak boleh menjatuhkan widget.
        }
    }

    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string RunName = "ClockWidget";

    public static bool Autostart
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(RunName) is string;
        }
        set
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey);
            if (value) key.SetValue(RunName, $"\"{Environment.ProcessPath}\"");
            else key.DeleteValue(RunName, false);
        }
    }
}
