# 全量 dump GamingBoxHost.exe 的 UTF16 字符串，过滤出接口/类名相关的
$b = [IO.File]::ReadAllBytes('C:\Program Files (x86)\Timi Personal Computing\GamingBox\1.4.0.851\GamingBoxHost.exe')
$utf16 = [Text.Encoding]::Unicode.GetString($b)

$out = New-Object System.Collections.Generic.List[string]
# 提取所有可读 UTF16 字符串（长度>=4）
$rx = New-Object System.Text.RegularExpressions.Regex '[\x20-\x7E\u4E00-\u9FFF]{4,}'
foreach ($mm in $rx.Matches($utf16)) {
    $s = $mm.Value
    if ($s -match 'Interface|interface|Wmi|WMI|wmi|Touchpad|TouchPad|touchpad|Windows_?[Kk]ey|Fn_?[Kk]ey|FnLock|WinLock|Inventec|FanStatus|fan_status|keyboard|Keyboard|Enumerate|Query') {
        $out.Add($s)
    }
}
$out | Out-File -FilePath 'D:\ZCode\.zcode\workspace\d5dbea00-ec3e-4c33-a2a1-69fb62e36ba5\host-strings.txt' -Encoding utf8
Write-Output ('提取 ' + $out.Count + ' 条 -> host-strings.txt')
