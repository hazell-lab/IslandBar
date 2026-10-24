using System;
using System.Windows.Threading;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;

namespace IslandBar.Services;

/// <summary>
/// Watches the system speaker volume using the Windows Core Audio API (through NAudio).
/// Events only fire when the volume or mute really changes. No polling.
/// </summary>
public sealed class VolumeService : IDisposable
{
    private readonly Dispatcher _ui;
    private readonly MMDeviceEnumerator _enumerator = new();
    private readonly DeviceWatcher _deviceWatcher;
    private MMDevice? _device;
    private bool _disposed;

    public int Percent { get; private set; }
    public bool IsMuted { get; private set; }

    /// <summary>Raised on the UI thread with (percent, muted).</summary>
    public event Action<int, bool>? VolumeChanged;

    public VolumeService(Dispatcher ui)
    {
        _ui = ui;
        _deviceWatcher = new DeviceWatcher(() => _ui.InvokeAsync(Attach));
        try { _enumerator.RegisterEndpointNotificationCallback(_deviceWatcher); } catch (Exception) { }
        Attach();
    }

    // Connect to whatever the default speaker is right now.
    private void Attach()
    {
        if (_disposed) return;
        Detach();
        try
        {
            _device = _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            var endpoint = _device.AudioEndpointVolume;
            Percent = (int)Math.Round(endpoint.MasterVolumeLevelScalar * 100);
            IsMuted = endpoint.Mute;
            endpoint.OnVolumeNotification += OnVolumeNotification;
        }
        catch (Exception)
        {
            _device = null;   // no audio device (for example a server): nothing to show
        }
    }

    private void Detach()
    {
        if (_device is null) return;
        try { _device.AudioEndpointVolume.OnVolumeNotification -= OnVolumeNotification; } catch (Exception) { }
        _device.Dispose();
        _device = null;
    }

    private void OnVolumeNotification(AudioVolumeNotificationData data)
    {
        var percent = (int)Math.Round(data.MasterVolume * 100);
        _ui.InvokeAsync(() =>
        {
            if (_disposed) return;
            var changed = percent != Percent || data.Muted != IsMuted;
            Percent = percent;
            IsMuted = data.Muted;
            if (changed) VolumeChanged?.Invoke(Percent, IsMuted);
        });
    }

    public void Dispose()
    {
        _disposed = true;
        try { _enumerator.UnregisterEndpointNotificationCallback(_deviceWatcher); } catch (Exception) { }
        Detach();
        _enumerator.Dispose();
    }

    // Tells us when the default speaker changes (for example plugging in headphones).
    private sealed class DeviceWatcher : IMMNotificationClient
    {
        private readonly Action _onDefaultChanged;
        public DeviceWatcher(Action onDefaultChanged) => _onDefaultChanged = onDefaultChanged;

        public void OnDefaultDeviceChanged(DataFlow flow, Role role, string defaultDeviceId)
        {
            if (flow == DataFlow.Render && role == Role.Multimedia) _onDefaultChanged();
        }
        public void OnDeviceStateChanged(string deviceId, DeviceState newState) { }
        public void OnDeviceAdded(string pwstrDeviceId) { }
        public void OnDeviceRemoved(string deviceId) { }
        public void OnPropertyValueChanged(string pwstrDeviceId, PropertyKey key) { }
    }
}
