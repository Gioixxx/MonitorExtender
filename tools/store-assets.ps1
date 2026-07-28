# Genera i materiali grafici per la scheda del Play Store.
#
#   powershell -File tools\store-assets.ps1                 icona e immagine in evidenza
#   powershell -File tools\store-assets.ps1 -Screenshots    cattura anche dal dispositivo
#
# Sono disegnati da codice invece che esportati da un file: restano riproducibili, coerenti
# con i colori dell'app e non aggiungono binari da mantenere nel repository.
param(
    [switch]$Screenshots,
    [string]$OutputDir
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

if (-not $OutputDir) {
    $OutputDir = Join-Path (Split-Path -Parent $PSScriptRoot) "docs\store"
}
New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

# Gli stessi valori di android/app/src/main/res/values/colors.xml
$bezel  = [System.Drawing.Color]::FromArgb(0x0F, 0x13, 0x18)
$panel  = [System.Drawing.Color]::FromArgb(0x17, 0x1D, 0x24)
$rule   = [System.Drawing.Color]::FromArgb(0x2A, 0x33, 0x3D)
$signal = [System.Drawing.Color]::FromArgb(0xF2, 0xA3, 0x3C)
$ink    = [System.Drawing.Color]::FromArgb(0xE7, 0xEC, 0xF2)
$inkDim = [System.Drawing.Color]::FromArgb(0x8B, 0x95, 0xA3)

function Save-Png($bitmap, $name) {
    $path = Join-Path $OutputDir $name
    $bitmap.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    "{0,-28} {1}x{2}  {3:N0} KB" -f $name, $bitmap.Width, $bitmap.Height, ((Get-Item $path).Length / 1KB)
}

function New-Canvas($w, $h, $color) {
    $bmp = New-Object System.Drawing.Bitmap($w, $h, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.TextRenderingHint = 'ClearTypeGridFit'
    $g.Clear($color)
    return @($bmp, $g)
}

# Un monitor: scocca scura, schermo ambra, piedistallo. Le proporzioni sono in frazioni del
# lato, cosi' la stessa funzione serve per l'icona e per l'immagine in evidenza.
function Draw-Monitor($g, $x, $y, $size) {
    $bodyW = $size
    $bodyH = [int]($size * 0.66)
    $radius = [int]($size * 0.06)

    $body = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $radius * 2
    $body.AddArc($x, $y, $d, $d, 180, 90)
    $body.AddArc(($x + $bodyW - $d), $y, $d, $d, 270, 90)
    $body.AddArc(($x + $bodyW - $d), ($y + $bodyH - $d), $d, $d, 0, 90)
    $body.AddArc($x, ($y + $bodyH - $d), $d, $d, 90, 90)
    $body.CloseFigure()

    $panelBrush = New-Object System.Drawing.SolidBrush($panel)
    $rulePen = New-Object System.Drawing.Pen($rule, [float]($size * 0.016))
    $g.FillPath($panelBrush, $body)
    $g.DrawPath($rulePen, $body)

    $inset = [int]($size * 0.07)
    $screenBrush = New-Object System.Drawing.SolidBrush($signal)
    $g.FillRectangle($screenBrush, ($x + $inset), ($y + $inset), ($bodyW - 2 * $inset), ($bodyH - 2 * $inset))

    # Piedistallo
    $neckW = [int]($size * 0.10)
    $neckH = [int]($size * 0.10)
    $g.FillRectangle($panelBrush, ($x + ($bodyW - $neckW) / 2), ($y + $bodyH), $neckW, $neckH)
    $baseW = [int]($size * 0.34)
    $baseH = [int]($size * 0.045)
    $g.FillRectangle($panelBrush, ($x + ($bodyW - $baseW) / 2), ($y + $bodyH + $neckH), $baseW, $baseH)

    $body.Dispose(); $panelBrush.Dispose(); $rulePen.Dispose(); $screenBrush.Dispose()
}

# --- Icona 512x512 -----------------------------------------------------------
# Play la vuole quadrata e piena: gli angoli arrotondati li applica lo store.
$icon, $ig = New-Canvas 512 512 $bezel
# Riempie il quadrato lasciando un margine uniforme: un'icona piccola dentro molto vuoto
# si perde nella griglia dello store.
Draw-Monitor $ig 66 103 380
Save-Png $icon "icon-512.png"
$ig.Dispose(); $icon.Dispose()

# --- Immagine in evidenza 1024x500 -------------------------------------------
$feature, $fg = New-Canvas 1024 500 $bezel

# Barra di link ambra sul bordo sinistro: lo stesso segno che nell'app indica
# un collegamento disponibile.
$signalBrush = New-Object System.Drawing.SolidBrush($signal)
$fg.FillRectangle($signalBrush, 0, 0, 10, 500)

Draw-Monitor $fg 76 158 240

# Play ritaglia i bordi laterali in alcune disposizioni: il testo resta ben dentro,
# con un margine destro abbondante.
$titleFont = New-Object System.Drawing.Font("Segoe UI Semibold", 44, [System.Drawing.FontStyle]::Regular)
$taglineFont = New-Object System.Drawing.Font("Segoe UI", 22, [System.Drawing.FontStyle]::Regular)
$capsFont = New-Object System.Drawing.Font("Consolas", 15, [System.Drawing.FontStyle]::Regular)

$inkBrush = New-Object System.Drawing.SolidBrush($ink)
$dimBrush = New-Object System.Drawing.SolidBrush($inkDim)

$fg.DrawString("MonitorExtender", $titleFont, $inkBrush, 388, 178)
$fg.DrawString("Il tuo tablet diventa lo schermo del PC", $taglineFont, $dimBrush, 394, 252)
$fg.DrawString("CAVO USB  ·  WI-FI  ·  MOUSE", $capsFont, $signalBrush, 396, 296)

Save-Png $feature "feature-1024x500.png"
$titleFont.Dispose(); $taglineFont.Dispose(); $capsFont.Dispose()
$inkBrush.Dispose(); $dimBrush.Dispose(); $signalBrush.Dispose()
$fg.Dispose(); $feature.Dispose()

# --- Screenshot dal dispositivo ----------------------------------------------
if ($Screenshots) {
    $adb = Join-Path $env:LOCALAPPDATA "Android\Sdk\platform-tools\adb.exe"
    if (-not (Test-Path $adb)) {
        Write-Host "adb non trovato: screenshot saltati." -ForegroundColor Yellow
        return
    }
    $devices = & $adb devices | Select-Object -Skip 1 | Where-Object { $_ -match "\sdevice$" }
    if (-not $devices) {
        Write-Host "Nessun dispositivo collegato: screenshot saltati." -ForegroundColor Yellow
        return
    }

    Write-Host ""
    Write-Host "Cattura dal dispositivo. Porta l'app nella schermata che vuoi immortalare," -ForegroundColor Cyan
    Write-Host "poi premi Invio. Lascia il nome vuoto per finire." -ForegroundColor Cyan
    $index = 1
    while ($true) {
        $label = Read-Host "nome dello screenshot $index (vuoto per finire)"
        if (-not $label) { break }
        $remote = "/sdcard/store-shot.png"
        & $adb shell screencap -p $remote
        $local = Join-Path $OutputDir ("screenshot-{0:d2}-{1}.png" -f $index, $label)
        & $adb pull $remote $local 2>&1 | Out-Null
        & $adb shell rm -f $remote
        "{0,-28} catturato" -f (Split-Path -Leaf $local)
        $index++
    }
}

Write-Host ""
Write-Host "Materiali in $OutputDir" -ForegroundColor Green
