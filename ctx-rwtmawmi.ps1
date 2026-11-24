# RW_TMAWMI 查询的上下文
$b = [IO.File]::ReadAllBytes('C:\Program Files (x86)\Timi Personal Computing\GamingBox\1.4.0.851\GamingBoxHost.exe')
$utf16 = [Text.Encoding]::Unicode.GetString($b)
$ascii = [Text.Encoding]::ASCII.GetString($b)

$out = New-Object System.Collections.Generic.List[string]
foreach ($src in @(@('UTF16', $utf16), @('ASCII', $ascii))) {
    $ea = [regex]::Escape('RW_TMAWMI')
    $m = [regex]::Matches($src[1], '.{0,500}' + $ea + '.{0,500}')
    foreach ($mm in $m) {
        $out.Add('=== [' + $src[0] + '] ===')
        $out.Add('  ' + ($mm.Value -replace '[\x00-\x1F]', '·'))
    }
}
$out | Out-File -FilePath 'D:\ZCode\.zcode\workspace\d5dbea00-ec3e-4c33-a2a1-69fb62e36ba5\rwtmawmi-ctx.txt' -Encoding utf8
Write-Output ('完成 ' + $out.Count + ' 行')
