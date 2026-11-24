# 扫描 GamingBox 二进制中的关键字符串 + 列出相关服务/驱动
$out = New-Object System.Collections.Generic.List[string]

$targets = @(
    'C:\Program Files (x86)\Timi Personal Computing\GamingBox\1.4.0.851\LaunchBroker.dll',
    'C:\Program Files (x86)\Timi Personal Computing\GamingBox\1.4.0.851\GamingBoxHost.exe',
    'C:\Program Files (x86)\Timi Personal Computing\GamingBox\1.4.0.851\KeyboardMonitor.dll'
)

$patterns = @('MICommonInterface', 'MiInterface', 'InData', 'OutData', 'root\\WMI', 'root\WMI', 'MIFS', 'mifs', 'WbemServices', 'ExecMethod', 'b60bfb48', 'B60BFB48', 'WMI', 'EcAccess', 'WinIo', 'WinRing', 'inpout', 'deviceIo', '\\\\.\\', 'GamingBoxHost')

foreach ($t in $targets) {
    $out.Add('===== ' + $t + ' =====')
    if (-not (Test-Path $t)) { $out.Add('  (不存在)'); continue }
    $bytes = [IO.File]::ReadAllBytes($t)
    $sb = New-Object System.Text.StringBuilder
    foreach ($b in $bytes) { [void]$sb.Append([char]$b) }
    $ascii = $sb.ToString()
    # UTF-16LE 也扫一遍
    $sb2 = New-Object System.Text.StringBuilder
    for ($i = 0; $i -lt $bytes.Length - 1; $i += 2) {
        if ($bytes[$i+1] -eq 0 -and $bytes[$i] -ge 32 -and $bytes[$i] -lt 127) { [void]$sb2.Append([char]$bytes[$i]) } else { [void]$sb2.Append([char]1) }
    }
    $utf16 = $sb2.ToString()
    foreach ($p in $patterns) {
        $cntA = ([regex]::Matches($ascii, [regex]::Escape($p))).Count
        if ($cntA -gt 0) { $out.Add(('  ASCII [' + $p + '] x' + $cntA)) }
        $cntU = ([regex]::Matches($utf16, [regex]::Escape($p))).Count
        if ($cntU -gt 0) { $out.Add(('  UTF16 [' + $p + '] x' + $cntU)) }
    }
}

# 服务与驱动
$out.Add('===== 相关服务/驱动 =====')
Get-CimInstance Win32_SystemDriver | Where-Object { $_.Name -match 'mi|timi|ec|fan' -and $_.Name -notmatch 'microsoft|volume|disk|mount|ndis|tcp|netbt|ahci|atapi|pci|mslldp|mmcs|nwifi|pacer|raspp|rassstp|rspndr|sdbus|storahci|tcpip|vwifibus|wanarp|ws2ifsl|kbdhid|mouhid|hidusb|monitor' } | ForEach-Object {
    $out.Add('  driver: ' + $_.Name + ' | ' + $_.DisplayName + ' | ' + $_.State + ' | ' + $_.PathName)
}
Get-Service | Where-Object { $_.Name -match 'gaming|timi|gamebox|mi' } | ForEach-Object {
    $out.Add('  service: ' + $_.Name + ' | ' + $_.DisplayName + ' | ' + $_.Status)
}

$out | Out-File -FilePath 'D:\ZCode\.zcode\workspace\d5dbea00-ec3e-4c33-a2a1-69fb62e36ba5\scan-result.txt' -Encoding utf8
