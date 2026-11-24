# 只读检查：PawnIO 当前残留状态，以及 setupapi 日志里最后一次与它相关的记录
Write-Output '=== 文件 / 注册表 ==='
Write-Output ('Program Files\PawnIO 目录: ' + (Test-Path 'C:\Program Files\PawnIO'))
Write-Output ('Uninstall 注册表项     : ' + (Test-Path 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\PawnIO'))

Write-Output ''
Write-Output '=== DriverStore 中的 pawnio 包 ==='
$ds = Get-ChildItem 'C:\Windows\System32\DriverStore\FileRepository' -Filter 'pawnio*' -ErrorAction SilentlyContinue
if (-not $ds) { Write-Output '  (无)' }
foreach ($d in $ds) {
    Write-Output ('  ' + $d.Name)
    Get-ChildItem $d.FullName | ForEach-Object { Write-Output ('     ' + $_.Name + '  ' + $_.Length + '  ' + $_.LastWriteTime) }
}

Write-Output ''
Write-Output '=== setupapi.dev.log 中 pawnio 相关记录（最后 12 条）==='
$log = 'C:\Windows\INF\setupapi.dev.log'
if (Test-Path $log) {
    $lines = Get-Content $log -ErrorAction SilentlyContinue
    $idx = New-Object System.Collections.Generic.List[int]
    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -match 'pawnio') { $idx.Add($i) }
    }
    Write-Output ('  匹配行数: ' + $idx.Count + '  (日志共 ' + $lines.Count + ' 行)')
    $start = [Math]::Max(0, $idx.Count - 12)
    for ($k = $start; $k -lt $idx.Count; $k++) {
        $i = $idx[$k]
        $j = $i
        while ($j -gt 0 -and $lines[$j] -notmatch '^>>>\s+\[') { $j-- }
        Write-Output ('  ' + $lines[$j].Trim() + '  |  ' + $lines[$i].Trim())
    }
} else { Write-Output '  日志不存在' }
