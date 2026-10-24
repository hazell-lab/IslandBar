using System;
using System.Media;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using IslandBar.Core;
using IslandBar.Services;

namespace IslandBar.Views;

// Shows data from the services: music, volume, battery and timer.
public partial class IslandWindow
{
    private const double VolumeBarWidth = 144, FallbackProgressWidth = 266;

    private static readonly MediaInfo EmptyMedia = new();
    private readonly BatteryAlertPolicy _batteryPolicy = new();
    private readonly DispatcherTimer _progressTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private BitmapSource? _artSource;
    private Brush? _artBrush;

    private MediaInfo CurrentMedia => _settings.Current.EnableMedia ? _media.Current : EmptyMedia;
    private bool TimerActive => _settings.Current.EnableTimer &&
        _timer.Countdown.State is TimerState.Running or TimerState.Paused;

    private void WireContent()
    {
        _media.Changed += UpdateMediaUi;
        _volume.VolumeChanged += OnVolumeChanged;
        _battery.Changed += OnBatteryChanged;
        _timer.Updated += UpdateTimerUi;
        _timer.Finished += OnTimerFinished;
        _settings.Changed += ApplySettings;
        _batteryPolicy.Evaluate(_battery.Current);   // first reading: remember it, no alert

        _progressTimer.Tick += (_, _) => UpdateProgress();

        PrevButton.Click += async (_, _) => await _media.PreviousAsync();
        NextButton.Click += async (_, _) => await _media.NextAsync();
        PlayPauseButton.Click += async (_, _) => await _media.TogglePlayPauseAsync();

        TabMedia.Checked += (_, _) => UpdatePages();
        TabTimer.Checked += (_, _) => UpdatePages();

        MinusButton.Click += (_, _) => _timer.SetDuration(_timer.Countdown.Duration - TimeSpan.FromMinutes(1));
        PlusButton.Click += (_, _) => _timer.SetDuration(_timer.Countdown.Duration + TimeSpan.FromMinutes(1));
        foreach (var child in PresetRow.Children)
        {
            if (child is Button preset && preset.Tag is string seconds)
                preset.Click += (_, _) => _timer.SetDuration(TimeSpan.FromSeconds(double.Parse(seconds)));
        }
        StartButton.Click += (_, _) =>
        {
            _settings.Current.TimerDefaultSeconds = (int)_timer.Countdown.Duration.TotalSeconds;
            _timer.Start();
        };
        PauseResumeButton.Click += (_, _) =>
        {
            if (_timer.Countdown.State == TimerState.Running) _timer.Pause(); else _timer.Resume();
        };
        CancelButton.Click += (_, _) => _timer.Cancel();
        DismissButton.Click += (_, _) => ClearAlert();

        _timer.SetDuration(TimeSpan.FromSeconds(_settings.Current.TimerDefaultSeconds));
    }

    private void RefreshAllContent()
    {
        UpdateMediaUi();
        UpdateTimerUi();
        UpdateBatteryChip();
        UpdatePages();
    }

    // ---------- Pages inside the expanded island ----------

    private void UpdatePages()
    {
        var s = _settings.Current;
        TabMedia.Visibility = s.EnableMedia ? Visibility.Visible : Visibility.Collapsed;
        TabTimer.Visibility = s.EnableTimer ? Visibility.Visible : Visibility.Collapsed;

        // If the checked tab was turned off, move to the other one.
        if (!s.EnableMedia && TabMedia.IsChecked == true && s.EnableTimer) TabTimer.IsChecked = true;
        if (!s.EnableTimer && TabTimer.IsChecked == true) TabMedia.IsChecked = true;

        var showTimer = s.EnableTimer && TabTimer.IsChecked == true;
        TimerPage.Visibility = showTimer ? Visibility.Visible : Visibility.Collapsed;
        MediaPage.Visibility = showTimer ? Visibility.Collapsed : Visibility.Visible;
        UpdateMediaUi();
        UpdateProgressTimer();
    }

    // ---------- Media ----------

