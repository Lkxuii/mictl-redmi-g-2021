Start-Sleep 8
$p = Get-Process MiCtl -ErrorAction SilentlyContinue
if ($p) {
    foreach ($x in $p) {
        Write-Output ('PID=' + $x.Id + '  Mem=' + [Math]::Round($x.WorkingSet64 / 1MB, 1) + 'MB  WindowTitle=' + $x.MainWindowTitle)
    }
} else {
    Write-Output '进程不在运行'
}
