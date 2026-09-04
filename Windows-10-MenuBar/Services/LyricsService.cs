using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows_10_MenuBar.Models;

namespace Windows_10_MenuBar.Services;

/// <summary>
/// Şarkı sözlerini çeker ve cache'ler.
/// Genius API kullanır - API key gerekmeden public scraping.
/// </summary>
public class LyricsService
{
    private static readonly HttpClient _http = new()
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    // Cache: "Artist - Title" -> LyricsInfo
    private readonly Dictionary<string, LyricsInfo> _cache = new();
    private readonly TimeSpan _cacheExpiration = TimeSpan.FromHours(24);

    /// <summary>
    /// Şarkı sözlerini al (cache'den veya API'den)
    /// </summary>
    public async Task<LyricsInfo?> GetLyricsAsync(string artist, string title)
    {
        if (string.IsNullOrWhiteSpace(artist) || string.IsNullOrWhiteSpace(title))
            return null;

        var cacheKey = $"{artist} - {title}".ToLowerInvariant();

        // Cache kontrolü
        if (_cache.TryGetValue(cacheKey, out var cached))
        {
            if (DateTime.Now - cached.CachedAt < _cacheExpiration)
                return cached;
            
            _cache.Remove(cacheKey);
        }

        // Yeni fetch
        var lyrics = await FetchLyricsAsync(artist, title);
        
        if (lyrics != null)
        {
            lyrics.CachedAt = DateTime.Now;
            
            // LRC format parse et (eğer varsa)
            lyrics.SyncedLyrics = ParseLrcFormat(lyrics.Lyrics);
            
            _cache[cacheKey] = lyrics;
        }

        return lyrics;
    }

    /// <summary>
    /// LRC format şarkı sözlerini parse et
    /// Format: [mm:ss.xx]Lyric text
    /// </summary>
    private List<LyricLine>? ParseLrcFormat(string lyrics)
    {
        if (string.IsNullOrEmpty(lyrics))
            return null;

        var lines = new List<LyricLine>();
        var lrcPattern = @"\[(\d{2}):(\d{2})(?:\.(\d{2}))?\](.*)";
        
        bool hasLrcFormat = false;

        foreach (var line in lyrics.Split('\n'))
        {
            var match = Regex.Match(line.Trim(), lrcPattern);
            if (match.Success)
            {
                hasLrcFormat = true;
                int minutes = int.Parse(match.Groups[1].Value);
                int seconds = int.Parse(match.Groups[2].Value);
                int milliseconds = match.Groups[3].Success ? int.Parse(match.Groups[3].Value) * 10 : 0;
                string text = match.Groups[4].Value.Trim();

                if (!string.IsNullOrWhiteSpace(text))
                {
                    lines.Add(new LyricLine
                    {
                        Time = new TimeSpan(0, 0, minutes, seconds, milliseconds),
                        Text = text
                    });
                }
            }
        }

        // LRC formatı yoksa, normal lyrics'i satır satır 3 saniye arayla ekle (fallback)
        if (!hasLrcFormat && !string.IsNullOrEmpty(lyrics))
        {
            var lyricsLines = lyrics.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            double secondsPerLine = 3.0; // Her satır 3 saniye
            
            for (int i = 0; i < lyricsLines.Length; i++)
            {
                var text = lyricsLines[i].Trim();
                if (!string.IsNullOrWhiteSpace(text) && 
                    !text.Contains("bulunamadı") && 
                    !text.Contains("yüklenemedi"))
                {
                    lines.Add(new LyricLine
                    {
                        Time = TimeSpan.FromSeconds(i * secondsPerLine),
                        Text = text
                    });
                }
            }
        }

        return lines.Count > 0 ? lines : null;
    }

    /// <summary>
    /// Cache'i temizle
    /// </summary>
    public void ClearCache() => _cache.Clear();

    // ── Private Methods ───────────────────────────────────────────────────────

    private async Task<LyricsInfo?> FetchLyricsAsync(string artist, string title)
    {
        try
        {
            // 1. LRCLIB (en iyi quality)
            var lrcLyrics = await FetchLrcLibAsync(artist, title);
            if (lrcLyrics != null)
            {
                System.Diagnostics.Debug.WriteLine($"Lyrics found: LRCLIB - {artist} - {title}");
                return lrcLyrics;
            }

            // 2. LRCGET (alternatif LRC source)
            lrcLyrics = await FetchLrcGetAsync(artist, title);
            if (lrcLyrics != null)
            {
                System.Diagnostics.Debug.WriteLine($"Lyrics found: LRCGET - {artist} - {title}");
                return lrcLyrics;
            }

            // 3. Musixmatch (community lyrics)
            var mxmLyrics = await FetchMusixmatchAsync(artist, title);
            if (mxmLyrics != null)
            {
                System.Diagnostics.Debug.WriteLine($"Lyrics found: Musixmatch - {artist} - {title}");
                return mxmLyrics;
            }

            // 4. Genius (fallback - text only)
            System.Diagnostics.Debug.WriteLine($"Trying Genius fallback for {artist} - {title}");
            return await FetchGeniusLyricsAsync(artist, title);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Lyrics fetch error: {ex.Message}");
            return new LyricsInfo
            {
                Artist = artist,
                Title = title,
                Lyrics = "Şarkı sözleri yüklenirken hata oluştu",
                IsAvailable = false
            };
        }
    }