    private void UpdateMediaUi()
    {
        var m = CurrentMedia;
        var s = _settings.Current;

        if (!ReferenceEquals(m.Art, _artSource))
        {
            _artSource = m.Art;
            if (m.Art is null) _artBrush = null;
            else
            {
                var brush = new ImageBrush(m.Art) { Stretch = Stretch.UniformToFill };
                brush.Freeze();
                _artBrush = brush;
            }
        }
        var title = string.IsNullOrWhiteSpace(m.Title) ? "Unknown title" : m.Title;
        var artist = string.IsNullOrWhiteSpace(m.Artist) ? m.Album : m.Artist;

        // Compact: picture on the left, status on the right.
        var timerActive = TimerActive;
        if (m.HasSession)
        {
            CompactArt.Visibility = Visibility.Visible;
            ShowArt(CompactArt, CompactNote, Geo("Icon.Note"));
        }
        else if (timerActive)
        {
            CompactArt.Visibility = Visibility.Visible;
            ShowArt(CompactArt, CompactNote, Geo("Icon.Timer"), forceIcon: true);
        }
        else CompactArt.Visibility = Visibility.Collapsed;

        // Peek
        ShowArt(PeekArt, PeekNote, Geo(m.HasSession ? "Icon.Note" : timerActive ? "Icon.Timer" : "Icon.Note"), forceIcon: !m.HasSession);
        if (m.HasSession) { PeekTitle.Text = title; PeekSubtitle.Text = artist; }
        else if (timerActive) { PeekTitle.Text = "Timer"; PeekSubtitle.Text = _timer.Countdown.State == TimerState.Paused ? "Paused" : "Counting down"; }
        else { PeekTitle.Text = "Nothing playing"; PeekSubtitle.Text = "Click to open"; }

        // Expanded
        ShowArt(ExpArt, ExpNote, Geo("Icon.Note"));
        ExpTitle.Text = title;
        ExpArtist.Text = artist;
        PlayPauseIcon.Data = Geo(m.IsPlaying ? "Icon.Pause" : "Icon.Play");
        PlayPauseButton.IsEnabled = m.HasSession && m.CanPlayPause;
        PrevButton.IsEnabled = m.HasSession && m.CanPrevious;
        NextButton.IsEnabled = m.HasSession && m.CanNext;

        var showInfo = m.HasSession ? Visibility.Visible : Visibility.Collapsed;
        MediaInfoPanel.Visibility = showInfo;
        MediaHint.Visibility = m.HasSession ? Visibility.Collapsed : Visibility.Visible;
        MediaHint.Text = s.EnableMedia ? "Nothing playing" : "Media is turned off in Settings";

        UpdateRightSide();
        UpdateProgress();
        UpdateProgressTimer();
        UpdateEqualizers();
    }

    private Geometry Geo(string key) => (Geometry)FindResource(key);

    // Shows the album picture, or an icon when there is no picture.
    private void ShowArt(Border border, Path icon, Geometry iconShape, bool forceIcon = false)
    {
        if (_artBrush is not null && !forceIcon)
        {
            border.Background = _artBrush;
            icon.Visibility = Visibility.Collapsed;
        }
        else
        {
            border.Background = (Brush)FindResource("SurfaceBrush");
            icon.Data = iconShape;
            icon.Visibility = Visibility.Visible;
        }
    }

    // Right side of the compact and peek views: timer text, equalizer, or a dot.
    private void UpdateRightSide()
    {
        var m = CurrentMedia;
        var timerActive = TimerActive;
        var text = CountdownTimer.Format(_timer.Countdown.Remaining);

        CompactTimerText.Text = text;
        PeekTimerText.Text = text;
        CompactTimerText.Visibility = timerActive ? Visibility.Visible : Visibility.Collapsed;
        PeekTimerText.Visibility = timerActive ? Visibility.Visible : Visibility.Collapsed;

        var showEq = !timerActive && m.IsPlaying;
        CompactEq.Visibility = showEq ? Visibility.Visible : Visibility.Collapsed;
        PeekEq.Visibility = showEq ? Visibility.Visible : Visibility.Collapsed;
        CompactDot.Visibility = !timerActive && !showEq ? Visibility.Visible : Visibility.Collapsed;
    }

    // Dancing bars run only while they are on screen, to save CPU.
    private void UpdateEqualizers()
    {
        var playing = CurrentMedia.IsPlaying;
        var animate = GetMotion().Enabled;
        CompactEq.SetPlaying(playing && _shown == IslandView.Compact, animate);
        PeekEq.SetPlaying(playing && _shown == IslandView.Peek, animate);
    }

    // ---------- Progress bar ----------

    private void UpdateProgressTimer()
    {
        var needed = _mode == IslandMode.Expanded && MediaPage.Visibility == Visibility.Visible &&
                     CurrentMedia.IsPlaying && CurrentMedia.Duration > TimeSpan.Zero;
        if (needed && !_progressTimer.IsEnabled) _progressTimer.Start();
        else if (!needed && _progressTimer.IsEnabled) _progressTimer.Stop();
    }

