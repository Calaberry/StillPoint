using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using System.Windows.Media;

namespace StillPoint.Models;

/// <summary>
/// Every user-visible knob of the overlay. This object is the single source of truth for both the
/// settings window preview and the real overlay window, so the two can never drift apart.
/// </summary>
public sealed class OverlaySettings : INotifyPropertyChanged
{
    // ---- defaults derived from the reference screenshot (Call of Duty MW4 built-in comfort dots) ----
    public const string DefaultColorHex = "#F4F7FB";
    public const double DefaultDotOpacity = 0.94;
    public const double DefaultDotSize = 1.0;
    public const double DefaultLayoutScale = 1.0;
    public const double DefaultSpacing = 1.0;
    public const double DefaultGlowOpacity = 0.13;
    public const double DefaultGlowRadius = 1.0;
    public const double DefaultShadowOpacity = 0.10;

    private bool _enabled = true;
    private string _dotColorHex = DefaultColorHex;
    private double _dotOpacity = DefaultDotOpacity;
    private double _dotSize = DefaultDotSize;
    private double _layoutScale = DefaultLayoutScale;
    private double _spacingHorizontal = DefaultSpacing;
    private double _spacingVertical = DefaultSpacing;
    private bool _linkSpacing = true;
    private double _glowOpacity = DefaultGlowOpacity;
    private double _glowRadius = DefaultGlowRadius;
    private double _shadowOpacity = DefaultShadowOpacity;
    private string _monitorDevice = string.Empty;
    private bool _hotkeyEnabled = true;
    private string _hotkey = "Ctrl+Alt+D";
    private bool _darkTheme;
    private string _previewBackdrop = "game";

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Raised for every change; the overlay and the settings window subscribe to it.</summary>
    public event EventHandler? Changed;

    public bool Enabled
    {
        get => _enabled;
        set => Set(ref _enabled, value);
    }

    public string DotColorHex
    {
        get => _dotColorHex;
        set
        {
            string normalised = NormaliseHex(value);
            Set(ref _dotColorHex, normalised);
        }
    }

    /// <summary>Core dot opacity (0.15 - 1).</summary>
    public double DotOpacity
    {
        get => _dotOpacity;
        set => Set(ref _dotOpacity, Clamp(value, 0.15, 1.0));
    }

    /// <summary>Multiplier on the reference dot diameter (0.5 - 2.5).</summary>
    public double DotSize
    {
        get => _dotSize;
        set => Set(ref _dotSize, Clamp(value, 0.5, 2.5));
    }

    /// <summary>Uniform scale of the whole 4x4 grid around the screen centre (0.6 - 1.5).</summary>
    public double LayoutScale
    {
        get => _layoutScale;
        set => Set(ref _layoutScale, Clamp(value, 0.6, 1.5));
    }

    /// <summary>Fine horizontal spacing multiplier (0.85 - 1.15). Kept linked to the vertical one by default.</summary>
    public double SpacingHorizontal
    {
        get => _spacingHorizontal;
        set
        {
            Set(ref _spacingHorizontal, Clamp(value, 0.85, 1.15));
            if (_linkSpacing)
            {
                Set(ref _spacingVertical, _spacingHorizontal, nameof(SpacingVertical));
            }
        }
    }

    /// <summary>Fine vertical spacing multiplier (0.85 - 1.15).</summary>
    public double SpacingVertical
    {
        get => _spacingVertical;
        set
        {
            Set(ref _spacingVertical, Clamp(value, 0.85, 1.15));
            if (_linkSpacing)
            {
                Set(ref _spacingHorizontal, _spacingVertical, nameof(SpacingHorizontal));
            }
        }
    }

    /// <summary>Locks horizontal and vertical spacing together so the grid can never be stretched.</summary>
    public bool LinkSpacing
    {
        get => _linkSpacing;
        set
        {
            Set(ref _linkSpacing, value);
            if (value)
            {
                Set(ref _spacingVertical, _spacingHorizontal, nameof(SpacingVertical));
            }
        }
    }

    /// <summary>Opacity of the soft halo that the eight outer dots carry (0 - 0.45).</summary>
    public double GlowOpacity
    {
        get => _glowOpacity;
        set => Set(ref _glowOpacity, Clamp(value, 0, 0.45));
    }

    /// <summary>Multiplier on the halo radius (0.4 - 2.4).</summary>
    public double GlowRadius
    {
        get => _glowRadius;
        set => Set(ref _glowRadius, Clamp(value, 0.4, 2.4));
    }

    /// <summary>Optional dark drop shadow that keeps the dots readable on very bright scenes (0 - 0.6).</summary>
    public double ShadowOpacity
    {
        get => _shadowOpacity;
        set => Set(ref _shadowOpacity, Clamp(value, 0, 0.6));
    }

