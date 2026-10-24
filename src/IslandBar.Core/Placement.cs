namespace IslandBar.Core;

public readonly record struct PixelRect(int X, int Y, int Width, int Height);

public static class Placement
{
    /// <summary>
    /// Works out the window rectangle in real screen pixels.
    /// The island is centered on the monitor and sticks to the top.
    /// Sizes are in DIPs (1/96 inch) and are multiplied by dpiScale (1.0 = 96 dpi).
    /// </summary>
    public static PixelRect ComputeWindowRect(PixelRect monitor, double dpiScale, int offsetXDip,
        int topMarginDip, double contentWidthDip, double contentHeightDip, double paddingDip)
    {
        var width = (int)Math.Ceiling((contentWidthDip + 2 * paddingDip) * dpiScale);
        var height = (int)Math.Ceiling((contentHeightDip + 2 * paddingDip) * dpiScale);

        var centerX = monitor.X + monitor.Width / 2.0 + offsetXDip * dpiScale;
        var left = (int)Math.Round(centerX - width / 2.0);
        var top = (int)Math.Round(monitor.Y + (topMarginDip - paddingDip) * dpiScale);

        // Keep the island horizontally inside the monitor.
        left = Math.Clamp(left, monitor.X, Math.Max(monitor.X, monitor.X + monitor.Width - width));
        return new PixelRect(left, top, width, height);
    }
}
