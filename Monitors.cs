using Forms = System.Windows.Forms;

namespace ClockWidget;

static class Monitors
{
    /// <summary>Monitor diurutkan dari kiri ke kanan, lalu atas ke bawah.</summary>
    public static Forms.Screen[] All =>
        Forms.Screen.AllScreens.OrderBy(s => s.Bounds.X).ThenBy(s => s.Bounds.Y).ToArray();

    /// <summary>Indeks di luar jangkauan (mis. monitor dicabut) jatuh ke monitor utama.</summary>
    public static int Resolve(int index)
    {
        var all = All;
        if (index >= 0 && index < all.Length) return index;
        int primary = Array.FindIndex(all, s => s.Primary);
        return Math.Max(primary, 0);
    }

    public static Forms.Screen Get(int index) => All[Resolve(index)];

    public static string Label(Forms.Screen screen, int index) =>
        $"Monitor {index + 1} — {screen.Bounds.Width}×{screen.Bounds.Height}" + (screen.Primary ? " (utama)" : "");
}
