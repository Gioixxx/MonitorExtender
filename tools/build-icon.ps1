# Genera l'icona .ico dell'eseguibile: lo stesso monitor stilizzato usato per l'icona di
# stato (TrayIcon.BuildIcon) e per gli asset del Play Store (store-assets.ps1) — cornice
# scura, schermo ambra. Disegnato da codice invece che importato da un file binario, cosi'
# resta riproducibile e coerente se i colori dell'app cambiano.
#
#   powershell -File tools\build-icon.ps1
#
# Produce src\MonitorExtender.Server\icon.ico, incorporata nell'exe da
# <ApplicationIcon> nel .csproj, e usata anche da installer\MonitorExtender.iss.
param(
    [string]$OutputPath
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

$root = Split-Path -Parent $PSScriptRoot
if (-not $OutputPath) {
    $OutputPath = Join-Path $root "src\MonitorExtender.Server\icon.ico"
}

# Gli stessi valori di TrayIcon.cs e android/app/src/main/res/values/colors.xml
$frame  = [System.Drawing.Color]::FromArgb(0x17, 0x1D, 0x24)
$signal = [System.Drawing.Color]::FromArgb(0xF2, 0xA3, 0x3C)

function New-IconBitmap([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.Clear([System.Drawing.Color]::Transparent)

    # Stesse proporzioni di TrayIcon.BuildIcon (32x32: frame 2,5,28,20 - schermo 5,8,22,14 -
    # piedistallo 12,25,8,3 e 8,28,16,3), scalate alla dimensione richiesta.
    $s = $size / 32.0
    $frameBrush = New-Object System.Drawing.SolidBrush($frame)
    $screenBrush = New-Object System.Drawing.SolidBrush($signal)

    $g.FillRectangle($frameBrush, 2 * $s, 5 * $s, 28 * $s, 20 * $s)
    $g.FillRectangle($screenBrush, 5 * $s, 8 * $s, 22 * $s, 14 * $s)
    if ($size -ge 24) {
        # Il piedistallo si perde sotto i 24px: meglio un rettangolo pieno che un pixel sporco.
        $g.FillRectangle($frameBrush, 12 * $s, 25 * $s, 8 * $s, 3 * $s)
        $g.FillRectangle($frameBrush, 8 * $s, 28 * $s, 16 * $s, 3 * $s)
    }

    $frameBrush.Dispose(); $screenBrush.Dispose(); $g.Dispose()
    return $bmp
}

# Formato ICO: ogni voce puo' contenere un PNG invece di un bitmap DIB (supportato da Windows
# Vista in poi) - l'unico modo pulito di includere anche la taglia 256, che il formato BMP
# classico non puo' rappresentare (l'altezza/larghezza a 1 byte usa 0 per significare 256).
$sizes = @(16, 32, 48, 256)
$images = foreach ($size in $sizes) {
    $bmp = New-IconBitmap $size
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    ,@{ Size = $size; Bytes = $ms.ToArray() }
}

$stream = New-Object System.IO.MemoryStream
$writer = New-Object System.IO.BinaryWriter($stream)

# ICONDIR
$writer.Write([uint16]0)      # reserved
$writer.Write([uint16]1)      # type: 1 = icona
$writer.Write([uint16]$images.Count)

$headerSize = 6 + (16 * $images.Count)
$offset = $headerSize
foreach ($img in $images) {
    $dim = if ($img.Size -ge 256) { 0 } else { $img.Size }  # 0 = 256 nel formato ICO
    $writer.Write([byte]$dim)          # larghezza
    $writer.Write([byte]$dim)          # altezza
    $writer.Write([byte]0)             # palette (0 = nessuna, true color)
    $writer.Write([byte]0)             # reserved
    $writer.Write([uint16]1)           # color planes
    $writer.Write([uint16]32)          # bit per pixel
    $writer.Write([uint32]$img.Bytes.Length)
    $writer.Write([uint32]$offset)
    $offset += $img.Bytes.Length
}
foreach ($img in $images) {
    $writer.Write($img.Bytes)
}

$writer.Flush()
[System.IO.File]::WriteAllBytes($OutputPath, $stream.ToArray())
$writer.Dispose(); $stream.Dispose()

Write-Host "Icona scritta in $OutputPath ($([Math]::Round((Get-Item $OutputPath).Length / 1KB, 1)) KB, $($sizes -join '/')px)" -ForegroundColor Green
