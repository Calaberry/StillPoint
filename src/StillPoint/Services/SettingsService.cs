using System.IO;
using System.Text.Json;
using System.Windows.Threading;
using StillPoint.Models;

namespace StillPoint.Services;

/// <summary>Loads and stores <see cref="OverlaySettings"/> as a small JSON file.</summary>
public sealed class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    private readonly DispatcherTimer _saveTimer;
    private OverlaySettings? _pending;

    public SettingsService()
    {
        _saveTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(400),
        };
        _saveTimer.Tick += (_, _) =>
        {
            _saveTimer.Stop();
            if (_pending is not null)
            {
                Save(_pending);
                _pending = null;
            }
        };
    }

    public static string DirectoryPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "StillPoint");

    public static string FilePath => Path.Combine(DirectoryPath, "settings.json");

    public OverlaySettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                string json = File.ReadAllText(FilePath);
                OverlaySettings? loaded = JsonSerializer.Deserialize<OverlaySettings>(json, JsonOptions);
                if (loaded is not null)
                {
                    loaded.Normalise();
                    Log.Info($"Settings loaded from {FilePath}");
                    return loaded;
                }
            }
        }
        catch (Exception exception)
        {
            Log.Warn($"Settings could not be loaded, falling back to defaults: {exception.Message}");
        }

        Log.Info("Using default settings.");
        return OverlaySettings.CreateDefaults();
    }

    public void Save(OverlaySettings settings)
    {
        try
        {
            Directory.CreateDirectory(DirectoryPath);
            string json = JsonSerializer.Serialize(settings, JsonOptions);
            string temp = FilePath + ".tmp";
            File.WriteAllText(temp, json);
            File.Move(temp, FilePath, overwrite: true);
        }
        catch (Exception exception)
        {
            Log.Warn($"Settings could not be saved: {exception.Message}");
        }
    }

    /// <summary>Coalesces rapid slider moves into a single write.</summary>
    public void SaveDebounced(OverlaySettings settings)
    {
        _pending = settings;
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    public void Flush()
    {
        _saveTimer.Stop();
        if (_pending is not null)
        {
            Save(_pending);
            _pending = null;
        }
    }
}
