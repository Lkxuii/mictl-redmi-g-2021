# GamingBoxHost.exe 全量逆向扫描
# 1) 全量字符串（ASCII+UTF16 带偏移） 2) RTTI 类名  3) MIFS 常量字节模式
$exe = 'C:\Program Files (x86)\Timi Personal Computing\GamingBox\1.4.0.851\GamingBoxHost.exe'
$root = 'D:\ZCode\.zcode\workspace\d5dbea00-ec3e-4c33-a2a1-69fb62e36ba5'
$b = [IO.File]::ReadAllBytes($exe)
Write-Output ('文件大小: ' + $b.Length)

# ---- 1. UTF16 字符串全量（带偏移）----
$utf16 = [Text.Encoding]::Unicode.GetString($b)
$sb = New-Object System.Text.StringBuilder
$rx = New-Object System.Text.RegularExpressions.Regex '[\x20-\x7E\u4E00-\u9FFF]{5,}'
foreach ($m in $rx.Matches($utf16)) {
    [void]$sb.AppendLine(('U16@0x' + $m.Index.ToString('X') + '  ' + $m.Value))
}
[IO.File]::WriteAllText(($root + '\rev\strings-u16.txt'), $sb.ToString())

# ---- 2. ASCII 字符串全量（带偏移）----
$ascii = [Text.Encoding]::ASCII.GetString($b)
$sb2 = New-Object System.Text.StringBuilder
foreach ($m in $rx.Matches($ascii)) {
    [void]$sb2.AppendLine(('ASC@0x' + $m.Index.ToString('X') + '  ' + $m.Value))
}
[IO.File]::WriteAllText(($root + '\rev\strings-ascii.txt'), $sb2.ToString())

# ---- 3. RTTI 类名（.?AV 模式，Mangled name）----
$sb3 = New-Object System.Text.StringBuilder
foreach ($m in [regex]::Matches($ascii, '\.\?A[VU][A-Za-z0-9_@$?]{4,120}')) {
    [void]$sb3.AppendLine(('RTTI@0x' + $m.Index.ToString('X') + '  ' + $m.Value))
}
[IO.File]::WriteAllText(($root + '\rev\rtti.txt'), $sb3.ToString())

# ---- 4. MIFS 常量调用模式：00 FA 00 fn / 00 FB 00 fn（32字节常量数组初始化）----
$sb4 = New-Object System.Text.StringBuilder
$cnt = 0
for ($i = 0; $i -lt $b.Length - 8; $i++) {
    if ($b[$i] -eq 0 -and ($b[$i+1] -eq 0xFA -or $b[$i+1] -eq 0xFB) -and $b[$i+2] -eq 0 -and $b[$i+3] -ne 0 -and $b[$i+3] -le 0x20) {
        # 打印前后 16 字节上下文
        $start = [Math]::Max(0, $i - 8)
        $seg = $b[$start..($i + 24)]
        [void]$sb4.AppendLine(('0x' + $i.ToString('X') + '  ' + (($seg | ForEach-Object { $_.ToString('X2') }) -join ' ')))
        $cnt++
    }
}
[IO.File]::WriteAllText(($root + '\rev\mifs-patterns.txt'), $sb4.ToString())
Write-Output ('MIFS 常量模式: ' + $cnt + ' 处')
Write-Output ('字符串文件已写 rev\strings-u16.txt / strings-ascii.txt / rtti.txt')
