using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace StillPoint.Controls;

/// <summary>
/// A compact HSV colour picker (saturation/value plane + hue strip + hex box).
/// Raises <see cref="ColorChanged"/> continuously so the overlay can update live.
/// </summary>
public partial class ColorPicker : UserControl
{
    private double _hue;          // 0..360
    private double _saturation = 1;
    private double _value = 1;
    private bool _draggingPlane;
    private bool _draggingHue;
    private bool _syncing;

    public ColorPicker()
    {
        InitializeComponent();
        Loaded += (_, _) => SyncThumbs();
        SizeChanged += (_, _) => SyncThumbs();
    }

    public event EventHandler<Color>? ColorChanged;

    public event EventHandler? Done;

    public Color SelectedColor
    {
        get => HsvToColor(_hue, _saturation, _value);
        set => SetColor(value, raiseEvent: false);
    }

    public void SetColor(Color color, bool raiseEvent = true)
    {
        _syncing = true;
        try
        {
            ColorToHsv(color, out _hue, out _saturation, out _value);
            HueLayer.Fill = new SolidColorBrush(HsvToColor(_hue, 1, 1));
            HexBox.Text = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
            PreviewChip.Background = new SolidColorBrush(color);
            SyncThumbs();
        }
        finally
        {
            _syncing = false;
        }

        if (raiseEvent)
        {
            ColorChanged?.Invoke(this, SelectedColor);
        }
    }

    // ---------------------------------------------------------------- thumbs

    private void SyncThumbs()
    {
        PlaneCanvas.Width = Math.Max(0, PlaneHost.ActualWidth);
        PlaneCanvas.Height = Math.Max(0, PlaneHost.ActualHeight);
        HueCanvas.Width = Math.Max(0, HueHost.ActualWidth);
        HueCanvas.Height = Math.Max(0, HueHost.ActualHeight);

        double planeW = PlaneCanvas.ActualWidth > 0 ? PlaneCanvas.ActualWidth : PlaneHost.ActualWidth;
        double planeH = PlaneCanvas.ActualHeight > 0 ? PlaneCanvas.ActualHeight : PlaneHost.ActualHeight;
        if (planeW > 0 && planeH > 0)
        {
            Canvas.SetLeft(PlaneThumb, _saturation * planeW - PlaneThumb.Width / 2);
            Canvas.SetTop(PlaneThumb, (1 - _value) * planeH - PlaneThumb.Height / 2);
        }

        double hueW = HueCanvas.ActualWidth > 0 ? HueCanvas.ActualWidth : HueHost.ActualWidth;
        double hueH = HueCanvas.ActualHeight > 0 ? HueCanvas.ActualHeight : HueHost.ActualHeight;
        if (hueW > 0)
        {
            Canvas.SetLeft(HueThumb, _hue / 360.0 * hueW - HueThumb.Width / 2);
            Canvas.SetTop(HueThumb, (hueH - HueThumb.Height) / 2);
        }

        HueLayer.Fill = new SolidColorBrush(HsvToColor(_hue, 1, 1));
    }

    // ---------------------------------------------------------------- plane input

    private void OnPlaneMouseDown(object sender, MouseButtonEventArgs e)
    {
        _draggingPlane = true;
        PlaneHost.CaptureMouse();
        UpdateFromPlane(e);
    }

    private void OnPlaneMouseMove(object sender, MouseEventArgs e)
    {
        if (_draggingPlane)
        {
            UpdateFromPlane(e);
        }
    }

    private void OnPlaneMouseUp(object sender, MouseButtonEventArgs e)
    {
        _draggingPlane = false;
        PlaneHost.ReleaseMouseCapture();
    }

    private void UpdateFromPlane(MouseEventArgs e)
    {
        double w = PlaneCanvas.ActualWidth;
        double h = PlaneCanvas.ActualHeight;
        if (w <= 0 || h <= 0)
        {
            return;
        }

        Point p = e.GetPosition(PlaneCanvas);
        _saturation = Math.Clamp(p.X / w, 0, 1);
        _value = Math.Clamp(1 - p.Y / h, 0, 1);
        ApplyFromHsv();
    }

