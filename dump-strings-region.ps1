# RW_TMAWMI 上下文（字符索引 0x632DC 是 Unicode 解码后的字符位置）
$exe = 'C:\Program Files (x86)\Timi Personal Computing\GamingBox\1.4.0.851\GamingBoxHost.exe'
$bytes = [IO.File]::ReadAllBytes($exe)
$utf16 = [Text.Encoding]::Unicode.GetString($bytes)
$seg = $utf16.Substring(0x63000, 0x1400)
$rx = New-Object System.Text.RegularExpressions.Regex '[\x20-\x7E]{4,}'
foreach ($m in $rx.Matches($seg)) {
    Write-Output ('@0x' + (0x63000 + $m.Index).ToString('X') + '  ' + $m.Value)
}
