using System.IO;
using System.Text;

namespace StillPoint.Services;

/// <summary>Minimal, thread-safe file logger so field problems can be diagnosed without a debugger.</summary>
public static class Log
{
    private static readonly object Gate = new();
    private static string? _path;

    public static string DirectoryPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "StillPoint",
        "logs");

    public static string FilePath
    {
        get
        {
            if (_path is null)
            {
                _path = Path.Combine(DirectoryPath, "stillpoint.log");
            }

            return _path;
        }
    }

    public static void Info(string message) => Write("INFO ", message);

    public static void Warn(string message) => Write("WARN ", message);

    public static void Error(string message, Exception? exception = null)
        => Write("ERROR", exception is null ? message : $"{message} :: {exception}");

    private static void Write(string level, string message)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(DirectoryPath);
                RotateIfNeeded();
                File.AppendAllText(
                    FilePath,
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}{Environment.NewLine}",
                    Encoding.UTF8);
            }
        }
        catch
        {
            // Logging must never take the app down.
        }
    }

    private static void RotateIfNeeded()
    {
        var file = new FileInfo(FilePath);
        if (file.Exists && file.Length > 512 * 1024)
        {
            string backup = FilePath + ".1";
            if (File.Exists(backup))
            {
                File.Delete(backup);
            }

            File.Move(FilePath, backup);
        }
    }
}
