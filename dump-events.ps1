# 枚举 root\WMI 的事件类（找 MIFS 事件类）+ 测试 fn11/fn12 SET
Write-Output '=== root\WMI 中的事件类（__Event 子类） ==='
$all = Get-CimClass -Namespace root\WMI
$eventClasses = $all | Where-Object { $_.CimSuperClassName -match '^__.*Event$' -or $_.CimClassName -match 'MIFS|MiEvent|A7|Bitland' }
foreach ($c in $eventClasses) {
    Write-Output ('  ' + $c.CimClassName + '  (super: ' + $c.CimSuperClassName + ')')
}
if (-not $eventClasses) {
    Write-Output '  (无直接匹配，列出所有 GUID 形式的动态类)'
    $all | Where-Object { $_.CimClassName -match '^[A-Za-z_][A-Za-z0-9_]{2,30}$' -and $_.CimClassQualifiers['guid'] } | ForEach-Object {
        Write-Output ('  ' + $_.CimClassName + '  guid=' + $_.CimClassQualifiers['guid'].Value)
    }
}
