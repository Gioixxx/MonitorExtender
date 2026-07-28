using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Windows.Forms;

namespace MonitorExtender.Server;

/// <summary>
/// Icona vicino all'orologio: l'unico modo per accorgersi che il server c'e' e per fermarlo,
/// visto che avviandosi da solo non ha ne' finestra ne' console.
///
/// L'icona e' disegnata a runtime invece di essere un file: sono due rettangoli, e un asset
/// binario nel repository andrebbe mantenuto in due dimensioni senza dare nulla in cambio.
/// I colori sono gli stessi dell'app — ambra quando qualcuno guarda, grigio quando nessuno.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class TrayIcon : IDisposable
{
    private static readonly Color Signal = Color.FromArgb(0xF2, 0xA3, 0x3C);
    private static readonly Color Idle = Color.FromArgb(0x8B, 0x95, 0xA3);

    private readonly StreamOptions _options;
    private readonly FrameBroker _broker;
    private readonly UsbLinkWatcher? _usb;
    private readonly Action _onExit;

    private readonly NotifyIcon _icon;
    private readonly ToolStripMenuItem _status;
    private readonly System.Windows.Forms.Timer _refresh;
    private readonly Icon _activeIcon;
    private readonly Icon _idleIcon;

    private bool _showingActive;

    public TrayIcon(StreamOptions options, FrameBroker broker, UsbLinkWatcher? usb, Action onExit)
    {
        _options = options;
        _broker = broker;
        _usb = usb;
        _onExit = onExit;

        _activeIcon = BuildIcon(Signal);
        _idleIcon = BuildIcon(Idle);

        _status = new ToolStripMenuItem("MonitorExtender") { Enabled = false };

        var menu = new ContextMenuStrip();
        menu.Items.Add(_status);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Apri la pagina di prova", null, (_, _) => OpenBrowser());
        menu.Items.Add("Copia l'indirizzo", null, (_, _) => CopyAddress());
        if (_usb != null) menu.Items.Add("Riattiva il cavo USB", null, (_, _) => _usb.LinkNow());
        menu.Items.Add("Apri il registro", null, (_, _) => OpenLog());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Esci", null, (_, _) => _onExit());

        _icon = new NotifyIcon
        {
            Icon = _idleIcon,
            Text = "MonitorExtender",
            ContextMenuStrip = menu,
            Visible = true,
        };
        _icon.DoubleClick += (_, _) => OpenBrowser();

        _refresh = new System.Windows.Forms.Timer { Interval = 2000 };
        _refresh.Tick += (_, _) => Refresh();
        _refresh.Start();
        Refresh();
    }

    private void Refresh()
    {
        var clients = _broker.Clients;
        var active = clients > 0;

        var line = active
            ? $"{_broker.Fps:F0} fps · {clients} client"
            : "in attesa di un collegamento";
        var usb = _usb == null ? "" : _usb.Linked ? " · cavo collegato" : " · cavo assente";

        _status.Text = $"MonitorExtender — {line}";
        // Il tooltip di Windows si ferma a 63 caratteri: oltre, non viene mostrato affatto.
        _icon.Text = Truncate($"MonitorExtender — {line}{usb}", 63);

        if (active != _showingActive)
        {
            _icon.Icon = active ? _activeIcon : _idleIcon;
            _showingActive = active;
        }
    }

    private static string Truncate(string text, int max) =>
        text.Length <= max ? text : text[..(max - 1)] + "…";

    private void OpenBrowser() => Start($"http://localhost:{_options.Port}/");

    private void OpenLog() => Start(Log.FilePath);

    private static void Start(string target)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(target)
            {
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            Log.Write($"[tray] impossibile aprire {target}: {ex.Message}");
        }
    }

    private void CopyAddress()
    {
        var address = Program.LanAddresses().FirstOrDefault();
        var url = address == null
            ? $"http://localhost:{_options.Port}/"
            : $"http://{address}:{_options.Port}/";

        try
        {
            Clipboard.SetText(url);
            _icon.ShowBalloonTip(2000, "Indirizzo copiato", url, ToolTipIcon.Info);
        }
        catch (Exception ex)
        {
            Log.Write($"[tray] copia negli appunti fallita: {ex.Message}");
        }
    }

    /// <summary>Un monitor stilizzato: base scura, schermo del colore che indica lo stato.</summary>
    private static Icon BuildIcon(Color color)
    {
        using var bitmap = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            using var frame = new SolidBrush(Color.FromArgb(0x17, 0x1D, 0x24));
            using var screen = new SolidBrush(color);

            g.FillRectangle(frame, 2, 5, 28, 20);
            g.FillRectangle(screen, 5, 8, 22, 14);
            g.FillRectangle(frame, 12, 25, 8, 3);
            g.FillRectangle(frame, 8, 28, 16, 3);
        }

        // FromHandle non possiede l'handle: si clona subito e si libera l'originale,
        // altrimenti l'icona resterebbe legata a un handle GDI da distruggere a mano.
        var handle = bitmap.GetHicon();
        try
        {
            using var temporary = Icon.FromHandle(handle);
            return (Icon)temporary.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    public void Dispose()
    {
        _refresh.Stop();
        _refresh.Dispose();
        _icon.Visible = false;
        _icon.Dispose();
        _activeIcon.Dispose();
        _idleIcon.Dispose();
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(nint handle);
}
