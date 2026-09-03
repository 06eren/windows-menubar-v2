using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.WiFi;
using Windows.Security.Credentials;
using Windows_10_MenuBar.Models;
using Wpf.Ui.Common;

namespace Windows_10_MenuBar.Services;

public class NetworkState
{
    public bool IsWifiConnected { get; init; }
    public bool HasWifi         { get; init; }
    public bool IsEthernet      { get; init; }
    public string NetworkName   { get; init; } = string.Empty;
    public SymbolRegular Icon   { get; init; } = SymbolRegular.WifiOff24;
}

public class NetworkService
{
    // ── Wi-Fi loop ───────────────────────────────────────────────────────────

    public async Task RunLoopAsync(
        ObservableCollection<WifiInfo> networks,
        Func<NetworkState, Task> onStateChanged,
        CancellationToken ct = default)
    {
        try
        {
            var access = await WiFiAdapter.RequestAccessAsync().AsTask(ct);
            if (access != WiFiAccessStatus.Allowed) return;

            while (!ct.IsCancellationRequested)
            {
                var adapters = await WiFiAdapter.FindAllAdaptersAsync().AsTask(ct);

                if (adapters.Count == 0)
                {
                    bool hasNet = NetworkInterface.GetIsNetworkAvailable();
                    await onStateChanged(new NetworkState
                    {
                        HasWifi     = false,
                        IsEthernet  = hasNet,
                        NetworkName = hasNet ? "Ethernet" : "Bağlantı Yok",
                        Icon        = hasNet ? SymbolRegular.Desktop24 : SymbolRegular.Globe24,
                    });
                    try { await Task.Delay(10000, ct); } catch (OperationCanceledException) { break; }  // 5s → 10s
                    continue;
                }

                var adapter = adapters[0];
                try
                {
                    await adapter.ScanAsync().AsTask(ct);
                    var available = adapter.NetworkReport.AvailableNetworks;
                    var connected = await adapter.NetworkAdapter.GetConnectedProfileAsync().AsTask(ct);
                    bool hasNet   = NetworkInterface.GetIsNetworkAvailable();

                    var state = BuildState(available, connected, hasNet);
                    await onStateChanged(state);
                    UpdateNetworkList(networks, available, connected);
                }
                catch (OperationCanceledException) { break; }
                catch { }

                try { await Task.Delay(10000, ct); } catch (OperationCanceledException) { break; }  // 5s → 10s
            }
        }
        catch (OperationCanceledException) { }
        catch { }
    }

    // ── Commands ─────────────────────────────────────────────────────────────

    public async Task SelectAsync(WifiInfo info, ObservableCollection<WifiInfo> all)
    {
        if (info == null || info.IsConnected) return;

        if (info.IsPasswordPromptVisible)
        {
            info.IsPasswordPromptVisible = false;
            info.StatusText = string.Empty;
            return;
        }

        foreach (var n in all) { n.IsPasswordPromptVisible = false; if (n.StatusText == "Şifre Gerekli") n.StatusText = string.Empty; }

        try
        {
            var adapters = await WiFiAdapter.FindAllAdaptersAsync();
            if (adapters.Count == 0) return;

            var network = adapters[0].NetworkReport.AvailableNetworks.FirstOrDefault(n => n.Ssid == info.Ssid);
            if (network != null)
            {
                info.StatusText = "Bağlanıyor...";
                var result = await adapters[0].ConnectAsync(network, WiFiReconnectionKind.Automatic);
                if (result.ConnectionStatus == WiFiConnectionStatus.Success)
                {
                    info.StatusText = "Bağlı";
                    foreach (var n in all) n.IsPasswordPromptVisible = false;
                    return;
                }
            }
        }
        catch { }

        foreach (var n in all) n.IsPasswordPromptVisible = false;
        info.IsPasswordPromptVisible = true;
        info.StatusText = "Şifre Gerekli";
    }

