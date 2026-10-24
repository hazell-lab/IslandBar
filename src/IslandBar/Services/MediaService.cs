using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Windows.Media.Control;
using Windows.Storage.Streams;

namespace IslandBar.Services;

/// <summary>What is playing right now. Everything can be empty, so the UI must handle that.</summary>
public sealed class MediaInfo
{
    public bool HasSession { get; init; }
    public string Title { get; init; } = "";
    public string Artist { get; init; } = "";
    public string Album { get; init; } = "";
    public BitmapSource? Art { get; init; }
    public bool IsPlaying { get; init; }
    public bool CanPlayPause { get; init; }
    public bool CanNext { get; init; }
    public bool CanPrevious { get; init; }
    public TimeSpan Position { get; init; }
    public TimeSpan Duration { get; init; }
    public DateTimeOffset PositionReportedAt { get; init; }
    public double PlaybackRate { get; init; } = 1.0;
}

/// <summary>
/// Talks to Windows "System Media Transport Controls" (the same thing the volume flyout uses).
/// Works with any app that publishes to it: Spotify, browsers, Media Player, and so on.
/// </summary>
public sealed class MediaService : IDisposable
{
    private readonly Dispatcher _ui;
    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private GlobalSystemMediaTransportControlsSession? _session;
    private int _refreshVersion;                 // lets us ignore slow, out-of-date refreshes
    private string _artKey = "";
    private BitmapSource? _art;
    private bool _disposed;

    public MediaInfo Current { get; private set; } = new();
    public event Action? Changed;

    public MediaService(Dispatcher ui) => _ui = ui;

    public async Task StartAsync()
    {
        try
        {
            _manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            _manager.CurrentSessionChanged += OnCurrentSessionChanged;
            AttachToSession(_manager.GetCurrentSession());
            await RefreshAsync();
        }
        catch (Exception)
        {
            _manager = null;   // media integration unavailable: the island still works without it
        }
    }

    // ---- Controls ----
    public Task TogglePlayPauseAsync() => RunAsync(s => s.TryTogglePlayPauseAsync().AsTask());
    public Task NextAsync() => RunAsync(s => s.TrySkipNextAsync().AsTask());
    public Task PreviousAsync() => RunAsync(s => s.TrySkipPreviousAsync().AsTask());

    private async Task RunAsync(Func<GlobalSystemMediaTransportControlsSession, Task<bool>> action)
    {
        var session = _session;
        if (session is null) return;
        try { await action(session); } catch (Exception) { /* the app may have closed: ignore */ }
    }

    // ---- Session handling ----
    private void OnCurrentSessionChanged(GlobalSystemMediaTransportControlsSessionManager sender, object args)
    {
        _ui.InvokeAsync(() =>
        {
            if (_disposed) return;
            AttachToSession(sender.GetCurrentSession());
            _ = RefreshAsync();
        });
    }

    private void AttachToSession(GlobalSystemMediaTransportControlsSession? session)
    {
        DetachSession();
        _session = session;
        if (session is null) return;
        session.MediaPropertiesChanged += OnSessionChanged;
        session.PlaybackInfoChanged += OnSessionChanged;
        session.TimelinePropertiesChanged += OnSessionChanged;
    }

    private void DetachSession()
    {
        if (_session is null) return;
        _session.MediaPropertiesChanged -= OnSessionChanged;
        _session.PlaybackInfoChanged -= OnSessionChanged;
        _session.TimelinePropertiesChanged -= OnSessionChanged;
        _session = null;
    }

    private void OnSessionChanged(GlobalSystemMediaTransportControlsSession sender, object args) =>
        _ui.InvokeAsync(() => { if (!_disposed) _ = RefreshAsync(); });

    // ---- Reading the current state ----
    private async Task RefreshAsync()
    {
        var version = ++_refreshVersion;
        var session = _session;
        if (session is null)
        {
            Publish(new MediaInfo());
            return;
        }

        try
        {
            var props = await session.TryGetMediaPropertiesAsync();
            var playback = session.GetPlaybackInfo();
            var timeline = session.GetTimelineProperties();

            // Only reload the picture when the track changed.
            var key = $"{props.Title}|{props.Artist}|{props.AlbumTitle}";
            if (key != _artKey)
            {
                _artKey = key;
                _art = null;
                var art = await LoadArtAsync(props.Thumbnail);
                if (version != _refreshVersion) return;   // a newer refresh is running
                _art = art;
            }
            if (version != _refreshVersion) return;

            var status = playback?.PlaybackStatus;
            var controls = playback?.Controls;
            var span = timeline?.EndTime - timeline?.StartTime ?? TimeSpan.Zero;
            Publish(new MediaInfo
            {
                HasSession = true,
                Title = props.Title ?? "",
                Artist = props.Artist ?? "",
                Album = props.AlbumTitle ?? "",
                Art = _art,
                IsPlaying = status == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing,
                CanPlayPause = controls?.IsPlayPauseToggleEnabled ?? false,
                CanNext = controls?.IsNextEnabled ?? false,
                CanPrevious = controls?.IsPreviousEnabled ?? false,
                Position = (timeline?.Position ?? TimeSpan.Zero) - (timeline?.StartTime ?? TimeSpan.Zero),
                Duration = span,
                PositionReportedAt = timeline?.LastUpdatedTime ?? DateTimeOffset.UtcNow,
                PlaybackRate = playback?.PlaybackRate ?? 1.0,
            });
        }
        catch (Exception)
        {
            if (version == _refreshVersion) Publish(new MediaInfo());   // session vanished mid-read
        }
    }

    private void Publish(MediaInfo info)
    {
        Current = info;
        Changed?.Invoke();
    }

    private static async Task<BitmapSource?> LoadArtAsync(IRandomAccessStreamReference? thumbnail)
    {
        if (thumbnail is null) return null;
        try
        {
            using var stream = await thumbnail.OpenReadAsync();
            if (stream.Size == 0 || stream.Size > 20_000_000) return null;
            var reader = new DataReader(stream);
            await reader.LoadAsync((uint)stream.Size);
            var bytes = new byte[stream.Size];
            reader.ReadBytes(bytes);

            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;   // read everything now so the stream can close
            image.DecodePixelWidth = 220;                   // small picture = small memory
            image.StreamSource = new MemoryStream(bytes);
            image.EndInit();
            image.Freeze();                                 // frozen images are safe to share between threads
            return image;
        }
        catch (Exception) { return null; }   // bad or missing artwork is fine
    }

    public void Dispose()
    {
        _disposed = true;
        DetachSession();
        if (_manager is not null) _manager.CurrentSessionChanged -= OnCurrentSessionChanged;
        _manager = null;
    }
}
