using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;
using Windows_10_MenuBar.Models;
using Wpf.Ui.Common;

namespace Windows_10_MenuBar.Services;

public class HardwareSnapshot
{
    public BatteryInfo Battery         { get; init; } = new();
    public bool IsMicrophoneActive     { get; init; }
    public bool IsCameraActive         { get; init; }
    public bool IsVpnConnected         { get; init; }
    public string VpnName              { get; init; } = string.Empty;
}

public class AudioSnapshot
{
    public int VolumeLevel             { get; init; }
    public SymbolRegular VolumeIcon    { get; init; } = SymbolRegular.Speaker224;
}

public class HardwareService : IDisposable
{
    public bool HasBluetooth { get; private set; }

    // ── System status loop ───────────────────────────────────────────────────

    public async Task RunSystemLoopAsync(
        Func<HardwareSnapshot, Task> onUpdate,
        CancellationToken ct = default)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var snap = BuildSystemSnapshot();
                await onUpdate(snap);
            }
            catch (OperationCanceledException) { break; }
            catch { }

            try { await Task.Delay(5000, ct); }  // 3s → 5s (batarya/kamera/mikrofon çok sık kontrol edilmemeli)
            catch (OperationCanceledException) { break; }
        }
    }

    // ── Audio — event-driven via NAudio callback ─────────────────────────────

    private MMDeviceEnumerator?         _mmEnum;
    private MMDevice?                   _mmDevice;
    private AudioEndpointVolumeCallback? _volCallback;
    private Func<AudioSnapshot, Task>?  _audioHandler;

    /// <summary>
    /// NAudio AudioEndpointVolumeNotification event'ini dinleyerek ses değişince
    /// callback'i tetikler. Polling yok, CPU sıfır.
    /// </summary>
    public void InitAudio(Func<AudioSnapshot, Task> onUpdate)
    {
        _audioHandler = onUpdate;
        try
        {
            _mmEnum    = new MMDeviceEnumerator();
            _mmDevice  = _mmEnum.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            _volCallback = new AudioEndpointVolumeCallback(OnVolumeChanged);
            _mmDevice.AudioEndpointVolume.OnVolumeNotification += _volCallback.OnNotify;

            // İlk değeri hemen oku
            PushCurrentVolume();
        }
        catch { }
    }

    private void OnVolumeChanged(AudioVolumeNotificationData data)
    {
        try
        {
            int vol   = (int)(data.MasterVolume * 100);
            bool muted = data.Muted || vol == 0;
            _ = _audioHandler?.Invoke(new AudioSnapshot
            {
                VolumeLevel = vol,
                VolumeIcon  = muted ? SymbolRegular.SpeakerMute24 : SymbolRegular.Speaker224,
            });
        }
        catch { }
    }

    private void PushCurrentVolume()
    {
        try
        {
            if (_mmDevice == null) return;
            int vol   = (int)(_mmDevice.AudioEndpointVolume.MasterVolumeLevelScalar * 100);
            bool muted = _mmDevice.AudioEndpointVolume.Mute || vol == 0;
            _ = _audioHandler?.Invoke(new AudioSnapshot
            {
                VolumeLevel = vol,
                VolumeIcon  = muted ? SymbolRegular.SpeakerMute24 : SymbolRegular.Speaker224,
            });
        }
        catch { }
    }

    // ── Bluetooth watcher ────────────────────────────────────────────────────

    public async Task InitBluetoothAsync(
        ObservableCollection<BluetoothDeviceInfo> devices,
        CancellationToken ct = default)
    {
        try
        {
            var adapter = await BluetoothAdapter.GetDefaultAsync().AsTask(ct);
            HasBluetooth = adapter != null;
            if (!HasBluetooth) return;

            string selector = BluetoothDevice.GetDeviceSelectorFromPairingState(true);
            var watcher = DeviceInformation.CreateWatcher(selector);

            watcher.Added   += (_, a) => App.Current.Dispatcher.InvokeAsync(() =>
            {
                if (!devices.Any(d => d.Id == a.Id))
                    devices.Add(new BluetoothDeviceInfo { Name = a.Name, Id = a.Id, DeviceInfo = a });
            });

            watcher.Updated += (_, a) => App.Current.Dispatcher.InvokeAsync(() =>
            {
                var d = devices.FirstOrDefault(x => x.Id == a.Id);
                d?.DeviceInfo?.Update(a);
            });

            watcher.Removed += (_, a) => App.Current.Dispatcher.InvokeAsync(() =>
            {
                var d = devices.FirstOrDefault(x => x.Id == a.Id);
                if (d != null) devices.Remove(d);
            });

            watcher.Start();
            ct.Register(() => { try { watcher.Stop(); } catch { } });
        }
        catch (OperationCanceledException) { }
        catch { }
    }

    public static void OpenBluetoothSettings(BluetoothDeviceInfo info)
    {
        info.StatusText = "Açılıyor...";
        try { Process.Start(new ProcessStartInfo("ms-settings:bluetooth") { UseShellExecute = true }); }
        catch { }
    }

    // ── Brightness ───────────────────────────────────────────────────────────

    public (int level, bool hasControl) GetBrightness()
    {
        try
        {
            using var s = new ManagementObjectSearcher("root\\wmi", "SELECT * FROM WmiMonitorBrightness");
            foreach (ManagementObject o in s.Get())
                return (Convert.ToInt32(o["CurrentBrightness"]), true);
        }
        catch { }
        return (100, false);
    }

    public void SetBrightness(int level)
    {
        try
        {
            using var s = new ManagementObjectSearcher("root\\wmi", "SELECT * FROM WmiMonitorBrightnessMethods");
            foreach (ManagementObject o in s.Get())
            {
                o.InvokeMethod("WmiSetBrightness", new object[] { (uint)1, (byte)level });
                break;
            }
        }
        catch { }
    }

    // ── IDisposable ──────────────────────────────────────────────────────────

    public void Dispose()
    {
        try
        {
            if (_mmDevice != null && _volCallback != null)
                _mmDevice.AudioEndpointVolume.OnVolumeNotification -= _volCallback.OnNotify;
            _mmDevice?.Dispose();
            _mmEnum?.Dispose();
        }
        catch { }
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private static HardwareSnapshot BuildSystemSnapshot() => new()
    {
        Battery            = ReadBattery(),
        IsMicrophoneActive = ReadCapabilityActive("microphone"),
        IsCameraActive     = ReadCapabilityActive("webcam"),
        IsVpnConnected     = TryGetVpn(out string vpn),
        VpnName            = vpn,
    };

    private static BatteryInfo ReadBattery()
    {
        var info = new BatteryInfo();
        try
        {
            var r   = Windows.Devices.Power.Battery.AggregateBattery.GetReport();
            bool has = r.Status != Windows.System.Power.BatteryStatus.NotPresent
                    && r.FullChargeCapacityInMilliwattHours != null;
            info.HasBattery = has;
            if (!has) return info;

            float pct       = (float)r.RemainingCapacityInMilliwattHours!.Value
                            / (float)r.FullChargeCapacityInMilliwattHours!.Value;
            info.Level      = (int)(pct * 100);
            info.IsCharging = r.Status is Windows.System.Power.BatteryStatus.Charging
                                       or Windows.System.Power.BatteryStatus.Idle;
            info.Icon = info.IsCharging ? SymbolRegular.BatteryCharge24
                      : info.Level > 80 ? SymbolRegular.Battery1024
                      : info.Level > 60 ? SymbolRegular.Battery824
                      : info.Level > 40 ? SymbolRegular.Battery624
                      : info.Level > 20 ? SymbolRegular.Battery424
                      :                   SymbolRegular.Battery224;
        }
        catch { }
        return info;
    }

    private static bool ReadCapabilityActive(string capability)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                $@"SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\{capability}\NonPackaged");
            if (key == null) return false;
            foreach (var sub in key.GetSubKeyNames())
            {
                using var sk = key.OpenSubKey(sub);
                if (sk?.GetValue("LastUsedTimeStop") is long t && t == 0) return true;
            }
        }
        catch { }
        return false;
    }

    private static bool TryGetVpn(out string name)
    {
        name = string.Empty;
        foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            if ((ni.NetworkInterfaceType == NetworkInterfaceType.Ppp ||
                 ni.Description.Contains("VPN",       StringComparison.OrdinalIgnoreCase) ||
                 ni.Description.Contains("TAP",       StringComparison.OrdinalIgnoreCase) ||
                 ni.Description.Contains("TUN",       StringComparison.OrdinalIgnoreCase) ||
                 ni.Description.Contains("WireGuard", StringComparison.OrdinalIgnoreCase)) &&
                ni.OperationalStatus == OperationalStatus.Up)
            {
                name = ni.Name;
                return true;
            }
        }
        return false;
    }
}

// ── NAudio callback adapter ───────────────────────────────────────────────────

/// <summary>
/// NAudio'nun IAudioEndpointVolumeCallback arayüzünü Action'a çevirir.
/// </summary>
internal sealed class AudioEndpointVolumeCallback
{
    private readonly Action<AudioVolumeNotificationData> _callback;
    public AudioEndpointVolumeCallback(Action<AudioVolumeNotificationData> cb) => _callback = cb;
    public void OnNotify(AudioVolumeNotificationData data) => _callback(data);
}
