using System.Windows;
using System.Windows.Threading;
using StillPoint.Models;
using StillPoint.Services;
using StillPoint.Views;

namespace StillPoint;

public partial class App : Application
{
    private SingleInstanceGuard? _instanceGuard;
    private OverlaySettings? _settings;
    private OverlayController? _overlay;
    private HotkeyService? _hotkeys;
    private SettingsService? _persistence;
    private TrayService? _tray;
    private SettingsWindow? _settingsWindow;
    private string _registeredHotkey = string.Empty;
    private bool _themeDark;
    private bool _exiting;

    public static OverlaySettings? CurrentSettings { get; private set; }

    public static OverlayController? Overlay { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += (_, args) => args.SetObserved();

        Log.Info("==== StillPoint starting ====");

        _instanceGuard = new SingleInstanceGuard();
        if (!_instanceGuard.TryAcquire())
        {
            Shutdown(0);
            return;
        }

        _persistence = new SettingsService();
        _settings = _persistence.Load();
        CurrentSettings = _settings;
        _themeDark = _settings.DarkTheme;
        ThemeManager.Apply(_themeDark);
        _settings.Changed += OnSettingsChanged;

        _overlay = new OverlayController(_settings)
        {
            AnchorProvider = () => _settingsWindow is { IsVisible: true } window
                ? new System.Windows.Interop.WindowInteropHelper(window).Handle
                : IntPtr.Zero,
        };
        Overlay = _overlay;
        _overlay.StateChanged += (_, _) => _tray?.UpdateState(_overlay.IsOverlayVisible);
        _overlay.Apply();

        _hotkeys = new HotkeyService();
        _hotkeys.Pressed += OnHotkeyPressed;
        RegisterHotkey();

        _tray = new TrayService(ShowSettings, ToggleOverlay, ExitApplication);
        _tray.UpdateState(_overlay.IsOverlayVisible);

        ShowSettings();
        Log.Info("Startup complete.");
    }

    // ------------------------------------------------------------------ settings window

    private void ShowSettings()
    {
        if (_settings is null || _overlay is null || _persistence is null)
        {
            return;
        }

        if (_settingsWindow is null)
        {
            _settingsWindow = new SettingsWindow(_settings, _overlay, _persistence, ExitApplication);
            _settingsWindow.Closed += (_, _) => _settingsWindow = null;
            _settingsWindow.IsVisibleChanged += (_, _) => _overlay.ReapplyDisplay();
        }

        if (!_settingsWindow.IsVisible)
        {
            _settingsWindow.Show();
        }

        if (_settingsWindow.WindowState == WindowState.Minimized)
        {
            _settingsWindow.WindowState = WindowState.Normal;
        }

        _settingsWindow.Activate();
    }

    private void ToggleOverlay()
    {
        if (_settings is not null)
        {
            _settings.Enabled = !_settings.Enabled;
        }
    }

    private void ExitApplication()
    {
        if (_exiting)
        {
            return;
        }

        _exiting = true;
        _persistence?.Flush();
        if (_settingsWindow is not null)
        {
            _settingsWindow.AllowClose = true;
            _settingsWindow.Close();
            _settingsWindow = null;
        }

        _overlay?.Destroy();
        Shutdown();
    }

    // ------------------------------------------------------------------ hotkey

    private void RegisterHotkey()
    {
        if (_hotkeys is null || _settings is null)
        {
            return;
        }

        if (!_settings.HotkeyEnabled || string.IsNullOrWhiteSpace(_settings.Hotkey))
        {
            _hotkeys.Unregister();
            _registeredHotkey = string.Empty;
            return;
        }

        if (_hotkeys.TryRegister(_settings.Hotkey))
        {
            _registeredHotkey = _settings.Hotkey;
        }
        else
        {
            _registeredHotkey = string.Empty;
        }
    }

    private void OnHotkeyPressed(object? sender, EventArgs e)
    {
        if (_settings is null || _overlay is null)
        {
            return;
        }

        _settings.Enabled = !_settings.Enabled;
        Log.Info($"Hotkey toggled overlay: Enabled={_settings.Enabled}");
        _tray?.UpdateState(_overlay.IsOverlayVisible);
    }

    // ------------------------------------------------------------------ lifecycle

    private void OnSettingsChanged(object? sender, EventArgs e)
    {
        if (_settings is null)
        {
            return;
        }

        // The settings window persists its own edits; the hotkey can also be changed elsewhere.
        _persistence?.SaveDebounced(_settings);

        if (_themeDark != _settings.DarkTheme)
        {
            _themeDark = _settings.DarkTheme;
            ThemeManager.Apply(_themeDark);
        }

        bool hotkeyWanted = _settings.HotkeyEnabled && !string.IsNullOrWhiteSpace(_settings.Hotkey);
        if (hotkeyWanted && !string.Equals(_settings.Hotkey, _registeredHotkey, StringComparison.Ordinal))
        {
            RegisterHotkey();
        }
        else if (!hotkeyWanted && !string.IsNullOrEmpty(_registeredHotkey))
        {
            RegisterHotkey();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.Info("==== StillPoint exiting ====");
        _persistence?.Flush();
        _tray?.Dispose();
        _hotkeys?.Dispose();
        _overlay?.Dispose();
        _instanceGuard?.Dispose();
        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Error("Unhandled dispatcher exception", e.Exception);
        MessageBox.Show(
            $"StillPoint 遇到了一个未预期的错误，已记录到日志：\n{Log.FilePath}\n\n{e.Exception.Message}",
            "StillPoint",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
        e.Handled = true;
    }

    private void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
        => Log.Error("Unhandled domain exception", e.ExceptionObject as Exception);
}
