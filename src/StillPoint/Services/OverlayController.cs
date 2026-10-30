using System.Windows;
using StillPoint.Models;
using StillPoint.Services;
using StillPoint.Views;

namespace StillPoint.Services;

/// <summary>Owns the lifetime of the overlay window and keeps it in sync with the settings.</summary>
public sealed class OverlayController : IDisposable
{
    private readonly OverlaySettings _settings;
    private OverlayWindow? _window;

    public OverlayController(OverlaySettings settings)
    {
        _settings = settings;
        _settings.Changed += OnSettingsChanged;
    }

    /// <summary>Raised whenever the overlay was shown, hidden, or moved to another display.</summary>
    public event EventHandler? StateChanged;

    public OverlayWindow? Window => _window;

    public bool IsOverlayVisible => _window is { IsVisible: true };

    public DisplayInfo? CoveredDisplay => _window?.CoveredDisplay;

    /// <summary>
    /// Gives the overlay a window it must stay below (the settings window). Evaluated lazily so the
    /// controller does not need to know about the settings window lifetime.
    /// </summary>
    public Func<IntPtr>? AnchorProvider
    {
        get => _anchorProvider;
        set
        {
            _anchorProvider = value;
            if (_window is not null)
            {
                _window.AnchorProvider = value;
            }
        }
    }

    private Func<IntPtr>? _anchorProvider;

    /// <summary>Brings the overlay window in line with <see cref="OverlaySettings.Enabled"/>.</summary>
    public void Apply()
    {
        if (_settings.Enabled)
        {
            ShowOverlay();
        }
        else
        {
            HideOverlay();
        }
    }

    public void ShowOverlay()
    {
        if (_window is null)
        {
            var window = new OverlayWindow(_settings)
            {
                AnchorProvider = _anchorProvider,
            };
            window.Closed += OnWindowClosed;
            _window = window;
            Log.Info("Overlay window created.");
        }

        if (!_window.IsVisible)
        {
            _window.Show();
            Log.Info("Overlay window shown.");
        }

        _window.PositionOverDisplay();
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void HideOverlay()
    {
        if (_window is { IsVisible: true })
        {
            _window.Hide();
            Log.Info("Overlay window hidden.");
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Moves the overlay to the currently configured display.</summary>
    public void ReapplyDisplay() => _window?.PositionOverDisplay();

    /// <summary>Closes and forgets the overlay window; used when the application exits.</summary>
    public void Destroy()
    {
        OverlayWindow? window = _window;
        _window = null;
        if (window is not null)
        {
            window.Closed -= OnWindowClosed;
            window.Close();
            Log.Info("Overlay window destroyed.");
        }
    }

    public void Dispose()
    {
        _settings.Changed -= OnSettingsChanged;
        Destroy();
    }

    private void OnSettingsChanged(object? sender, EventArgs e)
    {
        // Enabled flips are handled by Apply(); anything else just needs a re-position in case the
        // target display changed. Repainting itself is handled by the surface.
        if (_settings.Enabled != IsOverlayVisible)
        {
            Apply();
        }
        else if (_settings.Enabled)
        {
            ReapplyDisplay();
        }
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        if (_window is not null)
        {
            _window.Closed -= OnWindowClosed;
            _window = null;
        }

        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}
