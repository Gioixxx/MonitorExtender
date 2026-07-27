using System.Net;
using System.Runtime.Versioning;
using System.Text;

namespace MonitorExtender.Server;

/// <summary>
/// Server MJPEG su HttpListener.
///
/// Contratto di /stream:
///   Content-Type: multipart/x-mixed-replace; boundary=frame
///   per ogni frame:  --frame CRLF
///                    Content-Type: image/jpeg CRLF
///                    Content-Length: N CRLF CRLF
///                    &lt;N byte JPEG&gt; CRLF
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class MjpegServer : IDisposable
{
    private const string Boundary = "frame";
    private static readonly byte[] PartTerminator = "\r\n"u8.ToArray();

    private readonly StreamOptions _options;
    private readonly LiveSettings _settings;
    private readonly FrameBroker _broker;
    private HttpListener _listener = new();

    public MjpegServer(StreamOptions options, LiveSettings settings, FrameBroker broker)
    {
        _options = options;
        _settings = settings;
        _broker = broker;
    }

    /// <summary>True se il bind e' riuscito su tutte le interfacce (quindi raggiungibile dal telefono).</summary>
    public bool BoundToAllInterfaces { get; private set; }

    public void Start()
    {
        _listener.Prefixes.Add($"http://+:{_options.Port}/");
        try
        {
            _listener.Start();
            BoundToAllInterfaces = true;
            return;
        }
        catch (HttpListenerException ex) when (ex.ErrorCode == 5) // ERROR_ACCESS_DENIED
        {
            Console.WriteLine($"""
                [server] Bind su http://+:{_options.Port}/ negato da Windows (access denied).
                         Resta raggiungibile da questo PC e dal cavo USB (adb reverse), ma non
                         dalla rete. Per sbloccare anche la WiFi, una volta sola da terminale
                         amministratore:

                           netsh http add urlacl url=http://+:{_options.Port}/ user={Environment.UserDomainName}\{Environment.UserName}

                         (in alternativa esegui questo programma come amministratore)
                """);
        }

        // Uno Start() fallito lascia il listener nello stato disposto: per il ripiego
        // serve un'istanza nuova, non basta cambiare i prefissi.
        _listener.Close();
        _listener = new HttpListener();

        // Servono entrambi i prefissi. HTTP.sys smista le richieste confrontando l'hostname
        // testuale dell'header Host, non l'indirizzo risolto: "localhost" e "127.0.0.1" sono
        // due nomi distinti. Con il solo "localhost", una richiesta a http://127.0.0.1:PORTA/
        // riceve un 400 "Invalid Hostname" — ed e' esattamente quello che chiede il tablet
        // collegato via adb reverse.
        _listener.Prefixes.Add($"http://localhost:{_options.Port}/");
        _listener.Prefixes.Add($"http://127.0.0.1:{_options.Port}/");
        _listener.Start();
        BoundToAllInterfaces = false;
    }

    public async Task RunAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync().ConfigureAwait(false);
            }
            catch (Exception) when (token.IsCancellationRequested)
            {
                return;
            }
            catch (HttpListenerException)
            {
                return; // listener chiuso
            }

            // Ogni client per conto suo: uno lento non deve bloccare gli altri.
            _ = Task.Run(() => HandleAsync(context, token), CancellationToken.None);
        }
    }

    private async Task HandleAsync(HttpListenerContext context, CancellationToken token)
    {
        var path = context.Request.Url?.AbsolutePath ?? "/";
        var client = context.Request.RemoteEndPoint?.Address.ToString() ?? "?";

        try
        {
            switch (path)
            {
                case "/":
                case "/index.html":
                    await WriteIndexAsync(context).ConfigureAwait(false);
                    break;
                case "/snapshot":
                case "/snapshot.jpg":
                    await WriteSnapshotAsync(context).ConfigureAwait(false);
                    break;
                case "/stream":
                    ApplyQuerySettings(context.Request);
                    Console.WriteLine($"[server] client connesso: {client}");
                    await WriteStreamAsync(context, token).ConfigureAwait(false);
                    Console.WriteLine($"[server] client disconnesso: {client}");
                    break;
                case "/info":
                    await WriteInfoAsync(context).ConfigureAwait(false);
                    break;
                default:
                    context.Response.StatusCode = 404;
                    context.Response.Close();
                    break;
            }
        }
        catch (Exception ex) when (IsClientGone(ex))
        {
            Console.WriteLine($"[server] client disconnesso: {client}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[server] errore su {path} ({client}): {ex.Message}");
            try { context.Response.Abort(); } catch { /* gia' andato */ }
        }
    }

    private async Task WriteStreamAsync(HttpListenerContext context, CancellationToken token)
    {
        var response = context.Response;
        response.StatusCode = 200;
        response.ContentType = $"multipart/x-mixed-replace; boundary={Boundary}";
        response.SendChunked = true;
        response.KeepAlive = true;
        response.Headers.Add("Cache-Control", "no-store, no-cache, must-revalidate, private");
        response.Headers.Add("Pragma", "no-cache");

        using var subscription = _broker.Subscribe();
        var output = response.OutputStream;
        long lastId = 0;

        try
        {
            while (!token.IsCancellationRequested)
            {
                var frame = await _broker.NextFrameAsync().WaitAsync(token).ConfigureAwait(false);
                if (frame.Id == lastId) continue;
                lastId = frame.Id;

                var header = Encoding.ASCII.GetBytes(
                    $"--{Boundary}\r\nContent-Type: image/jpeg\r\nContent-Length: {frame.Jpeg.Length}\r\n\r\n");

                await output.WriteAsync(header, token).ConfigureAwait(false);
                await output.WriteAsync(frame.Jpeg, token).ConfigureAwait(false);
                await output.WriteAsync(PartTerminator, token).ConfigureAwait(false);
                await output.FlushAsync(token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // shutdown del server
        }
        finally
        {
            try { response.Close(); } catch { /* connessione gia' caduta */ }
        }
    }

    /// <summary>
    /// Legge `?fps=&amp;q=&amp;scale=` dalla richiesta e riconfigura lo stream. I valori fuori
    /// scala vengono limitati, non rifiutati: un parametro sbagliato non deve costare la
    /// connessione a chi sta solo provando dei numeri.
    /// </summary>
    private void ApplyQuerySettings(HttpListenerRequest request)
    {
        var query = request.QueryString;
        if (query.Count == 0) return;

        static int? Read(System.Collections.Specialized.NameValueCollection q, params string[] names)
        {
            foreach (var name in names)
            {
                var raw = q[name];
                if (!string.IsNullOrWhiteSpace(raw) && int.TryParse(raw, out var value)) return value;
            }
            return null;
        }

        if (_settings.Apply(
                scale: Read(query, "scale"),
                fps: Read(query, "fps"),
                quality: Read(query, "q", "quality")))
        {
            Console.WriteLine($"[server] richiesti nuovi parametri: {_settings}");
        }
    }

    /// <summary>Descrive il server: usato dalla discovery per confermare cosa si e' trovato.</summary>
    private async Task WriteInfoAsync(HttpListenerContext context)
    {
        var json = $$"""
            {"name":"{{JsonEscape(Environment.MachineName)}}","port":{{_options.Port}},"scale":{{_settings.Scale}},"fps":{{_settings.Fps}},"quality":{{_settings.Quality}},"source":"{{_broker.SourceWidth}}x{{_broker.SourceHeight}}","target":"{{_broker.TargetWidth}}x{{_broker.TargetHeight}}"}
            """;

        var bytes = Encoding.UTF8.GetBytes(json);
        context.Response.StatusCode = 200;
        context.Response.ContentType = "application/json; charset=utf-8";
        context.Response.ContentLength64 = bytes.Length;
        await context.Response.OutputStream.WriteAsync(bytes).ConfigureAwait(false);
        context.Response.Close();
    }

    internal static string JsonEscape(string value) =>
        value.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private async Task WriteSnapshotAsync(HttpListenerContext context)
    {
        // Il broker cattura solo con almeno un iscritto: ci si iscrive e si aspetta un frame.
        using var subscription = _broker.Subscribe();
        var frame = _broker.Latest ?? await _broker.NextFrameAsync()
            .WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);

        context.Response.StatusCode = 200;
        context.Response.ContentType = "image/jpeg";
        context.Response.ContentLength64 = frame.Jpeg.Length;
        context.Response.Headers.Add("Cache-Control", "no-store");
        await context.Response.OutputStream.WriteAsync(frame.Jpeg).ConfigureAwait(false);
        context.Response.Close();
    }

    private async Task WriteIndexAsync(HttpListenerContext context)
    {
        var html = """
            <!doctype html>
            <html lang="it">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1, viewport-fit=cover">
              <title>MonitorExtender</title>
              <style>
                html, body { margin: 0; height: 100%; background: #000; overflow: hidden; }
                img { width: 100%; height: 100%; object-fit: contain; display: block; }
              </style>
            </head>
            <body>
              <img src="/stream" alt="schermo del PC">
            </body>
            </html>
            """;

        var bytes = Encoding.UTF8.GetBytes(html);
        context.Response.StatusCode = 200;
        context.Response.ContentType = "text/html; charset=utf-8";
        context.Response.ContentLength64 = bytes.Length;
        await context.Response.OutputStream.WriteAsync(bytes).ConfigureAwait(false);
        context.Response.Close();
    }

    /// <summary>
    /// Il client che chiude la scheda del browser arriva qui: e' normale amministrazione,
    /// non un errore da segnalare.
    /// </summary>
    private static bool IsClientGone(Exception ex) => ex switch
    {
        HttpListenerException => true,
        IOException => true,
        ObjectDisposedException => true,
        OperationCanceledException => true,
        _ => false,
    };

    public void Dispose()
    {
        try { _listener.Stop(); } catch { /* gia' fermo */ }
        _listener.Close();
    }
}
