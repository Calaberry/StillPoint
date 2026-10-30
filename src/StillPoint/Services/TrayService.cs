using System.IO;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace StillPoint.Services;

/// <summary>Loads the shipped application icon; falls back to a drawn one if the resource is missing.</summary>
internal static class IconFactory
{
    private static Drawing.Icon? _cached;

    internal static Drawing.Icon AppIcon
    {
        get
        {
            if (_cached is not null)
            {
                return _cached;
            }

            _cached = LoadFromResource() ?? DrawFallback();
            return _cached;
        }
    }

    private static Drawing.Icon? LoadFromResource()
    {
        try
        {
            var uri = new Uri("pack://application:,,,/Resources/StillPoint.ico", UriKind.Absolute);
            using Stream? stream = System.Windows.Application.GetResourceStream(uri)?.Stream;
            return stream is null ? null : new Drawing.Icon(stream, 32, 32);
        }
        catch
        {
            return null;
        }
    }

    private static Drawing.Icon DrawFallback()
    {
        using var bitmap = new Drawing.Bitmap(32, 32);
        using (Drawing.Graphics g = Drawing.Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Drawing.Color.Transparent);

            using var background = new Drawing.SolidBrush(Drawing.Color.FromArgb(0x1C, 0x1C, 0x1E));
            using var path = RoundedSquare(1.5f, 1.5f, 29f, 29f, 7.5f);
            g.FillPath(background, path);

            using var dot = new Drawing.SolidBrush(Drawing.Color.FromArgb(0xF4, 0xF7, 0xFB));
            for (int row = 0; row < 4; row++)
            {
                for (int column = 0; column < 4; column++)
                {
                    g.FillEllipse(dot, 5f + column * 6.4f, 5f + row * 6.4f, 3.6f, 3.6f);
                }
            }
        }

        IntPtr handle = bitmap.GetHicon();
        return (Drawing.Icon)Drawing.Icon.FromHandle(handle).Clone();
    }

    private static Drawing.Drawing2D.GraphicsPath RoundedSquare(float x, float y, float w, float h, float r)
    {
        var path = new Drawing.Drawing2D.GraphicsPath();
        path.AddArc(x, y, r, r, 180, 90);
        path.AddArc(x + w - r, y, r, r, 270, 90);
        path.AddArc(x + w - r, y + h - r, r, r, 0, 90);
        path.AddArc(x, y + h - r, r, r, 90, 90);
        path.CloseFigure();
        return path;
    }
}

/// <summary>Notification-area icon plus its context menu.</summary>
public sealed class TrayService : IDisposable
{
    private readonly Forms.NotifyIcon _icon;
    private readonly Forms.ToolStripMenuItem _toggleItem;
    private bool _disposed;

    public TrayService(Action openSettings, Action toggleOverlay, Action exit)
    {
        _toggleItem = new Forms.ToolStripMenuItem("隐藏点阵");
        _toggleItem.Click += (_, _) => toggleOverlay();

        var menu = new Forms.ContextMenuStrip
        {
            ShowImageMargin = false,
        };
        menu.Items.Add(new Forms.ToolStripMenuItem("打开设置…", null, (_, _) => openSettings()));
        menu.Items.Add(_toggleItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(new Forms.ToolStripMenuItem("退出 StillPoint", null, (_, _) => exit()));

        _icon = new Forms.NotifyIcon
        {
            Icon = IconFactory.AppIcon,
            Text = "StillPoint · 点阵浮层",
            Visible = true,
            ContextMenuStrip = menu,
        };
        _icon.MouseDoubleClick += (_, _) => openSettings();
        _icon.BalloonTipTitle = "StillPoint";
        _icon.BalloonTipText = "点阵已按上次的设置运行，双击此图标打开设置。";
    }

    /// <summary>Keeps the tooltip and the menu label in sync with the actual overlay state.</summary>
    public void UpdateState(bool overlayVisible)
    {
        if (_disposed)
        {
            return;
        }

        _icon.Text = overlayVisible ? "StillPoint · 点阵运行中" : "StillPoint · 点阵已暂停";
        _toggleItem.Text = overlayVisible ? "隐藏点阵" : "显示点阵";
    }

    public void ShowBalloon()
    {
        if (!_disposed)
        {
            _icon.ShowBalloonTip(2500);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _icon.Visible = false;
        _icon.Dispose();
    }
}
