using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Windows.Forms;

[assembly: SupportedOSPlatform("windows")]

namespace MonitorExtender.Server;

internal static class Program
{
    private const int AttachParentProcess = -1;

    /// <summary>
    /// STA e obbligatorio per l'icona vicino all'orologio, e per questo il punto di ingresso
    /// non e' asincrono: Application.Run vuole restare sul thread principale.
    /// </summary>
    [STAThread]
    private static int Main(string[] args)
    {
        var options = StreamOptions.Parse(args);

        if (args.Contains("--help") || args.Contains("-h"))
        {
            AttachToConsole();
            PrintHelp();
            return 0;
        }

        if (args.Contains("--probe"))
        {
            AttachToConsole();
            return Probe(options);
        }

        if (args.Contains("--compare"))
        {
            AttachToConsole();
            return Compare(options);
        }

        // Senza --console il programma non ha finestra: si vive di icona e di registro.
        var headless = args.Contains("--console");
        if (headless) AttachToConsole();

        var settings = new LiveSettings(options);
        using var broker = new FrameBroker(settings);
        broker.Start();

        using var server = new MjpegServer(options, settings, broker);
        try
        {
            server.Start();
        }
        catch (Exception ex)
        {
            Log.Write($"Impossibile avviare il server sulla porta {options.Port}: {ex.Message}");
            return 1;
        }

        DiscoveryResponder? discovery = null;
        try
        {
            discovery = new DiscoveryResponder(options);
        }
        catch (Exception ex)
        {
            // Porta occupata o bloccata: si perde solo la ricerca automatica, non lo streaming.
            Log.Write($"[discovery] non disponibile ({ex.Message})");
        }

        var usb = UsbLinkWatcher.TryCreate(options.Port);
        if (usb == null) Log.Write("[usb] adb non trovato: il collegamento via cavo va attivato a mano");

        Announce(options, server, discovery, usb, headless);

        using var shutdown = new CancellationTokenSource();
        var background = Task.WhenAll(
            server.RunAsync(shutdown.Token),
            discovery?.RunAsync(shutdown.Token) ?? Task.CompletedTask,
            usb?.RunAsync(shutdown.Token) ?? Task.CompletedTask);

        if (headless)
        {
            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                shutdown.Cancel();
            };
            try { background.GetAwaiter().GetResult(); }
            catch (OperationCanceledException) { /* uscita richiesta */ }
        }
        else
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            using var tray = new TrayIcon(options, settings, broker, usb, Application.Exit);
            Application.Run();

