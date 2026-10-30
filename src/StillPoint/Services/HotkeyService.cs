using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;
using StillPoint.Interop;

namespace StillPoint.Services;

/// <summary>
/// Owns a message-only window plus a single global hotkey. The hotkey is optional: if the user
/// picks a combination another application already owns, registration simply fails and the UI reports it.
/// </summary>
public sealed class HotkeyService : IDisposable
{
    private const int WmHotkey = 0x0312;
    private const int HotkeyId = 0x5A17;

    private readonly HwndSource _source;
    private bool _registered;
    private uint _modifiers;
    private uint _virtualKey;

    public HotkeyService()
    {
        var parameters = new HwndSourceParameters("StillPointHotkeySink")
        {
            Width = 0,
            Height = 0,
            PositionX = 0,
            PositionY = 0,
            WindowStyle = 0,
            ExtendedWindowStyle = 0,
            ParentWindow = new IntPtr(-3), // HWND_MESSAGE
        };
        _source = new HwndSource(parameters);
        _source.AddHook(WndProc);
    }

    /// <summary>Raised on the UI thread when the registered hotkey is pressed.</summary>
    public event EventHandler? Pressed;

    public bool IsRegistered => _registered;

    /// <summary>Registers (or replaces) the hotkey described by text such as "Ctrl+Alt+D".</summary>
    public bool TryRegister(string? text)
    {
        Unregister();
        if (!HotkeyParser.TryParse(text, out uint modifiers, out uint virtualKey, out _))
        {
            Log.Warn($"Hotkey text not understood: '{text}'.");
            return false;
        }

        if (NativeMethods.RegisterHotKey(_source.Handle, HotkeyId, modifiers, virtualKey))
        {
            _modifiers = modifiers;
            _virtualKey = virtualKey;
            _registered = true;
            Log.Info($"Global hotkey registered: {HotkeyParser.Format(modifiers, virtualKey)}");
            return true;
        }

        int error = Marshal.GetLastWin32Error();
        Log.Warn($"RegisterHotKey failed for '{text}' (Win32 error {error}).");
        return false;
    }

    public void Unregister()
    {
        if (!_registered)
        {
            return;
        }

        NativeMethods.UnregisterHotKey(_source.Handle, HotkeyId);
        _registered = false;
    }

    public void Dispose()
    {
        Unregister();
        _source.RemoveHook(WndProc);
        _source.Dispose();
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotkey && wParam.ToInt32() == HotkeyId)
        {
            handled = true;
            Pressed?.Invoke(this, EventArgs.Empty);
        }

        return IntPtr.Zero;
    }
}

/// <summary>Parses "Ctrl+Alt+Shift+Key" strings into Win32 modifier flags and a virtual key code.</summary>
public static class HotkeyParser
{
    public const uint ModAlt = 0x0001;
    public const uint ModControl = 0x0002;
    public const uint ModShift = 0x0004;
    public const uint ModWin = 0x0008;

    private static readonly Dictionary<string, uint> NamedKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Space"] = 0x20, ["Tab"] = 0x09, ["Enter"] = 0x0D, ["Escape"] = 0x1B,
        ["Insert"] = 0x2D, ["Delete"] = 0x2E, ["Home"] = 0x24, ["End"] = 0x23,
        ["PageUp"] = 0x21, ["PageDown"] = 0x22, ["Left"] = 0x25, ["Up"] = 0x26,
        ["Right"] = 0x27, ["Down"] = 0x28, ["OemMinus"] = 0xBD, ["OemPlus"] = 0xBB,
    };

    public static bool TryParse(string? text, out uint modifiers, out uint virtualKey, out string normalised)
    {
        modifiers = 0;
        virtualKey = 0;
        normalised = string.Empty;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        string[] parts = text.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return false;
        }

        foreach (string part in parts)
        {
            switch (part.ToLowerInvariant())
            {
                case "ctrl":
                case "control":
                    modifiers |= ModControl;
                    continue;
                case "alt":
                    modifiers |= ModAlt;
                    continue;
                case "shift":
                    modifiers |= ModShift;
                    continue;
                case "win":
                case "windows":
                    modifiers |= ModWin;
                    continue;
            }

            if (TryParseKey(part, out uint vk))
            {
                if (virtualKey != 0)
                {
                    return false; // more than one non-modifier key
                }

                virtualKey = vk;
                continue;
            }

            return false;
        }

        if (virtualKey == 0 || modifiers == 0)
        {
            return false;
        }

        normalised = Format(modifiers, virtualKey);
        return true;
    }

    public static string Format(uint modifiers, uint virtualKey)
    {
        var builder = new List<string>(4);
        if ((modifiers & ModControl) != 0)
        {
            builder.Add("Ctrl");
        }

        if ((modifiers & ModAlt) != 0)
        {
            builder.Add("Alt");
        }

        if ((modifiers & ModShift) != 0)
        {
            builder.Add("Shift");
        }

        if ((modifiers & ModWin) != 0)
        {
            builder.Add("Win");
        }

        builder.Add(KeyName(virtualKey));
        return string.Join("+", builder);
    }

    public static string KeyName(uint virtualKey)
    {
        if (virtualKey is >= 0x30 and <= 0x5A)
        {
            return ((char)virtualKey).ToString();
        }

        if (virtualKey is >= 0x70 and <= 0x87)
        {
            return "F" + (virtualKey - 0x6F);
        }

        foreach (KeyValuePair<string, uint> pair in NamedKeys)
        {
            if (pair.Value == virtualKey)
            {
                return pair.Key;
            }
        }

        return "0x" + virtualKey.ToString("X2");
    }

    private static bool TryParseKey(string part, out uint virtualKey)
    {
        virtualKey = 0;
        if (part.Length == 1)
        {
            char c = char.ToUpperInvariant(part[0]);
            if (c is >= 'A' and <= 'Z' or >= '0' and <= '9')
            {
                virtualKey = c;
                return true;
            }
        }

        if (part.Length is 2 or 3 && (part[0] is 'F' or 'f') && int.TryParse(part[1..], out int f) && f is >= 1 and <= 24)
        {
            virtualKey = (uint)(0x6F + f);
            return true;
        }

        if (NamedKeys.TryGetValue(part, out uint named))
        {
            virtualKey = named;
            return true;
        }

        return false;
    }

    /// <summary>Turns the current keyboard state into a hotkey string; used by the capture control.</summary>
    public static string FromKeyEvent(KeyEventArgs e)
    {
        uint modifiers = 0;
        if ((Keyboard.Modifiers & ModifierKeys.Control) != 0)
        {
            modifiers |= ModControl;
        }

        if ((Keyboard.Modifiers & ModifierKeys.Alt) != 0)
        {
            modifiers |= ModAlt;
        }

        if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0)
        {
            modifiers |= ModShift;
        }

        if ((Keyboard.Modifiers & ModifierKeys.Windows) != 0)
        {
            modifiers |= ModWin;
        }

        Key key = e.Key == Key.System ? e.SystemKey : e.Key;
        uint vk = (uint)KeyInterop.VirtualKeyFromKey(key);
        if (vk == 0 || vk is 0x10 or 0x11 or 0x12 or 0x5B or 0x5C)
        {
            return string.Empty; // a modifier on its own is not a hotkey
        }

        if (modifiers == 0)
        {
            return string.Empty;
        }

        return Format(modifiers, vk);
    }
}
