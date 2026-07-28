using System.Net;
using System.Net.Sockets;
using System.Text;

namespace MonitorExtender.Server;

/// <summary>
/// Risponde alle sonde UDP in broadcast, cosi' l'app non deve farsi digitare l'IP.
///
/// Il telefono manda "MONITOREXTENDER?" in broadcast sulla porta di discovery; il server
/// risponde al mittente con un JSON che contiene nome della macchina e porta HTTP. Il client
/// ricava l'indirizzo dal mittente della risposta, non dal contenuto: cosi' funziona anche
/// quando il PC ha piu' interfacce e non sa quale sia quella giusta.
/// </summary>
public sealed class DiscoveryResponder : IDisposable
{
    public const int DiscoveryPort = 8079;
    public const string Probe = "MONITOREXTENDER?";

    private readonly StreamOptions _options;
    private readonly UdpClient _udp;

    public DiscoveryResponder(StreamOptions options)
    {
        _options = options;
        _udp = new UdpClient(AddressFamily.InterNetwork);
        _udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        _udp.Client.Bind(new IPEndPoint(IPAddress.Any, DiscoveryPort));
        _udp.EnableBroadcast = true;
    }

    public async Task RunAsync(CancellationToken token)
    {
        var reply = Encoding.UTF8.GetBytes(
            $$"""{"service":"monitorextender","name":"{{MjpegServer.JsonEscape(Environment.MachineName)}}","port":{{_options.Port}}}""");

        while (!token.IsCancellationRequested)
        {
            UdpReceiveResult request;
            try
            {
                request = await _udp.ReceiveAsync(token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (SocketException)
            {
                // Un pacchetto malformato o un ICMP di ritorno non devono spegnere la discovery.
                continue;
            }
            catch (ObjectDisposedException)
            {
                return;
            }

            var text = Encoding.UTF8.GetString(request.Buffer).Trim();
            if (!text.StartsWith(Probe, StringComparison.OrdinalIgnoreCase)) continue;

            try
            {
                await _udp.SendAsync(reply, request.RemoteEndPoint, token).ConfigureAwait(false);
                Log.Write($"[discovery] risposto a {request.RemoteEndPoint.Address}");
            }
            catch (Exception ex) when (ex is SocketException or ObjectDisposedException)
            {
                // Il client se n'e' andato prima della risposta: pazienza.
            }
        }
    }

    public void Dispose() => _udp.Dispose();
}
