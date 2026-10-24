using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using IslandBar.Core;
using IslandBar.Services;

namespace IslandBar.Views;

public partial class SettingsWindow : Window
{
    private readonly SettingsService _service;
    private bool _loading;   // true while we fill the controls, so that does not count as a change

    public SettingsWindow(SettingsService service)
    {
        _service = service;
        InitializeComponent();
        BuildMonitorList();
        LoadValues();
        Hook();
    }

    private AppSettings S => _service.Current;

    // One radio button per monitor, plus "Main monitor".
    private void BuildMonitorList()
    {
        MonitorList.Children.Clear();
        AddMonitorOption("Main monitor", "");
        var index = 1;
        foreach (var screen in Screen.AllScreens)
        {
            var label = $"Monitor {index++}: {screen.Bounds.Width} × {screen.Bounds.Height}" + (screen.Primary ? " (main)" : "");
            AddMonitorOption(label, screen.DeviceName);
        }
    }

    private void AddMonitorOption(string label, string deviceName)
    {
        var option = new System.Windows.Controls.RadioButton
        {
            Content = label,
            Tag = deviceName,
            GroupName = "Monitor",
            Style = (Style)FindResource("TabChip"),
            HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
            Margin = new Thickness(0, 1, 0, 1),
        };
        option.Checked += (_, _) =>
        {
            if (_loading) return;
            S.MonitorDeviceName = (string)option.Tag;
            _service.NotifyChanged();
        };
        MonitorList.Children.Add(option);
    }

    private void LoadValues()
    {
        _loading = true;
        foreach (System.Windows.Controls.RadioButton option in MonitorList.Children)
            option.IsChecked = (string)option.Tag == S.MonitorDeviceName;
        // The saved monitor may be unplugged. Fall back to "Main monitor" in the list.
        if (!AnyChecked()) ((System.Windows.Controls.RadioButton)MonitorList.Children[0]).IsChecked = true;

        OffsetSlider.Value = S.OffsetX;
        TopSlider.Value = S.TopMargin;
        SizeSlider.Value = S.SizePercent;
        UpdateLabels();

        AnimOff.IsChecked = S.Animation == AnimationIntensity.Off;
        AnimGentle.IsChecked = S.Animation == AnimationIntensity.Gentle;
        AnimNormal.IsChecked = S.Animation == AnimationIntensity.Normal;
        AnimPlayful.IsChecked = S.Animation == AnimationIntensity.Playful;
        FollowReducedMotion.IsChecked = S.FollowSystemReducedMotion;
        HoverPreview.IsChecked = S.HoverPreview;

        EnableMedia.IsChecked = S.EnableMedia;
        EnableVolume.IsChecked = S.EnableVolume;
        EnableBattery.IsChecked = S.EnableBattery;
        EnableTimer.IsChecked = S.EnableTimer;
        TimerSound.IsChecked = S.PlaySoundOnTimerEnd;

        AlwaysOnTop.IsChecked = S.AlwaysOnTop;
        HideInFullscreen.IsChecked = S.HideInFullscreen;
        LaunchAtStartup.IsChecked = S.LaunchAtStartup;
        _loading = false;
    }

    private bool AnyChecked()
    {
        foreach (System.Windows.Controls.RadioButton option in MonitorList.Children)
            if (option.IsChecked == true) return true;
        return false;
    }

    private void UpdateLabels()
    {
        OffsetLabel.Text = $"{(int)OffsetSlider.Value} px";
        TopLabel.Text = $"{(int)TopSlider.Value} px";
        SizeLabel.Text = $"{(int)SizeSlider.Value}%";
    }

    private void Hook()
    {
        OffsetSlider.ValueChanged += (_, _) => Changed(() => S.OffsetX = (int)OffsetSlider.Value);
        TopSlider.ValueChanged += (_, _) => Changed(() => S.TopMargin = (int)TopSlider.Value);
        SizeSlider.ValueChanged += (_, _) => Changed(() => S.SizePercent = (int)SizeSlider.Value);

        HookAnimation(AnimOff, AnimationIntensity.Off);
        HookAnimation(AnimGentle, AnimationIntensity.Gentle);
        HookAnimation(AnimNormal, AnimationIntensity.Normal);
        HookAnimation(AnimPlayful, AnimationIntensity.Playful);

        HookToggle(FollowReducedMotion, v => S.FollowSystemReducedMotion = v);
        HookToggle(HoverPreview, v => S.HoverPreview = v);
        HookToggle(EnableMedia, v => S.EnableMedia = v);
        HookToggle(EnableVolume, v => S.EnableVolume = v);
        HookToggle(EnableBattery, v => S.EnableBattery = v);
        HookToggle(EnableTimer, v => S.EnableTimer = v);
        HookToggle(TimerSound, v => S.PlaySoundOnTimerEnd = v);
        HookToggle(AlwaysOnTop, v => S.AlwaysOnTop = v);
        HookToggle(HideInFullscreen, v => S.HideInFullscreen = v);
        HookToggle(LaunchAtStartup, v => S.LaunchAtStartup = v);
    }

    private void HookAnimation(System.Windows.Controls.RadioButton button, AnimationIntensity value) =>
        button.Checked += (_, _) => Changed(() => S.Animation = value);

    private void HookToggle(System.Windows.Controls.CheckBox box, Action<bool> apply)
    {
        box.Checked += (_, _) => Changed(() => apply(true));
        box.Unchecked += (_, _) => Changed(() => apply(false));
    }

    private void Changed(Action apply)
    {
        if (_loading) return;
        apply();
        UpdateLabels();
        _service.NotifyChanged();
    }
}
