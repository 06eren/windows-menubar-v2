using System;
using System.Threading;
using System.Threading.Tasks;
using Windows.UI.Notifications;
using Windows.UI.Notifications.Management;

namespace Windows_10_MenuBar.Services;

public class NotificationService
{
    private UserNotificationListener? _listener;
    private int _lastCount;

    /// <summary>Okunmamış bildirim sayısı değişince tetiklenir.</summary>
    public event Action<int>? CountChanged;

    /// <summary>Yeni bildirim gelince (sayı arttığında) tetiklenir.</summary>
    public event Action? NewNotificationArrived;

    // ── Init ─────────────────────────────────────────────────────────────────

    public async Task InitAsync(CancellationToken ct = default)
    {
        try
        {
            _listener = UserNotificationListener.Current;
            var access = await _listener.RequestAccessAsync();

            if (access != UserNotificationListenerAccessStatus.Allowed)
            {
                // İzin yoksa bildirim sayısı sıfır kalır — sessizce devam et
                _listener = null;
                return;
            }

            // İlk okumayı yap, sonra loop'a gir
            await RefreshAsync();
            _ = RunLoopAsync(ct);
        }
        catch { }
    }

    // ── Public ───────────────────────────────────────────────────────────────

    public async Task<int> GetCountAsync()
    {
        if (_listener == null) return 0;
        try
        {
            var notifications = await _listener.GetNotificationsAsync(NotificationKinds.Toast);
            return notifications.Count;
        }
        catch { return 0; }
    }

    // ── Loop ─────────────────────────────────────────────────────────────────

    private async Task RunLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try { await Task.Delay(5000, ct); } catch (OperationCanceledException) { break; }  // 3s → 5s (bildirim çok sık kontrol edilmemeli)
            await RefreshAsync();
        }
    }

    private async Task RefreshAsync()
    {
        int count = await GetCountAsync();

        if (count != _lastCount)
        {
            bool isNew = count > _lastCount;
            _lastCount = count;
            CountChanged?.Invoke(count);
            if (isNew) NewNotificationArrived?.Invoke();
        }
    }
}
