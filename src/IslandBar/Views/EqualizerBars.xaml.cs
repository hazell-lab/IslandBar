using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace IslandBar.Views;

public partial class EqualizerBars : UserControl
{
    private bool _running;

    public EqualizerBars() => InitializeComponent();

    /// <summary>Starts or stops the dance. When stopped (or motion is reduced) the bars stay still.</summary>
    public void SetPlaying(bool playing, bool animate)
    {
        var shouldRun = playing && animate;
        if (shouldRun == _running) return;
        _running = shouldRun;

        Animate(Bar1, 0.25, 1.0, 520, shouldRun);
        Animate(Bar2, 0.35, 1.0, 410, shouldRun);
        Animate(Bar3, 0.2, 0.9, 630, shouldRun);
    }

    private static void Animate(Border bar, double from, double to, int ms, bool run)
    {
        var scale = (ScaleTransform)bar.RenderTransform;
        if (!run)
        {
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);   // removes the animation
            scale.ScaleY = 0.5;
            return;
        }
        var animation = new DoubleAnimation(from, to, TimeSpan.FromMilliseconds(ms))
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
        };
        Timeline.SetDesiredFrameRate(animation, 24);   // 24 fps is plenty and keeps CPU low
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
    }
}