    private void UpdateProgress()
    {
        var m = CurrentMedia;
        if (!m.HasSession || m.Duration <= TimeSpan.Zero)
        {
            ProgressFill.Width = 0;
            PosText.Text = "";
            DurText.Text = "";
            return;
        }
        var position = IslandBar.Core.MediaTimeline.EstimatePosition(m.Position, m.PositionReportedAt, DateTimeOffset.UtcNow,
            m.IsPlaying, m.PlaybackRate, m.Duration);
        var trackWidth = (ProgressFill.Parent as FrameworkElement)?.ActualWidth ?? 0;
        if (trackWidth <= 0) trackWidth = FallbackProgressWidth;
        ProgressFill.Width = trackWidth * IslandBar.Core.MediaTimeline.Progress(position, m.Duration);
        PosText.Text = CountdownTimer.Format(position);
        DurText.Text = CountdownTimer.Format(m.Duration);
    }

    // ---------- Timer ----------

    private void UpdateTimerUi()
    {
        var timer = _timer.Countdown;
        var state = timer.State;

        TimerDisplay.Text = CountdownTimer.Format(timer.Remaining);
        TimerStatus.Text = state switch
        {
            TimerState.Running => "Running",
            TimerState.Paused => "Paused",
            TimerState.Finished => "Finished",
            _ => "Ready",
        };

        var canEdit = state is TimerState.Idle or TimerState.Finished;
        MinusButton.IsEnabled = PlusButton.IsEnabled = canEdit;
        PresetRow.IsEnabled = canEdit;

        StartButton.Visibility = canEdit ? Visibility.Visible : Visibility.Collapsed;
        StartButton.Content = state == TimerState.Finished ? "Restart" : "Start";
        PauseResumeButton.Visibility = canEdit ? Visibility.Collapsed : Visibility.Visible;
        PauseResumeButton.Content = state == TimerState.Paused ? "Resume" : "Pause";
        CancelButton.Visibility = state == TimerState.Idle ? Visibility.Collapsed : Visibility.Visible;

        // Fresh state for the compact and peek views.
        UpdateMediaUi();
    }

    private void OnTimerFinished()
    {
        if (!_settings.Current.EnableTimer) { _timer.Cancel(); return; }
        if (_settings.Current.PlaySoundOnTimerEnd)
        {
            try { SystemSounds.Asterisk.Play(); } catch (Exception) { /* no sound device */ }
        }
        TimerAlertSub.Text = $"{CountdownTimer.Format(_timer.Countdown.Duration)} is up";
        ShowAlert(IslandView.TimerAlert, TimeSpan.FromSeconds(20));
    }

    // ---------- Volume ----------

    private void OnVolumeChanged(int percent, bool muted)
    {
        if (!_settings.Current.EnableVolume) return;
        var level = muted ? 0 : percent;
        VolFill.Width = VolumeBarWidth * level / 100.0;
        VolPercent.Text = muted ? "Muted" : $"{percent}%";
        VolCross.Visibility = level == 0 ? Visibility.Visible : Visibility.Collapsed;
        VolWaves.Visibility = level == 0 ? Visibility.Collapsed : Visibility.Visible;
        ShowAlert(IslandView.VolumeAlert, TimeSpan.FromMilliseconds(1800));
    }

    // ---------- Battery ----------

    private void OnBatteryChanged()
    {
        var snapshot = _battery.Current;
        var kind = _batteryPolicy.Evaluate(snapshot);
        UpdateBatteryChip();
        if (!_settings.Current.EnableBattery || kind == BatteryAlertKind.None) return;

        AlertBatText.Text = kind switch
        {
            BatteryAlertKind.PluggedIn => $"Charging · {snapshot.Percent}%",
            BatteryAlertKind.Unplugged => $"On battery · {snapshot.Percent}%",
            BatteryAlertKind.Low => $"Low battery · {snapshot.Percent}%",
            _ => $"Battery critical · {snapshot.Percent}%",
        };
        AlertBatBolt.Visibility = kind == BatteryAlertKind.PluggedIn ? Visibility.Visible : Visibility.Collapsed;
        AlertBatIcon.SetLevel(snapshot.Percent, kind == BatteryAlertKind.PluggedIn);
        ShowAlert(IslandView.BatteryAlert, TimeSpan.FromMilliseconds(2600));
    }

    private void UpdateBatteryChip()
    {
        var b = _battery.Current;
        // Desktops have no battery: hide the chip instead of showing 0%.
        var show = _settings.Current.EnableBattery && b.HasBattery;
        BatteryChip.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        if (!show) return;
        ChipText.Text = $"{b.Percent}%";
        ChipIcon.SetLevel(b.Percent, b.IsPluggedIn);
        ChipBolt.Visibility = b.IsPluggedIn ? Visibility.Visible : Visibility.Collapsed;
    }
}
