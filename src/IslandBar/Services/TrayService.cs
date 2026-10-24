using System;
using System.Drawing;
using System.Windows.Forms;

namespace IslandBar.Services;

/// <summary>The notification-area (system tray) icon and its menu.</summary>
public sealed class TrayService : IDisposable
{
    private readonly NotifyIcon _icon;
    private readonly ToolStripMenuItem _showHide;
    private readonly ToolStripMenuItem _alwaysOnTop;

    public TrayService(Icon icon, Func<bool> isIslandHidden, Action toggleIsland, Action openSettings,
                       Func<bool> isAlwaysOnTop, Action<bool> setAlwaysOnTop, Action quit)
    {
        _showHide = new ToolStripMenuItem("Hide island");
        _showHide.Click += (_, _) => toggleIsland();

        var settings = new ToolStripMenuItem("Settings…");
        settings.Click += (_, _) => openSettings();

        _alwaysOnTop = new ToolStripMenuItem("Always on top") { CheckOnClick = true };
        _alwaysOnTop.Click += (_, _) => setAlwaysOnTop(_alwaysOnTop.Checked);

        var exit = new ToolStripMenuItem("Quit");
        exit.Click += (_, _) => quit();

        var menu = new ContextMenuStrip();
        menu.Items.AddRange(new ToolStripItem[] { _showHide, settings, _alwaysOnTop, new ToolStripSeparator(), exit });
        // Refresh the labels each time the menu opens.
        menu.Opening += (_, _) =>
        {
            _showHide.Text = isIslandHidden() ? "Show island" : "Hide island";
            _alwaysOnTop.Checked = isAlwaysOnTop();
        };

        _icon = new NotifyIcon
        {
            Icon = icon,
            Text = "IslandBar",
            ContextMenuStrip = menu,
            Visible = true,
        };
        _icon.DoubleClick += (_, _) => openSettings();
    }

    public void Dispose()
    {
        _icon.Visible = false;   // removes the icon right away (no ghost icon)
        _icon.ContextMenuStrip?.Dispose();
        _icon.Dispose();
    }
}
