using System.Text.Json;
using System.Text.Json.Serialization;

namespace IslandBar.Core;

public enum AnimationIntensity { Off, Gentle, Normal, Playful }

/// <summary>All user settings. Saved as JSON.</summary>
public sealed class AppSettings
{
    // Position and size
    public string MonitorDeviceName { get; set; } = "";   // empty = primary monitor
    public int OffsetX { get; set; } = 0;                 // pixels left (-) or right (+) of center
    public int TopMargin { get; set; } = 8;               // gap from top of the screen
    public int SizePercent { get; set; } = 100;           // 70..150

    // Animation
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public AnimationIntensity Animation { get; set; } = AnimationIntensity.Normal;
    public bool FollowSystemReducedMotion { get; set; } = true;
    public bool HoverPreview { get; set; } = true;

    // Window behavior
    public bool AlwaysOnTop { get; set; } = true;
    public bool HideInFullscreen { get; set; } = true;
    public bool LaunchAtStartup { get; set; } = false;

    // Features
    public bool EnableMedia { get; set; } = true;
    public bool EnableVolume { get; set; } = true;
    public bool EnableBattery { get; set; } = true;
    public bool EnableTimer { get; set; } = true;
    public bool PlaySoundOnTimerEnd { get; set; } = true;
    public int TimerDefaultSeconds { get; set; } = 300;

    public const int MinSize = 70, MaxSize = 150;

    /// <summary>Forces every value into a safe range (protects against hand-edited files).</summary>
    public void Clamp()
    {
        MonitorDeviceName ??= "";
        OffsetX = Math.Clamp(OffsetX, -3000, 3000);
        TopMargin = Math.Clamp(TopMargin, 0, 300);
        SizePercent = Math.Clamp(SizePercent, MinSize, MaxSize);
        TimerDefaultSeconds = Math.Clamp(TimerDefaultSeconds, 10, 180 * 60);
        if (!Enum.IsDefined(Animation)) Animation = AnimationIntensity.Normal;
    }

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    /// <summary>Reads settings from JSON. Bad or empty text gives defaults.</summary>
    public static AppSettings FromJson(string? json)
    {
        try
        {
            var loaded = string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
            loaded ??= new AppSettings();
            loaded.Clamp();
            return loaded;
        }
        catch (JsonException) { return new AppSettings(); }
    }

    public AppSettings Clone() => FromJson(ToJson());
}
