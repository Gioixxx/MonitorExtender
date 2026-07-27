using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.Versioning;

namespace MonitorExtender.Server;

/// <summary>
/// Codifica un Bitmap in JPEG alla qualita' richiesta. Il codec e i parametri sono
/// risolti una volta sola: cercarli a ogni frame e' una perdita secca.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class JpegEncoder : IDisposable
{
    private readonly ImageCodecInfo _codec;
    private readonly EncoderParameters _parameters;
    private readonly MemoryStream _buffer = new(256 * 1024);

    public JpegEncoder(int quality)
    {
        _codec = ImageCodecInfo.GetImageEncoders().FirstOrDefault(c => c.MimeType == "image/jpeg")
                 ?? throw new InvalidOperationException("Codec JPEG non disponibile su questo sistema.");

        _parameters = new EncoderParameters(1);
        _parameters.Param[0] = new EncoderParameter(Encoder.Quality, (long)quality);
    }

    /// <summary>
    /// Codifica il bitmap e restituisce una copia dei byte JPEG. La copia e'
    /// voluta: il frame viene condiviso tra piu' client e non puo' essere
    /// sovrascritto dal frame successivo mentre qualcuno lo sta ancora spedendo.
    /// </summary>
    public byte[] Encode(Bitmap frame)
    {
        _buffer.SetLength(0);
        frame.Save(_buffer, _codec, _parameters);
        return _buffer.ToArray();
    }

    public void Dispose()
    {
        _buffer.Dispose();
        _parameters.Dispose();
    }
}
