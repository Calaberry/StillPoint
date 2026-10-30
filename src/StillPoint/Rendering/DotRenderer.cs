using System.Windows;
using System.Windows.Media;
using StillPoint.Models;

namespace StillPoint.Rendering;

/// <summary>
/// Draws the 16 comfort dots. The very same code paints the live overlay window and the preview
/// inside the settings window, so what you see in the preview is exactly what lands on the game.
/// </summary>
public static class DotRenderer
{
    /// <summary>Paints all dots into <paramref name="dc"/> for a surface of the given size.</summary>
    public static void Render(DrawingContext dc, double width, double height, OverlaySettings settings, double opacityMultiplier = 1.0, double radiusScale = 1.0)
    {
        if (width < 4 || height < 4 || opacityMultiplier <= 0)
        {
            return;
        }

        DotSpec[] dots = DotLayout.Build(width, height, settings);
        Color core = settings.DotColor;
        double coreAlpha = Clamp01(settings.DotOpacity * opacityMultiplier);

        // Brushes depend only on the settings, so they are cached per (colour, opacity, size) tuple.
        var cache = new RenderCache(core, coreAlpha, settings, height);

        foreach (DotSpec original in dots)
        {
            DotSpec dot = radiusScale != 1.0
                ? original with { Radius = original.Radius * radiusScale }
                : original;

            if (dot.HasGlow && cache.GlowBrush is not null)
            {
                double glowRadius = dot.Radius * DotLayout.GlowRadiusFactor * settings.GlowRadius;
                if (glowRadius > dot.Radius * 1.05)
                {
                    // The reference halo sits very slightly outboard of the dot, which reads as a
                    // soft shadow cast onto the scene rather than a uniform ring.
                    double outward = dot.Column == 0 ? -dot.Radius * 0.35 : dot.Radius * 0.35;
                    dc.DrawEllipse(cache.GlowBrush, null, new Point(dot.X + outward, dot.Y + dot.Radius * 0.25), glowRadius, glowRadius);
                }
            }

            if (cache.ShadowBrush is not null)
            {
                double shadowRadius = dot.Radius * 1.28;
                dc.DrawEllipse(
                    cache.ShadowBrush,
                    null,
                    new Point(dot.X, dot.Y + dot.Radius * 0.55),
                    shadowRadius,
                    shadowRadius);
            }

            dc.DrawEllipse(cache.CoreBrush, null, new Point(dot.X, dot.Y), dot.Radius, dot.Radius);
        }
    }

    /// <summary>Builds a frozen drawing group so the overlay can be redrawn cheaply.</summary>
    public static DrawingGroup CreateDrawing(double width, double height, OverlaySettings settings, double radiusScale = 1.0)
    {
        var visual = new DrawingVisual();
        using (DrawingContext dc = visual.RenderOpen())
        {
            Render(dc, width, height, settings, 1.0, radiusScale);
        }

        var group = new DrawingGroup();
        using (DrawingContext dc = group.Open())
        {
            dc.DrawDrawing(visual.Drawing);
        }

        group.Freeze();
        return group;
    }

    private static double Clamp01(double value) => Math.Min(1.0, Math.Max(0.0, value));

    private static Color WithAlpha(Color color, double alpha)
    {
        byte a = (byte)Math.Round(Math.Min(1.0, Math.Max(0.0, alpha)) * 255.0);
        return Color.FromArgb(a, color.R, color.G, color.B);
    }

    /// <summary>Immutable set of brushes for one parameter combination.</summary>
    private readonly struct RenderCache
    {
        internal RenderCache(Color core, double coreAlpha, OverlaySettings settings, double surfaceHeight)
        {
            var solid = new SolidColorBrush(WithAlpha(core, coreAlpha));
            solid.Freeze();
            CoreBrush = solid;

            if (settings.ShadowOpacity > 0.005)
            {
                var shadow = new SolidColorBrush(Color.FromArgb(
                    (byte)Math.Round(Math.Min(1.0, settings.ShadowOpacity) * 255.0), 0, 0, 0));
                shadow.Freeze();
                ShadowBrush = shadow;
            }

            if (settings.GlowOpacity > 0.004)
            {
                double a = Math.Min(0.45, settings.GlowOpacity) * coreAlpha;
                var glow = new RadialGradientBrush
                {
                    MappingMode = BrushMappingMode.RelativeToBoundingBox,
                    GradientOrigin = new Point(0.5, 0.5),
                    Center = new Point(0.5, 0.5),
                    RadiusX = 0.5,
                    RadiusY = 0.5,
                };
                // Measured reference halo: fairly flat shoulder out to ~60 % of the radius, then a
                // gentle fall to nothing, never a visible hard ring.
                glow.GradientStops.Add(new GradientStop(WithAlpha(core, a * 0.62), 0.0));
                glow.GradientStops.Add(new GradientStop(WithAlpha(core, a * 0.55), 0.34));
                glow.GradientStops.Add(new GradientStop(WithAlpha(core, a * 0.30), 0.62));
                glow.GradientStops.Add(new GradientStop(WithAlpha(core, a * 0.09), 0.84));
                glow.GradientStops.Add(new GradientStop(WithAlpha(core, 0), 1.0));
                glow.Freeze();
                GlowBrush = glow;
            }
        }

        internal Brush CoreBrush { get; }

        internal Brush? ShadowBrush { get; }

        internal Brush? GlowBrush { get; }
    }
}

