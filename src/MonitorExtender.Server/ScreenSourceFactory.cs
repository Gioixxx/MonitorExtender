using System.Runtime.Versioning;

namespace MonitorExtender.Server;

/// <summary>
/// Sceglie il capturer per lo stream live: Desktop Duplication (DXGI) se disponibile, altrimenti
/// GDI. La duplicazione puo' fallire per motivi che non dipendono dal codice (nessun monitor
/// collegato, sessione Remote Desktop, un altro processo la occupa gia' - Windows ne ammette una
/// sola per uscita), quindi il fallback non e' opzionale: e' la normale condizione operativa di
/// un server avviato in automatico al login, senza nessuno pronto a risolvere a mano.
/// </summary>
[SupportedOSPlatform("windows")]
internal static class ScreenSourceFactory
{
    public static IScreenSource Create(int scale, bool preferGdi = false)
    {
        if (preferGdi)
        {
            Log.Write("[capture] sorgente: GDI (modalita' compatibilita' forzata)");
            return new ScreenCapturer(scale);
        }

        try
        {
            var dxgi = new DuplicationCapturer(scale);
            Log.Write($"[capture] sorgente: {dxgi.Name}");
            return dxgi;
        }
        catch (Exception ex)
        {
            Log.Write($"[capture] Desktop Duplication non disponibile ({ex.Message.Split('\n')[0]}) — uso GDI");
            return new ScreenCapturer(scale);
        }
    }
}
