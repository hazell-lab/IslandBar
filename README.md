# IslandBar — a Dynamic Island for Windows 11

A floating black pill at the top of your screen. It shows what is playing, pops up when the volume or battery changes, and has a countdown timer. Hover to peek, click to open, click outside or press **Esc** to close.

## Why this stack

**.NET 8 + WPF (C#).** WPF gives smooth animated, see-through windows. .NET can call the Windows media, battery and audio APIs directly, so there is no web engine to bundle (an Electron app would add about 150 MB and more idle memory). The only NuGet package is **NAudio.Wasapi 2.2.1** (MIT license), a thin wrapper around the Windows Core Audio API for volume events.

## Requirements

- Windows 10 version 2004 (build 19041) or newer. Windows 11 is the target.
- To **build**: [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (x64). Visual Studio 2022 is optional.
- To **run a framework-dependent build**: the [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0). The self-contained build needs nothing.
- No accounts, no internet, no backend. Admin rights are not needed.

## Run it

Open PowerShell in this folder:

```powershell
dotnet run --project src/IslandBar/IslandBar.csproj
```

Or open `IslandBar.sln` in Visual Studio 2022 and press F5 (startup project: `IslandBar`).

The island appears at the top-center of your main monitor. Look for the tray icon (near the clock; it may be under the ^ arrow). Double-click it, or right-click → **Settings…**.

Run the logic checks any time: `dotnet run --project tests/IslandBar.CoreChecks`

## Package it

```powershell
.\scripts\publish.ps1
```

This makes two zips in `publish\`:

| Output | Size | Needs on the other PC |
|---|---|---|
| `IslandBar-framework-dependent.zip` | small | .NET 8 Desktop Runtime |
| `IslandBar-self-contained.zip` | ~150 MB (single exe) | nothing |

Unzip anywhere and run `IslandBar.exe`. Windows SmartScreen may warn about an unsigned app; code signing needs a certificate, which this project does not include.

## How to use

| Action | What happens |
|---|---|
| Hover the pill | Peek: title/artist, or timer time |
| Click the pill | Opens the full island (Media and Timer tabs) |
| Click anywhere else, or press **Esc** | Closes it |
| **Ctrl+Alt+I** | Opens the island *and gives it the keyboard*: Tab / Shift+Tab move between buttons, Space/Enter press them, Esc closes and gives focus back to your previous window |
| Change the volume | A volume pop-up appears for about 2 seconds |
| Plug in / unplug, or battery hits 20% and 10% | A battery pop-up appears |
| Timer finishes | "Timer finished" pop-up stays up to 20 seconds, with a Dismiss button, and plays a sound |

**Keyboard note.** Mouse clicks never take keyboard focus from your other apps (the window is "no-activate"). Because of that, Esc after a mouse click works through a global Esc hot key that is only registered *while the island is open*. If you want to use the keyboard on the island itself, use Ctrl+Alt+I.

## Settings

Position (monitor, horizontal offset, distance from top), size (70–150%), animation intensity (Off / Gentle / Normal / Playful), follow Windows' "Show animations" setting, hover preview, feature toggles (media, volume, battery, timer, timer sound), always on top, hide in fullscreen, start with Windows. Settings are saved to `%APPDATA%\IslandBar\settings.json` and apply instantly.

## Code map

```
src/IslandBar.Core/          Plain .NET code with no Windows parts (so it can be tested anywhere)
  AppSettings.cs             Settings model, JSON load/save, value limits
  CountdownTimer.cs          Timer state machine: start / pause / resume / cancel / finish
  BatteryAlertPolicy.cs      Decides when a battery change deserves a pop-up
  Placement.cs               Works out the window rectangle (monitor, DPI, offset)
  MediaTimeline.cs           Estimates the track position between Windows updates
  FullscreenLogic.cs         Maps Windows' notification state to "fullscreen or not"
src/IslandBar/               The WPF app
  App.xaml(.cs)              Start-up, wiring, quit. Keeps running when Settings closes
  Views/IslandWindow.xaml    The island's look (compact, peek, expanded, 3 alerts)
  Views/IslandWindow.xaml.cs State machine + animations (start here to change motion)
  Views/IslandWindow.Placement.cs  Monitor, DPI and window-size logic, no-activate style
  Views/IslandWindow.Input.cs      Hover, click, Esc, Ctrl+Alt+I, "click outside" hook
  Views/IslandWindow.Content.cs    Puts media / timer / volume / battery data on screen
  Views/SettingsWindow.*     The settings window
  Views/EqualizerBars.*, BatteryIcon.*   Small reusable controls
  Services/MediaService.cs   Windows media session (track, art, play/pause/next/previous)
  Services/VolumeService.cs  System volume events (Core Audio)
  Services/BatteryService.cs Battery level and charging events
  Services/TimerService.cs   Runs the countdown, ticks only while it is running
  Services/FullscreenWatcher.cs  Detects fullscreen apps
  Services/TrayService.cs    Tray icon and menu
  Services/SettingsService.cs / StartupService.cs / SingleInstance.cs
  Interop/NativeMethods.cs   The few Windows API calls we need
  Themes/Icons.xaml          Original vector icons (drawn for this project)
  Themes/Styles.xaml         Colors and button styles (change the accent color here)
  Assets/app.ico             Original app icon
tests/IslandBar.CoreChecks/  Dependency-free test runner for the Core library
```

Common edits: accent color → `Themes/Styles.xaml` (`AccentColor`); island sizes → `SizeOf()` in `IslandWindow.xaml.cs` (and the matching `Width`/`Height` of each view in `IslandWindow.xaml`); alert length → `ShowAlert(...)` calls in `IslandWindow.Content.cs`.

## APIs used (all documented)

- Media: `Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager` (event-driven)
- Volume: Core Audio `IAudioEndpointVolume` change notifications + `IMMNotificationClient` for device changes (through NAudio)
- Battery: `Windows.Devices.Power.Battery.AggregateBattery.ReportUpdated`, with `SystemInformation.PowerStatus` as a fallback
- Fullscreen: `SHQueryUserNotificationState` + `SetWinEventHook(EVENT_SYSTEM_FOREGROUND)`
- Window: `WS_EX_NOACTIVATE`, `WS_EX_TOOLWINDOW`, `SetWindowPos`, `RegisterHotKey`, a low-level mouse hook (`WH_MOUSE_LL`, only while the island is open, never blocks clicks), `GetDpiForMonitor`
- Start with Windows: `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`

No special permissions are needed. The mouse hook only looks at *where* a click happened (to close the island) and does not record anything.

## Limitations (please read)

- **Media works only with apps that publish to Windows' media controls** (Spotify, Edge/Chrome, Windows Media Player, VLC with its media-key plugin, and so on). Windows chooses which session is "current"; IslandBar follows that choice. Some apps give no album art or no timeline; the island then shows a music-note icon and hides the progress bar.
- **Fullscreen detection has no Windows event for "an app just went fullscreen".** IslandBar reacts to foreground changes and also checks every 2 seconds while "Hide during fullscreen" is on. Exclusive-fullscreen games are detected by Windows; some borderless-window apps may not be. It does not check which monitor the fullscreen app is on.
- **Always on top** puts the island above normal windows. Windows can still draw a few system surfaces (like the lock screen, or a fullscreen exclusive game) above it.
- **Faint shadow:** the shadow is shown only while the island is open or an alert is up. During that time the window is a few pixels larger than the black shape, and that thin margin can intercept a click.
- The Ctrl+Alt+I hot key is fixed. If another app already owns it, keyboard mode is unavailable (the mouse still works).
- Battery alerts fire on plug/unplug and at 20% and 10%. On desktops without a battery the battery features are hidden.
- The x64 build is the supported target (it also runs on ARM64 Windows under emulation).

## What was tested

See `TESTING.md`.
