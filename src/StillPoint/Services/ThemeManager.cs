using System.Windows;

namespace StillPoint.Services;

/// <summary>
/// Swaps the palette resource dictionary at runtime. Both dictionaries expose exactly the same keys,
/// and every brush is referenced with <c>DynamicResource</c>, so changing the theme repaints the open
/// settings window in place instead of recreating it.
/// </summary>
public static class ThemeManager
{
    private static ResourceDictionary? _current;

    public static bool IsDark { get; private set; }

    /// <summary>Raised after the palette changed, for elements that paint with DrawingContext.</summary>
    public static event EventHandler? Changed;

    public static void Apply(bool dark)
    {
        Application? app = Application.Current;
        if (app is null)
        {
            IsDark = dark;
            return;
        }

        string source = dark ? "Styles/ThemeDark.xaml" : "Styles/Theme.xaml";
        var dictionary = new ResourceDictionary
        {
            Source = new Uri($"pack://application:,,,/{source}", UriKind.Absolute),
        };

        ResourceDictionary merged = app.Resources.MergedDictionaries[0];
        if (_current is not null)
        {
            int index = app.Resources.MergedDictionaries.IndexOf(_current);
            if (index >= 0)
            {
                app.Resources.MergedDictionaries[index] = dictionary;
            }
            else
            {
                app.Resources.MergedDictionaries.Add(dictionary);
            }
        }
        else
        {
            // Theme.xaml is the first merged dictionary declared in App.xaml.
            app.Resources.MergedDictionaries[0] = dictionary;
        }

        _current = dictionary;
        IsDark = dark;
        Changed?.Invoke(null, EventArgs.Empty);
    }

    /// <summary>Reads a brush from the active palette; returns null when the key is missing.</summary>
    public static System.Windows.Media.Brush? Brush(string key)
        => Application.Current?.TryFindResource(key) as System.Windows.Media.Brush;
}
