using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace MonitorExtender.Server;

/// <summary>
/// Cattura lo schermo primario e restituisce un bitmap gia' ridimensionato alla
/// risoluzione target. Bitmap e Graphics sono riusati tra un frame e l'altro:
/// allocarli a ogni giro costerebbe piu' della cattura stessa.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class ScreenCapturer : IDisposable
{
    private const int SmCxScreen = 0;
    private const int SmCyScreen = 1;
    private static readonly nint DpiAwarePerMonitorV2 = -4;

    private readonly Bitmap _full;
    private readonly Graphics _fullGraphics;
    private readonly Bitmap _scaled;
    private readonly Graphics _scaledGraphics;

    public int SourceWidth { get; }
    public int SourceHeight { get; }
    public int TargetWidth { get; }
    public int TargetHeight { get; }

    public ScreenCapturer(int targetHeight)
    {
        EnableDpiAwareness();

        SourceWidth = GetSystemMetrics(SmCxScreen);
        SourceHeight = GetSystemMetrics(SmCyScreen);
        if (SourceWidth <= 0 || SourceHeight <= 0)
            throw new InvalidOperationException("Impossibile leggere la risoluzione dello schermo primario.");

        // Non ingrandire mai: se lo schermo e' piu' basso del target, si resta 1:1.
        TargetHeight = Math.Min(targetHeight, SourceHeight);
        TargetWidth = (int)Math.Round(SourceWidth * (double)TargetHeight / SourceHeight);
        // Larghezza pari: fa piacere a qualsiasi encoder ci finisca dietro (vedi Fase 5).
        if (TargetWidth % 2 != 0) TargetWidth++;

        _full = new Bitmap(SourceWidth, SourceHeight, PixelFormat.Format32bppArgb);
        _fullGraphics = Graphics.FromImage(_full);

        _scaled = new Bitmap(TargetWidth, TargetHeight, PixelFormat.Format24bppRgb);
        _scaledGraphics = Graphics.FromImage(_scaled);
        _scaledGraphics.CompositingMode = CompositingMode.SourceCopy;
        _scaledGraphics.CompositingQuality = CompositingQuality.HighSpeed;
        _scaledGraphics.InterpolationMode = InterpolationMode.Bilinear;
        _scaledGraphics.SmoothingMode = SmoothingMode.None;
        _scaledGraphics.PixelOffsetMode = PixelOffsetMode.Half;
    }

    /// <summary>
    /// Cattura un frame e lo ridimensiona. Il bitmap restituito e' riusato al giro
    /// successivo: va consumato (encodato) prima di richiamare Capture.
    /// </summary>
    public Bitmap Capture()
    {
        _fullGraphics.CopyFromScreen(0, 0, 0, 0, new Size(SourceWidth, SourceHeight), CopyPixelOperation.SourceCopy);

        if (TargetWidth == SourceWidth && TargetHeight == SourceHeight)
            return _full;

        _scaledGraphics.DrawImage(_full, 0, 0, TargetWidth, TargetHeight);
        return _scaled;
    }

    private static void EnableDpiAwareness()
    {
        // Senza questo, su display con scaling != 100% Windows ci mente sulla
        // risoluzione e catturiamo un'immagine sfocata e piu' piccola del vero.
        try
        {
            if (SetProcessDpiAwarenessContext(DpiAwarePerMonitorV2)) return;
        }
        catch (EntryPointNotFoundException)
        {
            // Windows precedente a 10 1703: si ripiega sull'API storica.
        }

        try { SetProcessDPIAware(); }
        catch (EntryPointNotFoundException) { /* nulla da fare, si procede */ }
    }

    public void Dispose()
    {
        _scaledGraphics.Dispose();
        _scaled.Dispose();
        _fullGraphics.Dispose();
        _full.Dispose();
    }

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetProcessDpiAwarenessContext(nint value);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetProcessDPIAware();
}
