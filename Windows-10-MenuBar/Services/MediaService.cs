using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Windows.Media.Control;
using Windows_10_MenuBar.Models;

namespace Windows_10_MenuBar.Services;

public class MediaService
{
    private GlobalSystemMediaTransportControlsSession? _session;
    private DispatcherTimer? _positionTimer;
    private DateTime _lastPlayStartTime = DateTime.Now;
    private TimeSpan _lastPosition = TimeSpan.Zero;
    private string _lastTrackId = "";

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
                _session.TimelinePropertiesChanged -= OnTimelineChanged;
            }
            catch { }
        }

        _session = session;

        if (session == null)
        {
            // Oturum tamamen kapandıysa paneli temizle
            StopPositionTimer();
            MediaChanged?.Invoke(new MediaInfo());
            return;
        }

        session.MediaPropertiesChanged += OnPropertiesChanged;
        session.PlaybackInfoChanged    += OnPlaybackChanged;
        session.TimelinePropertiesChanged += OnTimelineChanged;
        _ = ReadMediaAsync(session);
        
        // Position tracking timer başlat (seek detection için)
        StartPositionTimer();
    }

    private async void OnTimelineChanged(GlobalSystemMediaTransportControlsSession s, TimelinePropertiesChangedEventArgs? _)
    {
        // Timeline değişti = seek/forward yapıldı, position'ı resetle
        _lastPosition = TimeSpan.Zero;
        _lastPlayStartTime = DateTime.Now;
        System.Diagnostics.Debug.WriteLine("TIMELINE CHANGED - Position reset");
        await ReadMediaAsync(s);
    }

    private void StartPositionTimer()
    {
        if (_positionTimer == null)
        {
            _positionTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromSeconds(1) // Her saniye position güncelle
            };
            _positionTimer.Tick += async (s, e) => 
            {
                if (_session != null)
                    await ReadMediaAsync(_session);
            };
        }
        
        if (!_positionTimer.IsEnabled)
            _positionTimer.Start();
    }

    private void StopPositionTimer()
    {
        _positionTimer?.Stop();
    }

    private async void OnPropertiesChanged(GlobalSystemMediaTransportControlsSession s, MediaPropertiesChangedEventArgs? _)
        => await ReadMediaAsync(s);

    private async void OnPlaybackChanged(GlobalSystemMediaTransportControlsSession s, PlaybackInfoChangedEventArgs? _)
    {
        // Playback durumu değişti - pause/resume için position güncelle
        var play = s.GetPlaybackInfo();
        if (play.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
        {
            // Resume - şu anki position'dan devam et
            _lastPlayStartTime = DateTime.Now;
            System.Diagnostics.Debug.WriteLine("RESUMED");
        }
        else if (play.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Paused)
        {
            // Pause - mevcut position'ı kaydet
            _lastPosition = _lastPosition + (DateTime.Now - _lastPlayStartTime);
            System.Diagnostics.Debug.WriteLine($"PAUSED at {_lastPosition:mm\\:ss}");
        }
        
        await ReadMediaAsync(s);
    }

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

            // Track ID oluştur (şarkı değişikliği tespiti için)
            string trackId = $"{props.Artist}|{props.Title}";
            
            // Şarkı değişti mi kontrol et
            if (trackId != _lastTrackId)
            {
                _lastTrackId = trackId;
                _lastPosition = TimeSpan.Zero;
                _lastPlayStartTime = DateTime.Now;
                System.Diagnostics.Debug.WriteLine($"NEW TRACK: {props.Title}");
            }
            
            // Position hesapla - oynatılıyorsa elapsed time ekle
            TimeSpan position = _lastPosition;
            if (isPlaying)
            {
                position = _lastPosition + (DateTime.Now - _lastPlayStartTime);
            }
            
            // Duration - SMTC'den alınamıyor, sıfır bırak
            TimeSpan duration = TimeSpan.Zero;

            MediaChanged?.Invoke(new MediaInfo
            {
                Title     = props.Title,
                Artist    = props.Artist,
                IsPlaying = isPlaying,
                Thumbnail = thumbnail,
                Position  = position,
                Duration  = duration,
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
