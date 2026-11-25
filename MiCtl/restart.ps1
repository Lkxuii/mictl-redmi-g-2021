# 重启 MiCtl（需要管理员权限运行本脚本）
# 流程：结束正在运行的 MiCtl（它以管理员运行，普通权限结束不了）→ 编译 → 以管理员身份启动。
# 在普通权限的终端里这样调用（会弹一次 UAC）：
#   Start-Process pwsh -Verb RunAs -ArgumentList '-NoProfile','-ExecutionPolicy','Bypass','-File','<本脚本路径>'
# 结果写到 out\restart.log。运行中的 exe 文件被系统锁定，所以必须先结束进程再编译。
param([switch]$SkipBuild)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$exe = Join-Path $root 'out\MiCtl.exe'
$log = Join-Path $root 'out\restart.log'
$lines = New-Object System.Collections.Generic.List[string]
try
{
    $old = @(Get-Process MiCtl -ErrorAction SilentlyContinue)
    $lines.Add("旧实例: " + $(if ($old.Count) { ($old | ForEach-Object { $_.Id }) -join ',' } else { '无' }))
    if ($old.Count) { $old | Stop-Process -Force }
    for ($i = 0; $i -lt 50 -and (Get-Process MiCtl -ErrorAction SilentlyContinue); $i++) { Start-Sleep -Milliseconds 100 }

    if (-not $SkipBuild)
    {
        $buildOut = & (Join-Path $root 'build.ps1') 2>&1 | ForEach-Object { $_.ToString() }
        $lines.AddRange([string[]]$buildOut)
        if (($buildOut -join ' ') -notmatch 'BUILD OK') { throw '编译失败，见上方输出' }
    }

    Start-Process -FilePath $exe
    Start-Sleep -Seconds 2
    $p = Get-Process MiCtl -ErrorAction SilentlyContinue
    $lines.Add("新实例: " + $(if ($p) { 'PID ' + ($p.Id -join ',') } else { '启动后未找到进程' }))
    $lines.Add('OK')
}
catch
{
    $lines.Add('FAILED: ' + $_.Exception.Message)
}
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $log) | Out-Null
[System.IO.File]::WriteAllLines($log, $lines, (New-Object System.Text.UTF8Encoding($false)))
