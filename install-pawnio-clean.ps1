# 安装 PawnIO 2.1.0（吸取上次教训：不做任何 sc delete，装完当场验证服务与设备）
$root = 'D:\ZCode\.zcode\workspace\d5dbea00-ec3e-4c33-a2a1-69fb62e36ba5'
$log = New-Object System.Collections.Generic.List[string]

Write-Output '--- 安装前状态 ---'
$pre = sc.exe query PawnIO 2>&1 | Out-String
$log.Add('安装前服务状态: ' + ($pre -match '指定的服务未安装' ? '未安装' : ($pre -match 'RUNNING' ? 'RUNNING' : '其他')))
$log.Add($pre.Trim())

Write-Output '--- 静默安装 2.1.0 ---'
Start-Process -Wait -FilePath ($root + '\PawnIO_setup_210.exe') -ArgumentList '-install','-silent'
Start-Sleep 2

Write-Output '--- 验证 ---'
$q = sc.exe query PawnIO 2>&1 | Out-String
$log.Add('服务: ' + ($q -match 'RUNNING' ? 'RUNNING' : ($q -match 'STOPPED' ? 'STOPPED（已注册）' : '未安装')))
$log.Add($q.Trim())

$dev = pnputil /enum-devices 2>$null | Out-String
$devHit = ($dev -split "(\r?\n){2,}") | Where-Object { $_ -match 'PawnIO' }
$log.Add('设备: ' + ($devHit ? '存在 ROOT\PAWNIO 设备' : '无设备'))
if ($devHit) { $log.Add(($devHit -join "`n").Trim()) }

$ds = Get-ChildItem 'C:\Windows\System32\DriverStore\FileRepository' -Filter 'pawnio*' -ErrorAction SilentlyContinue
foreach ($d in $ds) {
    $sys = Test-Path (Join-Path $d.FullName 'PawnIO.sys')
    $log.Add('DriverStore 包: ' + $d.Name + '  PawnIO.sys=' + $sys)
}

$log | Out-File -FilePath ($root + '\pawnio-install-result.txt') -Encoding utf8
Get-Content ($root + '\pawnio-install-result.txt')
