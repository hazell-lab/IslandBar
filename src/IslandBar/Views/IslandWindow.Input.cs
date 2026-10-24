using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using IslandBar.Interop;

namespace IslandBar.Views;

// Mouse, keyboard, hot keys and the "click outside" hook.
public partial class IslandWindow
{
    private const int ToggleHotKeyId = 1, EscapeHotKeyId = 2;

    private readonly DispatcherTimer _hoverInTimer = new() { Interval = TimeSpan.FromMilliseconds(120) };
    private readonly DispatcherTimer _hoverOutTimer = new() { Interval = TimeSpan.FromMilliseconds(260) };
    private NativeMethods.LowLevelMouseProc? _mouseProc;   // stored so it is not garbage collected
    private IntPtr _mouseHook;
    private bool _escapeHotKeyOn;
    private bool _keyboardMode;
    private IntPtr _previousForeground;

    /// <summary>True if Ctrl+Alt+I could be registered (another app may already own it).</summary>
    public bool ToggleHotKeyAvailable { get; private set; }

    private void WireInput()
    {
        Island.MouseEnter += (_, _) => { _hoverOutTimer.Stop(); _hoverInTimer.Start(); };
        Island.MouseLeave += (_, _) => { _hoverInTimer.Stop(); _hoverOutTimer.Start(); };

        _hoverInTimer.Tick += (_, _) =>
        {
            _hoverInTimer.Stop();
            if (Island.IsMouseOver && _mode == IslandMode.Compact && _settings.Current.HoverPreview)
                SetMode(IslandMode.Peek);
        };
        _hoverOutTimer.Tick += (_, _) =>
        {
            _hoverOutTimer.Stop();
            if (!Island.IsMouseOver && _mode == IslandMode.Peek) SetMode(IslandMode.Compact);
        };

        // A click on the island body opens it. Buttons handle their own clicks.
        Island.PreviewMouseLeftButtonUp += (_, e) =>
        {
            if (_mode != IslandMode.Expanded && !IsInsideButton(e.OriginalSource as DependencyObject))
                SetMode(IslandMode.Expanded);
        };

        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) { Collapse(); e.Handled = true; }
        };

        // If the user clicks another window while in keyboard mode, close the island.
        Deactivated += (_, _) => { if (_keyboardMode) Collapse(); };
    }

    private static bool IsInsideButton(DependencyObject? element)
    {
        while (element is not null)
        {
            if (element is System.Windows.Controls.Primitives.ButtonBase) return true;
            element = System.Windows.Media.VisualTreeHelper.GetParent(element) ?? LogicalTreeHelper.GetParent(element);
        }
        return false;
    }

    // ---------- Global hot keys ----------

    private void RegisterToggleHotKey()
    {
        ToggleHotKeyAvailable = NativeMethods.RegisterHotKey(_hwnd, ToggleHotKeyId,
            NativeMethods.MOD_CONTROL | NativeMethods.MOD_ALT | NativeMethods.MOD_NOREPEAT, NativeMethods.VK_I);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY)
        {
            var id = wParam.ToInt32();
            if (id == ToggleHotKeyId)
            {
                if (_mode == IslandMode.Expanded) Collapse(); else EnterKeyboardMode();
                handled = true;
            }
            else if (id == EscapeHotKeyId)
            {
                Collapse();
                handled = true;
            }
        }
        return IntPtr.Zero;
    }

    /// <summary>
    /// Ctrl+Alt+I opens the island and gives it the keyboard so you can Tab between buttons.
    /// (Mouse clicks never take the keyboard. This is the only time the island takes focus.)
    /// </summary>
    private void EnterKeyboardMode()
    {
        if (Visibility != Visibility.Visible) return;
        _previousForeground = NativeMethods.GetForegroundWindow();
        _keyboardMode = true;
        SetNoActivate(false);
        SetMode(IslandMode.Expanded);
        Activate();
        Dispatcher.InvokeAsync(() =>
        {
            var first = TabMedia.Visibility == Visibility.Visible && MediaPage.Visibility == Visibility.Visible
                ? (UIElement)PlayPauseButton : StartButton;
            first.Focus();
            Keyboard.Focus(first);
        }, DispatcherPriority.Loaded);
    }

    private void LeaveKeyboardMode()
    {
        if (!_keyboardMode) return;
        _keyboardMode = false;
        SetNoActivate(true);
        if (_previousForeground != IntPtr.Zero) NativeMethods.SetForegroundWindow(_previousForeground);
        _previousForeground = IntPtr.Zero;
    }

    // ---------- Expanded start / end ----------

    private void OnExpandedStarted()
    {
        _hoverInTimer.Stop();
        _hoverOutTimer.Stop();

        // Escape works without focus because we register it only while the island is open.
        if (!_escapeHotKeyOn)
            _escapeHotKeyOn = NativeMethods.RegisterHotKey(_hwnd, EscapeHotKeyId, NativeMethods.MOD_NOREPEAT, NativeMethods.VK_ESCAPE);

        InstallMouseHook();
        RefreshAllContent();
        UpdateProgressTimer();
    }

    private void OnExpandedEnded()
    {
        if (_escapeHotKeyOn)
        {
            NativeMethods.UnregisterHotKey(_hwnd, EscapeHotKeyId);
            _escapeHotKeyOn = false;
        }
        RemoveMouseHook();
        LeaveKeyboardMode();
        UpdateProgressTimer();
    }

    // ---------- Click outside to close ----------

    private void InstallMouseHook()
    {
        if (_mouseHook != IntPtr.Zero) return;
        _mouseProc = MouseHookCallback;
        _mouseHook = NativeMethods.SetWindowsHookEx(NativeMethods.WH_MOUSE_LL, _mouseProc,
            NativeMethods.GetModuleHandle(null), 0);
    }

    private void RemoveMouseHook()
    {
        if (_mouseHook == IntPtr.Zero) return;
        NativeMethods.UnhookWindowsHookEx(_mouseHook);
        _mouseHook = IntPtr.Zero;
        _mouseProc = null;
    }

    // Must be fast. We only check the position and hand the work to the UI thread.
    private IntPtr MouseHookCallback(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0)
        {
            var message = wParam.ToInt32();
            if (message is NativeMethods.WM_LBUTTONDOWN or NativeMethods.WM_RBUTTONDOWN or NativeMethods.WM_MBUTTONDOWN)
            {
                var data = System.Runtime.InteropServices.Marshal.PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lParam);
                var x = data.pt.X;
                var y = data.pt.Y;
                Dispatcher.InvokeAsync(() => { if (_mode == IslandMode.Expanded && !IsScreenPointInsideIsland(x, y)) Collapse(); });
            }
        }
        return NativeMethods.CallNextHookEx(_mouseHook, code, wParam, lParam);   // never swallow the click
    }

    private bool IsScreenPointInsideIsland(int screenX, int screenY)
    {
        var topLeft = Island.PointToScreen(new Point(0, 0));
        var bottomRight = Island.PointToScreen(new Point(Island.ActualWidth, Island.ActualHeight));
        return screenX >= topLeft.X && screenX <= bottomRight.X && screenY >= topLeft.Y && screenY <= bottomRight.Y;
    }

    // ---------- Cleanup ----------

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        RemoveMouseHook();
        if (_hwnd != IntPtr.Zero)
        {
            NativeMethods.UnregisterHotKey(_hwnd, ToggleHotKeyId);
            NativeMethods.UnregisterHotKey(_hwnd, EscapeHotKeyId);
        }
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        _alertTimer.Stop();
        _hoverInTimer.Stop();
        _hoverOutTimer.Stop();
        _progressTimer.Stop();
    }
}
