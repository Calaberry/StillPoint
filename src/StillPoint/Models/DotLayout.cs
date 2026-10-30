namespace StillPoint.Models;

/// <summary>One rendered dot in surface (DIP) coordinates.</summary>
public readonly record struct DotSpec(int Index, int Column, int Row, double X, double Y, double Radius, bool HasGlow);

/// <summary>
/// The 4x4 comfort-dot grid measured from the reference screenshot (2560x1440, Call of Duty MW4
/// built-in comfort setting). Positions are normalised so the layout is resolution and DPI agnostic:
///
///   columns (px @2560 wide):  448   1002   1556   2110   -> margin 17.54 % of width, even spacing
///   rows    (px @1440 high):  112    517    922   1330   -> margin  7.71 % of height, even spacing
///   dot core diameter:        ~6.5 px @1440p  -> 0.4514 % of height
///   halo of the outer dots:   ~12 px radius   -> ~1.85x the dot radius
///
/// Exactly 16 dots are always produced, independent of surface size, scale or DPI.
/// </summary>
public static class DotLayout
{
    public const int Columns = 4;
    public const int Rows = 4;
    public const int DotCount = Columns * Rows;

    /// <summary>Horizontal margin of the dot centres, as a fraction of surface width.</summary>
    public const double MarginX = 0.17539;

    /// <summary>Vertical margin of the dot centres, as a fraction of surface height.</summary>
    public const double MarginY = 0.07708;

    /// <summary>Dot diameter as a fraction of surface height (reference: 6.5 px at 1440p).</summary>
    public const double DotDiameterRatio = 0.004514;

    /// <summary>Glow radius expressed as a multiple of the dot radius (reference: ~1.85).</summary>
    public const double GlowRadiusFactor = 1.85;

    /// <summary>Outer columns (0 and 3) carry the halo; the inner eight dots stay plain.</summary>
    public static bool HasGlow(int column) => column is 0 or Columns - 1;

    /// <summary>
    /// Builds the 16 dots for a surface of the given size (device independent pixels / window units).
    /// </summary>
    public static DotSpec[] Build(double width, double height, OverlaySettings settings)
    {
        var dots = new DotSpec[DotCount];
        if (width < 4 || height < 4 || double.IsNaN(width) || double.IsNaN(height))
        {
            return dots;
        }

        // Uniform scale first, then the (default linked) fine spacing trims.
        double scaleX = settings.LayoutScale * settings.SpacingHorizontal;
        double scaleY = settings.LayoutScale * settings.SpacingVertical;
        double centreX = width / 2.0;
        double centreY = height / 2.0;

        double radius = Math.Max(0.75, height * DotDiameterRatio * settings.DotSize * 0.5);
        double stepX = (1.0 - 2.0 * MarginX) / (Columns - 1);
        double stepY = (1.0 - 2.0 * MarginY) / (Rows - 1);

        int index = 0;
        for (int row = 0; row < Rows; row++)
        {
            double ny = MarginY + row * stepY;
            double y = centreY + (ny * height - centreY) * scaleY;
            for (int column = 0; column < Columns; column++)
            {
                double nx = MarginX + column * stepX;
                double x = centreX + (nx * width - centreX) * scaleX;

                // Keep every dot fully inside the surface even at extreme scale values.
                x = Math.Min(width - radius - 1, Math.Max(radius + 1, x));
                y = Math.Min(height - radius - 1, Math.Max(radius + 1, y));

                dots[index] = new DotSpec(index, column, row, x, y, radius, HasGlow(column));
                index++;
            }
        }

        return dots;
    }
}
