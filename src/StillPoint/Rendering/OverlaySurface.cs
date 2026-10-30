using System.Windows;
using System.Windows.Media;
using StillPoint.Models;

namespace StillPoint.Rendering;

/// <summary>
/// The single drawing surface of the overlay. It paints nothing but the 16 dots, so the layered
/// window stays fully transparent everywhere else and the game remains clickable underneath.
/// </summary>
public sealed class OverlaySurface : FrameworkElement
{
    private OverlaySettings? _settings;
    private DrawingGroup? _cached;
    private double _cachedWidth;
    private double _cachedHeight;
    private double _cachedRevision = -1;
    private double _cacheOpacity = 1.0;

    public OverlaySettings? Settings
    {
        get => _settings;
        set
        {
            if (ReferenceEquals(_settings, value))
            {
                return;
            }

            if (_settings is not null)
            {
                _settings.Changed -= OnSettingsChanged;
            }

            _settings = value;
            if (_settings is not null)
            {
                _settings.Changed += OnSettingsChanged;
            }

            _cachedRevision = -1;
            InvalidateVisual();
        }
    }

    /// <summary>Fades the whole overlay without touching the persisted settings (used for previews).</summary>
    public double OpacityMultiplier
    {
        get => _cacheOpacity;
        set
        {
            double v = Math.Min(1, Math.Max(0, value));
            if (Math.Abs(v - _cacheOpacity) < 0.0001)
            {
                return;
            }

            _cacheOpacity = v;
            _cachedRevision = -1;
            InvalidateVisual();
        }
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        if (_settings is null || ActualWidth < 4 || ActualHeight < 4)
        {
            return;
        }

        if (_cached is null
            || Math.Abs(_cachedWidth - ActualWidth) > 0.01
            || Math.Abs(_cachedHeight - ActualHeight) > 0.01
            || _cachedRevision != _settingsRevision)
        {
            _cached = DotRenderer.CreateDrawing(ActualWidth, ActualHeight, _settings);
            _cachedWidth = ActualWidth;
            _cachedHeight = ActualHeight;
            _cachedRevision = _settingsRevision;
        }

        drawingContext.PushOpacity(_cacheOpacity);
        drawingContext.DrawDrawing(_cached);
        drawingContext.Pop();
    }

    private double _settingsRevision;
    private int _revisionCounter;

    private void OnSettingsChanged(object? sender, EventArgs e)
    {
        _settingsRevision = ++_revisionCounter;
        InvalidateVisual();
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        InvalidateVisual();
    }
}
