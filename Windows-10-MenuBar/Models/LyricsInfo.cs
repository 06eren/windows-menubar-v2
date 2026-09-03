namespace Windows_10_MenuBar.Models;

/// <summary>
/// Şarkı sözü bilgileri
/// </summary>
public class LyricsInfo
{
    public string Artist { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Lyrics { get; set; } = string.Empty;
    public string AlbumArtUrl { get; set; } = string.Empty;
    public bool IsAvailable { get; set; } = false;
    public DateTime CachedAt { get; set; } = DateTime.MinValue;
    
    // Senkronize lyrics için
    public List<LyricLine>? SyncedLyrics { get; set; }
}

/// <summary>
/// Timestamp'li şarkı sözü satırı
/// </summary>
public class LyricLine
{
    public TimeSpan Time { get; set; }
    public string Text { get; set; } = string.Empty;
}
