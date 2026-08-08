using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace MonitorExtender.Server;

public sealed record Frame(byte[] Jpeg, long Id);

/// <summary>
/// Un solo ciclo di cattura per tutti i client collegati. Se ogni client catturasse
/// per conto suo, due telefoni raddoppierebbero il costo CPU per la stessa immagine.
/// Il ciclo si ferma da solo quando non c'e' nessuno collegato.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class FrameBroker : IDisposable
{
    private readonly LiveSettings _settings;
    private readonly CancellationTokenSource _cts = new();
    private readonly Thread _thread;

    private TaskCompletionSource<Frame> _next = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Frame? _latest;
    private int _subscribers;

    public FrameBroker(LiveSettings settings)
    {
        _settings = settings;
        _thread = new Thread(CaptureLoop)
        {
            IsBackground = true,
            Name = "capture",
            // La cattura deve stare davanti al lavoro di rete: se slitta, slittano i frame.
            Priority = ThreadPriority.AboveNormal,
        };
    }

    public int SourceWidth { get; private set; }
    public int SourceHeight { get; private set; }
    public int TargetWidth { get; private set; }
    public int TargetHeight { get; private set; }

    /// <summary>Quanti client stanno guardando. Letto dall'icona vicino all'orologio.</summary>
    public int Clients => Volatile.Read(ref _subscribers);

    /// <summary>Fps dell'ultima finestra di misura; 0 finche' non ce n'e' una completa.</summary>
    public double Fps { get; private set; }

    public void Start() => _thread.Start();

    public Frame? Latest => Volatile.Read(ref _latest);

    public Task<Frame> NextFrameAsync() => Volatile.Read(ref _next).Task;

    /// <summary>Segnala che un client e' collegato; il Dispose del risultato lo scollega.</summary>
    public IDisposable Subscribe()
    {
        Interlocked.Increment(ref _subscribers);
        return new Subscription(this);
    }

    private void CaptureLoop()
    {
        // Di default il timer di Windows si sveglia ogni ~15 ms: un'attesa da 50 ms ne
        // dura 62 e i 20 fps richiesti diventano 16. Un tick da 1 ms rimette in riga
        // la cadenza, al prezzo di un filo di consumo in piu' finche' il server gira.
        var fineTimer = TimeBeginPeriod(1) == 0;

        var current = _settings.Read();
        var capturer = ScreenSourceFactory.Create(current.Scale);
        var encoder = new JpegEncoder(current.Quality);
        UpdateDimensions(capturer);

        var token = _cts.Token;
        var stopwatch = new Stopwatch();
        var report = Stopwatch.StartNew();
        var clock = Stopwatch.StartNew();
        var nextDueMs = 0L;
        var lastRebuildMs = 0L;
        long frameId = 0, framesInWindow = 0, msInWindow = 0, bytesInWindow = 0;

        // Cadenza ancorata a scadenze assolute: sommare l'attesa dopo il lavoro farebbe
        // accumulare il ritardo di ogni frame su tutti i successivi.
        void Pace()
        {
            nextDueMs += current.FrameIntervalMs;
            var now = clock.ElapsedMilliseconds;
            if (nextDueMs <= now)
                nextDueMs = now; // in ritardo cronico: si riparte da adesso, senza rincorrere
            else
                token.WaitHandle.WaitOne((int)(nextDueMs - now));
        }

        try
        {
            while (!token.IsCancellationRequested)
            {
                if (Volatile.Read(ref _subscribers) == 0)
                {
                    // Nessuno guarda: non ha senso bruciare CPU.
                    Fps = 0;
                    report.Restart();
                    framesInWindow = msInWindow = bytesInWindow = 0;
                    nextDueMs = clock.ElapsedMilliseconds;
                    token.WaitHandle.WaitOne(200);
                    continue;
                }

                if (_settings.Version != current.Version)
                {
                    // Un client ha chiesto parametri diversi: si ricostruiscono cattura ed
                    // encoder. Costa un paio di millisecondi e capita solo su richiesta.
                    var updated = _settings.Read();
                    if (updated.Scale != current.Scale)
                    {
                        capturer.Dispose();
                        capturer = ScreenSourceFactory.Create(updated.Scale);
                        UpdateDimensions(capturer);
                    }
                    if (updated.Quality != current.Quality)
                    {
                        encoder.Dispose();
                        encoder = new JpegEncoder(updated.Quality);
                    }
                    current = updated;
                    Log.Write($"[capture] parametri aggiornati: {_settings} " +
                              $"({capturer.TargetWidth}x{capturer.TargetHeight})");
                }

                stopwatch.Restart();
                Bitmap? bitmap;
                try
                {
                    bitmap = capturer.Capture(current.FrameIntervalMs);
                }
                catch (Exception ex)
                {
                    // Cattura fallita (cambio risoluzione, sessione bloccata, UAC in primo
                    // piano, o la duplicazione DXGI persa per cambio al desktop sicuro/blocco
                    // sessione/reset driver): si ricostruisce il capturer, ma non a ogni giro -
                    // al massimo ogni 3s, altrimenti un fallimento persistente lo terrebbe
                    // occupato in un loop di ricostruzioni costose invece che aspettare e basta.
                    // ScreenSourceFactory riprova DXGI e ripiega su GDI se serve, da sola.
                    Log.Write($"[capture] frame saltato: {ex.Message}");
                    var now = clock.ElapsedMilliseconds;
                    if (now - lastRebuildMs >= 3000)
                    {
                        lastRebuildMs = now;
                        capturer.Dispose();
                        capturer = ScreenSourceFactory.Create(current.Scale);
                        UpdateDimensions(capturer);
                    }
                    token.WaitHandle.WaitOne(200);
                    continue;
                }
                stopwatch.Stop();

                if (bitmap == null)
                {
                    // Solo DXGI puo' restituirlo: nessun cambiamento sullo schermo entro il
                    // timeout, non e' un errore. Si evita di ricodificare e ripubblicare lo
                    // stesso frame di prima - il client resta comunque fermo sull'ultimo.
                    Pace();
                    continue;
                }

                byte[] jpeg;
                try
                {
                    jpeg = encoder.Encode(bitmap);
                }
                catch (Exception ex)
                {
                    Log.Write($"[capture] frame saltato: {ex.Message}");
                    token.WaitHandle.WaitOne(200);
                    continue;
                }

                Publish(new Frame(jpeg, ++frameId));

                framesInWindow++;
                msInWindow += stopwatch.ElapsedMilliseconds;
                bytesInWindow += jpeg.Length;

                if (report.ElapsedMilliseconds >= 5000)
                {
                    var avgMs = msInWindow / (double)framesInWindow;
                    var fps = framesInWindow * 1000.0 / report.ElapsedMilliseconds;
                    var mbps = bytesInWindow * 8.0 / report.ElapsedMilliseconds / 1000.0;
                    Fps = fps;
                    Log.Write(
                        $"[capture] {fps:F1} fps effettivi · {avgMs:F1} ms/frame · " +
                        $"{bytesInWindow / framesInWindow / 1024} KB/frame · {mbps:F1} Mbit/s · " +
                        $"{Volatile.Read(ref _subscribers)} client");
                    report.Restart();
                    framesInWindow = msInWindow = bytesInWindow = 0;
                }

                Pace();
            }
        }
        finally
        {
            capturer.Dispose();
            encoder.Dispose();
            if (fineTimer) TimeEndPeriod(1);
        }
    }

    private void UpdateDimensions(IScreenSource capturer)
    {
        SourceWidth = capturer.SourceWidth;
        SourceHeight = capturer.SourceHeight;
        TargetWidth = capturer.TargetWidth;
        TargetHeight = capturer.TargetHeight;
    }

    private void Publish(Frame frame)
    {
        Volatile.Write(ref _latest, frame);
        var waiters = Interlocked.Exchange(ref _next, new TaskCompletionSource<Frame>(TaskCreationOptions.RunContinuationsAsynchronously));
        waiters.TrySetResult(frame);
    }

    public void Dispose()
    {
        _cts.Cancel();
        if (_thread.IsAlive) _thread.Join(TimeSpan.FromSeconds(2));
        _cts.Dispose();
    }

    [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod")]
    private static extern uint TimeBeginPeriod(uint ms);

    [DllImport("winmm.dll", EntryPoint = "timeEndPeriod")]
    private static extern uint TimeEndPeriod(uint ms);

    private sealed class Subscription(FrameBroker broker) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
                Interlocked.Decrement(ref broker._subscribers);
        }
    }
}
