namespace MonitorExtender.Server;

/// <summary>
/// I parametri dello stream, modificabili mentre il server gira tramite query string
/// (`/stream?fps=15&amp;q=40&amp;scale=480`).
///
/// La cattura e' una sola e condivisa, quindi anche questi parametri sono condivisi: l'ultima
/// richiesta vince e vale per tutti i client collegati. Per un mirroring personale, dove il
/// client e' normalmente uno, e' il compromesso giusto — l'alternativa (un ciclo di cattura
/// per profilo) moltiplicherebbe il costo CPU per risolvere un problema che non si presenta.
/// </summary>
public sealed class LiveSettings
{
    private readonly object _lock = new();
    private int _version;

    public LiveSettings(StreamOptions options)
    {
        Scale = options.Scale;
        Fps = options.Fps;
        Quality = options.Quality;
    }

    public int Scale { get; private set; }
    public int Fps { get; private set; }
    public int Quality { get; private set; }

    /// <summary>Cambia a ogni modifica: il ciclo di cattura lo confronta per sapere se ricostruirsi.</summary>
    public int Version => Volatile.Read(ref _version);

    public int FrameIntervalMs => Math.Max(1, 1000 / Fps);

    public Snapshot Read()
    {
        lock (_lock) return new Snapshot(Scale, Fps, Quality, Version);
    }

    /// <summary>Applica i valori indicati (null = invariato). Restituisce true se qualcosa e' cambiato.</summary>
    public bool Apply(int? scale, int? fps, int? quality)
    {
        lock (_lock)
        {
            var newScale = scale.HasValue ? Math.Clamp(scale.Value, 120, 2160) : Scale;
            var newFps = fps.HasValue ? Math.Clamp(fps.Value, 1, 60) : Fps;
            var newQuality = quality.HasValue ? Math.Clamp(quality.Value, 1, 100) : Quality;

            if (newScale == Scale && newFps == Fps && newQuality == Quality) return false;

            Scale = newScale;
            Fps = newFps;
            Quality = newQuality;
            Interlocked.Increment(ref _version);
            return true;
        }
    }

    public override string ToString() => $"scale={Scale}p fps={Fps} quality={Quality}";

    public readonly record struct Snapshot(int Scale, int Fps, int Quality, int Version)
    {
        public int FrameIntervalMs => Math.Max(1, 1000 / Fps);
    }
}
