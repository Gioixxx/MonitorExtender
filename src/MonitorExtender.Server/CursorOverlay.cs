using System.Drawing;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace MonitorExtender.Server;

/// <summary>
/// Disegna il cursore di sistema su un Graphics qualsiasi. Ne' CopyFromScreen (GDI) ne' la
/// Desktop Duplication (DXGI) includono il cursore nel frame catturato: lo compone il
/// compositore di Windows sopra il framebuffer, non lo si trova in nessuna delle due sorgenti.
/// Va disegnato a mano da entrambi i capturer, quindi vive qui invece che dentro uno dei due.
/// </summary>
[SupportedOSPlatform("windows")]
internal static class CursorOverlay
{
    public static void Draw(Graphics graphics)
    {
        var cursor = new CursorInfo { cbSize = Marshal.SizeOf<CursorInfo>() };
        if (!GetCursorInfo(out cursor) || cursor.flags != CursorShowing) return;
        if (!GetIconInfo(cursor.hCursor, out var iconInfo)) return;

        try
        {
            // GetCursorInfo da' l'angolo dell'icona, non il punto cliccabile: va corretto con
            // l'hotspot, altrimenti il cursore disegnato risulta spostato rispetto al vero click.
            var x = cursor.screenPos.X - iconInfo.xHotspot;
            var y = cursor.screenPos.Y - iconInfo.yHotspot;
            var hdc = graphics.GetHdc();
            try
            {
                DrawIconEx(hdc, x, y, cursor.hCursor, 0, 0, 0, nint.Zero, DiNormal);
            }
            finally
            {
                graphics.ReleaseHdc(hdc);
            }
        }
        finally
        {
            // GetIconInfo alloca due bitmap GDI: senza liberarle qui si perdono handle a ogni frame.
            if (iconInfo.hbmMask != nint.Zero) DeleteObject(iconInfo.hbmMask);
            if (iconInfo.hbmColor != nint.Zero) DeleteObject(iconInfo.hbmColor);
        }
    }

    private const int CursorShowing = 1;
    private const uint DiNormal = 0x0003;

    [StructLayout(LayoutKind.Sequential)]
    private struct CursorInfo
    {
        public int cbSize;
        public int flags;
        public nint hCursor;
        public Point screenPos;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IconInfo
    {
        [MarshalAs(UnmanagedType.Bool)] public bool fIcon;
        public int xHotspot;
        public int yHotspot;
        public nint hbmMask;
        public nint hbmColor;
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorInfo(out CursorInfo pci);

    [DllImport("user32.dll")]
    private static extern bool GetIconInfo(nint hIcon, out IconInfo piconinfo);

    [DllImport("user32.dll")]
    private static extern bool DrawIconEx(nint hdc, int xLeft, int yTop, nint hIcon,
        int cxWidth, int cyHeight, uint istepIfAniCur, nint hbrFlickerFreeDraw, uint diFlags);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(nint hObject);
}
