using System;
using System.Windows.Forms;
using System.Windows.Threading;
using Microsoft.Win32;
using Windows.Devices.Power;
using IslandBar.Core;

namespace IslandBar.Services;

/// <summary>
/// Reads battery level and charging state.
/// Main source: the Windows aggregate battery report, which raises an event on every change.
/// Fallback: the classic power status, refreshed when Windows raises a power event.
/// Desktops with no battery report HasBattery = false.
/// </summary>
public sealed class BatteryService : IDisposable
{
    private readonly Dispatcher _ui;
    private Battery? _battery;
    private bool _disposed;

    public BatterySnapshot Current { get; private set; }

    /// <summary>Raised on the UI thread whenever the reading changes.</summary>
    public event Action? Changed;

    public BatteryService(Dispatcher ui)
    {
        _ui = ui;
        try
        {
            _battery = Battery.AggregateBattery;
            _battery.ReportUpdated += OnReportUpdated;
        }
        catch (Exception) { _battery = null; }

        SystemEvents.PowerModeChanged += OnPowerModeChanged;   // covers plug/unplug and resume from sleep
        Current = Read();
    }

    private void OnReportUpdated(Battery sender, object args) => Refresh();

    private void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs e) => Refresh();

    private void Refresh() => _ui.InvokeAsync(() =>
    {
        if (_disposed) return;
        var next = Read();
        if (next == Current) return;
        Current = next;
        Changed?.Invoke();
    });

    private BatterySnapshot Read()
    {
        var power = SystemInformation.PowerStatus;
        var noBattery = power.BatteryChargeStatus.HasFlag(BatteryChargeStatus.NoSystemBattery);
        var pluggedIn = power.PowerLineStatus == PowerLineStatus.Online;

        try
        {
            if (_battery is not null)
            {
                var report = _battery.GetReport();
                if (report.Status == Windows.System.Power.BatteryStatus.NotPresent)
                    return new BatterySnapshot(false, 0, pluggedIn, false);

                if (report.FullChargeCapacityInMilliwattHours is int full && full > 0 &&
                    report.RemainingCapacityInMilliwattHours is int left)
                {
                    var percent = (int)Math.Round(Math.Clamp(left * 100.0 / full, 0, 100));
                    var charging = report.Status == Windows.System.Power.BatteryStatus.Charging;
                    return new BatterySnapshot(true, percent, pluggedIn, charging);
                }
            }
        }
        catch (Exception) { /* fall through to the classic reading */ }

        if (noBattery) return new BatterySnapshot(false, 0, pluggedIn, false);
        var fallbackPercent = (int)Math.Round(Math.Clamp(power.BatteryLifePercent, 0f, 1f) * 100);
        var isCharging = power.BatteryChargeStatus.HasFlag(BatteryChargeStatus.Charging);
        return new BatterySnapshot(true, fallbackPercent, pluggedIn, isCharging);
    }

    public void Dispose()
    {
        _disposed = true;
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        if (_battery is not null) _battery.ReportUpdated -= OnReportUpdated;
        _battery = null;
    }
}
