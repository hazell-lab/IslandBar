using System;
using System.Linq;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Forms;
using IslandBar.Core;
using IslandBar.Interop;
using Microsoft.Win32;

namespace IslandBar.Views;

// Position, size, monitor, DPI and the special Windows window styles.
public partial class IslandWindow
{
    private IntPtr _hwnd;
    private Size _lastBoxSize = new(150, 36);

    /// <summary>The scale from the Size setting (1.0 = 100%).</summary>
    private double UserScale => _settings.Current.SizePercent / 100.0;

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _hwnd = new WindowInteropHelper(this).Handle;

        // No-activate: clicking the island never steals keyboard focus.
        // Tool window: keeps it out of Alt+Tab.
        SetNoActivate(true);

        HwndSource.FromHwnd(_hwnd)?.AddHook(WndProc);
        RegisterToggleHotKey();

        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        DpiChanged += (_, _) => Dispatcher.InvokeAsync(() => ApplyWindowBox(_lastBoxSize));
        ApplyWindowBox(_targetSize);
    }

    private void SetNoActivate(bool noActivate)
    {
        if (_hwnd == IntPtr.Zero) return;
        var style = NativeMethods.GetWindowLongPtr(_hwnd, NativeMethods.GWL_EXSTYLE).ToInt64();
        style |= NativeMethods.WS_EX_TOOLWINDOW;
        if (noActivate) style |= NativeMethods.WS_EX_NOACTIVATE;
        else style &= ~(long)NativeMethods.WS_EX_NOACTIVATE;
        NativeMethods.SetWindowLongPtr(_hwnd, NativeMethods.GWL_EXSTYLE, new IntPtr(style));
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e) =>
        Dispatcher.InvokeAsync(() => ApplyWindowBox(_lastBoxSize));

    private Screen GetTargetScreen()
    {
        var name = _settings.Current.MonitorDeviceName;
        return Screen.AllScreens.FirstOrDefault(s => s.DeviceName == name)
               ?? Screen.PrimaryScreen
               ?? Screen.AllScreens[0];
    }

    // Asks Windows for the real scaling of that monitor (125%, 150% ...).
    private double GetDpiScale(Screen screen)
    {
        var center = new NativeMethods.POINT
        {
            X = screen.Bounds.X + screen.Bounds.Width / 2,
            Y = screen.Bounds.Y + screen.Bounds.Height / 2,
        };
        var monitor = NativeMethods.MonitorFromPoint(center, 2 /* nearest */);
        if (monitor != IntPtr.Zero && NativeMethods.GetDpiForMonitor(monitor, 0, out var dpiX, out _) == 0 && dpiX > 0)
            return dpiX / 96.0;
        return VisualTreeHelper.GetDpi(this).DpiScaleX;
    }

    /// <summary>
    /// Moves and resizes the see-through window so it fits an island of the given size
    /// (in unscaled units) on the chosen monitor.
    /// </summary>
    private void ApplyWindowBox(Size islandSize)
    {
        _lastBoxSize = islandSize;
        if (_hwnd == IntPtr.Zero) return;

        var s = _settings.Current;
        var screen = GetTargetScreen();
        var dpi = GetDpiScale(screen);
        var bounds = new PixelRect(screen.Bounds.X, screen.Bounds.Y, screen.Bounds.Width, screen.Bounds.Height);

        var rect = Placement.ComputeWindowRect(bounds, dpi, s.OffsetX, s.TopMargin,
            islandSize.Width * UserScale, islandSize.Height * UserScale, Pad);

        NativeMethods.SetWindowPos(_hwnd, IntPtr.Zero, rect.X, rect.Y, rect.Width, rect.Height,
            NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE);
    }

    // Called at start and every time a setting changes.
    private void ApplySettings()
    {
        var s = _settings.Current;
        Topmost = s.AlwaysOnTop;
        Island.LayoutTransform = new ScaleTransform(UserScale, UserScale);
        ApplyWindowBox(_lastBoxSize);
        RefreshAllContent();
    }
}
