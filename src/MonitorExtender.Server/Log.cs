using System.Text;

namespace MonitorExtender.Server;

/// <summary>
/// Registro dei messaggi.
///
/// Avviato dall'icona vicino all'orologio il programma non ha una console: quello che prima
/// finiva a schermo deve restare leggibile da qualche parte, altrimenti diagnosticare un
/// problema diventa impossibile. La scrittura su console resta comunque, perche' con
/// `--console` il server si comporta come prima.
/// </summary>
public static class Log
{
    private const long MaxBytes = 1024 * 1024;

    private static readonly object Gate = new();

    private static readonly string Folder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MonitorExtender");

    // Non si chiama Path: nasconderebbe System.IO.Path in tutta la classe.
    public static string FilePath { get; } = Path.Combine(Folder, "server.log");

    public static void Write(string message)
    {
        var line = $"{DateTime.Now:HH:mm:ss} {message}";
        Console.WriteLine(line);

        lock (Gate)
        {
            try
            {
                Directory.CreateDirectory(Folder);
                Rotate();
                File.AppendAllText(FilePath, line + Environment.NewLine, Encoding.UTF8);
            }
            catch (IOException)
            {
                // Il registro e' un servizio accessorio: se il disco e' pieno o il file e'
                // bloccato, il server continua a funzionare.
            }
        }
    }

    /// <summary>Un solo giro di rotazione: il file corrente diventa .1, il precedente si perde.</summary>
    private static void Rotate()
    {
        var info = new FileInfo(FilePath);
        if (!info.Exists || info.Length < MaxBytes) return;

        var previous = FilePath + ".1";
        File.Delete(previous);
        File.Move(FilePath, previous);
    }
}
