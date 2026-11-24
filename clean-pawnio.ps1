# 清理 PawnIO 残留（服务 + 注册表 + 目录），为干净重装做准备
Write-Output '--- 清理前状态 ---'
sc.exe query PawnIO 2>&1 | Out-String
sc.exe stop PawnIO 2>$null | Out-Null
sc.exe delete PawnIO 2>&1 | Out-String

$regPath = 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\PawnIO'
if (Test-Path $regPath) {
    Remove-Item $regPath -Recurse -Force
    Write-Output '已删除注册表项'
}
if (Test-Path 'C:\Program Files\PawnIO') {
    Remove-Item 'C:\Program Files\PawnIO' -Recurse -Force
    Write-Output '已删除安装目录'
}
if (Test-Path 'C:\Windows\System32\drivers\PawnIO.sys') {
    Remove-Item 'C:\Windows\System32\drivers\PawnIO.sys' -Force
    Write-Output '已删除驱动文件'
}
Write-Output '--- 清理完成 ---'
