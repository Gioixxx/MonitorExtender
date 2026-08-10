# Compila l'installer Windows di MonitorExtender: build autonoma del server + Inno Setup.
#
#   powershell -File tools\build-installer.ps1
#   powershell -File tools\build-installer.ps1 -Version 1.0.0
#
# Produce dist\MonitorExtenderSetup-<versione>.exe, pronto da allegare a una release GitHub.
# Richiede Inno Setup (ISCC.exe) installato solo su questa macchina di build, mai su quella
# di chi scarica l'installer: se manca, stampa il comando per installarlo e si ferma.
param(
    [string]$Version = "1.0.0",
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot

# 1. Compilazione autonoma: chi scarica l'installer non deve avere .NET gia' installato.
& (Join-Path $PSScriptRoot "publish-server.ps1") -Version $Version -Runtime $Runtime
if ($LASTEXITCODE -ne 0) { exit 1 }

$staging = Join-Path $root "dist\MonitorExtender-$Version-$Runtime"
if (-not (Test-Path $staging)) {
    Write-Host "Staging non trovato: $staging" -ForegroundColor Red
    exit 1
}

# 2. Individua ISCC.exe (dipendenza solo di build).
$iscc = (Get-Command ISCC.exe -ErrorAction SilentlyContinue).Source
if (-not $iscc) {
    $candidate = "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
    if (Test-Path $candidate) { $iscc = $candidate }
}
if (-not $iscc) {
    Write-Host "Inno Setup non trovato. Installa con:" -ForegroundColor Red
    Write-Host "  winget install JRSoftware.InnoSetup" -ForegroundColor Yellow
    exit 1
}

# 3. Compila l'installer.
$iss = Join-Path $root "installer\MonitorExtender.iss"
& $iscc "/DMyAppVersion=$Version" "/DSourceDir=$staging" $iss
if ($LASTEXITCODE -ne 0) {
    Write-Host "Compilazione dell'installer fallita." -ForegroundColor Red
    exit 1
}

$exe = Get-Item (Join-Path $root "dist\MonitorExtenderSetup-$Version.exe")
Write-Host ""
Write-Host "Fatto." -ForegroundColor Green
"  installer  : {0}  ({1:N1} MB)" -f $exe.FullName, ($exe.Length / 1MB)
