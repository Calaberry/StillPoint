using System.Windows;
using System.Windows.Media;
using StillPoint.Models;
using StillPoint.Rendering;
using StillPoint.Services;

namespace StillPoint.Controls;

/// <summary>
/// Live preview of the dot field. It paints the very same <see cref="DotRenderer"/> output as the
/// overlay; only the surface is scaled down to fit the window, with a small minimum dot size so the
/// dots stay visible on a screen-sized grid squeezed into a few hundred pixels.
/// </summary>
public sealed class DotPreview : FrameworkElement
{
    /// <summary>Everything is laid out on a virtual 16:9 surface, the same shape as a game screen.</summary>
    private const double VirtualHeight = 720.0;

    private const double VirtualWidth = 1280.0;

    private const double VisibleDiameter = 4.0;

    private const double ScreenCorner = 10.0;

    private static readonly Brush FrameFallback = Freeze(new SolidColorBrush(Color.FromRgb(0xF3, 0xF3, 0xF5)));

    private static readonly Brush ScreenStrokeFallback = Freeze(new SolidColorBrush(Color.FromArgb(0x14, 0, 0, 0)));

    public static readonly DependencyProperty SettingsProperty = DependencyProperty.Register(
        nameof(Settings),
        typeof(OverlaySettings),
        typeof(DotPreview),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnSettingsChanged));

    /// <summary>"light", "dark" or "game".</summary>
    public static readonly DependencyProperty BackdropProperty = DependencyProperty.Register(
        nameof(Backdrop),
        typeof(string),
        typeof(DotPreview),
        new FrameworkPropertyMetadata("game", FrameworkPropertyMetadataOptions.AffectsRender));

    private OverlaySettings? _subscribed;

    public DotPreview()
    {
        Loaded += (_, _) => ThemeManager.Changed += OnThemeChanged;
        Unloaded += (_, _) => ThemeManager.Changed -= OnThemeChanged;
    }

    public OverlaySettings? Settings
    {
        get => (OverlaySettings?)GetValue(SettingsProperty);
        set => SetValue(SettingsProperty, value);
    }

    public string Backdrop
    {
        get => (string)GetValue(BackdropProperty);
        set => SetValue(BackdropProperty, value);
    }

    internal static Brush BackdropBrush(string? backdrop) => backdrop switch
    {
        "light" => Freeze(new SolidColorBrush(Color.FromRgb(0xF1, 0xF2, 0xF4))),
        "dark" => Freeze(new SolidColorBrush(Color.FromRgb(0x11, 0x12, 0x15))),
        _ => Freeze(new SolidColorBrush(Color.FromRgb(0x39, 0x3E, 0x45))),
    };

    protected override void OnRender(DrawingContext drawingContext)
    {
        double w = ActualWidth;
        double h = ActualHeight;
        if (w <= 0 || h <= 0)
        {
            return;
        }

        // Letterbox a real 16:9 screen into the available box so the preview keeps the geometry the
        // user will actually see — a wide, short box would otherwise stretch the grid sideways.
        double aspect = VirtualWidth / VirtualHeight;
        double screenW = Math.Min(w, h * aspect);
        double screenH = screenW / aspect;
        double left = (w - screenW) / 2.0;
        double top = (h - screenH) / 2.0;
        var screen = new Rect(left, top, screenW, screenH);

        Brush frame = ThemeManager.Brush("PreviewFrameBrush") ?? FrameFallback;
        Brush strokeBrush = ThemeManager.Brush("PreviewScreenStrokeBrush") ?? ScreenStrokeFallback;
        var stroke = Freeze(new Pen(strokeBrush, 1.0));

        drawingContext.DrawRectangle(frame, null, new Rect(0, 0, w, h));
        drawingContext.DrawRoundedRectangle(BackdropBrush(Backdrop), stroke, screen, ScreenCorner, ScreenCorner);

        if (Settings is null || screenH < 12)
        {
            return;
        }

        double scale = screenH / VirtualHeight;
        double virtualWidth = screenW / scale;
        double faithfulDiameter = VirtualHeight * DotLayout.DotDiameterRatio * Settings.DotSize * scale;
        double wanted = VisibleDiameter * Settings.DotSize;
        double radiusScale = faithfulDiameter > 0.01 ? Math.Max(1.0, wanted / faithfulDiameter) : 1.0;

        drawingContext.PushClip(new RectangleGeometry(screen, ScreenCorner, ScreenCorner));
        drawingContext.PushTransform(new TranslateTransform(left, top));
        drawingContext.PushTransform(new ScaleTransform(scale, scale));
        DotRenderer.Render(drawingContext, virtualWidth, VirtualHeight, Settings, 1.0, radiusScale);
        drawingContext.Pop();
        drawingContext.Pop();
        drawingContext.Pop();
    }

    private static void OnSettingsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((DotPreview)d).Rebind(e.OldValue as OverlaySettings, e.NewValue as OverlaySettings);
    }

    private static T Freeze<T>(T freezable)
        where T : Freezable
    {
        if (freezable.CanFreeze)
        {
            freezable.Freeze();
        }

        return freezable;
    }

    private void OnThemeChanged(object? sender, EventArgs e) => InvalidateVisual();

    private void Rebind(OverlaySettings? oldSettings, OverlaySettings? newSettings)
    {
        if (_subscribed is not null)
        {
            _subscribed.Changed -= OnSettingsChangedInternal;
            _subscribed = null;
        }

        if (newSettings is not null)
        {
            _subscribed = newSettings;
            newSettings.Changed += OnSettingsChangedInternal;
        }

        InvalidateVisual();
    }

    private void OnSettingsChangedInternal(object? sender, EventArgs e) => InvalidateVisual();
}
