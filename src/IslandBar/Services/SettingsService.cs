using System;
using System.IO;
using System.Windows.Threading;
using IslandBar.Core;

namespace IslandBar.Services;

/// <summary>Loads and saves settings in %APPDATA%\IslandBar\settings.json.</summary>
public sealed class SettingsService : IDisposable
{
    private readonly string _path;
    private readonly DispatcherTimer _saveTimer;   // waits a moment so sliders do not write the file constantly

    public AppSettings Current { get; private set; }
    public event Action? Changed;

    public SettingsService()
    {
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "IslandBar");
        _path = Path.Combine(folder, "settings.json");
        Current = Load();
        _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        _saveTimer.Tick += (_, _) => { _saveTimer.Stop(); SaveNow(); };
    }

    private AppSettings Load()
    {
        try { return File.Exists(_path) ? AppSettings.FromJson(File.ReadAllText(_path)) : new AppSettings(); }
        catch (Exception) { return new AppSettings(); }   // unreadable file: start fresh
    }

    /// <summary>Call after changing Current. Tells the app and saves soon.</summary>
    public void NotifyChanged()
    {
        Current.Clamp();
        Changed?.Invoke();
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    public void SaveNow()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            var temp = _path + ".tmp";
            File.WriteAllText(temp, Current.ToJson());
            File.Move(temp, _path, overwrite: true);   // swap in one step so a crash cannot leave half a file
        }
        catch (Exception) { /* disk full or locked: keep running with in-memory settings */ }
    }

    public void Dispose()
    {
        _saveTimer.Stop();
        SaveNow();
    }
}
