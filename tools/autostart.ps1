# Registra MonitorExtender perche' parta al login, tramite Utilita' di pianificazione.
#
#   powershell -File tools\autostart.ps1            installa
#   powershell -File tools\autostart.ps1 -Remove    rimuove
#   powershell -File tools\autostart.ps1 -Status    mostra lo stato
#
# Perche' non un servizio Windows: i servizi girano nella sessione 0, isolata dal desktop
# dell'utente, e non possono catturare lo schermo. Un servizio partirebbe regolarmente e
# produrrebbe frame neri. L'unica strada e' un programma nella sessione interattiva.
#
# Il compito e' registrato per l'utente corrente e non richiede privilegi di amministratore.
param(
    [string]$Task = "MonitorExtender",
    [int]$DelaySeconds = 20,
    [string]$Exe,
    [switch]$Remove,
    [switch]$Status
)

$ErrorActionPreference = "Stop"

if (-not $Exe) {
    $root = Split-Path -Parent $PSScriptRoot
    $Exe = Join-Path $root "src\MonitorExtender.Server\bin\Debug\net8.0-windows\MonitorExtender.Server.exe"
}

function Show-Status {
    $existing = Get-ScheduledTask -TaskName $Task -ErrorAction SilentlyContinue
    if (-not $existing) {
        Write-Host "Compito '$Task': non registrato" -ForegroundColor Yellow
        return
    }
    $info = Get-ScheduledTaskInfo -TaskName $Task
    Write-Host "Compito '$Task': registrato" -ForegroundColor Green
    Write-Host "  stato          : $($existing.State)"
    Write-Host "  azione         : $($existing.Actions[0].Execute)"
    Write-Host "  ultima esecuz. : $($info.LastRunTime)  (esito $($info.LastTaskResult))"
    $running = Get-Process -Name "MonitorExtender.Server" -ErrorAction SilentlyContinue
    if ($running) {
        Write-Host "  processo       : in esecuzione (PID $($running.Id))" -ForegroundColor Green
    } else {
        Write-Host "  processo       : non in esecuzione" -ForegroundColor Yellow
    }
}

if ($Status) { Show-Status; exit 0 }

if ($Remove) {
    if (Get-ScheduledTask -TaskName $Task -ErrorAction SilentlyContinue) {
        Unregister-ScheduledTask -TaskName $Task -Confirm:$false
        Write-Host "Compito '$Task' rimosso." -ForegroundColor Green
    } else {
        Write-Host "Compito '$Task' non era registrato." -ForegroundColor Yellow
    }
    Write-Host "Il server gia' avviato resta in esecuzione: chiudilo dall'icona vicino all'orologio."
    exit 0
}

if (-not (Test-Path $Exe)) {
    Write-Host "Eseguibile non trovato: $Exe" -ForegroundColor Red
    Write-Host "Compilalo prima con:  dotnet build src\MonitorExtender.Server" -ForegroundColor Yellow
    exit 1
}

$action = New-ScheduledTaskAction -Execute $Exe -WorkingDirectory (Split-Path -Parent $Exe)

# Il ritardo evita di contendere il disco con tutto il resto che parte al login.
$trigger = New-ScheduledTaskTrigger -AtLogOn -User "$env:USERDOMAIN\$env:USERNAME"
$trigger.Delay = "PT${DelaySeconds}S"

$settings = New-ScheduledTaskSettingsSet `
    -AllowStartIfOnBatteries `
    -DontStopIfGoingOnBatteries `
    -StartWhenAvailable `
    -ExecutionTimeLimit ([TimeSpan]::Zero) `
    -MultipleInstances IgnoreNew

# Interactive: il compito gira nella sessione dell'utente, che e' l'unico modo per
# vedere il desktop e mostrare l'icona vicino all'orologio.
$principal = New-ScheduledTaskPrincipal `
    -UserId "$env:USERDOMAIN\$env:USERNAME" `
    -LogonType Interactive `
    -RunLevel Limited

Register-ScheduledTask -TaskName $Task -Action $action -Trigger $trigger `
    -Settings $settings -Principal $principal -Force `
    -Description "Avvia MonitorExtender al login. Rimuovi con tools\autostart.ps1 -Remove." | Out-Null

Write-Host "Compito '$Task' registrato." -ForegroundColor Green
Write-Host "  eseguibile : $Exe"
Write-Host "  avvio      : al login di $env:USERDOMAIN\$env:USERNAME, dopo $DelaySeconds s"
Write-Host ""
Write-Host "Per provarlo subito senza riavviare:" -ForegroundColor Cyan
Write-Host "  Start-ScheduledTask -TaskName $Task"
Write-Host "Per rimuoverlo:" -ForegroundColor Cyan
Write-Host "  powershell -File tools\autostart.ps1 -Remove"
