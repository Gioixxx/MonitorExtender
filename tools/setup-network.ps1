# Prepara Windows a servire lo stream sulla LAN. Da eseguire una volta sola.
#
#   powershell -File tools\setup-network.ps1
#
# Si auto-eleva (comparira' il prompt UAC). Fa due cose, entrambe idempotenti:
#   1. urlacl  -> permette a HttpListener di fare bind su http://+:<porta>/ senza admin
#   2. firewall -> apre la porta in ingresso sulle reti private
#   3. (con -TrustNetwork) marca la rete attuale come "privata", altrimenti la
#      regola del punto 2 non si applica e il telefono resta tagliato fuori
param(
    [int]$Port = 8080,
    [int]$DiscoveryPort = 8079,
    [switch]$Remove,
    [switch]$TrustNetwork
)

$ErrorActionPreference = "Stop"
$url = "http://+:$Port/"
$ruleName = "MonitorExtender $Port"
$discoveryRuleName = "MonitorExtender discovery $DiscoveryPort"

$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$isAdmin = ([Security.Principal.WindowsPrincipal]$identity).IsInRole(
    [Security.Principal.WindowsBuiltInRole]::Administrator)

if (-not $isAdmin) {
    Write-Host "Servono privilegi di amministratore: rilancio con UAC..." -ForegroundColor Yellow
    $argList = @("-ExecutionPolicy", "Bypass", "-NoProfile", "-File", "`"$PSCommandPath`"",
                 "-Port", $Port, "-DiscoveryPort", $DiscoveryPort)
    if ($Remove) { $argList += "-Remove" }
    if ($TrustNetwork) { $argList += "-TrustNetwork" }
    try {
        $p = Start-Process powershell -Verb RunAs -ArgumentList $argList -Wait -PassThru
    } catch {
        Write-Host "UAC rifiutato o annullato: nulla e' stato modificato." -ForegroundColor Red
        exit 1
    }
    exit $p.ExitCode
}

if ($Remove) {
    Write-Host "Rimozione configurazione per la porta $Port..." -ForegroundColor Cyan
    netsh http delete urlacl url=$url | Out-Null
    netsh advfirewall firewall delete rule name="$ruleName" | Out-Null
    netsh advfirewall firewall delete rule name="$discoveryRuleName" | Out-Null
    Write-Host "Fatto." -ForegroundColor Green
    Start-Sleep -Seconds 3
    exit 0
}

$user = "$($env:USERDOMAIN)\$($env:USERNAME)"
Write-Host "Configurazione per la porta $Port (utente $user)" -ForegroundColor Cyan

# --- 1. urlacl -------------------------------------------------------------
# Una prenotazione preesistente intestata a un altro utente farebbe fallire l'add:
# si cancella prima, cosi' lo script e' rieseguibile senza sorprese.
netsh http delete urlacl url=$url 2>&1 | Out-Null
netsh http add urlacl url=$url user=$user | Out-Null
if ($LASTEXITCODE -eq 0) {
    Write-Host "  [ok] prenotazione URL $url -> $user" -ForegroundColor Green
} else {
    Write-Host "  [errore] netsh http add urlacl ha restituito $LASTEXITCODE" -ForegroundColor Red
}

# --- 2. firewall -----------------------------------------------------------
$existing = Get-NetFirewallRule -DisplayName $ruleName -ErrorAction SilentlyContinue
if ($existing) {
    Write-Host "  [ok] regola firewall gia' presente" -ForegroundColor Green
} else {
    New-NetFirewallRule -DisplayName $ruleName -Direction Inbound -Action Allow `
        -Protocol TCP -LocalPort $Port -Profile Private | Out-Null
    Write-Host "  [ok] regola firewall creata (solo reti private)" -ForegroundColor Green
}

# --- 2b. firewall per la discovery (UDP) ------------------------------------
# La regola TCP non copre la discovery: sono protocolli diversi e Windows li filtra a parte.
if (Get-NetFirewallRule -DisplayName $discoveryRuleName -ErrorAction SilentlyContinue) {
    Write-Host "  [ok] regola firewall discovery gia' presente" -ForegroundColor Green
} else {
    New-NetFirewallRule -DisplayName $discoveryRuleName -Direction Inbound -Action Allow `
        -Protocol UDP -LocalPort $DiscoveryPort -Profile Private | Out-Null
    Write-Host "  [ok] regola firewall discovery creata (UDP $DiscoveryPort)" -ForegroundColor Green
}

# --- 3. categoria della rete ------------------------------------------------
# La regola vale solo sul profilo "privato": se Windows ha classificato la WiFi
# come pubblica, i pacchetti del telefono vengono scartati in silenzio e il
# browser resta appeso senza mai ricevere un errore.
$public = @(Get-NetConnectionProfile | Where-Object { $_.NetworkCategory -eq "Public" })
if ($public.Count -gt 0) {
    foreach ($net in $public) {
        if ($TrustNetwork) {
            Set-NetConnectionProfile -InterfaceIndex $net.InterfaceIndex -NetworkCategory Private
            Write-Host "  [ok] rete '$($net.Name)' ($($net.InterfaceAlias)) marcata come privata" -ForegroundColor Green
        } else {
            Write-Host "  [attenzione] la rete '$($net.Name)' ($($net.InterfaceAlias)) e' classificata" -ForegroundColor Yellow
            Write-Host "               PUBBLICA: la regola firewall non si applica e il telefono" -ForegroundColor Yellow
            Write-Host "               non riuscira' a connettersi. Rilancia con -TrustNetwork" -ForegroundColor Yellow
            Write-Host "               se e' la tua rete di casa." -ForegroundColor Yellow
        }
    }
}

# --- verifica --------------------------------------------------------------
Write-Host ""
Write-Host "Verifica:" -ForegroundColor Cyan
$acl = (netsh http show urlacl url=$url | Out-String)
if ($acl -match [regex]::Escape($url)) {
    Write-Host "  prenotazione URL : presente" -ForegroundColor Green
} else {
    Write-Host "  prenotazione URL : ASSENTE" -ForegroundColor Red
}
if (Get-NetFirewallRule -DisplayName $ruleName -ErrorAction SilentlyContinue) {
    Write-Host "  firewall TCP $Port : presente" -ForegroundColor Green
} else {
    Write-Host "  firewall TCP $Port : ASSENTE" -ForegroundColor Red
}
if (Get-NetFirewallRule -DisplayName $discoveryRuleName -ErrorAction SilentlyContinue) {
    Write-Host "  firewall UDP $DiscoveryPort : presente" -ForegroundColor Green
} else {
    Write-Host "  firewall UDP $DiscoveryPort : ASSENTE" -ForegroundColor Red
}
foreach ($net in Get-NetConnectionProfile) {
    $color = if ($net.NetworkCategory -eq "Public") { "Red" } else { "Green" }
    Write-Host "  rete '$($net.Name)' : $($net.NetworkCategory)" -ForegroundColor $color
}

Write-Host ""
Write-Host "Questa finestra si chiude tra 8 secondi."
Start-Sleep -Seconds 8
