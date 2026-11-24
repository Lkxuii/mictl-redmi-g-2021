# 卸载 2.2.0 → 安装 2.1.0 → 验证部署
$root = 'D:\ZCode\.zcode\workspace\d5dbea00-ec3e-4c33-a2a1-69fb62e36ba5'
$log = New-Object System.Collections.Generic.List[string]

Write-Output '--- 卸载 2.2.0 ---'
Start-Process -Wait -FilePath ($root + '\PawnIO_setup.exe') -ArgumentList '-uninstall','-silent'
$log.Add('卸载 2.2.0 完成')
sc.exe delete PawnIO 2>&1 | ForEach-Object { $log.Add($_) }
Start-Sleep 1

Write-Output '--- 安装 2.1.0 ---'
Start-Process -Wait -FilePath ($root + '\PawnIO_setup_210.exe') -ArgumentList '-install','-silent'
$log.Add('安装 2.1.0 完成')
Start-Sleep 2

Write-Output '--- 验证 ---'
$log.Add('PawnIO.sys @System32\drivers: ' + (Test-Path 'C:\Windows\System32\drivers\PawnIO.sys'))
$ds = Get-ChildItem 'C:\Windows\System32\DriverStore\FileRepository' -Filter 'pawnio*' -ErrorAction SilentlyContinue
foreach ($d in $ds) {
    $log.Add('DriverStore: ' + $d.Name)
    Get-ChildItem $d.FullName | ForEach-Object { $log.Add('  ' + $_.Name + '  ' + $_.Length) }
}
(sc.exe query PawnIO 2>&1 | Out-String).Split("`n") | ForEach-Object { $log.Add($_) }
$log | Out-File -FilePath ($root + '\pawnio210-result.txt') -Encoding utf8
