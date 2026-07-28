using System.Diagnostics;

namespace MonitorExtender.Server;

/// <summary>
/// Tiene in piedi l'inoltro della porta lungo il cavo.
///
/// `adb reverse` non sopravvive allo scollegamento del cavo ne' al riavvio del PC: finora
/// andava rilanciato a mano ogni volta. Qui il controllo diventa continuo — appena un
/// dispositivo autorizzato compare e l'inoltro manca, viene ristabilito.
///
/// Il ritmo cambia a seconda della situazione: con il cavo attaccato basta controllare di rado
/// che l'inoltro regga, senza cavo si guarda piu' spesso per accorgersi in fretta quando
/// arriva. Ogni controllo e' un avvio di processo, quindi non va fatto ogni secondo.
/// </summary>
public sealed class UsbLinkWatcher
{
    private static readonly TimeSpan WhenLinked = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan WhenWaiting = TimeSpan.FromSeconds(4);

    private readonly int _port;
    private readonly string _adb;

    private UsbLinkWatcher(string adb, int port)
    {
        _adb = adb;
        _port = port;
    }

    public bool Linked { get; private set; }

    /// <summary>Restituisce null se adb non e' installato: la sorveglianza semplicemente non parte.</summary>
    public static UsbLinkWatcher? TryCreate(int port)
    {
        var adb = FindAdb();
        return adb == null ? null : new UsbLinkWatcher(adb, port);
    }

    private static string? FindAdb()
    {
        var candidates = new List<string>();

        foreach (var variable in new[] { "ANDROID_HOME", "ANDROID_SDK_ROOT" })
        {
            var root = Environment.GetEnvironmentVariable(variable);
            if (!string.IsNullOrWhiteSpace(root))
                candidates.Add(Path.Combine(root, "platform-tools", "adb.exe"));
        }

        candidates.Add(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Android", "Sdk", "platform-tools", "adb.exe"));

        foreach (var candidate in candidates)
            if (File.Exists(candidate)) return candidate;

        // Ultimo tentativo: adb nel PATH.
        foreach (var directory in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';'))
        {
            if (string.IsNullOrWhiteSpace(directory)) continue;
            try
            {
                var candidate = Path.Combine(directory.Trim(), "adb.exe");
                if (File.Exists(candidate)) return candidate;
            }
            catch (ArgumentException)
            {
                // Voce di PATH malformata: si passa alla successiva.
            }
        }

        return null;
    }

    public async Task RunAsync(CancellationToken token)
    {
        Log.Write($"[usb] sorveglianza attiva ({_adb})");

        while (!token.IsCancellationRequested)
        {
            try
            {
                Check();
            }
            catch (Exception ex)
            {
                Log.Write($"[usb] controllo fallito: {ex.Message}");
            }

            try
            {
                await Task.Delay(Linked ? WhenLinked : WhenWaiting, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    /// <summary>Ristabilisce l'inoltro adesso, senza aspettare il prossimo giro.</summary>
    public void LinkNow()
    {
        try
        {
            Check(force: true);
        }
        catch (Exception ex)
        {
            Log.Write($"[usb] collegamento non riuscito: {ex.Message}");
        }
    }

    private void Check(bool force = false)
    {
        if (!HasDevice())
        {
            if (Linked) Log.Write("[usb] dispositivo scollegato");
            Linked = false;
            return;
        }

        if (!force && Linked && HasReverse()) return;

        if (force || !HasReverse())
        {
            Run($"reverse tcp:{_port} tcp:{_port}");
            if (HasReverse())
            {
                if (!Linked) Log.Write($"[usb] inoltro ristabilito sulla porta {_port}");
                Linked = true;
                return;
            }
            Linked = false;
            return;
        }

        Linked = true;
    }

    /// <summary>Vero se c'e' almeno un dispositivo autorizzato: "unauthorized" e "offline" non contano.</summary>
    private bool HasDevice() =>
        Run("devices").Split('\n')
            .Skip(1)
            .Any(line => line.TrimEnd().EndsWith("\tdevice", StringComparison.Ordinal));

    private bool HasReverse() =>
        Run("reverse --list").Contains($"tcp:{_port} tcp:{_port}", StringComparison.Ordinal);

    private string Run(string arguments)
    {
        using var process = Process.Start(new ProcessStartInfo(_adb, arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        });

        if (process == null) return "";

        var output = process.StandardOutput.ReadToEnd();
        if (!process.WaitForExit(5000))
        {
            try { process.Kill(); } catch { /* gia' terminato */ }
            return "";
        }
        return output;
    }
}
