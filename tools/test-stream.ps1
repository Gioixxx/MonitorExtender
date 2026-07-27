# Verifica il contratto MJPEG di /stream: boundary, header di parte, Content-Length
# e magic byte JPEG (FFD8..FFD9) di ogni frame. Riporta fps effettivi e banda.
#
#   powershell -File tools\test-stream.ps1
#   powershell -File tools\test-stream.ps1 -Url http://192.168.1.62:8080/stream -Seconds 10
param(
    [string]$Url = "http://localhost:8080/stream",
    [int]$Seconds = 5
)

Add-Type -AssemblyName System.Net.Http
$client = [System.Net.Http.HttpClient]::new()
$client.Timeout = [TimeSpan]::FromSeconds(30)

$resp = $client.GetAsync($Url, [System.Net.Http.HttpCompletionOption]::ResponseHeadersRead).GetAwaiter().GetResult()
"HTTP $([int]$resp.StatusCode)"
"Content-Type: $($resp.Content.Headers.ContentType)"

$stream = $resp.Content.ReadAsStreamAsync().GetAwaiter().GetResult()
$buffer = New-Object byte[] 65536
$acc = New-Object byte[] 8388608   # accumulatore: le letture arrivano spezzate
$accLen = 0
$deadline = (Get-Date).AddSeconds($Seconds)
$frames = 0; $total = 0; $bad = 0; $sumSize = 0
$rx = [regex]"--frame\r\nContent-Type: image/jpeg\r\nContent-Length: (\d+)\r\n\r\n"

while ((Get-Date) -lt $deadline) {
    $n = $stream.Read($buffer, 0, $buffer.Length)
    if ($n -le 0) { break }
    [System.Buffer]::BlockCopy($buffer, 0, $acc, $accLen, $n)
    $accLen += $n
    $total += $n

    # Estrae tutte le parti complete presenti nell'accumulatore.
    while ($true) {
        $head = [System.Text.Encoding]::ASCII.GetString($acc, 0, [Math]::Min(300, $accLen))
        $m = $rx.Match($head)
        if (-not $m.Success) { break }
        $len = [int]$m.Groups[1].Value
        $start = $m.Index + $m.Length
        if ($accLen -lt $start + $len + 2) { break }   # frame non ancora arrivato per intero

        if ($acc[$start] -ne 0xFF -or $acc[$start + 1] -ne 0xD8 -or
            $acc[$start + $len - 2] -ne 0xFF -or $acc[$start + $len - 1] -ne 0xD9) { $bad++ }

        $frames++; $sumSize += $len
        $consumed = $start + $len + 2
        [System.Buffer]::BlockCopy($acc, $consumed, $acc, 0, $accLen - $consumed)
        $accLen -= $consumed
    }
}
$stream.Dispose(); $resp.Dispose(); $client.Dispose()

"frame ricevuti : $frames in ${Seconds}s  ->  {0:N1} fps" -f ($frames / $Seconds)
"frame corrotti : $bad"
"dimensione med : {0:N0} KB" -f $(if ($frames) { $sumSize / $frames / 1024 } else { 0 })
"banda          : {0:N1} Mbit/s" -f ($total * 8 / $Seconds / 1000000)
