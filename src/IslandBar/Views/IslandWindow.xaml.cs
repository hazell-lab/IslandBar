using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using IslandBar.Core;
using IslandBar.Services;

namespace IslandBar.Views;

/// <summary>What the user asked for: small, hover preview, or fully open.</summary>
public enum IslandMode { Compact, Peek, Expanded }

/// <summary>Every look the island can have. Alerts temporarily replace the mode's look.</summary>
internal enum IslandView { Compact, Peek, Expanded, VolumeAlert, BatteryAlert, TimerAlert }

/// <summary>
/// The floating island. This file holds the state and animations.
/// Other parts: Placement (position), Input (mouse and keys), Content (showing data).
/// </summary>
public partial class IslandWindow : Window
{
    private const double Pad = 12;   // see-through margin around the island (must match Island.Margin in XAML)

    private readonly SettingsService _settings;
    private readonly MediaService _media;
    private readonly VolumeService _volume;
    private readonly BatteryService _battery;
    private readonly TimerService _timer;

    private IslandMode _mode = IslandMode.Compact;
    private IslandView? _alert;                  // the alert on screen right now, if any
    private IslandView _shown = IslandView.Compact;
    private int _transitionId;                   // newest animation wins; older callbacks are ignored
    private Size _targetSize = new(150, 36);
    private bool _userHidden;
    private bool _fullscreenHidden;

    private readonly DispatcherTimer _alertTimer = new();

    // Animated number that drives the island's corner roundness.
    public static readonly DependencyProperty IslandCornerProperty = DependencyProperty.Register(
        nameof(IslandCorner), typeof(double), typeof(IslandWindow),
        new PropertyMetadata(18.0, (d, _) => ((IslandWindow)d).OnCornerChanged()));

    public double IslandCorner
    {
        get => (double)GetValue(IslandCornerProperty);
        set => SetValue(IslandCornerProperty, value);
    }

    public IslandWindow(SettingsService settings, MediaService media, VolumeService volume,
                        BatteryService battery, TimerService timer)
    {
        _settings = settings;
        _media = media;
        _volume = volume;
        _battery = battery;
        _timer = timer;

        InitializeComponent();

        _alertTimer.Tick += (_, _) => ClearAlert();
        Island.SizeChanged += (_, _) => UpdateClip();

        WireInput();
        WireContent();
        ApplySettings();
    }

    // ---------- Public actions (used by the tray and hot key) ----------

    public bool IsUserHidden => _userHidden;

    public void SetUserHidden(bool hidden)
    {
        _userHidden = hidden;
        if (hidden) Collapse();
        UpdateVisibility();
    }

    public void SetFullscreenHidden(bool hidden)
    {
        _fullscreenHidden = hidden;
        if (hidden) Collapse();
        UpdateVisibility();
    }

    private void UpdateVisibility()
    {
        var shouldShow = !_userHidden && !_fullscreenHidden;
        if (shouldShow && Visibility != Visibility.Visible)
        {
            Visibility = Visibility.Visible;
            ApplyWindowBox(_targetSize);
        }
        else if (!shouldShow && Visibility == Visibility.Visible)
        {
            Visibility = Visibility.Hidden;
        }
    }

    public void Expand() => SetMode(IslandMode.Expanded);
    public void Collapse() => SetMode(IslandMode.Compact);

    // ---------- Modes and alerts ----------

    private void SetMode(IslandMode mode)
    {
        if (mode == IslandMode.Expanded && Visibility != Visibility.Visible) return;
        var wasExpanded = _mode == IslandMode.Expanded;
        _mode = mode;

        if (mode == IslandMode.Expanded)
        {
            ClearAlertState();
            OnExpandedStarted();
        }
        else if (wasExpanded)
        {
            OnExpandedEnded();
        }
        ShowView(DesiredView());
    }

    private IslandView DesiredView() => _alert ?? _mode switch
    {
        IslandMode.Peek => IslandView.Peek,
        IslandMode.Expanded => IslandView.Expanded,
        _ => IslandView.Compact,
    };

    private void ShowAlert(IslandView alert, TimeSpan duration)
    {
        // Alerts never interrupt the open island, and never show while hidden.
        if (_mode == IslandMode.Expanded || Visibility != Visibility.Visible) return;
        _alert = alert;
        _alertTimer.Stop();
        _alertTimer.Interval = duration;
        _alertTimer.Start();
        ShowView(DesiredView());
    }

    private void ClearAlert()
    {
        // Dismissing the "timer finished" alert also resets the timer.
        if (_alert == IslandView.TimerAlert && _timer.Countdown.State == TimerState.Finished) _timer.Cancel();
        ClearAlertState();
        ShowView(DesiredView());
    }

    private void ClearAlertState()
    {
        _alertTimer.Stop();
        _alert = null;
    }

    // ---------- Animation ----------

    private readonly record struct Motion(bool Enabled, double TimeScale, double Overshoot);

    private Motion GetMotion()
    {
        var s = _settings.Current;
        var reduced = s.Animation == AnimationIntensity.Off ||
                      (s.FollowSystemReducedMotion && !SystemParameters.ClientAreaAnimation);
        if (reduced) return new Motion(false, 0, 0);
        return s.Animation switch
        {
            AnimationIntensity.Gentle => new Motion(true, 0.8, 0),
            AnimationIntensity.Playful => new Motion(true, 1.15, 0.3),
            _ => new Motion(true, 1.0, 0.15),
        };
    }

