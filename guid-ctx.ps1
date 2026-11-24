# GUID 在 GamingBoxHost.exe 中的上下文
$root = 'D:\ZCode\.zcode\workspace\d5dbea00-ec3e-4c33-a2a1-69fb62e36ba5'
$b = [IO.File]::ReadAllBytes('C:\Program Files (x86)\Timi Personal Computing\GamingBox\1.4.0.851\GamingBoxHost.exe')
$ascii = [Text.Encoding]::ASCII.GetString($b)
$utf16 = [Text.Encoding]::Unicode.GetString($b)

$out = New-Object System.Collections.Generic.List[string]
foreach ($g in @('6E5C5DE2-0AC5-446A-A473-2F736CD06B18', '799F1D61-045A-4A3C-895A-03152C28A2F2', 'DF541A2B-1DAD-4930-B42B-79719761F838')) {
    $ea = [regex]::Escape($g)
    foreach ($src in @(@('ASCII', $ascii), @('UTF16', $utf16))) {
        $m = [regex]::Matches($src[1], '.{0,300}' + $ea + '.{0,300}')
        foreach ($mm in $m) {
            $out.Add('=== [' + $g + '/' + $src[0] + '] ===')
            $out.Add('  ' + ($mm.Value -replace '[\x00-\x1F]', '·'))
        }
    }
}
$out | Out-File -FilePath ($root + '\guid-ctx.txt') -Encoding utf8
Write-Output ('完成，' + $out.Count + ' 行')