    // ---------------------------------------------------------------- hue input

    private void OnHueMouseDown(object sender, MouseButtonEventArgs e)
    {
        _draggingHue = true;
        HueHost.CaptureMouse();
        UpdateFromHue(e);
    }

    private void OnHueMouseMove(object sender, MouseEventArgs e)
    {
        if (_draggingHue)
        {
            UpdateFromHue(e);
        }
    }

    private void OnHueMouseUp(object sender, MouseButtonEventArgs e)
    {
        _draggingHue = false;
        HueHost.ReleaseMouseCapture();
    }

    private void UpdateFromHue(MouseEventArgs e)
    {
        double w = HueCanvas.ActualWidth;
        if (w <= 0)
        {
            return;
        }

        Point p = e.GetPosition(HueCanvas);
        _hue = Math.Clamp(p.X / w, 0, 1) * 360.0;
        ApplyFromHsv();
    }

    private void ApplyFromHsv()
    {
        if (_syncing)
        {
            return;
        }

        Color color = HsvToColor(_hue, _saturation, _value);
        _syncing = true;
        try
        {
            HexBox.Text = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
            PreviewChip.Background = new SolidColorBrush(color);
            SyncThumbs();
        }
        finally
        {
            _syncing = false;
        }

        ColorChanged?.Invoke(this, color);
    }

    // ---------------------------------------------------------------- hex box

    private void OnHexKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            CommitHex();
            e.Handled = true;
        }
    }

    private void OnHexLostFocus(object sender, RoutedEventArgs e) => CommitHex();

    private void CommitHex()
    {
        if (_syncing)
        {
            return;
        }

        string text = HexBox.Text.Trim();
        if (!text.StartsWith('#'))
        {
            text = "#" + text;
        }

        if (text.Length != 7 ||
            !int.TryParse(text.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int rgb))
        {
            SetColor(SelectedColor, raiseEvent: false);
            return;
        }

        SetColor(Color.FromRgb((byte)((rgb >> 16) & 0xFF), (byte)((rgb >> 8) & 0xFF), (byte)(rgb & 0xFF)));
    }

    private void OnDoneClick(object sender, RoutedEventArgs e) => Done?.Invoke(this, EventArgs.Empty);

    // ---------------------------------------------------------------- conversions

    private static Color HsvToColor(double h, double s, double v)
    {
        h = ((h % 360) + 360) % 360;
        double c = v * s;
        double x = c * (1 - Math.Abs(h / 60.0 % 2 - 1));
        double m = v - c;

        (double r, double g, double b) = h switch
        {
            < 60 => (c, x, 0.0),
            < 120 => (x, c, 0.0),
            < 180 => (0.0, c, x),
            < 240 => (0.0, x, c),
            < 300 => (x, 0.0, c),
            _ => (c, 0.0, x),
        };

        return Color.FromRgb(
            (byte)Math.Round((r + m) * 255),
            (byte)Math.Round((g + m) * 255),
            (byte)Math.Round((b + m) * 255));
    }

    private static void ColorToHsv(Color color, out double h, out double s, out double v)
    {
        double r = color.R / 255.0;
        double g = color.G / 255.0;
        double b = color.B / 255.0;
        double max = Math.Max(r, Math.Max(g, b));
        double min = Math.Min(r, Math.Min(g, b));
        double delta = max - min;

        h = 0;
        if (delta > 1e-9)
        {
            if (max == r)
            {
                h = 60 * (((g - b) / delta) % 6);
            }
            else if (max == g)
            {
                h = 60 * ((b - r) / delta + 2);
            }
            else
            {
                h = 60 * ((r - g) / delta + 4);
            }
        }

        if (h < 0)
        {
            h += 360;
        }

        s = max <= 1e-9 ? 0 : delta / max;
        v = max;
    }
}
