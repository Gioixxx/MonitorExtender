# Simula quello che fa l'app: manda la sonda UDP in broadcast e stampa chi risponde.
#
#   powershell -File tools\test-discovery.ps1
param(
    [int]$Port = 8079,
    [int]$TimeoutMs = 2000,
    [string]$Target = "255.255.255.255"
)

$probe = [System.Text.Encoding]::UTF8.GetBytes("MONITOREXTENDER?")
$udp = New-Object System.Net.Sockets.UdpClient
$udp.EnableBroadcast = $true
$udp.Client.ReceiveTimeout = $TimeoutMs

try {
    $endpoint = New-Object System.Net.IPEndPoint([System.Net.IPAddress]::Parse($Target), $Port)
    [void]$udp.Send($probe, $probe.Length, $endpoint)
    "sonda inviata a ${Target}:$Port, attesa risposte per ${TimeoutMs}ms..."

    $deadline = (Get-Date).AddMilliseconds($TimeoutMs)
    $found = 0
    while ((Get-Date) -lt $deadline) {
        try {
            $from = New-Object System.Net.IPEndPoint([System.Net.IPAddress]::Any, 0)
            $data = $udp.Receive([ref]$from)
            $text = [System.Text.Encoding]::UTF8.GetString($data)
            "  risposta da $($from.Address): $text"
            $found++
        } catch [System.Net.Sockets.SocketException] {
            break   # timeout di ricezione
        }
    }
    if ($found -eq 0) { "  nessuna risposta" } else { "  $found server trovati" }
} finally {
    $udp.Close()
}
