using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace ClockWidget;

public partial class MainWindow : Window
{
    const double Center = 110;
    const double EdgeMargin = 24;
    static readonly CultureInfo Indonesian = CultureInfo.GetCultureInfo("id-ID");
    static readonly FontFamily DisplayFont = new("Segoe UI Variable Display, Segoe UI");

    readonly Settings _s;
    readonly DispatcherTimer _timer = new();
    readonly List<AnimatedText> _cells = new();
    string _pattern = "";

    RotateTransform? _hourRot, _minuteRot, _secondRot;
    Line? _secondHand;

    IntPtr _hwnd, _hook;
    Native.WinEventProc? _hookProc;

    public MainWindow(Settings settings)
    {
        _s = settings;
        InitializeComponent();

        _timer.Tick += (_, _) => Tick(true);
        SourceInitialized += OnSourceInitialized;
        Loaded += (_, _) => { ApplySettings(); _timer.Start(); };
        SizeChanged += (_, _) => ApplyPosition();
        MouseLeftButtonDown += OnDrag;
        SystemParameters.StaticPropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SystemParameters.WorkArea)) ApplyPosition();
        };
        // Monitor ditambah/dicabut atau resolusi berubah.
        EventHandler displayChanged = (_, _) => Dispatcher.BeginInvoke(ApplyPosition);
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += displayChanged;
        Closed += (_, _) =>
        {
            Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= displayChanged;
            if (_hook != IntPtr.Zero) Native.UnhookWinEvent(_hook);
        };
    }

    void OnSourceInitialized(object? sender, EventArgs e)
    {
        _hwnd = new WindowInteropHelper(this).Handle;

        // Tool window: tidak muncul di Alt+Tab.
        long ex = Native.GetWindowLongPtr(_hwnd, Native.GWL_EXSTYLE).ToInt64();
        Native.SetWindowLongPtr(_hwnd, Native.GWL_EXSTYLE, new IntPtr(ex | Native.WS_EX_TOOLWINDOW));

        _hookProc = (_, _, _, _, _, _, _) => UpdateDesktopLayer();
        _hook = Native.SetWinEventHook(Native.EVENT_SYSTEM_FOREGROUND, Native.EVENT_SYSTEM_FOREGROUND,
            IntPtr.Zero, _hookProc, 0, 0, Native.WINEVENT_OUTOFCONTEXT);
    }

    // ---------- Penerapan pengaturan ----------

    public void ApplySettings()
    {
        var accent = new SolidColorBrush(_s.Accent);
        accent.Freeze();

        Zoom.ScaleX = Zoom.ScaleY = _s.Scale;
        Root.Opacity = _s.Opacity;

        var align = _s.Position switch
        {
            Anchor.TopLeft or Anchor.Left or Anchor.BottomLeft => HorizontalAlignment.Left,
            Anchor.TopRight or Anchor.Right or Anchor.BottomRight => HorizontalAlignment.Right,
            _ => HorizontalAlignment.Center,
        };
        TimeHost.HorizontalAlignment = Analog.HorizontalAlignment = DateText.HorizontalAlignment = align;

        bool analog = _s.Style == ClockStyle.Analog;
        TimeHost.Visibility = analog ? Visibility.Collapsed : Visibility.Visible;
        Analog.Visibility = analog ? Visibility.Visible : Visibility.Collapsed;

        TextElement.SetForeground(TimeHost, accent);
        TextElement.SetFontFamily(TimeHost, DisplayFont);
        TextElement.SetFontSize(TimeHost, _s.Style == ClockStyle.Flip ? 60 : 76);
        TextElement.SetFontWeight(TimeHost, _s.Style == ClockStyle.Flip ? FontWeights.SemiBold : FontWeights.Light);

        DateText.Foreground = accent;
        DateText.Visibility = _s.ShowDate ? Visibility.Visible : Visibility.Collapsed;
        DateText.Margin = new Thickness(4, _s.Style == ClockStyle.Minimal ? -4 : 10, 4, 0);

        // Paksa susun ulang tampilan jam.
        _pattern = "";
        _cells.Clear();
        TimeHost.Children.Clear();
        Analog.Children.Clear();
        if (analog) BuildAnalog(accent);

        Tick(false);
        ApplyLayer();
        ApplyPosition();
    }

    void Tick(bool animate)
    {
        var now = DateTime.Now;
        if (_s.Style == ClockStyle.Analog) UpdateAnalog(now, animate);
        else UpdateDigits(now.ToString(_s.TimeFormat, CultureInfo.InvariantCulture), animate);

        if (_s.ShowDate) DateText.Text = now.ToString(_s.DateFormat, Indonesian);

        // Selaraskan detak berikutnya tepat di pergantian detik.
        _timer.Interval = TimeSpan.FromMilliseconds(1000 - DateTime.Now.Millisecond + 5);
    }

    // ---------- Digital & Flip ----------

    static char Classify(char c) => char.IsDigit(c) ? 'd' : char.IsLetter(c) ? 'a' : c;

    void UpdateDigits(string text, bool animate)
    {
        string pattern = string.Concat(text.Select(Classify));
        if (pattern != _pattern)
        {
            BuildDigits(text, pattern);
            return;
        }
        for (int i = 0; i < _cells.Count; i++)
            _cells[i].Set(text[i].ToString(), _s.Animation, animate);
    }

    void BuildDigits(string text, string pattern)
    {
        _pattern = pattern;
        _cells.Clear();
        TimeHost.Children.Clear();
        bool flip = _s.Style == ClockStyle.Flip;

        int i = 0;
        while (i < text.Length)
        {
            // Angka dan huruf dikelompokkan per deret; pemisah berdiri sendiri.
            int start = i;
            char kind = pattern[start];
            i++;
            if (kind is 'd' or 'a')
                while (i < text.Length && pattern[i] == kind) i++;

            var group = new StackPanel { Orientation = Orientation.Horizontal };
            for (int k = start; k < i; k++)
            {
                var cell = new AnimatedText();
                cell.Set(text[k].ToString(), _s.Animation, false);
                _cells.Add(cell);
                group.Children.Add(cell);
            }

            if (kind == 'a')
            {
                // AM/PM lebih kecil, sejajar bawah.
                TextElement.SetFontSize(group, flip ? 22 : 26);
                TextElement.SetFontWeight(group, flip ? FontWeights.SemiBold : FontWeights.Normal);
                group.VerticalAlignment = VerticalAlignment.Bottom;
                if (!flip) group.Margin = new Thickness(2, 0, 0, 16);
            }

            if (flip && kind is 'd' or 'a')
                TimeHost.Children.Add(MakeCard(group, kind == 'a'));
            else if (flip && kind == ' ')
                TimeHost.Children.Add(new Border { Width = 4 });
            else
            {
                if (flip) group.Margin = new Thickness(3, 0, 3, 6);
                TimeHost.Children.Add(group);
            }
        }
    }

    static Border MakeCard(UIElement content, bool small)
    {
        var grid = new Grid();
        grid.Children.Add(content);
        // Garis engsel khas flip clock.
        grid.Children.Add(new Rectangle
        {
            Height = small ? 1 : 2,
            Fill = new SolidColorBrush(Color.FromArgb(0xB0, 0, 0, 0)),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(small ? -8 : -12, 0, small ? -8 : -12, 0),
            IsHitTestVisible = false,
        });
        return new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0xEB, 0x1B, 0x1C, 0x20)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x30, 0xFF, 0xFF, 0xFF)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(small ? 6 : 10),
            Padding = small ? new Thickness(8, 2, 8, 3) : new Thickness(12, 0, 12, 4),
            Margin = new Thickness(3),
            VerticalAlignment = small ? VerticalAlignment.Bottom : VerticalAlignment.Center,
            ClipToBounds = true,
            Child = grid,
        };
    }

    // ---------- Analog ----------

    void BuildAnalog(Brush accent)
    {
        Analog.Children.Add(new Ellipse
        {
            Width = 2 * Center, Height = 2 * Center,
            Fill = new SolidColorBrush(Color.FromArgb(0x70, 0, 0, 0)),
            Stroke = accent, StrokeThickness = 3,
        });

        for (int i = 0; i < 60; i++)
        {
            bool hour = i % 5 == 0, quarter = i % 15 == 0;
            double a = i * 6 * Math.PI / 180;
            double outer = Center - 10, inner = outer - (quarter ? 14 : hour ? 9 : 3);
            Analog.Children.Add(new Line
            {
                X1 = Center + Math.Sin(a) * inner, Y1 = Center - Math.Cos(a) * inner,
                X2 = Center + Math.Sin(a) * outer, Y2 = Center - Math.Cos(a) * outer,
                Stroke = accent,
                StrokeThickness = quarter ? 3.5 : hour ? 2 : 1,
                Opacity = hour ? 1 : 0.5,
                StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round,
            });
        }

        // Jarum detik kontras terhadap warna utama.
        var c = _s.Accent;
        var secondBrush = c.R > 0xD8 && c.G > 0xD8 && c.B > 0xD8
            ? new SolidColorBrush(Color.FromRgb(0xFF, 0x5A, 0x5A))
            : Brushes.White;

        Analog.Children.Add(MakeHand(52, 10, 6, accent, out _hourRot));
        Analog.Children.Add(MakeHand(78, 12, 4, accent, out _minuteRot));
        _secondHand = MakeHand(88, 20, 1.6, secondBrush, out _secondRot);
        Analog.Children.Add(_secondHand);
        Analog.Children.Add(Dot(12, accent));
        Analog.Children.Add(Dot(5, secondBrush));
    }

    static Line MakeHand(double length, double tail, double thickness, Brush brush, out RotateTransform rotation)
    {
        rotation = new RotateTransform(0, Center, Center);
        return new Line
        {
            X1 = Center, Y1 = Center + tail, X2 = Center, Y2 = Center - length,
            Stroke = brush, StrokeThickness = thickness,
            StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round,
            RenderTransform = rotation,
        };
    }

    static Ellipse Dot(double size, Brush brush)
    {
        var dot = new Ellipse { Width = size, Height = size, Fill = brush };
        Canvas.SetLeft(dot, Center - size / 2);
        Canvas.SetTop(dot, Center - size / 2);
        return dot;
    }

    void UpdateAnalog(DateTime now, bool animate)
    {
        if (_hourRot is null || _minuteRot is null || _secondRot is null || _secondHand is null) return;

        _hourRot.Angle = (now.Hour % 12 + now.Minute / 60.0) * 30;
        _minuteRot.Angle = (now.Minute + now.Second / 60.0) * 6;

        // Jarum detik mengikuti format jam: tanpa detik berarti disembunyikan.
        bool showSeconds = _s.TimeFormat.Contains('s');
        _secondHand.Visibility = showSeconds ? Visibility.Visible : Visibility.Collapsed;
        if (!showSeconds) return;

        double target = now.Second * 6;
        if (!animate || _s.Animation == AnimationKind.None)
        {
            _secondRot.BeginAnimation(RotateTransform.AngleProperty, null);
            _secondRot.Angle = target;
            return;
        }

        DoubleAnimation anim = _s.Animation switch
        {
            // Fade = sapuan halus, Slide = detak lembut, Flip = detak memantul.
            AnimationKind.Fade => new DoubleAnimation(target, target + 6, TimeSpan.FromSeconds(1)),
            AnimationKind.Slide => new DoubleAnimation(target - 6, target, TimeSpan.FromMilliseconds(260))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            },
            _ => new DoubleAnimation(target - 6, target, TimeSpan.FromMilliseconds(650))
            {
                EasingFunction = new ElasticEase { Oscillations = 2, Springiness = 4, EasingMode = EasingMode.EaseOut },
            },
        };
        _secondRot.BeginAnimation(RotateTransform.AngleProperty, anim);
    }

    // ---------- Posisi ----------

    void ApplyPosition()
    {
        if (!IsLoaded || ActualWidth <= 0) return;
        double w = ActualWidth, h = ActualHeight;

        // Area kerja monitor terpilih (piksel) dikonversi ke satuan WPF.
        var work = Monitors.Get(_s.MonitorIndex).WorkingArea;
        var toDip = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;
        var area = new Rect(
            toDip.Transform(new Point(work.Left, work.Top)),
            toDip.Transform(new Point(work.Right, work.Bottom)));

        if (_s.Position == Anchor.Custom)
        {
            double minX = SystemParameters.VirtualScreenLeft, minY = SystemParameters.VirtualScreenTop;
            double maxX = minX + SystemParameters.VirtualScreenWidth - w;
            double maxY = minY + SystemParameters.VirtualScreenHeight - h;
            Left = Math.Max(minX, Math.Min(_s.CustomX, maxX));
            Top = Math.Max(minY, Math.Min(_s.CustomY, maxY));
            return;
        }

        int index = (int)_s.Position;
        int column = index % 3, row = index / 3;
        Left = column switch
        {
            0 => area.Left + EdgeMargin,
            1 => area.Left + (area.Width - w) / 2,
            _ => area.Right - w - EdgeMargin,
        };
        Top = row switch
        {
            0 => area.Top + EdgeMargin,
            1 => area.Top + (area.Height - h) / 2,
            _ => area.Bottom - h - EdgeMargin,
        };
    }

    void OnDrag(object sender, MouseButtonEventArgs e)
    {
        double x = Left, y = Top;
        try { DragMove(); } catch (InvalidOperationException) { return; }

        if (Math.Abs(Left - x) > 1 || Math.Abs(Top - y) > 1)
        {
            _s.Position = Anchor.Custom;
            _s.CustomX = Left;
            _s.CustomY = Top;
            _s.Save();
            ((App)Application.Current).NotifySettingsChanged();
        }
    }

    // ---------- Lapisan ----------

    void ApplyLayer()
    {
        if (_hwnd == IntPtr.Zero) return;
        if (_s.Layer == LayerMode.Topmost)
        {
            Topmost = false;
            Topmost = true;
            return;
        }
        Topmost = false;
        SendToBottom();
        UpdateDesktopLayer();
    }

    void SendToBottom()
    {
        const uint flags = Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE;
        Native.SetWindowPos(_hwnd, Native.HWND_NOTOPMOST, 0, 0, 0, 0, flags);
        Native.SetWindowPos(_hwnd, Native.HWND_BOTTOM, 0, 0, 0, 0, flags);
    }

    // Mode desktop: jam berada di bawah semua jendela. Saat desktop menjadi fokus
    // (termasuk Win+D), jam diangkat agar tidak ikut tersembunyi.
    void UpdateDesktopLayer()
    {
        if (_s.Layer != LayerMode.Desktop || _hwnd == IntPtr.Zero) return;

        IntPtr foreground = Native.GetForegroundWindow();
        if (foreground == IntPtr.Zero) return;
        Native.GetWindowThreadProcessId(foreground, out uint pid);
        if (pid == (uint)Environment.ProcessId) return;

        if (Native.ClassName(foreground) is "WorkerW" or "Progman")
            Native.SetWindowPos(_hwnd, Native.HWND_TOPMOST, 0, 0, 0, 0,
                Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
        else
            SendToBottom();
    }

    void OnMenuSettings(object sender, RoutedEventArgs e) => ((App)Application.Current).ShowSettings();
    void OnMenuExit(object sender, RoutedEventArgs e) => ((App)Application.Current).Quit();
}
