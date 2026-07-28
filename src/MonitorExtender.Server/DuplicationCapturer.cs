using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;

namespace MonitorExtender.Server;

/// <summary>
/// Cattura tramite Desktop Duplication API (DXGI).
///
/// Differenza sostanziale rispetto a CopyFromScreen: qui il frame arriva gia' pronto dalla
/// scheda video, senza che la CPU debba leggere il framebuffer attraverso GDI. In piu' la
/// duplicazione consegna un frame **solo quando lo schermo cambia**, quindi su un desktop fermo
/// non si paga nulla, mentre GDI ricopia comunque tutto ogni volta.
///
/// Il prezzo e' il trasferimento dalla memoria video a quella di sistema: la texture del
/// desktop va copiata in una texture di staging e poi mappata. E' l'unico punto in cui la CPU
/// tocca i pixel, ed e' incluso nella misura perche' altrimenti il confronto sarebbe truccato.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class DuplicationCapturer : IScreenSource
{
    private readonly ID3D11Device _device;
    private readonly ID3D11DeviceContext _context;
    private readonly IDXGIOutputDuplication _duplication;
    private readonly ID3D11Texture2D _staging;

    private readonly Bitmap _full;
    private readonly Bitmap _scaled;
    private readonly Graphics _scaledGraphics;

    public string Name => "Desktop Duplication (DXGI)";

    public int SourceWidth { get; }
    public int SourceHeight { get; }
    public int TargetWidth { get; }
    public int TargetHeight { get; }

    public DuplicationCapturer(int targetHeight)
    {
        using var factory = DXGI.CreateDXGIFactory1<IDXGIFactory1>();

        // Si prende la prima uscita della prima scheda: lo schermo primario. Con piu' monitor
        // servirebbe scegliere, ma il progetto duplica lo schermo principale.
        factory.EnumAdapters1(0, out var adapter).CheckError();
        using var _ = adapter;
        adapter.EnumOutputs(0, out var output).CheckError();
        using var __ = output;
        using var output1 = output.QueryInterface<IDXGIOutput1>();

        var result = D3D11.D3D11CreateDevice(
            adapter,
            DriverType.Unknown,
            DeviceCreationFlags.BgraSupport,
            [FeatureLevel.Level_11_0, FeatureLevel.Level_10_1, FeatureLevel.Level_10_0],
            out _device!,
            out _context!);
        result.CheckError();

        _duplication = output1.DuplicateOutput(_device);

        var bounds = output.Description.DesktopCoordinates;
        SourceWidth = bounds.Right - bounds.Left;
        SourceHeight = bounds.Bottom - bounds.Top;

        TargetHeight = Math.Min(targetHeight, SourceHeight);
        TargetWidth = (int)Math.Round(SourceWidth * (double)TargetHeight / SourceHeight);
        if (TargetWidth % 2 != 0) TargetWidth++;

        // La texture di staging e' l'unica leggibile dalla CPU: la copia dalla texture del
        // desktop avviene sulla GPU, poi si mappa questa.
        _staging = _device.CreateTexture2D(new Texture2DDescription
        {
            Width = (uint)SourceWidth,
            Height = (uint)SourceHeight,
            MipLevels = 1,
            ArraySize = 1,
            Format = Format.B8G8R8A8_UNorm,
            SampleDescription = new SampleDescription(1, 0),
            Usage = ResourceUsage.Staging,
            BindFlags = BindFlags.None,
            CPUAccessFlags = CpuAccessFlags.Read,
            MiscFlags = ResourceOptionFlags.None,
        });

        _full = new Bitmap(SourceWidth, SourceHeight, PixelFormat.Format32bppRgb);

        _scaled = new Bitmap(TargetWidth, TargetHeight, PixelFormat.Format24bppRgb);
        _scaledGraphics = Graphics.FromImage(_scaled);
        _scaledGraphics.CompositingMode = CompositingMode.SourceCopy;
        _scaledGraphics.CompositingQuality = CompositingQuality.HighSpeed;
        _scaledGraphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.Bilinear;
        _scaledGraphics.SmoothingMode = SmoothingMode.None;
        _scaledGraphics.PixelOffsetMode = PixelOffsetMode.Half;
    }

    /// <summary>
    /// Elenca schede e uscite e prova a duplicare ciascuna, riportando l'esito.
    /// Serve quando la duplicazione fallisce: la causa (scheda sbagliata, uscita non
    /// collegata, duplicazione gia' occupata da un altro programma) si distingue solo
    /// vedendo i risultati uno per uno.
    /// </summary>
    public static void Diagnose(TextWriter output)
    {
        using var factory = DXGI.CreateDXGIFactory1<IDXGIFactory1>();

        for (uint a = 0; ; a++)
        {
            if (factory.EnumAdapters1(a, out var adapter).Failure) break;
            using (adapter)
            {
                var description = adapter.Description1;
                output.WriteLine($"scheda {a}: {description.Description.Trim()} " +
                                 $"({description.DedicatedVideoMemory / 1024 / 1024} MB dedicati)");

                var anyOutput = false;
                for (uint o = 0; ; o++)
                {
                    if (adapter.EnumOutputs(o, out var monitor).Failure) break;
                    anyOutput = true;
                    using (monitor)
                    {
                        var bounds = monitor.Description.DesktopCoordinates;
                        var size = $"{bounds.Right - bounds.Left}x{bounds.Bottom - bounds.Top}";
                        var attached = monitor.Description.AttachedToDesktop ? "collegata" : "non collegata";
                        output.Write($"  uscita {o}: {size}, {attached} → ");

                        try
                        {
                            using var monitor1 = monitor.QueryInterface<IDXGIOutput1>();
                            // Il tipo esplicito toglie l'ambiguita' tra i due overload che
                            // differiscono solo nell'ultimo parametro di uscita.
                            ID3D11Device device;
                            ID3D11DeviceContext context;
                            D3D11.D3D11CreateDevice(
                                adapter, DriverType.Unknown, DeviceCreationFlags.BgraSupport,
                                [FeatureLevel.Level_11_0, FeatureLevel.Level_10_0],
                                out device, out context).CheckError();

                            using (device)
                            using (context)
                            {
                                using var duplication = monitor1.DuplicateOutput(device);
                                output.WriteLine("duplicazione riuscita");
                            }
                        }
                        catch (Exception ex)
                        {
                            output.WriteLine($"duplicazione fallita — {ex.Message.Split('\n')[0]}");
                        }
                    }
                }

                if (!anyOutput) output.WriteLine("  nessuna uscita");
            }
        }
    }

    public Bitmap? Capture(int timeoutMs = 100)
    {
        var result = _duplication.AcquireNextFrame((uint)timeoutMs, out _, out var resource);
        if (result == Vortice.DXGI.ResultCode.WaitTimeout)
        {
            // Nessun cambiamento sullo schermo entro il tempo concesso: non e' un errore.
            return null;
        }
        result.CheckError();

        try
        {
            using (resource)
            using (var texture = resource.QueryInterface<ID3D11Texture2D>())
            {
                _context.CopyResource(_staging, texture);
            }

            CopyToBitmap();
        }
        finally
        {
            _duplication.ReleaseFrame();
        }

        if (TargetWidth == SourceWidth && TargetHeight == SourceHeight) return _full;

        _scaledGraphics.DrawImage(_full, 0, 0, TargetWidth, TargetHeight);
        return _scaled;
    }

    /// <summary>
    /// Porta i pixel dalla texture di staging al bitmap GDI+. Si copia riga per riga perche'
    /// il passo della texture non coincide quasi mai con quello del bitmap.
    /// </summary>
    private unsafe void CopyToBitmap()
    {
        var source = _context.Map(_staging, 0, MapMode.Read, Vortice.Direct3D11.MapFlags.None);
        var destination = _full.LockBits(
            new Rectangle(0, 0, SourceWidth, SourceHeight), ImageLockMode.WriteOnly, _full.PixelFormat);

        try
        {
            var bytesPerRow = SourceWidth * 4;
            for (var y = 0; y < SourceHeight; y++)
            {
                var from = source.DataPointer + y * (int)source.RowPitch;
                var to = destination.Scan0 + y * destination.Stride;
                Buffer.MemoryCopy((void*)from, (void*)to, bytesPerRow, bytesPerRow);
            }
        }
        finally
        {
            _full.UnlockBits(destination);
            _context.Unmap(_staging, 0);
        }
    }

    public void Dispose()
    {
        _scaledGraphics.Dispose();
        _scaled.Dispose();
        _full.Dispose();
        _staging.Dispose();
        _duplication.Dispose();
        _context.Dispose();
        _device.Dispose();
    }
}
