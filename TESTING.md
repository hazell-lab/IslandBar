# Testing status

Checked on Windows 11 on 16 November 2026 with .NET SDK 10.0.402.

## Automated
- `dotnet build -c Release` builds the whole solution, including the WPF app: 0 warnings, 0 errors.
- `dotnet run --project tests/IslandBar.CoreChecks`: **38 of 38 checks pass** (timer states, settings clamping and JSON, battery alert rules, placement, DPI and multi-monitor math, track-position math, fullscreen mapping).

## Checked by hand
Media controls, volume and battery pop-ups, the tray icon, animations, placement, click-through, fullscreen hiding and start-with-Windows depend on live Windows APIs, so they are checked with the plan below rather than by automated tests.

## Quick manual test plan (5 minutes)
1. `dotnet run --project src/IslandBar/IslandBar.csproj` – the pill appears top-center.
2. Play music in Spotify or a browser – art/title appear; hover to peek; click to open; try prev / play-pause / next.
3. Change the volume – pop-up appears. Mute – "Muted".
4. Timer tab – pick 1 min (or edit the duration), Start, Pause, Resume, Cancel, then let one finish.
5. Unplug/plug the charger (laptop) – battery pop-up.
6. Click another window – island closes. Open it, press Esc – closes. Press Ctrl+Alt+I, Tab around, Esc.
7. Settings via the tray: move it, resize it, switch monitor, turn animation Off, close Settings – island keeps running. Tray → Quit.
