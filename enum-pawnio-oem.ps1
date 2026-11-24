# 只读：列出系统中登记的 pawnio 相关第三方驱动包（oemNN.inf）
$raw = pnputil /enum-drivers | Out-String
$blocks = $raw -split "(\r?\n){2,}"
$hit = @($blocks | Where-Object { $_ -match 'pawnio' })
Write-Output ('pawnio 相关 oem 驱动包: ' + $hit.Count + ' 个')
foreach ($b in $hit) {
    Write-Output '--------------------------------'
    Write-Output $b.Trim()
}

Write-Output ''
Write-Output '=== 当前 pawnio 设备 ==='
$dev = pnputil /enum-devices /connected 2>$null | Out-String
$dblocks = $dev -split "(\r?\n){2,}"
$dhit = @($dblocks | Where-Object { $_ -match 'pawnio' })
if ($dhit.Count -eq 0) { Write-Output '  (无)' }
foreach ($b in $dhit) { Write-Output $b.Trim() }
