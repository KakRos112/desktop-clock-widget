using System.Windows;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace ClockWidget;

public partial class App : Application
{
    Mutex? _mutex;
    Forms.NotifyIcon? _tray;
    Settings _settings = new();
    MainWindow? _widget;
    SettingsWindow? _settingsWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _mutex = new Mutex(true, "ClockWidget.SingleInstance", out bool first);
        if (!first)
        {
            Shutdown();
            return;
        }

        _settings = Settings.Load();
        _widget = new MainWindow(_settings);
        _widget.Closed += (_, _) => Quit();
        _widget.Show();

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Pengaturan…", null, (_, _) => ShowSettings());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Keluar", null, (_, _) => Quit());

        _tray = new Forms.NotifyIcon
        {
            Icon = LoadIcon(),
            Text = "Jam Desktop",
            ContextMenuStrip = menu,
            Visible = true,
        };
        _tray.MouseClick += (_, args) =>
        {
            if (args.Button == Forms.MouseButtons.Left) ShowSettings();
        };

        if (e.Args.Contains("--settings")) ShowSettings();
    }

    public void ShowSettings()
    {
        if (_widget is null) return;
        if (_settingsWindow is null)
        {
            _settingsWindow = new SettingsWindow(_settings, _widget);
            _settingsWindow.Closed += (_, _) => _settingsWindow = null;
            _settingsWindow.Show();
        }
        _settingsWindow.Activate();
    }

    /// <summary>Dipanggil saat widget diseret, agar jendela pengaturan ikut diperbarui.</summary>
    public void NotifySettingsChanged() => _settingsWindow?.LoadFromSettings();

    public void Quit()
    {
        if (_tray is not null)
        {
            _tray.Visible = false;
            _tray.Dispose();
            _tray = null;
        }
        Shutdown();
    }

    // Ikon tray diambil dari ikon exe (app.ico), sama dengan ikon jendela dan taskbar.
    static Drawing.Icon LoadIcon()
    {
        try
        {
            if (Environment.ProcessPath is string path && Drawing.Icon.ExtractAssociatedIcon(path) is { } icon)
                return icon;
        }
        catch
        {
            // Jatuh ke ikon bawaan Windows.
        }
        return Drawing.SystemIcons.Application;
    }
}
