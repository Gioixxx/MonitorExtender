# Prepara il server per la distribuzione: un solo eseguibile, senza .NET da installare.
#
#   powershell -File tools\publish-server.ps1
#   powershell -File tools\publish-server.ps1 -Version 1.0.0
#
# Produce dist\MonitorExtender-<versione>-win-x64.zip, pronto da allegare a una release
# GitHub. Dentro ci sono l'eseguibile, gli script di supporto e il README.
param(
    [string]$Version = "1.0.0",
    [string]$Runtime = "win-x64",
    # Pacchetto leggero (~1 MB invece di ~68) che pero' pretende .NET 8 gia' installato.
    [switch]$FrameworkDependent
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root "src\MonitorExtender.Server\MonitorExtender.Server.csproj"
$suffix = if ($FrameworkDependent) { "$Runtime-net8" } else { $Runtime }
$staging = Join-Path $root "dist\MonitorExtender-$Version-$suffix"
$archive = "$staging.zip"

if (Get-Process -Name "MonitorExtender.Server" -ErrorAction SilentlyContinue) {
    Write-Host "Il server e' in esecuzione e tiene bloccato l'eseguibile." -ForegroundColor Red
    Write-Host "Chiudilo dall'icona accanto all'orologio, poi rilancia." -ForegroundColor Yellow
    exit 1
}

Remove-Item $staging -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item $archive -Force -ErrorAction SilentlyContinue

if ($FrameworkDependent) {
    Write-Host "Compilazione leggera ($Runtime, richiede .NET 8 installato)..." -ForegroundColor Cyan
    dotnet publish $project `
        --configuration Release `
        --runtime $Runtime `
        --self-contained false `
        -p:PublishSingleFile=true `
        -p:Version=$Version `
        --output $staging | Out-Null
} else {
    Write-Host "Compilazione autonoma ($Runtime)..." -ForegroundColor Cyan

    # Autonomo e in file singolo: chi lo scarica non deve installare .NET. Pesa molto piu'
    # del programma vero, ma per un'utilita' da desktop scaricare 68 MB una volta e' meno
    # fastidioso che scoprire di dover installare un runtime.
    # ReadyToRun accorcia l'avvio, che conta perche' il programma parte al login.
    dotnet publish $project `
        --configuration Release `
        --runtime $Runtime `
        --self-contained true `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:PublishReadyToRun=true `
        -p:Version=$Version `
        --output $staging | Out-Null
}

if ($LASTEXITCODE -ne 0) {
    Write-Host "Compilazione fallita." -ForegroundColor Red
    exit 1
}

# I file di debug non servono a chi usa il programma e pesano piu' dell'eseguibile.
Get-ChildItem $staging -Filter "*.pdb" | Remove-Item -Force

$tools = New-Item -ItemType Directory -Force -Path (Join-Path $staging "tools")
foreach ($script in @("setup-network.ps1", "usb-link.ps1", "autostart.ps1", "test-stream.ps1", "test-discovery.ps1")) {
    Copy-Item (Join-Path $PSScriptRoot $script) $tools
}
Copy-Item (Join-Path $root "README.md") $staging
Copy-Item (Join-Path $root "LICENSE") $staging

Compress-Archive -Path "$staging\*" -DestinationPath $archive -CompressionLevel Optimal

$exe = Get-Item (Join-Path $staging "MonitorExtender.Server.exe")
Write-Host ""
Write-Host "Fatto." -ForegroundColor Green
"  eseguibile : {0:N1} MB" -f ($exe.Length / 1MB)
"  archivio   : {0}  ({1:N1} MB)" -f $archive, ((Get-Item $archive).Length / 1MB)
Write-Host ""
Write-Host "Da allegare alla release su GitHub:" -ForegroundColor Cyan
Write-Host "  $archive"
