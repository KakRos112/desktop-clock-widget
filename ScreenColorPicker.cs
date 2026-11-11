using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace ClockWidget;

/// <summary>Pipet: lapisan sekat layar penuh untuk mengambil warna satu piksel di layar.</summary>
sealed class ScreenColorPicker : Window
{
    const int LoupePixels = 11;   // lebar area yang diperbesar, dalam piksel layar
    const double LoupeSize = 132; // ukuran kaca pembesar

    readonly BitmapSource _shot;
    readonly Drawing.Rectangle _screen;
    readonly Canvas _canvas = new();
    readonly Border _panel;
    readonly ImageBrush _zoom;
    readonly Border _swatch = new() { Width = 18, Height = 18, CornerRadius = new CornerRadius(9) };
    readonly TextBlock _hex = new()
    {
        Foreground = Brushes.White, FontFamily = new FontFamily("Consolas"), FontSize = 14,
        Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center,
    };

    Color? _result;
    Color _current;

    /// <summary>Mengembalikan warna yang diklik, atau null bila dibatalkan (Esc / klik kanan).</summary>
    public static Color? Pick()
    {
        var picker = new ScreenColorPicker();
        picker.ShowDialog();
        return picker._result;
    }

    ScreenColorPicker()
    {
        // Layar dipotret dulu, sehingga warna dibaca dari gambar beku tanpa lapisan ini.
        _screen = Forms.SystemInformation.VirtualScreen;
        _shot = Capture(_screen);

        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0));
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        Topmost = true;
        Cursor = Cursors.Cross;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;

        _zoom = new ImageBrush(_shot)
        {
            ViewboxUnits = BrushMappingMode.Absolute,
            Stretch = Stretch.Fill,
        };
        var loupe = new Grid { Width = LoupeSize, Height = LoupeSize, ClipToBounds = true, Background = Brushes.Black };
        var image = new Rectangle { Fill = _zoom };
        RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.NearestNeighbor);
        loupe.Children.Add(image);
        double cell = LoupeSize / LoupePixels;
        loupe.Children.Add(new Border
        {
            Width = cell + 2, Height = cell + 2,
            BorderBrush = Brushes.White, BorderThickness = new Thickness(1),
            Child = new Border { BorderBrush = Brushes.Black, BorderThickness = new Thickness(1) },
        });

        var info = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
        info.Children.Add(_swatch);
        info.Children.Add(_hex);

        var stack = new StackPanel();
        stack.Children.Add(loupe);
        stack.Children.Add(info);
        stack.Children.Add(new TextBlock
        {
            Text = "Klik: ambil  ·  Esc: batal",
            Foreground = new SolidColorBrush(Color.FromArgb(0xB0, 0xFF, 0xFF, 0xFF)),
            FontSize = 11, Margin = new Thickness(0, 6, 0, 0),
        });

        _panel = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0xF2, 0x1B, 0x1C, 0x20)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x60, 0xFF, 0xFF, 0xFF)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(8),
            IsHitTestVisible = false,
            Child = stack,
        };
        _canvas.Children.Add(_panel);
        Content = _canvas;

        Loaded += (_, _) => { Activate(); Focus(); Track(Mouse.GetPosition(_canvas)); };
        MouseMove += (_, e) => Track(e.GetPosition(_canvas));
        MouseLeftButtonDown += (_, e) =>
        {
            Track(e.GetPosition(_canvas));
            _result = _current;
            Close();
        };
        MouseRightButtonDown += (_, _) => Close();
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) Close();
        };
        Deactivated += (_, _) =>
        {
            if (IsVisible && _result is null) Activate();
        };
    }

    void Track(Point position)
    {
        var cursor = Forms.Cursor.Position;
        int x = Math.Clamp(cursor.X - _screen.Left, 0, _shot.PixelWidth - 1);
        int y = Math.Clamp(cursor.Y - _screen.Top, 0, _shot.PixelHeight - 1);

        var pixel = new byte[4];
        _shot.CopyPixels(new Int32Rect(x, y, 1, 1), pixel, 4, 0);
        _current = Color.FromRgb(pixel[2], pixel[1], pixel[0]);
        _swatch.Background = new SolidColorBrush(_current);
        _hex.Text = $"#{_current.R:X2}{_current.G:X2}{_current.B:X2}";

        int half = LoupePixels / 2;
        _zoom.Viewbox = new Rect(x - half, y - half, LoupePixels, LoupePixels);

        // Panel mengikuti kursor dan berpindah sisi saat mendekati tepi layar.
        _panel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        double w = _panel.DesiredSize.Width, h = _panel.DesiredSize.Height;
        double left = position.X + 22, top = position.Y + 22;
        if (left + w > ActualWidth) left = position.X - 22 - w;
        if (top + h > ActualHeight) top = position.Y - 22 - h;
        Canvas.SetLeft(_panel, left);
        Canvas.SetTop(_panel, top);
    }

    static BitmapSource Capture(Drawing.Rectangle area)
    {
        using var bitmap = new Drawing.Bitmap(area.Width, area.Height, Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var g = Drawing.Graphics.FromImage(bitmap))
            g.CopyFromScreen(area.Left, area.Top, 0, 0, bitmap.Size);

        IntPtr handle = bitmap.GetHbitmap();
        try
        {
            var source = Imaging.CreateBitmapSourceFromHBitmap(
                handle, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            source.Freeze();
            return source;
        }
        finally
        {
            Native.DeleteObject(handle);
        }
    }
}
