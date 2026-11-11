using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ClockWidget;

public partial class SettingsWindow : Window
{
    readonly Settings _s;
    readonly MainWindow _widget;
    readonly List<ToggleButton> _colorButtons = new();
    bool _loading = true;

    public SettingsWindow(Settings settings, MainWindow widget)
    {
        _s = settings;
        _widget = widget;
        InitializeComponent();

        foreach (ToggleButton button in PositionGrid.Children)
        {
            button.Margin = new Thickness(2);
            button.Padding = new Thickness(0);
            button.Height = 38;
            button.HorizontalAlignment = HorizontalAlignment.Stretch;
            button.Click += OnPosition;
        }

        for (int i = 0; i < Settings.Palette.Length; i++)
        {
            var (name, color) = Settings.Palette[i];
            var button = new ToggleButton
            {
                Tag = i,
                ToolTip = name,
                Padding = new Thickness(6),
                Margin = new Thickness(0, 0, 6, 6),
                Content = new Ellipse
                {
                    Width = 18, Height = 18,
                    Fill = new SolidColorBrush(color),
                    Stroke = new SolidColorBrush(Color.FromArgb(0x60, 0x80, 0x80, 0x80)),
                    StrokeThickness = 1,
                },
            };
            button.Click += OnColor;
            _colorButtons.Add(button);
            ColorPanel.Children.Add(button);
        }

        var now = DateTime.Now;
        var indonesian = CultureInfo.GetCultureInfo("id-ID");
        foreach (string format in Settings.TimeFormats)
            TimeFormatBox.Items.Add(now.ToString(format, CultureInfo.InvariantCulture)
                + (format.Contains("tt") ? "  (12 jam)" : "  (24 jam)"));
        foreach (string format in Settings.DateFormats)
            DateFormatBox.Items.Add(now.ToString(format, indonesian));

        var screens = Monitors.All;
        for (int i = 0; i < screens.Length; i++)
            MonitorBox.Items.Add(Monitors.Label(screens[i], i));

        LoadFromSettings();
    }

    public void LoadFromSettings()
    {
        _loading = true;
        foreach (ToggleButton button in PositionGrid.Children)
            button.IsChecked = (string)button.Tag == _s.Position.ToString();
        PositionHint.Text = _s.Position == Anchor.Custom
            ? "Posisi bebas (hasil seret). Pilih panah untuk kembali ke posisi tetap."
            : "Jam juga bisa diseret langsung dengan mouse.";

        foreach (var button in _colorButtons)
            button.IsChecked = (int)button.Tag == _s.ColorIndex;
        CustomColorButton.IsChecked = _s.ColorIndex < 0;
        CustomSwatch.Fill = new SolidColorBrush(_s.CustomColorValue);

        StyleBox.SelectedIndex = (int)_s.Style;
        AnimationBox.SelectedIndex = (int)_s.Animation;
        LayerBox.SelectedIndex = (int)_s.Layer;
        MonitorBox.SelectedIndex = Math.Min(Monitors.Resolve(_s.MonitorIndex), MonitorBox.Items.Count - 1);
        ShowBox.SelectedIndex = _s.ShowDate ? 1 : 0;
        TimeFormatBox.SelectedIndex = Math.Clamp(_s.TimeFormatIndex, 0, Settings.TimeFormats.Length - 1);
        DateFormatBox.SelectedIndex = Math.Clamp(_s.DateFormatIndex, 0, Settings.DateFormats.Length - 1);
        DateFormatBox.IsEnabled = _s.ShowDate;
        ScaleSlider.Value = _s.Scale;
        OpacitySlider.Value = _s.Opacity;
        AutostartBox.IsChecked = Settings.Autostart;
        UpdateLabels();
        _loading = false;
    }

    void UpdateLabels()
    {
        ScaleLabel.Text = $"Ukuran: {ScaleSlider.Value * 100:0}%";
        OpacityLabel.Text = $"Opacity: {OpacitySlider.Value * 100:0}%";
    }

    void Commit()
    {
        _s.Save();
        _widget.ApplySettings();
    }

    void OnPosition(object sender, RoutedEventArgs e)
    {
        _s.Position = Enum.Parse<Anchor>((string)((ToggleButton)sender).Tag);
        Commit();
        LoadFromSettings();
    }

    void OnColor(object sender, RoutedEventArgs e)
    {
        _s.ColorIndex = (int)((ToggleButton)sender).Tag;
        Commit();
        LoadFromSettings();
    }

    void OnCustomColor(object sender, RoutedEventArgs e)
    {
        var current = _s.Accent;
        using var dialog = new System.Windows.Forms.ColorDialog
        {
            FullOpen = true,
            Color = System.Drawing.Color.FromArgb(current.R, current.G, current.B),
        };

        // Jadikan jendela pengaturan sebagai pemilik dialog.
        var owner = new System.Windows.Forms.NativeWindow();
        owner.AssignHandle(new System.Windows.Interop.WindowInteropHelper(this).Handle);
        try
        {
            if (dialog.ShowDialog(owner) == System.Windows.Forms.DialogResult.OK)
            {
                _s.CustomColor = Color.FromRgb(dialog.Color.R, dialog.Color.G, dialog.Color.B).ToString();
                _s.ColorIndex = -1;
                Commit();
            }
        }
        finally
        {
            owner.ReleaseHandle();
        }
        LoadFromSettings();
    }

    async void OnPickFromScreen(object sender, RoutedEventArgs e)
    {
        // Jendela pengaturan disembunyikan dulu agar tidak ikut terpotret.
        Hide();
        await Task.Delay(250);
        Color? picked = null;
        try
        {
            picked = ScreenColorPicker.Pick();
        }
        finally
        {
            Show();
            Activate();
        }

        if (picked is Color color)
        {
            _s.CustomColor = color.ToString();
            _s.ColorIndex = -1;
            Commit();
        }
        LoadFromSettings();
    }

    void OnChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        _s.Style = (ClockStyle)StyleBox.SelectedIndex;
        _s.Animation = (AnimationKind)AnimationBox.SelectedIndex;
        _s.Layer = (LayerMode)LayerBox.SelectedIndex;
        _s.MonitorIndex = MonitorBox.SelectedIndex;
        _s.ShowDate = ShowBox.SelectedIndex == 1;
        _s.TimeFormatIndex = TimeFormatBox.SelectedIndex;
        _s.DateFormatIndex = DateFormatBox.SelectedIndex;
        DateFormatBox.IsEnabled = _s.ShowDate;
        Commit();
    }

    void OnSlider(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_loading) return;
        _s.Scale = ScaleSlider.Value;
        _s.Opacity = OpacitySlider.Value;
        UpdateLabels();
        Commit();
    }

    void OnAutostart(object sender, RoutedEventArgs e)
    {
        try
        {
            Settings.Autostart = AutostartBox.IsChecked == true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Gagal mengubah autostart: " + ex.Message, "Pengaturan Jam");
            AutostartBox.IsChecked = Settings.Autostart;
        }
    }
}
