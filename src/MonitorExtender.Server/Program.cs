using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.Versioning;
using MonitorExtender.Server;

[assembly: SupportedOSPlatform("windows")]

var options = StreamOptions.Parse(args);

if (args.Contains("--help") || args.Contains("-h"))
{
    Console.WriteLine("""
        MonitorExtender.Server — mirroring dello schermo via MJPEG

          --port <n>      porta HTTP (default 8080)
          --scale <n>     altezza target in pixel (default 720)
          --fps <n>       frame al secondo (default 20)
          --quality <n>   qualita' JPEG 1-100 (default 60)
          --probe         cattura un solo frame, lo salva su disco ed esce
        """);
    return 0;
}

if (args.Contains("--probe"))
    return Probe(options);

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
    Console.Error.WriteLine($"Impossibile avviare il server sulla porta {options.Port}: {ex.Message}");
    return 1;
}

using var shutdown = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    shutdown.Cancel();
};

Console.WriteLine($"MonitorExtender · {options}");
Console.WriteLine($"  locale        http://localhost:{options.Port}/stream");
if (server.BoundToAllInterfaces)
{
    foreach (var ip in GetLanAddresses())
        Console.WriteLine($"  dal telefono  http://{ip}:{options.Port}/stream");
}
Console.WriteLine("  /             pagina di prova a schermo intero");
Console.WriteLine("  /snapshot     singolo frame JPEG");
Console.WriteLine("  /info         parametri correnti in JSON");
Console.WriteLine("  /input        controllo del mouse — solo da loopback (cavo USB)");
Console.WriteLine("  parametri live: /stream?fps=15&q=40&scale=480");

DiscoveryResponder? discovery = null;
try
{
    discovery = new DiscoveryResponder(options);
    Console.WriteLine($"  discovery     UDP {DiscoveryResponder.DiscoveryPort} attiva");
}
catch (Exception ex)
{
    // Porta occupata o bloccata: si perde solo la ricerca automatica, non lo streaming.
    Console.WriteLine($"  discovery     non disponibile ({ex.Message})");
}

Console.WriteLine("Ctrl+C per fermare.");
Console.WriteLine();

var serving = server.RunAsync(shutdown.Token);
var discovering = discovery?.RunAsync(shutdown.Token) ?? Task.CompletedTask;
await Task.WhenAll(serving, discovering);

discovery?.Dispose();
Console.WriteLine("Server fermato.");
return 0;

// --- Fase 1: verifica in isolamento della catena cattura -> downscale -> encode ---
static int Probe(StreamOptions options)
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
    Console.WriteLine($"cattura     {captureWatch.Elapsed.TotalMilliseconds:F1} ms");
    Console.WriteLine($"encode      {encodeWatch.Elapsed.TotalMilliseconds:F1} ms");
    Console.WriteLine($"totale      {totalMs:F1} ms/frame → tetto teorico {1000 / totalMs:F0} fps");
    Console.WriteLine($"dimensione  {jpeg.Length / 1024} KB → {jpeg.Length * 8.0 * options.Fps / 1_000_000:F1} Mbit/s a {options.Fps} fps");
    Console.WriteLine($"salvato in  {path}");
    return 0;
}

static IEnumerable<string> GetLanAddresses() =>
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
