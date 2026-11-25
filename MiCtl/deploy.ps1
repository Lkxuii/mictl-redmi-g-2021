# 部署新版：等用户从托盘退出 MiCtl → 编译 → 以管理员身份启动新版（弹一次 UAC）
# 在普通权限的终端里运行：pwsh -File deploy.ps1 [-WaitSeconds 180]
# 与 restart.ps1 的区别：这里不结束进程、不提权运行脚本，只提权启动 exe 本身。
# 某些安全软件（如本机的火绒）会拦截"提权运行脚本 + 结束进程"这类组合，这种方式更不容易被误判。
param([int]$WaitSeconds = 180)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$deadline = (Get-Date).AddSeconds($WaitSeconds)
while ((Get-Process MiCtl -ErrorAction SilentlyContinue) -and (Get-Date) -lt $deadline) {
    Start-Sleep -Seconds 2
}
if (Get-Process MiCtl -ErrorAction SilentlyContinue) {
    Write-Output 'TIMEOUT: MiCtl 仍在运行，未编译'
    exit 2
}
Write-Output 'MiCtl 已退出，开始编译'
$before = (Get-Date)
& (Join-Path $root 'build.ps1')
if ($LASTEXITCODE -ne 0) {
    Write-Output "BUILD FAILED ($LASTEXITCODE)"
    exit 3
}
# 自检：产物必须存在且是刚生成的（本机的火绒曾经拦掉刚编译出来的 exe，而编译器返回的是成功）
$exe = Join-Path $root 'out\MiCtl.exe'
if (-not (Test-Path $exe) -or (Get-Item $exe).LastWriteTime -lt $before.AddSeconds(-5)) {
    Write-Output 'BUILD OUTPUT MISSING: out\MiCtl.exe 不存在或不是刚生成的（可能被安全软件拦截），不启动'
    exit 4
}
Start-Process -FilePath $exe -Verb RunAs
Start-Sleep -Seconds 3
$p = Get-Process MiCtl -ErrorAction SilentlyContinue
if ($p) { Write-Output ('STARTED PID ' + ($p.Id -join ',')) } else { Write-Output 'NOT RUNNING after start' }
