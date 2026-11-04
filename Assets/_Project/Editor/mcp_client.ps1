param(
    [Parameter(Mandatory=$true)]
    [string]$Type,
    [string]$ParamsJson = "{}",
    [string]$ParamsFile = ""
)

if ($ParamsFile -ne "" -and (Test-Path $ParamsFile)) {
    $ParamsJson = Get-Content $ParamsFile -Raw
}

# Resolve port from status files
$port = 6400
try {
    $statusFiles = Get-ChildItem "$env:USERPROFILE\.unity-mcp\*.json" -ErrorAction SilentlyContinue
    foreach ($f in $statusFiles) {
        $content = Get-Content $f.FullName -Raw | ConvertFrom-Json -ErrorAction SilentlyContinue
        if ($content -and $content.project_path -and $content.project_path -like "*Hush Block*") {
            $port = [int]$content.unity_port
            break
        }
    }
} catch {}

$client = $null
for ($i = 0; $i -lt 10; $i++) {
    try {
        $client = New-Object System.Net.Sockets.TcpClient("127.0.0.1", $port)
        if ($client.Connected) { break }
    } catch {
        Start-Sleep -Milliseconds 800
    }
}
if ($null -eq $client -or -not $client.Connected) {
    Write-Error "Failed to connect to Unity MCP bridge on port $port"
    exit 1
}
$stream = $client.GetStream()

# Read welcome line
$welcomeBytes = New-Object System.Collections.Generic.List[byte]
while ($true) {
    $b = $stream.ReadByte()
    if ($b -eq -1 -or $b -eq 10) { break }
    if ($b -ne 13) { $welcomeBytes.Add($b) }
}

$cmdJson = "{`"type`":`"$Type`",`"params`":$ParamsJson}"

$payload = [System.Text.Encoding]::UTF8.GetBytes($cmdJson)
$lenBytes = [System.BitConverter]::GetBytes([uint64]$payload.Length)
if ([System.BitConverter]::IsLittleEndian) { [System.Array]::Reverse($lenBytes) }
$stream.Write($lenBytes, 0, 8)
$stream.Write($payload, 0, $payload.Length)

# Read 8-byte response header
$respHeader = New-Object byte[] 8
$total = 0
while ($total -lt 8) {
    $read = $stream.Read($respHeader, $total, 8 - $total)
    if ($read -le 0) { throw "Socket closed reading header" }
    $total += $read
}
if ([System.BitConverter]::IsLittleEndian) { [System.Array]::Reverse($respHeader) }
$respLen = [System.BitConverter]::ToUInt64($respHeader, 0)

# Read response payload
$respBody = New-Object byte[] $respLen
$total = 0
while ($total -lt $respLen) {
    $read = $stream.Read($respBody, $total, [int]($respLen - $total))
    if ($read -le 0) { throw "Socket closed reading body" }
    $total += $read
}
$client.Close()
[System.Text.Encoding]::UTF8.GetString($respBody)
