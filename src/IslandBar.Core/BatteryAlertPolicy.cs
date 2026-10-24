namespace IslandBar.Core;

/// <summary>A battery reading. HasBattery is false on desktops.</summary>
public readonly record struct BatterySnapshot(bool HasBattery, int Percent, bool IsPluggedIn, bool IsCharging);

public enum BatteryAlertKind { None, PluggedIn, Unplugged, Low, Critical }

/// <summary>Decides when a battery change deserves a pop-up. Remembers the last reading.</summary>
public sealed class BatteryAlertPolicy
{
    public const int LowPercent = 20, CriticalPercent = 10;
    private BatterySnapshot? _last;

    public BatteryAlertKind Evaluate(BatterySnapshot now)
    {
        var previous = _last;
        _last = now;

        // No battery, or the very first reading: nothing to compare with.
        if (!now.HasBattery || previous is not { HasBattery: true } before) return BatteryAlertKind.None;

        if (now.IsPluggedIn != before.IsPluggedIn)
            return now.IsPluggedIn ? BatteryAlertKind.PluggedIn : BatteryAlertKind.Unplugged;

        if (!now.IsPluggedIn)
        {
            if (before.Percent > CriticalPercent && now.Percent <= CriticalPercent) return BatteryAlertKind.Critical;
            if (before.Percent > LowPercent && now.Percent <= LowPercent) return BatteryAlertKind.Low;
        }
        return BatteryAlertKind.None;
    }
}