    /// <summary>Device name of the monitor to cover; empty means "primary monitor".</summary>
    public string MonitorDevice
    {
        get => _monitorDevice;
        set => Set(ref _monitorDevice, value ?? string.Empty);
    }

    public bool HotkeyEnabled
    {
        get => _hotkeyEnabled;
        set => Set(ref _hotkeyEnabled, value);
    }

    /// <summary>Global hotkey that toggles the dots, e.g. "Ctrl+Alt+D"; empty disables it.</summary>
    public string Hotkey
    {
        get => _hotkey;
        set => Set(ref _hotkey, value ?? string.Empty);
    }

    public bool DarkTheme
    {
        get => _darkTheme;
        set => Set(ref _darkTheme, value);
    }

    /// <summary>Preview backdrop: "light" | "dark" | "game".</summary>
    public string PreviewBackdrop
    {
        get => _previewBackdrop;
        set => Set(ref _previewBackdrop, string.IsNullOrWhiteSpace(value) ? "game" : value);
    }

    [JsonIgnore]
    public Color DotColor
    {
        get
        {
            try
            {
                return (Color)ColorConverter.ConvertFromString(_dotColorHex)!;
            }
            catch
            {
                return (Color)ColorConverter.ConvertFromString(DefaultColorHex)!;
            }
        }
    }

    /// <summary>Restores every value to the factory default (used by "恢复默认").</summary>
    public void ResetToDefaults()
    {
        Enabled = true;
        DotColorHex = DefaultColorHex;
        DotOpacity = DefaultDotOpacity;
        DotSize = DefaultDotSize;
        LayoutScale = DefaultLayoutScale;
        LinkSpacing = true;
        SpacingHorizontal = DefaultSpacing;
        SpacingVertical = DefaultSpacing;
        GlowOpacity = DefaultGlowOpacity;
        GlowRadius = DefaultGlowRadius;
        ShadowOpacity = DefaultShadowOpacity;
        MonitorDevice = string.Empty;
        HotkeyEnabled = true;
        Hotkey = "Ctrl+Alt+D";
        DarkTheme = false;
        PreviewBackdrop = "game";
    }

    /// <summary>Restores layout-only values (used by "恢复默认布局").</summary>
    public void ResetLayout()
    {
        LayoutScale = DefaultLayoutScale;
        LinkSpacing = true;
        SpacingHorizontal = DefaultSpacing;
        SpacingVertical = DefaultSpacing;
    }

    /// <summary>Restores appearance-only values (used by "恢复默认外观").</summary>
    public void ResetAppearance()
    {
        DotColorHex = DefaultColorHex;
        DotOpacity = DefaultDotOpacity;
        DotSize = DefaultDotSize;
        GlowOpacity = DefaultGlowOpacity;
        GlowRadius = DefaultGlowRadius;
        ShadowOpacity = DefaultShadowOpacity;
    }

    public OverlaySettings Clone()
    {
        return (OverlaySettings)MemberwiseClone();
    }

    public static OverlaySettings CreateDefaults() => new();

    /// <summary>Re-applies every setter so values loaded from disk are clamped and internally consistent.</summary>
    public void Normalise()
    {
        bool linked = _linkSpacing;
        LinkSpacing = false;
        Enabled = _enabled;
        DotColorHex = _dotColorHex;
        DotOpacity = _dotOpacity;
        DotSize = _dotSize;
        LayoutScale = _layoutScale;
        SpacingHorizontal = _spacingHorizontal;
        SpacingVertical = _spacingVertical;
        GlowOpacity = _glowOpacity;
        GlowRadius = _glowRadius;
        ShadowOpacity = _shadowOpacity;
        MonitorDevice = _monitorDevice;
        HotkeyEnabled = _hotkeyEnabled;
        Hotkey = _hotkey;
        DarkTheme = _darkTheme;
        PreviewBackdrop = _previewBackdrop;
        LinkSpacing = linked;
        if (linked)
        {
            Set(ref _spacingVertical, _spacingHorizontal, nameof(SpacingVertical));
        }
    }

    private static double Clamp(double value, double min, double max)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return min;
        }

        return Math.Min(max, Math.Max(min, value));
    }

    private static string NormaliseHex(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return DefaultColorHex;
        }

        string text = value.Trim();
        if (!text.StartsWith('#'))
        {
            text = "#" + text;
        }

        if (text.Length == 9)
        {
            // Ignore a user supplied alpha channel: opacity is its own setting.
            text = "#" + text[3..];
        }

        if (text.Length != 7 || !int.TryParse(text[1..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _))
        {
            return DefaultColorHex;
        }

        return text.ToUpperInvariant();
    }

    private void Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
