using System;
using System.Diagnostics;
using Microsoft.Win32;

namespace IslandBar.Services;

/// <summary>Turns "start with Windows" on or off using the per-user Run registry key (no admin needed).</summary>
public static class StartupService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "IslandBar";

    public static void Apply(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            if (key is null) return;
            if (enabled)
            {
                var exe = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;
                if (exe is not null) key.SetValue(ValueName, $"\"{exe}\"");
            }
            else key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
        catch (Exception) { /* blocked by policy: ignore */ }
    }
}
