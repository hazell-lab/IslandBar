using System;
using System.Windows.Controls;
using System.Windows.Media;

namespace IslandBar.Views;

public partial class BatteryIcon : UserControl
{
    private const double FullFillWidth = 19.6;

    public BatteryIcon() => InitializeComponent();

    /// <summary>Sets how full the icon looks. Green when charging or healthy, orange when low, red when critical.</summary>
    public void SetLevel(int percent, bool charging)
    {
        percent = Math.Clamp(percent, 0, 100);
        Fill.Width = Math.Max(2, FullFillWidth * percent / 100.0);
        var key = charging || percent > 20 ? "GreenBrush" : percent > 10 ? "AccentBrush" : "RedBrush";
        Fill.Background = (Brush)FindResource(key);
    }
}
