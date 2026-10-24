using System;
using System.Drawing;
using System.Windows;
using IslandBar.Services;
using IslandBar.Views;

namespace IslandBar;

public partial class App : System.Windows.Application
{
    private SingleInstance? _singleInstance;
    private SettingsService? _settings;
    private MediaService? _media;
    private VolumeService? _volume;
    private BatteryService? _battery;
    private TimerService? _timer;
    private FullscreenWatcher? _fullscreen;
    private TrayService? _tray;
    private IslandWindow? _island;
    private SettingsWindow? _settingsWindow;
    private bool _lastStartupValue;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstance = new SingleInstance(() => Dispatcher.InvokeAsync(ShowSettings));
        if (!_singleInstance.IsFirst)
        {
            Shutdown();
            return;
        }

        // A hiccup in one feature (for example a media app crashing) must not close the island.
        DispatcherUnhandledException += (_, args) => args.Handled = true;

        _settings = new SettingsService();
        _media = new MediaService(Dispatcher);
        _volume = new VolumeService(Dispatcher);
        _battery = new BatteryService(Dispatcher);
        _timer = new TimerService();
        _fullscreen = new FullscreenWatcher();

        _island = new IslandWindow(_settings, _media, _volume, _battery, _timer);
        _fullscreen.FullscreenChanged += hidden => _island.SetFullscreenHidden(hidden);
        _settings.Changed += ApplyBehaviorSettings;
        _island.Show();
        ApplyBehaviorSettings();

        _tray = new TrayService(LoadTrayIcon(),
            isIslandHidden: () => _island.IsUserHidden,
            toggleIsland: () => _island.SetUserHidden(!_island.IsUserHidden),
            openSettings: ShowSettings,
            isAlwaysOnTop: () => _settings.Current.AlwaysOnTop,
            setAlwaysOnTop: on => { _settings.Current.AlwaysOnTop = on; _settings.NotifyChanged(); },
            quit: Quit);

        _ = _media.StartAsync();   // media integration starts in the background

        // Launch with "--settings" to open the Settings window right away.
        if (e.Args.Length > 0 && e.Args[0] == "--settings") ShowSettings();
    }

    // Things that live outside the island window: start-up entry and fullscreen watching.
    private void ApplyBehaviorSettings()
    {
        if (_settings is null || _fullscreen is null) return;
        var s = _settings.Current;

        if (s.LaunchAtStartup != _lastStartupValue)
        {
            _lastStartupValue = s.LaunchAtStartup;
            StartupService.Apply(s.LaunchAtStartup);
        }

        if (s.HideInFullscreen) _fullscreen.Start(); else _fullscreen.Stop();
    }

    private static Icon LoadTrayIcon()
    {
        try
        {
            var stream = GetResourceStream(new Uri("pack://application:,,,/Assets/app.ico"))?.Stream;
            if (stream is not null) return new Icon(stream, new System.Drawing.Size(16, 16));
        }
        catch (Exception) { /* fall back below */ }
        return SystemIcons.Application;
    }

    private void ShowSettings()
    {
        if (_settings is null) return;
        if (_settingsWindow is null)
        {
            _settingsWindow = new SettingsWindow(_settings);
            _settingsWindow.Closed += (_, _) => _settingsWindow = null;   // closing it leaves the island running
            _settingsWindow.Show();
        }
        else
        {
            if (_settingsWindow.WindowState == WindowState.Minimized) _settingsWindow.WindowState = WindowState.Normal;
            _settingsWindow.Activate();
        }
    }

    private void Quit()
    {
        _tray?.Dispose();
        _tray = null;
        _settingsWindow?.Close();
        _island?.Close();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _fullscreen?.Dispose();
        _tray?.Dispose();
        _media?.Dispose();
        _volume?.Dispose();
        _battery?.Dispose();
        _timer?.Dispose();
        _settings?.Dispose();
        _singleInstance?.Dispose();
        base.OnExit(e);
    }
}