    /// <summary>
    /// LRCLIB API - en popüler LRC source
    /// </summary>
    private async Task<LyricsInfo?> FetchLrcLibAsync(string artist, string title)
    {
        try
        {
            var artistEncoded = Uri.EscapeDataString(artist);
            var titleEncoded = Uri.EscapeDataString(title);
            var url = $"https://lrclib.net/api/get?artist_name={artistEncoded}&track_name={titleEncoded}";
            
            var response = await _http.GetStringAsync(url);
            using var doc = JsonDocument.Parse(response);
            var root = doc.RootElement;

            // Senkronize lyrics var mı?
            if (root.TryGetProperty("syncedLyrics", out var syncedLyrics) && 
                syncedLyrics.ValueKind != JsonValueKind.Null)
            {
                var lrcText = syncedLyrics.GetString();
                if (!string.IsNullOrEmpty(lrcText))
                {
                    return new LyricsInfo
                    {
                        Artist = artist,
                        Title = title,
                        Lyrics = lrcText,
                        IsAvailable = true
                    };
                }
            }

            // Plain lyrics varsa onu kullan
            if (root.TryGetProperty("plainLyrics", out var plainLyrics) && 
                plainLyrics.ValueKind != JsonValueKind.Null)
            {
                var text = plainLyrics.GetString();
                if (!string.IsNullOrEmpty(text))
                {
                    return new LyricsInfo
                    {
                        Artist = artist,
                        Title = title,
                        Lyrics = text,
                        IsAvailable = true
                    };
                }
            }
        }
        catch { }
        
        return null;
    }

    /// <summary>
    /// Musixmatch community lyrics (ücretsiz public API)
    /// </summary>
    private async Task<LyricsInfo?> FetchMusixmatchAsync(string artist, string title)
    {
        try
        {
            // Musixmatch public search endpoint
            var query = Uri.EscapeDataString($"{artist} {title}");
            var url = $"https://apic-desktop.musixmatch.com/ws/1.1/macro.subtitles.get?format=json&q={query}&user_language=en";
            
            _http.DefaultRequestHeaders.Clear();
            _http.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");
            
            var response = await _http.GetStringAsync(url);
            using var doc = JsonDocument.Parse(response);
            
            // Lyrics path: message.body.macro_calls.track.subtitles.get.message.body.subtitle_list
            if (doc.RootElement.TryGetProperty("message", out var message) &&
                message.TryGetProperty("body", out var body) &&
                body.TryGetProperty("macro_calls", out var macros))
            {
                foreach (var call in macros.EnumerateObject())
                {
                    if (call.Value.TryGetProperty("message", out var callMsg) &&
                        callMsg.TryGetProperty("body", out var callBody) &&
                        callBody.TryGetProperty("subtitle_list", out var subList) &&
                        subList.GetArrayLength() > 0)
                    {
                        var firstSub = subList[0];
                        if (firstSub.TryGetProperty("subtitle", out var subtitle) &&
                            subtitle.TryGetProperty("subtitle_body", out var subBody))
                        {
                            var lrcText = subBody.GetString();
                            if (!string.IsNullOrEmpty(lrcText))
                            {
                                return new LyricsInfo
                                {
                                    Artist = artist,
                                    Title = title,
                                    Lyrics = lrcText,
                                    IsAvailable = true
                                };
                            }
                        }
                    }
                }
            }
        }
        catch { }
        
        return null;
    }

    /// <summary>
    /// Alternative LRC source - LRCGET
    /// </summary>
    private async Task<LyricsInfo?> FetchLrcGetAsync(string artist, string title)
    {
        try
        {
            var query = Uri.EscapeDataString($"{artist} {title}");
            var url = $"https://lrcget.com/api/search?q={query}";
            
            var response = await _http.GetStringAsync(url);
            using var doc = JsonDocument.Parse(response);
            
            if (doc.RootElement.TryGetProperty("results", out var results) && 
                results.GetArrayLength() > 0)
            {
                var firstResult = results[0];
                if (firstResult.TryGetProperty("lrc", out var lrc))
                {
                    var lrcText = lrc.GetString();
                    if (!string.IsNullOrEmpty(lrcText))
                    {
                        return new LyricsInfo
                        {
                            Artist = artist,
                            Title = title,
                            Lyrics = lrcText,
                            IsAvailable = true
                        };
                    }
                }
            }
        }
        catch { }
        
        return null;
    }

