# 全量分析 events.log：事件 id 分布统计
$log = 'C:\Users\Lkxuii\AppData\Local\MiCtl\events.log'
$lines = Get-Content $log
Write-Output ('总事件数: ' + $lines.Count)

# EventDetail 前 2 字节 = type id
$stats = @{}
foreach ($l in $lines) {
    if ($l -match 'EventDetail\]=([0-9A-F]{2}) ([0-9A-F]{2}) ([0-9A-F]{2}) ([0-9A-F]{2})') {
        $id = $Matches[2]
        $v = $Matches[3] + $Matches[4]
        if (-not $stats.ContainsKey($id)) { $stats[$id] = @{ Count = 0; Values = @{} } }
        $stats[$id].Count++
        if (-not $stats[$id].Values.ContainsKey($v)) { $stats[$id].Values[$v] = 0 }
        $stats[$id].Values[$v]++
    }
}
Write-Output '=== 事件 id 分布 ==='
foreach ($k in $stats.Keys) {
    $vals = ($stats[$k].Values.GetEnumerator() | ForEach-Object { $_.Key + 'x' + $_.Value }) -join ', '
    Write-Output ('  id 0x' + $k + ' (' + [Convert]::ToInt32($k, 16) + '): ' + $stats[$k].Count + ' 次  values: ' + $vals)
}

# id 26/32 的 value 是 16bit RPM：低字节=第3字节 高字节=第4字节
Write-Output ''
Write-Output '=== 风扇事件（id 0x1A=26 CPU / 0x20=32 GPU）明细 ==='
$lines | Where-Object { $_ -match 'EventDetail\]=01 (1A|20) ' } | Select-Object -First 20 | ForEach-Object { Write-Output ('  ' + $_.Trim()) }