            shutdown.Cancel();
            // Non si aspetta all'infinito: le connessioni aperte cadono comunque alla chiusura.
            background.Wait(TimeSpan.FromSeconds(3));
        }

        discovery?.Dispose();
        Log.Write("Server fermato.");
        return 0;
    }

    private static void PrintHelp() => Console.WriteLine("""
        MonitorExtender.Server — mirroring dello schermo via MJPEG

          --port <n>      porta HTTP (default 8080)
          --scale <n>     altezza target in pixel (default 720)
          --fps <n>       frame al secondo (default 20)
          --quality <n>   qualita' JPEG 1-100 (default 60)
          --console       resta in primo piano con il registro a schermo,
                          invece dell'icona vicino all'orologio
          --probe         cattura un solo frame, lo salva su disco ed esce
          --compare       mette a confronto GDI e Desktop Duplication ed esce
        """);

    private static void Announce(
        StreamOptions options,
        MjpegServer server,
        DiscoveryResponder? discovery,
        UsbLinkWatcher? usb,
        bool headless)
    {
        Log.Write($"MonitorExtender · {options}");
        Log.Write($"  locale        http://localhost:{options.Port}/stream");
        if (server.BoundToAllInterfaces)
            foreach (var ip in LanAddresses())
                Log.Write($"  dal telefono  http://{ip}:{options.Port}/stream");

        Log.Write("  /             pagina di prova a schermo intero");
        Log.Write("  /snapshot     singolo frame JPEG");
        Log.Write("  /info         parametri correnti in JSON");
        Log.Write("  controllo del mouse: solo da loopback (cavo USB)");
        if (discovery != null) Log.Write($"  discovery     UDP {DiscoveryResponder.DiscoveryPort} attiva");
        if (usb != null) Log.Write("  cavo USB      inoltro sorvegliato in automatico");
        Log.Write(headless ? "Ctrl+C per fermare." : $"Registro in {Log.FilePath}");
    }

    // --- Fase 1: verifica in isolamento della catena cattura -> downscale -> encode ---
    private static int Probe(StreamOptions options)
    {
        using var capturer = new ScreenCapturer(options.Scale);
        using var encoder = new JpegEncoder(options.Quality);

        // Un giro a vuoto per pagare una volta sola il costo di inizializzazione di GDI+,
        // altrimenti la misura del primo frame e' falsata verso l'alto.
        encoder.Encode(capturer.Capture());

        var captureWatch = Stopwatch.StartNew();
        var bitmap = capturer.Capture();
        captureWatch.Stop();

        var encodeWatch = Stopwatch.StartNew();
        var jpeg = encoder.Encode(bitmap);
        encodeWatch.Stop();

        var path = Path.Combine(Path.GetTempPath(), "monitorextender-probe.jpg");
        File.WriteAllBytes(path, jpeg);

        var totalMs = captureWatch.Elapsed.TotalMilliseconds + encodeWatch.Elapsed.TotalMilliseconds;
        Console.WriteLine($"sorgente    {capturer.SourceWidth}x{capturer.SourceHeight}");
        Console.WriteLine($"target      {capturer.TargetWidth}x{capturer.TargetHeight} (qualita' {options.Quality})");
        Console.WriteLine($"cattura     {captureWatch.Elapsed.TotalMilliseconds:F1} ms " +
                          $"(copia schermo {capturer.LastCopyMs:F1} + ridimensiona {capturer.LastScaleMs:F1})");
        Console.WriteLine($"encode      {encodeWatch.Elapsed.TotalMilliseconds:F1} ms");
        Console.WriteLine($"totale      {totalMs:F1} ms/frame → tetto teorico {1000 / totalMs:F0} fps");
        Console.WriteLine($"dimensione  {jpeg.Length / 1024} KB → {jpeg.Length * 8.0 * options.Fps / 1_000_000:F1} Mbit/s a {options.Fps} fps");
        Console.WriteLine($"salvato in  {path}");
        Console.WriteLine();
        Console.WriteLine("Nota: misurare mentre il server e' in esecuzione falsa il risultato,");
        Console.WriteLine("      perche' due catture dello stesso schermo si ostacolano.");
        return 0;
    }

    /// <summary>
    /// Mette a confronto le due catture **alternandole nello stesso ciclo**, non una dopo
    /// l'altra: la macchina cambia carico di continuo, e misurare in due momenti diversi
    /// direbbe piu' su com'era il PC che su quale tecnica sia migliore.
    ///
    /// Si misura fino al bitmap ridimensionato **e codificato in JPEG**, cioe' fino a quello
    /// che il server spedisce davvero. Fermarsi prima favorirebbe la duplicazione, che
    /// consegna una texture e non un'immagine pronta.
    /// </summary>
    private static int Compare(StreamOptions options)
    {
        const int rounds = 40;
        const int timeoutMs = 500;

        using var gdi = new ScreenCapturer(options.Scale);
        using var encoder = new JpegEncoder(options.Quality);

        DuplicationCapturer dxgi;
        try
        {
            dxgi = new DuplicationCapturer(options.Scale);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Desktop Duplication non disponibile: {ex.Message.Split('\n')[0]}");
            Console.WriteLine();
            Console.WriteLine("Diagnostica:");
            try { DuplicationCapturer.Diagnose(Console.Out); }
            catch (Exception inner) { Console.WriteLine($"  fallita anche la diagnostica: {inner.Message}"); }
            return 1;
        }

        using (dxgi)
        {
            Console.WriteLine($"Confronto su {rounds} giri, {gdi.SourceWidth}x{gdi.SourceHeight} → " +
                              $"{gdi.TargetWidth}x{gdi.TargetHeight}, qualita' {options.Quality}.");
            Console.WriteLine("Il cursore viene spostato di un pixel a ogni giro: senza un cambiamento");
            Console.WriteLine("sullo schermo la duplicazione non consegnerebbe alcun frame.");
            Console.WriteLine();

            // Riscaldamento: la prima cattura di entrambi paga inizializzazioni che non
            // si ripetono, e falserebbe la media.
            encoder.Encode(gdi.Capture());
            dxgi.Capture(timeoutMs);

            var gdiTimes = new List<double>(rounds);
            var dxgiTimes = new List<double>(rounds);
            var timeouts = 0;
            var watch = new Stopwatch();

            for (var round = 0; round < rounds; round++)
            {
                InputInjector.Nudge(round % 2 == 0 ? 1 : -1, 0);
                Thread.Sleep(20);

                watch.Restart();
                encoder.Encode(gdi.Capture());
                watch.Stop();
                gdiTimes.Add(watch.Elapsed.TotalMilliseconds);

                watch.Restart();
                var frame = dxgi.Capture(timeoutMs);
                if (frame == null)
                {
                    timeouts++;
                    continue;
                }
                encoder.Encode(frame);
                watch.Stop();
                dxgiTimes.Add(watch.Elapsed.TotalMilliseconds);
            }

            Report("GDI  CopyFromScreen", gdiTimes);
            Report("DXGI Duplication   ", dxgiTimes);
            if (timeouts > 0)
                Console.WriteLine($"\n{timeouts} giri senza frame nuovo dalla duplicazione (schermo fermo).");

            if (gdiTimes.Count > 0 && dxgiTimes.Count > 0)
            {
                var gain = Median(gdiTimes) / Median(dxgiTimes);
                Console.WriteLine();
                Console.WriteLine($"La duplicazione e' {gain:F1}× {(gain >= 1 ? "piu' veloce" : "piu' lenta")} " +
                                  $"sulla mediana → tetto teorico {1000 / Median(dxgiTimes):F0} fps " +
                                  $"contro {1000 / Median(gdiTimes):F0}.");
            }
        }

        return 0;
    }

    private static void Report(string label, List<double> times)
    {
        if (times.Count == 0)
        {
            Console.WriteLine($"{label} : nessuna misura");
            return;
        }

        Console.WriteLine($"{label} : mediana {Median(times):F1} ms · min {times.Min():F1} · " +
                          $"max {times.Max():F1} · media {times.Average():F1}  ({times.Count} misure)");
    }

    /// <summary>Mediana e non media: un singolo picco di sistema sposterebbe la media e non la conclusione.</summary>
    private static double Median(List<double> values)
    {
        var sorted = values.OrderBy(v => v).ToList();
        var middle = sorted.Count / 2;
        return sorted.Count % 2 == 1 ? sorted[middle] : (sorted[middle - 1] + sorted[middle]) / 2;
    }

    internal static IEnumerable<string> LanAddresses() =>
        NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n.OperationalStatus == OperationalStatus.Up)
            .Where(n => n.NetworkInterfaceType is NetworkInterfaceType.Wireless80211 or NetworkInterfaceType.Ethernet)
            // Le interfacce virtuali (WSL, Hyper-V, VPN) non sono raggiungibili dal telefono.
            .Where(n => !n.Description.Contains("Virtual", StringComparison.OrdinalIgnoreCase)
                        && !n.Description.Contains("Hyper-V", StringComparison.OrdinalIgnoreCase))
            .SelectMany(n => n.GetIPProperties().UnicastAddresses)
            .Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork)
            .Select(a => a.Address.ToString())
            .Where(a => !a.StartsWith("169.254", StringComparison.Ordinal))
            .Distinct();

    /// <summary>
    /// Il programma e' compilato senza console per non mostrare una finestra nera all'avvio
    /// automatico. Quando invece viene lanciato da un terminale, qui si riaggancia a quello
    /// del chiamante: senza, --probe e --console non stamperebbero nulla.
    /// </summary>
    private static void AttachToConsole()
    {
        // Se chi ci ha avviati ha gia' rediretto lo stdout su una pipe o su un file, quello
        // e' il posto giusto dove scrivere e non serve agganciarsi a nessuna console.
        if (BindStandardOutput()) return;
        if (AttachConsole(AttachParentProcess)) BindStandardOutput();
    }

    private static bool BindStandardOutput()
    {
        var stream = Console.OpenStandardOutput();
        if (stream == Stream.Null) return false;

        Console.SetOut(new StreamWriter(stream) { AutoFlush = true });
        return true;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AttachConsole(int processId);
}