    /// <summary>
    /// Genius fallback - text only
    /// </summary>
    private async Task<LyricsInfo?> FetchGeniusLyricsAsync(string artist, string title)
    {
        try
        {
            var searchUrl = BuildGeniusSearchUrl(artist, title);
            var searchResponse = await _http.GetStringAsync(searchUrl);
            
            var geniusUrl = ExtractGeniusUrl(searchResponse);
            if (string.IsNullOrEmpty(geniusUrl))
            {
                return new LyricsInfo
                {
                    Artist = artist,
                    Title = title,
                    Lyrics = "Şarkı sözleri bulunamadı 😔",
                    IsAvailable = false
                };
            }

            var pageHtml = await _http.GetStringAsync(geniusUrl);
            var lyrics = ExtractLyricsFromHtml(pageHtml);
            var albumArt = ExtractAlbumArtFromHtml(pageHtml);

            return new LyricsInfo
            {
                Artist = artist,
                Title = title,
                Lyrics = lyrics ?? "Şarkı sözleri yüklenemedi",
                AlbumArtUrl = albumArt ?? string.Empty,
                IsAvailable = !string.IsNullOrEmpty(lyrics)
            };
        }
        catch
        {
            return null;
        }
    }

    private string BuildGeniusSearchUrl(string artist, string title)
    {
        // Genius public search (API key gerektirmez)
        var query = Uri.EscapeDataString($"{artist} {title}");
        return $"https://genius.com/api/search/multi?per_page=5&q={query}";
    }

    private string? ExtractGeniusUrl(string jsonResponse)
    {
        try
        {
            using var doc = JsonDocument.Parse(jsonResponse);
            var sections = doc.RootElement
                .GetProperty("response")
                .GetProperty("sections");

            foreach (var section in sections.EnumerateArray())
            {
                if (!section.TryGetProperty("hits", out var hits))
                    continue;

                foreach (var hit in hits.EnumerateArray())
                {
                    if (!hit.TryGetProperty("result", out var result))
                        continue;

                    if (!result.TryGetProperty("url", out var url))
                        continue;

                    var urlString = url.GetString();
                    if (!string.IsNullOrEmpty(urlString) && urlString.Contains("genius.com"))
                        return urlString;
                }
            }
        }
        catch { }

        return null;
    }

    private string? ExtractLyricsFromHtml(string html)
    {
        try
        {
            // Genius HTML'inden lyrics div'ini bul
            // Lyrics genellikle data-lyrics-container="true" attribute'una sahip div'lerde
            var lyricsPattern = @"<div[^>]*data-lyrics-container=""true""[^>]*>(.*?)</div>";
            var matches = Regex.Matches(html, lyricsPattern, RegexOptions.Singleline | RegexOptions.IgnoreCase);

            if (matches.Count == 0)
                return null;

            var lyrics = string.Join("\n\n", matches.Select(m => m.Groups[1].Value));

            // HTML tag'lerini temizle
            lyrics = Regex.Replace(lyrics, @"<br\s*/?>", "\n", RegexOptions.IgnoreCase);
            lyrics = Regex.Replace(lyrics, @"<[^>]+>", "");
            lyrics = System.Net.WebUtility.HtmlDecode(lyrics);
            lyrics = lyrics.Trim();

            return string.IsNullOrWhiteSpace(lyrics) ? null : lyrics;
        }
        catch
        {
            return null;
        }
    }

    private string? ExtractAlbumArtFromHtml(string html)
    {
        try
        {
            // Album art genellikle meta property="og:image" içinde
            var match = Regex.Match(html, @"<meta\s+property=""og:image""\s+content=""([^""]+)""", RegexOptions.IgnoreCase);
            if (match.Success)
                return match.Groups[1].Value;

            // Alternatif: song_art_image_url
            match = Regex.Match(html, @"""song_art_image_url""\s*:\s*""([^""]+)""", RegexOptions.IgnoreCase);
            if (match.Success)
                return match.Groups[1].Value;
        }
        catch { }

        return null;
    }

    /// <summary>
    /// Cache boyutunu döndür (test/debug için)
    /// </summary>
    public int GetCacheSize() => _cache.Count;
}
