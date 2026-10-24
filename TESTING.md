# Testing status (honest report)

The project was written in a Linux sandbox that has **no Windows and no access to NuGet**, so the WPF app could **not be compiled or run** there.

## Done and passing
- `IslandBar.Core` builds, and `tests/IslandBar.CoreChecks` runs: **38 of 38 checks pass** (timer states, settings clamping/JSON, battery alert rules, placement/DPI/multi-monitor math, track-position math, fullscreen mapping).
- Every C# file in the WPF project was parsed with the .NET compiler's parser: **no syntax errors**.
- All XAML files are well-formed; every control name used in code exists in XAML, and every resource key resolves.
- The vector icons were rendered in a browser and checked by eye.

## NOT verified (needs a Windows machine)
- That the WPF project compiles (it should; if it reports an error, please send me the message).
- Real behavior: media controls with a live player, volume/battery events, tray icon, animations, multi-monitor and DPI placement, click-through, fullscreen hiding, start-with-Windows.
- The NAudio and WinRT API calls are written from their documentation, not compiled against the packages.

## Quick manual test plan (5 minutes)
1. `dotnet run --project src/IslandBar/IslandBar.csproj` – the pill appears top-center.
2. Play music in Spotify or a browser – art/title appear; hover to peek; click to open; try prev / play-pause / next.
3. Change the volume – pop-up appears. Mute – "Muted".
4. Timer tab – pick 1 min (or edit the duration), Start, Pause, Resume, Cancel, then let one finish.
5. Unplug/plug the charger (laptop) – battery pop-up.
6. Click another window – island closes. Open it, press Esc – closes. Press Ctrl+Alt+I, Tab around, Esc.
7. Settings via the tray: move it, resize it, switch monitor, turn animation Off, close Settings – island keeps running. Tray → Quit.
