using System.Drawing;
using System.Runtime.Versioning;

namespace MonitorExtender.Server;

/// <summary>
/// Sorgente dei frame. Esiste per poter mettere a confronto due modi di catturare lo schermo
/// senza cambiare nulla a valle: quello che conta e' il costo per arrivare a un bitmap gia'
/// ridimensionato e pronto da codificare, non il costo della singola chiamata di sistema.
/// </summary>
[SupportedOSPlatform("windows")]
public interface IScreenSource : IDisposable
{
    string Name { get; }

    int SourceWidth { get; }
    int SourceHeight { get; }
    int TargetWidth { get; }
    int TargetHeight { get; }

    /// <summary>
    /// Il bitmap restituito e' riusato al giro successivo: va consumato prima di richiamare
    /// Capture. Restituisce null se non c'e' un frame nuovo entro il tempo concesso.
    /// </summary>
    Bitmap? Capture(int timeoutMs = 100);
}
