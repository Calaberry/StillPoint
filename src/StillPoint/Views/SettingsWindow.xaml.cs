using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using StillPoint.Controls;
using StillPoint.Models;
using StillPoint.Services;

namespace StillPoint.Views;

public partial class SettingsWindow : Window
{
    private sealed record ColourPreset(string Name, string Hex);

    private static readonly ColourPreset[] Presets =
    {
        new("柔和白", OverlaySettings.DefaultColorHex),
        new("纯白", "#FFFFFF"),
        new("暖白", "#FFF3E4"),
        new("雾蓝", "#CFE2FF"),
        new("薄荷", "#CFF0DC"),
        new("淡紫", "#E4DEFF"),
    };

    private readonly OverlaySettings _settings;
    private readonly OverlayController _overlay;
    private readonly SettingsService _persistence;
    private readonly Action _requestExit;
    private readonly DispatcherTimer _statusTimer;
    private bool _updating;
    private bool _ready;
    private bool _capturingHotkey;
    private Popup? _colourPopup;
    private ColorPicker? _colourPicker;

    public SettingsWindow(OverlaySettings settings, OverlayController overlay, SettingsService persistence, Action requestExit)
    {
        InitializeComponent();

        _settings = settings;
        _overlay = overlay;
        _persistence = persistence;
        _requestExit = requestExit;

        Preview.Settings = _settings;

        BuildSwatches();
        SyncFromSettings();
        _settings.Changed += OnSettingsChanged;

        _statusTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(1),
        };
        _statusTimer.Tick += (_, _) => UpdateRuntimePanel();
        _statusTimer.Start();
        UpdateRuntimePanel();

        _ready = true;

