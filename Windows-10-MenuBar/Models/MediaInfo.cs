using System.Windows.Media;
using Wpf.Ui.Common;

namespace Windows_10_MenuBar.Models;

public class MediaInfo
{
    public string Title  { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public bool IsPlaying { get; set; }

    /// <summary>
    /// Bir medya kaynağı yüklü mü (oynatılıyor VEYA duraklatılmış)?
    /// Bu true olduğu sürece bar'daki medya paneli görünür kalır.
    /// </summary>
    public bool HasMedia => !string.IsNullOrEmpty(Title);

    /// <summary>Oynatma durumuna göre Play/Pause ikonu.</summary>
    public Wpf.Ui.Common.SymbolRegular PlayPauseIcon =>
        IsPlaying ? Wpf.Ui.Common.SymbolRegular.Pause24
                  : Wpf.Ui.Common.SymbolRegular.Play24;

    /// <summary>SMTC'den alınan kapak fotoğrafı. Spotify, YouTube, tarayıcı vs. destekler.</summary>
    public ImageSource? Thumbnail { get; set; }
}
