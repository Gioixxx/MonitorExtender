# Collega il tablet al server attraverso il cavo USB invece che via WiFi.
#
#   powershell -File tools\usb-link.ps1
#   powershell -File tools\usb-link.ps1 -Remove
#
# Come funziona: "adb reverse" apre sul dispositivo una porta locale che adb inoltra al
# PC lungo il cavo. Dal punto di vista dell'app il server sta su 127.0.0.1, e nessun
# pacchetto passa dalla rete: niente firewall, niente urlacl, niente WiFi.
#
# Misurato su HONOR Pad 10, stesso stream (900p, 30 fps, qualita' 85, 150 KB/frame):
#   WiFi  13.8 Mbit/s  -> consegna ~11 dei 30 fps prodotti
#   USB   36.8 Mbit/s  -> li consegna tutti
#
# Richiede il debug USB attivo sul dispositivo: e' una funzione da sviluppatore, non
# qualcosa che si possa chiedere a un utente qualsiasi.
param(
    [int]$Port = 8080,
    [switch]$Remove
)

$adb = Join-Path $env:LOCALAPPDATA "Android\Sdk\platform-tools\adb.exe"
if (-not (Test-Path $adb)) {
    Write-Host "adb non trovato in $adb" -ForegroundColor Red
    exit 1
}

$devices = & $adb devices | Select-Object -Skip 1 | Where-Object { $_ -match "\sdevice$" }
if (-not $devices) {
    Write-Host "Nessun dispositivo collegato con il debug USB attivo." -ForegroundColor Red
    Write-Host "Controlla che in Windows compaia anche 'ADB Interface' tra i dispositivi USB:" -ForegroundColor Yellow
    Write-Host "  se vedi solo il tablet come dispositivo portatile (WPD), il debug non e' attivo." -ForegroundColor Yellow
    exit 1
}

if ($Remove) {
    & $adb reverse --remove "tcp:$Port" 2>&1 | Out-Null
    Write-Host "Inoltro USB rimosso. L'app deve tornare all'indirizzo di LAN." -ForegroundColor Green
    exit 0
}

& $adb reverse "tcp:$Port" "tcp:$Port" | Out-Null
if ($LASTEXITCODE -ne 0) {
    Write-Host "adb reverse non riuscito." -ForegroundColor Red
    exit 1
}

Write-Host "Inoltro attivo:" -ForegroundColor Green
& $adb reverse --list

# Prova concreta che il tratto funziona, invece di fidarsi del solo exit code.
$info = & $adb shell "curl -s --max-time 5 http://127.0.0.1:$Port/info" 2>&1
if ($info -match '"name"') {
    Write-Host ""
    Write-Host "Il dispositivo raggiunge il server via cavo:" -ForegroundColor Green
    Write-Host "  $info"
    Write-Host ""
    Write-Host "Nell'app premi 'Cerca il PC': trova da sola il collegamento USB e lo preferisce." -ForegroundColor Cyan
    Write-Host "Con questa banda puoi alzare la qualita', per esempio:" -ForegroundColor Cyan
    Write-Host "  http://127.0.0.1:$Port/stream?scale=1080&fps=30&q=85"
} else {
    Write-Host ""
    Write-Host "Inoltro creato ma il server non risponde da 127.0.0.1:$Port." -ForegroundColor Yellow
    Write-Host "E' avviato sul PC?" -ForegroundColor Yellow
}