        Closed += (_, _) =>
        {
            _settings.Changed -= OnSettingsChanged;
            _statusTimer.Stop();
        };
    }

    /// <summary>Set by the app when it is really shutting down; otherwise closing only hides the window.</summary>
    public bool AllowClose { get; set; }

    // ---------------------------------------------------------------- synchronisation

    private void OnSettingsChanged(object? sender, EventArgs e)
    {
        _persistence.SaveDebounced(_settings);
        if (!_updating)
        {
            SyncFromSettings();
        }
    }

    private void SyncFromSettings()
    {
        _updating = true;
        try
        {
            EnableToggle.IsChecked = _settings.Enabled;
            DotSizeSlider.Value = _settings.DotSize;
            GlowSlider.Value = _settings.GlowOpacity;
            OpacitySlider.Value = _settings.DotOpacity;
            ScaleSlider.Value = _settings.LayoutScale;
            SpacingSlider.Value = _settings.SpacingHorizontal;
            SpacingHSlider.Value = _settings.SpacingHorizontal;
            SpacingVSlider.Value = _settings.SpacingVertical;
            SeparateSpacingToggle.IsChecked = !_settings.LinkSpacing;
            bool separate = !_settings.LinkSpacing;
            SeparateSpacingPanel.Visibility = separate ? Visibility.Visible : Visibility.Collapsed;
            SeparateSpacingPanelV.Visibility = separate ? Visibility.Visible : Visibility.Collapsed;

            BackdropGame.IsChecked = _settings.PreviewBackdrop != "light" && _settings.PreviewBackdrop != "dark";
            BackdropLight.IsChecked = _settings.PreviewBackdrop == "light";
            BackdropDark.IsChecked = _settings.PreviewBackdrop == "dark";
            Preview.Backdrop = _settings.PreviewBackdrop;

            ThemeLight.IsChecked = !_settings.DarkTheme;
            ThemeDark.IsChecked = _settings.DarkTheme;

            UpdateValueLabels();
            UpdateSwatchSelection();
            CustomChip.Fill = new SolidColorBrush(_settings.DotColor);
            UpdateHotkeyUi();
            UpdateRuntimePanel();
        }
        finally
        {
            _updating = false;
        }
    }

    private void UpdateValueLabels()
    {
        DotSizeValue.Text = $"{_settings.DotSize * 100:0}%";
        GlowValue.Text = $"{_settings.GlowOpacity * 100:0}%";
        OpacityValue.Text = $"{_settings.DotOpacity * 100:0}%";
        ScaleValue.Text = $"{_settings.LayoutScale * 100:0}%";
        SpacingValue.Text = $"{_settings.SpacingHorizontal * 100:0}%";
        SpacingHValue.Text = $"{_settings.SpacingHorizontal * 100:0}%";
        SpacingVValue.Text = $"{_settings.SpacingVertical * 100:0}%";
    }

    private void UpdateRuntimePanel()
    {
        bool visible = _overlay.IsOverlayVisible;
        OverlayStateText.Text = visible ? "浮层已显示" : "浮层已隐藏";
        StatusLabel.Text = visible ? "运行中" : "已暂停";
        StatusDot.Fill = (Brush)FindResource(visible ? "SuccessBrush" : "TextTertiaryBrush");

        DisplayInfo? display = _overlay.CoveredDisplay;
        OverlayDetailText.Text = display is null
            ? "点阵当前不显示"
            : $"覆盖 {display.FriendlyName} · 16 个点";
        OverlayToggleButton.Content = visible ? "隐藏点阵" : "显示点阵";

        ForegroundInfo info = CompatibilityService.DescribeForeground();
        ForegroundText.Text = string.IsNullOrEmpty(info.Display) ? "—" : info.Display;
        FullscreenBadge.Text = info.Mode;
    }

    // ---------------------------------------------------------------- colour

    private void BuildSwatches()
    {
        SwatchHost.Children.Clear();
        foreach (ColourPreset preset in Presets)
        {
            var swatch = new RadioButton
            {
                Style = (Style)FindResource("ColorSwatch"),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(preset.Hex)),
                GroupName = "DotColour",
                Tag = preset.Hex,
                ToolTip = $"{preset.Name} {preset.Hex}",
            };
            swatch.Checked += OnSwatchChecked;
            System.Windows.Automation.AutomationProperties.SetName(swatch, preset.Name);
            System.Windows.Automation.AutomationProperties.SetHelpText(swatch, preset.Hex);
            SwatchHost.Children.Add(swatch);
        }
    }

    private void UpdateSwatchSelection()
    {
        foreach (object child in SwatchHost.Children)
        {
            if (child is RadioButton swatch && swatch.Tag is string hex)
            {
                swatch.IsChecked = string.Equals(hex, _settings.DotColorHex, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    private void OnSwatchChecked(object sender, RoutedEventArgs e)
    {
        if (_updating || !_ready || sender is not RadioButton { Tag: string hex })
        {
            return;
        }

        _settings.DotColorHex = hex;
    }

    private void OnCustomColourClick(object sender, RoutedEventArgs e)
    {
        EnsureColourPopup();
        Popup popup = _colourPopup!;
        ColorPicker picker = _colourPicker!;
        picker.SetColor(_settings.DotColor, raiseEvent: false);
        popup.IsOpen = !popup.IsOpen;
    }

    private void EnsureColourPopup()
    {
        if (_colourPopup is not null)
        {
            return;
        }

        var picker = new ColorPicker { SelectedColor = _settings.DotColor };
        picker.ColorChanged += (_, color) =>
        {
            _settings.DotColorHex = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
            CustomChip.Fill = new SolidColorBrush(color);
        };
        picker.Done += (_, _) =>
        {
            if (_colourPopup is not null)
            {
                _colourPopup.IsOpen = false;
            }
        };
        _colourPicker = picker;

        _colourPopup = new Popup
        {
            PlacementTarget = CustomColourButton,
            Placement = PlacementMode.Bottom,
            VerticalOffset = 6,
            StaysOpen = false,
            AllowsTransparency = true,
            PopupAnimation = PopupAnimation.Fade,
            Child = new Border
            {
                Background = (Brush)FindResource("CardBackgroundBrush"),
                BorderBrush = (Brush)FindResource("CardStrokeBrush"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Child = picker,
                Effect = new DropShadowEffect
                {
                    BlurRadius = 24,
                    ShadowDepth = 2,
                    Direction = 270,
                    Opacity = 0.18,
                },
            },
        };
    }

    private void OnResetColourClick(object sender, RoutedEventArgs e)
        => _settings.DotColorHex = OverlaySettings.DefaultColorHex;

    // ---------------------------------------------------------------- appearance sliders

    private void OnDotSizeChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_updating || !_ready)
        {
            return;
        }

        _settings.DotSize = DotSizeSlider.Value;
        DotSizeValue.Text = $"{_settings.DotSize * 100:0}%";
    }

    private void OnGlowChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_updating || !_ready)
        {
            return;
        }

        _settings.GlowOpacity = GlowSlider.Value;
        GlowValue.Text = $"{_settings.GlowOpacity * 100:0}%";
    }

    private void OnOpacityChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_updating || !_ready)
        {
            return;
        }

        _settings.DotOpacity = OpacitySlider.Value;
        OpacityValue.Text = $"{_settings.DotOpacity * 100:0}%";
    }

    // ---------------------------------------------------------------- layout sliders

    private void OnScaleChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_updating || !_ready)
        {
            return;
        }

        _settings.LayoutScale = ScaleSlider.Value;
        ScaleValue.Text = $"{_settings.LayoutScale * 100:0}%";
    }

    private void OnSpacingChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_updating || !_ready)
        {
            return;
        }

        _settings.SpacingHorizontal = SpacingSlider.Value;
        SpacingValue.Text = $"{_settings.SpacingHorizontal * 100:0}%";
    }

    private void OnSpacingHChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_updating || !_ready)
        {
            return;
        }

        _settings.SpacingHorizontal = SpacingHSlider.Value;
        SpacingHValue.Text = $"{_settings.SpacingHorizontal * 100:0}%";
    }

    private void OnSpacingVChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_updating || !_ready)
        {
            return;
        }

        _settings.SpacingVertical = SpacingVSlider.Value;
        SpacingVValue.Text = $"{_settings.SpacingVertical * 100:0}%";
    }

    private void OnSeparateSpacingChanged(object sender, RoutedEventArgs e)
    {
        if (_updating || !_ready)
        {
            return;
        }

        _settings.LinkSpacing = SeparateSpacingToggle.IsChecked != true;
    }

    private void OnResetLayoutClick(object sender, RoutedEventArgs e) => _settings.ResetLayout();

    // ---------------------------------------------------------------- preview backdrop

    private void OnBackdropChanged(object sender, RoutedEventArgs e)
    {
        if (_updating || !_ready || sender is not RadioButton { IsChecked: true, Tag: string tag })
        {
            return;
        }

        _settings.PreviewBackdrop = tag;
        Preview.Backdrop = tag;
    }

    // ---------------------------------------------------------------- UI theme

    private void OnThemeChanged(object sender, RoutedEventArgs e)
    {
        if (_updating || !_ready || sender is not RadioButton { IsChecked: true, Tag: string tag })
        {
            return;
        }

        // The palette swap itself is applied centrally in App.OnSettingsChanged.
        _settings.DarkTheme = tag == "dark";
    }

    // ---------------------------------------------------------------- enable / runtime

    private void OnEnableChanged(object sender, RoutedEventArgs e)
    {
        if (_updating || !_ready)
        {
            return;
        }

        _settings.Enabled = EnableToggle.IsChecked == true;
    }

    private void OnToggleOverlayClick(object sender, RoutedEventArgs e)
    {
        _settings.Enabled = !_settings.Enabled;
    }

    private void OnResetAllClick(object sender, RoutedEventArgs e)
    {
        _settings.ResetToDefaults();
        _persistence.Save(_settings);
    }

    // ---------------------------------------------------------------- hotkey

    private void UpdateHotkeyUi()
    {
        HotkeyToggle.IsChecked = _settings.HotkeyEnabled;
        HotkeyButton.Content = _capturingHotkey
            ? "按下组合键…"
            : _settings.HotkeyEnabled ? _settings.Hotkey : "未启用";
        HotkeyButton.IsEnabled = _settings.HotkeyEnabled;
        HotkeyHint.Text = _capturingHotkey ? "Esc 取消，至少包含一个修饰键" : "仅在设置窗口之外生效，不影响游戏按键";
    }

    private void OnHotkeyButtonClick(object sender, RoutedEventArgs e)
    {
        _capturingHotkey = true;
        PreviewKeyDown += OnHotkeyCaptureKey;
        UpdateHotkeyUi();
    }

    private void OnHotkeyCaptureKey(object sender, KeyEventArgs e)
    {
        e.Handled = true;
        if (e.Key == Key.Escape)
        {
            EndHotkeyCapture();
            return;
        }

        string text = HotkeyParser.FromKeyEvent(e);
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        EndHotkeyCapture();
        _settings.Hotkey = text;
        UpdateHotkeyUi();
    }

    private void EndHotkeyCapture()
    {
        _capturingHotkey = false;
        PreviewKeyDown -= OnHotkeyCaptureKey;
        UpdateHotkeyUi();
    }

    private void OnHotkeyToggleChanged(object sender, RoutedEventArgs e)
    {
        if (_updating || !_ready)
        {
            return;
        }

        _settings.HotkeyEnabled = HotkeyToggle.IsChecked == true;
        UpdateHotkeyUi();
    }

    // ---------------------------------------------------------------- window chrome

    private void OnHeaderDrag(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void OnMinimiseClick(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void OnCloseClick(object sender, RoutedEventArgs e) => Hide();

    private void OnExitClick(object sender, RoutedEventArgs e)
    {
        _persistence.Flush();
        _requestExit();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!AllowClose)
        {
            e.Cancel = true;
            _persistence.Flush();
            Hide();
        }

        base.OnClosing(e);
    }
}
