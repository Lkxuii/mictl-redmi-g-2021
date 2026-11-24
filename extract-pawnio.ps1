# 在 PawnIO_setup.exe 中扫描内嵌的 PE 文件（驱动）
$exe = 'D:\ZCode\.zcode\workspace\d5dbea00-ec3e-4c33-a2a1-69fb62e36ba5\PawnIO_setup.exe'
$bytes = [IO.File]::ReadAllBytes($exe)
Write-Output ('文件大小: ' + $bytes.Length)

$found = New-Object System.Collections.Generic.List[int]
# 扫描 "PE\0\0" 紧跟有效 COFF 头的位置（跳过主 PE 的头部）
$limit = $bytes.Length - 4
for ($i = 0; $i -lt $limit - 6; $i++) {
    if ($bytes[$i] -eq 0x50 -and $bytes[$i+1] -eq 0x45 -and $bytes[$i+2] -eq 0 -and $bytes[$i+3] -eq 0) {
        # COFF header: Machine (2B) 必须是 x64 0x8664 或 x86 0x014C
        $machine = $bytes[$i+4] -bor ($bytes[$i+5] -shl 8)
        if ($machine -eq 0x8664 -or $machine -eq 0x014C) {
            # 向前找 MZ（PE header 之前最多 1KB）
            $ok = $false
            for ($j = $i; $j -ge [Math]::Max(0, $i - 1024); $j--) {
                if ($bytes[$j] -eq 0x4D -and $bytes[$j+1] -eq 0x5A) { $found.Add($j); $ok = $true; break }
            }
            if (-not $ok) { $found.Add($i) }
            $i += 256  # 跳过本次命中区域
        }
    }
}
Write-Output ('内嵌 PE 候选: ' + $found.Count + ' 个')
foreach ($pos in $found) {
    Write-Output ('  offset 0x' + $pos.ToString('X') + ' (' + $pos + ')')
}

# 对每个候选，检查是否含 "PawnIO" 字符串特征并保存
$outDir = 'D:\ZCode\.zcode\workspace\d5dbea00-ec3e-4c33-a2a1-69fb62e36ba5\pawnio-extract'
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
$idx = 0
foreach ($pos in $found) {
    # 假设 PE 到文件结尾（最后一个候选）或者到下一个候选
    $end = if ($idx -lt $found.Count - 1) { $found[$idx + 1] } else { $bytes.Length }
    $len = $end - $pos
    if ($len -lt 1024 -or $len -gt 8MB) { $idx++; continue }
    $chunk = New-Object byte[] $len
    [Array]::Copy($bytes, $pos, $chunk, 0, $len)
    $chunkStr = [Text.Encoding]::ASCII.GetString($chunk[0..([Math]::Min($len, 2MB))])
    $isPawn = $chunkStr.Contains('PawnIO')
    Write-Output ('候选 ' + $idx + ': offset=0x' + $pos.ToString('X') + ' len=' + $len + ' 含PawnIO=' + $isPawn)
    if ($isPawn -and $chunkStr.Contains('.sys')) {
        [IO.File]::WriteAllBytes(($outDir + '\embed_' + $idx + '.bin'), $chunk)
        Write-Output ('  已保存 embed_' + $idx + '.bin')
    }
    $idx++
}
