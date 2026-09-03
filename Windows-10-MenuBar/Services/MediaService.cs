using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Windows.Media.Control;
using Windows_10_MenuBar.Models;

namespace Windows_10_MenuBar.Services;

public class MediaService
{
    private GlobalSystemMediaTransportControlsSession? _session;

    public event Action<MediaInfo>? MediaChanged;

    // ── Init ─────────────────────────────────────────────────────────────────

    public async Task InitAsync()
    {
        try
        {
            var manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            if (manager == null) return;

            manager.CurrentSessionChanged += (s, _) => SetSession(s.GetCurrentSession());
            SetSession(manager.GetCurrentSession());
        }
        catch { }
    }

    // ── Commands ─────────────────────────────────────────────────────────────

    public async Task PreviousAsync()  { try { if (_session != null) await _session.TrySkipPreviousAsync();    } catch { } }
    public async Task PlayPauseAsync() { try { if (_session != null) await _session.TryTogglePlayPauseAsync(); } catch { } }
    public async Task NextAsync()      { try { if (_session != null) await _session.TrySkipNextAsync();         } catch { } }

    // ── Session management ────────────────────────────────────────────────────

    private void SetSession(GlobalSystemMediaTransportControlsSession? session)
    {
        if (_session != null)
        {
            try
            {
                _session.MediaPropertiesChanged -= OnPropertiesChanged;
                _session.PlaybackInfoChanged    -= OnPlaybackChanged;
            }
            catch { }
        }

        _session = session;

        if (session == null)
        {
            // Oturum tamamen kapandıysa paneli temizle
            MediaChanged?.Invoke(new MediaInfo());
            return;
        }

        session.MediaPropertiesChanged += OnPropertiesChanged;
        session.PlaybackInfoChanged    += OnPlaybackChanged;
        _ = ReadMediaAsync(session);
    }

    private async void OnPropertiesChanged(GlobalSystemMediaTransportControlsSession s, MediaPropertiesChangedEventArgs? _)
        => await ReadMediaAsync(s);

    private async void OnPlaybackChanged(GlobalSystemMediaTransportControlsSession s, PlaybackInfoChangedEventArgs? _)
        => await ReadMediaAsync(s);

    // ── Read ─────────────────────────────────────────────────────────────────

    private async Task ReadMediaAsync(GlobalSystemMediaTransportControlsSession session)
    {
        try
        {
            var props = await session.TryGetMediaPropertiesAsync();
            var play  = session.GetPlaybackInfo();

            bool isPlaying = play.PlaybackStatus ==
                             GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;

            // Duraksatılmış (Paused) veya oynatılıyor — her ikisinde de başlık var
            bool hasTitle = !string.IsNullOrEmpty(props.Title);

            ImageSource? thumbnail = null;
            if (hasTitle && props.Thumbnail != null)
                thumbnail = await LoadThumbnailAsync(props.Thumbnail);

            MediaChanged?.Invoke(new MediaInfo
            {
                Title     = props.Title,
                Artist    = props.Artist,
                IsPlaying = isPlaying,
                Thumbnail = thumbnail,
            });
        }
        catch { }
    }

    // ── Thumbnail loader ─────────────────────────────────────────────────────

    /// <summary>
    /// SMTC thumbnail stream'ini WPF ImageSource'a dönüştürür.
    /// Spotify, YouTube Music, tarayıcı video oynatıcıları dahil tüm SMTC
    /// kaynaklarından çalışır — ayrı Spotify API gerekmez.
    /// </summary>
    private static async Task<ImageSource?> LoadThumbnailAsync(
        Windows.Storage.Streams.IRandomAccessStreamReference streamRef)
    {
        try
        {
            using var stream = await streamRef.OpenReadAsync();
            using var dotnetStream = stream.AsStreamForRead();

            // UI thread'e geçmeden önce byte'a oku
            var bytes = new byte[stream.Size];
            await dotnetStream.ReadExactlyAsync(bytes, 0, bytes.Length);

            // BitmapImage UI thread'de oluşturulmalı
            BitmapImage? bmp = null;
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.StreamSource  = new MemoryStream(bytes);
                bmp.CacheOption   = BitmapCacheOption.OnLoad;
                bmp.DecodePixelHeight = 24; // bar yüksekliğiyle orantılı küçük boyut
                bmp.EndInit();
                bmp.Freeze(); // cross-thread erişim için
            });

            return bmp;
        }
        catch { return null; }
    }
}