    private static Size SizeOf(IslandView view) => view switch
    {
        IslandView.Peek => new Size(300, 48),
        IslandView.Expanded => new Size(392, 188),
        IslandView.VolumeAlert => new Size(260, 46),
        IslandView.BatteryAlert => new Size(260, 46),
        IslandView.TimerAlert => new Size(320, 64),
        _ => new Size(150, 36),
    };

    private FrameworkElement ElementOf(IslandView view) => view switch
    {
        IslandView.Peek => PeekView,
        IslandView.Expanded => ExpandedView,
        IslandView.VolumeAlert => VolumeAlertView,
        IslandView.BatteryAlert => BatteryAlertView,
        IslandView.TimerAlert => TimerAlertView,
        _ => CompactView,
    };

    private static readonly IslandView[] AllViews =
        { IslandView.Compact, IslandView.Peek, IslandView.Expanded, IslandView.VolumeAlert, IslandView.BatteryAlert, IslandView.TimerAlert };

    /// <summary>Morphs the island into the wanted view: size, roundness, shadow and content fade.</summary>
    private void ShowView(IslandView view, bool force = false)
    {
        if (view == _shown && !force) return;
        _shown = view;
        var id = ++_transitionId;
        var motion = GetMotion();
        var size = SizeOf(view);
        _targetSize = size;

        // Make the window big enough for the whole animation, then shrink it afterwards.
        var current = new Size(Math.Max(Island.ActualWidth, 1), Math.Max(Island.ActualHeight, 1));
        ApplyWindowBox(new Size(Math.Max(size.Width, current.Width), Math.Max(size.Height, current.Height)));

        foreach (var other in AllViews) FadeView(ElementOf(other), other == view, motion, id);

        var corner = view == IslandView.Expanded ? 34 : size.Height / 2;
        var growing = size.Width * size.Height >= current.Width * current.Height;
        var duration = TimeSpan.FromMilliseconds((growing ? 460 : 340) * motion.TimeScale);
        var ease = motion.Overshoot > 0
            ? (IEasingFunction)new BackEase { Amplitude = motion.Overshoot, EasingMode = EasingMode.EaseOut }
            : new CubicEase { EasingMode = EasingMode.EaseOut };

        AnimateNumber(Island, FrameworkElement.WidthProperty, size.Width, duration, ease, motion.Enabled, null);
        AnimateNumber(Island, FrameworkElement.HeightProperty, size.Height, duration, ease, motion.Enabled,
            () => { if (id == _transitionId) ApplyWindowBox(size); });
        AnimateNumber(this, IslandCornerProperty, corner, duration, new CubicEase { EasingMode = EasingMode.EaseOut }, motion.Enabled, null);

        var shadowOn = view is IslandView.Expanded or IslandView.VolumeAlert or IslandView.BatteryAlert or IslandView.TimerAlert;
        AnimateNumber(Shadow, DropShadowEffect.OpacityProperty, shadowOn ? 0.55 : 0, TimeSpan.FromMilliseconds(250 * motion.TimeScale),
            new CubicEase(), motion.Enabled, null);

        UpdateEqualizers();
    }

    private static void AnimateNumber(DependencyObject target, DependencyProperty property, double to,
        TimeSpan duration, IEasingFunction ease, bool animate, Action? done)
    {
        if (animate && duration > TimeSpan.Zero)
        {
            var animation = new DoubleAnimation(to, duration) { EasingFunction = ease };
            if (done is not null) animation.Completed += (_, _) => done();
            // BeginAnimation exists on UIElement and Animatable; both implement IAnimatable.
            ((IAnimatable)target).BeginAnimation(property, animation);
        }
        else
        {
            ((IAnimatable)target).BeginAnimation(property, null);   // drop any running animation
            target.SetValue(property, to);
            done?.Invoke();
        }
    }

    private void FadeView(FrameworkElement element, bool show, Motion motion, int id)
    {
        element.IsHitTestVisible = show;
        if (show)
        {
            element.Visibility = Visibility.Visible;
            var fadeIn = TimeSpan.FromMilliseconds(200 * motion.TimeScale);
            var delay = TimeSpan.FromMilliseconds(70 * motion.TimeScale);
            AnimateFade(element, 1, fadeIn, delay, motion.Enabled, null);
        }
        else if (element.Visibility == Visibility.Visible)
        {
            AnimateFade(element, 0, TimeSpan.FromMilliseconds(110 * motion.TimeScale), TimeSpan.Zero, motion.Enabled, () =>
            {
                if (id == _transitionId && !element.IsHitTestVisible) element.Visibility = Visibility.Collapsed;
            });
        }
    }

    private static void AnimateFade(UIElement element, double to, TimeSpan duration, TimeSpan delay, bool animate, Action? done)
    {
        if (animate && duration > TimeSpan.Zero)
        {
            var animation = new DoubleAnimation(to, duration) { BeginTime = delay };
            if (done is not null) animation.Completed += (_, _) => done();
            element.BeginAnimation(UIElement.OpacityProperty, animation);
        }
        else
        {
            element.BeginAnimation(UIElement.OpacityProperty, null);
            element.Opacity = to;
            done?.Invoke();
        }
    }

    private void OnCornerChanged()
    {
        if (Island is null) return;
        Island.CornerRadius = new CornerRadius(IslandCorner);
        UpdateClip();
    }

    // Keeps content inside the rounded shape while it morphs.
    private void UpdateClip()
    {
        var radius = Math.Max(0, IslandCorner - 1);
        Island.Clip = new RectangleGeometry(new Rect(0, 0, Island.ActualWidth, Island.ActualHeight), radius, radius);
    }
}
