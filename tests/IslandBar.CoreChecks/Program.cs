using IslandBar.Core;

int passed = 0, failed = 0;
void Check(string name, bool ok)
{
    if (ok) { passed++; Console.WriteLine($"  ok   {name}"); }
    else { failed++; Console.WriteLine($"  FAIL {name}"); }
}

// ---- CountdownTimer ----
var clock = TimeSpan.Zero;
var timer = new CountdownTimer(() => clock);
var finishedCount = 0;
timer.Finished += () => finishedCount++;

timer.SetDuration(TimeSpan.FromSeconds(60));
Check("idle shows full duration", timer.Remaining == TimeSpan.FromSeconds(60));
timer.Start();
clock = TimeSpan.FromSeconds(20);
Check("running counts down", timer.Remaining == TimeSpan.FromSeconds(40));
timer.Pause();
clock = TimeSpan.FromSeconds(500);
Check("paused keeps remaining", timer.Remaining == TimeSpan.FromSeconds(40));
Check("cannot change duration while paused", !timer.SetDuration(TimeSpan.FromMinutes(9)));
timer.Resume();
clock = TimeSpan.FromSeconds(530);
timer.Tick();
Check("not finished early", timer.State == TimerState.Running && finishedCount == 0);
clock = TimeSpan.FromSeconds(540);
timer.Tick(); timer.Tick();
Check("finishes once", timer.State == TimerState.Finished && finishedCount == 1);
timer.Cancel();
Check("cancel returns to idle", timer.State == TimerState.Idle && timer.Remaining == timer.Duration);
timer.SetDuration(TimeSpan.FromSeconds(1));
Check("duration has 10s minimum", timer.Duration == TimeSpan.FromSeconds(10));
timer.SetDuration(TimeSpan.FromDays(1));
Check("duration has 3h maximum", timer.Duration == TimeSpan.FromHours(3));
Check("format mm:ss", CountdownTimer.Format(TimeSpan.FromSeconds(75)) == "01:15");
Check("format h:mm:ss", CountdownTimer.Format(TimeSpan.FromSeconds(3725)) == "1:02:05");
Check("format rounds up", CountdownTimer.Format(TimeSpan.FromMilliseconds(400)) == "00:01");

// ---- AppSettings ----
Check("bad json gives defaults", AppSettings.FromJson("{not json").SizePercent == 100);
Check("empty json gives defaults", AppSettings.FromJson("").Animation == AnimationIntensity.Normal);
var odd = AppSettings.FromJson("{\"SizePercent\": 900, \"TopMargin\": -5, \"TimerDefaultSeconds\": 1}");
Check("values are clamped", odd.SizePercent == 150 && odd.TopMargin == 0 && odd.TimerDefaultSeconds == 10);
var custom = new AppSettings { Animation = AnimationIntensity.Playful, OffsetX = 40, EnableBattery = false };
var round = AppSettings.FromJson(custom.ToJson());
Check("settings round-trip", round.Animation == AnimationIntensity.Playful && round.OffsetX == 40 && !round.EnableBattery);
Check("enum saved as text", custom.ToJson().Contains("\"Playful\""));

// ---- BatteryAlertPolicy ----
var policy = new BatteryAlertPolicy();
Check("first reading is silent", policy.Evaluate(new(true, 50, false, false)) == BatteryAlertKind.None);
Check("plug in alerts", policy.Evaluate(new(true, 50, true, true)) == BatteryAlertKind.PluggedIn);
Check("unplug alerts", policy.Evaluate(new(true, 50, false, false)) == BatteryAlertKind.Unplugged);
Check("small change is silent", policy.Evaluate(new(true, 49, false, false)) == BatteryAlertKind.None);
policy.Evaluate(new(true, 21, false, false));
Check("low alert at 20", policy.Evaluate(new(true, 20, false, false)) == BatteryAlertKind.Low);
Check("no repeat low alert", policy.Evaluate(new(true, 19, false, false)) == BatteryAlertKind.None);
policy.Evaluate(new(true, 11, false, false));
Check("critical alert at 10", policy.Evaluate(new(true, 10, false, false)) == BatteryAlertKind.Critical);
Check("no battery is silent", new BatteryAlertPolicy().Evaluate(new(false, 0, true, false)) == BatteryAlertKind.None);

// ---- Placement ----
var screen = new PixelRect(0, 0, 1920, 1080);
var rect = Placement.ComputeWindowRect(screen, 1.0, 0, 8, 150, 36, 6);
Check("centered horizontally", rect.X + rect.Width / 2 == 960);
Check("island top = margin", rect.Y + 6 == 8);
var scaled = Placement.ComputeWindowRect(screen, 1.5, 0, 8, 150, 36, 6);
Check("dpi scales width", scaled.Width == (int)Math.Ceiling(162 * 1.5));
var second = new PixelRect(1920, 0, 2560, 1440);
var onSecond = Placement.ComputeWindowRect(second, 1.0, 0, 8, 392, 188, 6);
Check("second monitor centered", onSecond.X + onSecond.Width / 2 == 1920 + 1280);
var far = Placement.ComputeWindowRect(screen, 1.0, 5000, 8, 392, 188, 6);
Check("offset stays on screen", far.X + far.Width <= 1920);

// ---- MediaTimeline ----
var t0 = DateTimeOffset.UnixEpoch;
var est = MediaTimeline.EstimatePosition(TimeSpan.FromSeconds(10), t0, t0.AddSeconds(5), true, 1.0, TimeSpan.FromSeconds(100));
Check("playing adds elapsed time", est == TimeSpan.FromSeconds(15));
est = MediaTimeline.EstimatePosition(TimeSpan.FromSeconds(10), t0, t0.AddSeconds(5), false, 1.0, TimeSpan.FromSeconds(100));
Check("paused does not move", est == TimeSpan.FromSeconds(10));
est = MediaTimeline.EstimatePosition(TimeSpan.FromSeconds(98), t0, t0.AddSeconds(5), true, 1.0, TimeSpan.FromSeconds(100));
Check("position clamps to length", est == TimeSpan.FromSeconds(100));
Check("progress unknown length = 0", MediaTimeline.Progress(TimeSpan.FromSeconds(5), TimeSpan.Zero) == 0);
Check("progress half", Math.Abs(MediaTimeline.Progress(TimeSpan.FromSeconds(50), TimeSpan.FromSeconds(100)) - 0.5) < 1e-9);

// ---- FullscreenLogic ----
Check("busy = fullscreen", FullscreenLogic.IsFullscreen(2));
Check("d3d fullscreen", FullscreenLogic.IsFullscreen(3));
Check("normal desktop is not", !FullscreenLogic.IsFullscreen(5));

Console.WriteLine($"\n{passed} passed, {failed} failed");
return failed == 0 ? 0 : 1;
