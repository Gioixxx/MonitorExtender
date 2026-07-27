namespace MonitorExtender.Server;

/// <summary>
/// I tre parametri che governano la banda: risoluzione (altezza target, larghezza
/// derivata mantenendo l'aspect ratio), fps e qualita' JPEG.
/// </summary>
public sealed class StreamOptions
{
    public const int DefaultPort = 8080;
    public const int DefaultScale = 720;
    public const int DefaultFps = 20;
    public const int DefaultQuality = 60;

    public int Port { get; init; } = DefaultPort;

    /// <summary>Altezza target in pixel. La larghezza segue l'aspect ratio dello schermo.</summary>
    public int Scale { get; init; } = DefaultScale;

    public int Fps { get; init; } = DefaultFps;

    /// <summary>Qualita' JPEG 1-100.</summary>
    public int Quality { get; init; } = DefaultQuality;

    public int FrameIntervalMs => Math.Max(1, 1000 / Fps);

    public static StreamOptions Parse(string[] args)
    {
        var port = DefaultPort;
        var scale = DefaultScale;
        var fps = DefaultFps;
        var quality = DefaultQuality;

        for (var i = 0; i < args.Length; i++)
        {
            var name = args[i];
            if (!name.StartsWith("--", StringComparison.Ordinal)) continue;

            // Accetta sia "--fps 20" sia "--fps=20".
            string? value;
            var eq = name.IndexOf('=');
            if (eq >= 0)
            {
                value = name[(eq + 1)..];
                name = name[..eq];
            }
            else
            {
                value = i + 1 < args.Length ? args[i + 1] : null;
            }

            switch (name)
            {
                case "--port" when int.TryParse(value, out var p): port = p; break;
                case "--scale" when int.TryParse(value, out var s): scale = s; break;
                case "--fps" when int.TryParse(value, out var f): fps = f; break;
                case "--quality" or "--q" when int.TryParse(value, out var q): quality = q; break;
            }
        }

        return new StreamOptions
        {
            Port = Math.Clamp(port, 1, 65535),
            Scale = Math.Clamp(scale, 120, 2160),
            Fps = Math.Clamp(fps, 1, 60),
            Quality = Math.Clamp(quality, 1, 100),
        };
    }

    public override string ToString() => $"scale={Scale}p fps={Fps} quality={Quality}";
}