    public async Task ConnectWithPasswordAsync(WifiInfo? info, string password)
    {
        if (info == null || string.IsNullOrWhiteSpace(password)) return;
        info.StatusText = "Bağlanıyor...";
        try
        {
            var adapters = await WiFiAdapter.FindAllAdaptersAsync();
            if (adapters.Count == 0) return;

            var network = adapters[0].NetworkReport.AvailableNetworks.FirstOrDefault(n => n.Ssid == info.Ssid);
            if (network == null) return;

            var cred   = new PasswordCredential { Password = password };
            var result = await adapters[0].ConnectAsync(network, WiFiReconnectionKind.Automatic, cred);

            info.StatusText              = result.ConnectionStatus == WiFiConnectionStatus.Success ? "Bağlı" : "Hatalı Şifre";
            info.IsPasswordPromptVisible = result.ConnectionStatus == WiFiConnectionStatus.Success ? false : info.IsPasswordPromptVisible;
        }
        catch { info.StatusText = "Hata"; }
    }

    public async Task ForgetAsync(WifiInfo info)
    {
        if (info == null) return;
        try
        {
            var p = new Process();
            p.StartInfo = new ProcessStartInfo("netsh", $"wlan delete profile name=\"{info.Ssid}\"")
                { UseShellExecute = false, CreateNoWindow = true };
            p.Start();
            p.WaitForExit();

            info.StatusText = "Unutuldu";
            info.IsPasswordPromptVisible = false;
            if (info.IsConnected) await DisconnectAsync(info);
        }
        catch { }
    }

    public async Task DisconnectAsync(WifiInfo info)
    {
        if (info == null || !info.IsConnected) return;
        try
        {
            var adapters = await WiFiAdapter.FindAllAdaptersAsync();
            if (adapters.Count > 0) adapters[0].Disconnect();
            info.IsConnected = false;
            info.StatusText  = string.Empty;
        }
        catch { }
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private static NetworkState BuildState(
        IReadOnlyList<WiFiAvailableNetwork> available,
        Windows.Networking.Connectivity.ConnectionProfile? connected,
        bool hasNet)
    {
        if (!hasNet)
            return new NetworkState { HasWifi = true, NetworkName = "Bağlantı Yok", Icon = SymbolRegular.WifiOff24 };

        if (connected != null)
        {
            var cn  = available.FirstOrDefault(n => n.Ssid == connected.ProfileName);
            int bars = cn?.SignalBars ?? 4;
            return new NetworkState
            {
                HasWifi = true, IsWifiConnected = true,
                NetworkName = connected.ProfileName ?? "Wi-Fi",
                Icon = bars >= 4 ? SymbolRegular.Wifi424
                     : bars == 3 ? SymbolRegular.Wifi324
                     : bars == 2 ? SymbolRegular.Wifi224
                     : SymbolRegular.Wifi124,
            };
        }

        return new NetworkState
        {
            HasWifi = true, IsEthernet = hasNet,
            NetworkName = hasNet ? "Ethernet" : "Bağlantı Yok",
            Icon = hasNet ? SymbolRegular.Desktop24 : SymbolRegular.WifiOff24,
        };
    }

    private static void UpdateNetworkList(
        ObservableCollection<WifiInfo> list,
        IReadOnlyList<WiFiAvailableNetwork> incoming,
        Windows.Networking.Connectivity.ConnectionProfile? connected)
    {
        var sorted = incoming.OrderByDescending(n => n.NetworkRssiInDecibelMilliwatts).ToList();

        // Remove stale
        foreach (var old in list.Where(n => sorted.All(i => i.Ssid != n.Ssid)).ToList())
            list.Remove(old);

        // Add / update
        foreach (var net in sorted)
        {
            bool isConn  = connected?.ProfileName == net.Ssid;
            var existing = list.FirstOrDefault(n => n.Ssid == net.Ssid);
            if (existing != null)
            {
                existing.SignalStrength = net.SignalBars;
                existing.IsConnected   = isConn;
                if (isConn) existing.StatusText = "Bağlı";
                else if (existing.StatusText == "Bağlı") existing.StatusText = string.Empty;
            }
            else
            {
                list.Add(new WifiInfo
                {
                    Ssid           = net.Ssid,
                    SignalStrength = net.SignalBars,
                    IsConnected    = isConn,
                    StatusText     = isConn ? "Bağlı" : string.Empty,
                });
            }
        }
    }
}
