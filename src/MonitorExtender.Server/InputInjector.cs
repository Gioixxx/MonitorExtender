using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace MonitorExtender.Server;

/// <summary>
/// Inietta eventi di mouse nel sistema tramite SendInput.
///
/// Le coordinate arrivano normalizzate (0..1 sul frame) e vengono convertite nella scala
/// 0..65535 che SendInput usa per il posizionamento assoluto sullo schermo primario. Siccome
/// e' proprio lo schermo primario quello che catturiamo, la corrispondenza e' diretta: quello
/// che si tocca sul tablet e' quello che si tocca sul PC.
///
/// Due limiti imposti da Windows, non aggirabili:
/// - un processo non amministratore non puo' iniettare input nelle finestre di app
///   amministratore (UIPI): il cursore ci si muove sopra, ma i click vengono ignorati;
/// - sul desktop sicuro (prompt UAC, schermata di blocco) l'iniezione non funziona affatto,
///   nemmeno da amministratore. E' una protezione del sistema.
/// </summary>
[SupportedOSPlatform("windows")]
public static class InputInjector
{
    private const uint InputMouse = 0;

    private const uint MouseEventMove = 0x0001;
    private const uint MouseEventLeftDown = 0x0002;
    private const uint MouseEventLeftUp = 0x0004;
    private const uint MouseEventRightDown = 0x0008;
    private const uint MouseEventRightUp = 0x0010;
    private const uint MouseEventMiddleDown = 0x0020;
    private const uint MouseEventMiddleUp = 0x0040;
    private const uint MouseEventWheel = 0x0800;
    private const uint MouseEventAbsolute = 0x8000;

    public enum Button { Left, Right, Middle }

    /// <summary>Sposta il cursore. x e y sono frazioni (0..1) della larghezza e altezza dello schermo.</summary>
    public static void MoveTo(double x, double y)
    {
        var input = NewMouseInput(MouseEventMove | MouseEventAbsolute);
        input.u.mi.dx = ToAbsolute(x);
        input.u.mi.dy = ToAbsolute(y);
        Send(input);
    }

    public static void ButtonDown(Button button) => Send(NewMouseInput(button switch
    {
        Button.Right => MouseEventRightDown,
        Button.Middle => MouseEventMiddleDown,
        _ => MouseEventLeftDown,
    }));

    public static void ButtonUp(Button button) => Send(NewMouseInput(button switch
    {
        Button.Right => MouseEventRightUp,
        Button.Middle => MouseEventMiddleUp,
        _ => MouseEventLeftUp,
    }));

    /// <summary>
    /// Sposta il cursore di pochi pixel rispetto a dov'e' ora, senza saltare altrove.
    /// Serve al banco di prova: la Desktop Duplication consegna un frame solo quando lo
    /// schermo cambia, quindi va provocato un cambiamento a ogni giro.
    /// </summary>
    public static void Nudge(int dx, int dy)
    {
        var input = NewMouseInput(MouseEventMove); // senza ABSOLUTE il movimento e' relativo
        input.u.mi.dx = dx;
        input.u.mi.dy = dy;
        Send(input);
    }

    /// <summary>Rotella: 120 e' uno scatto verso l'alto, -120 verso il basso.</summary>
    public static void Wheel(int delta)
    {
        var input = NewMouseInput(MouseEventWheel);
        input.u.mi.mouseData = unchecked((uint)delta);
        Send(input);
    }

    private static int ToAbsolute(double fraction) =>
        (int)Math.Round(Math.Clamp(fraction, 0, 1) * 65535);

    private static Input NewMouseInput(uint flags) => new()
    {
        type = InputMouse,
        u = new InputUnion { mi = new MouseInput { dwFlags = flags } },
    };

    private static void Send(Input input)
    {
        var inputs = new[] { input };
        if (SendInput(1, inputs, Marshal.SizeOf<Input>()) == 0)
        {
            // Succede quando in primo piano c'e' una finestra elevata o il desktop sicuro:
            // non e' un errore da propagare, e' un limite del sistema.
            var error = Marshal.GetLastWin32Error();
            Log.Write($"[input] evento rifiutato da Windows (codice {error})");
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint type;
        public InputUnion u;
    }

    // Le tre varianti condividono la stessa memoria: la dimensione della struttura la
    // determina la piu' grande, quindi vanno dichiarate tutte anche se qui serve solo il mouse.
    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public MouseInput mi;
        [FieldOffset(0)] public KeyboardInput ki;
        [FieldOffset(0)] public HardwareInput hi;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public nint dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public nint dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HardwareInput
    {
        public uint uMsg;
        public ushort wParamL;
        public ushort wParamH;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, Input[] pInputs, int cbSize);
}
