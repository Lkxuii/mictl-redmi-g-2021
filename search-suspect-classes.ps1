# 搜索 root\WMI 中可疑的英业达/触控板类名
$all = Get-CimClass -Namespace root\WMI
Write-Output '=== 类名含可疑关键词 ==='
$all | Where-Object { $_.CimClassName -match 'TMA|Inventec|ITE|Wmi[A-Z]|^[A-Z]{2,6}_?' -and $_.CimClassName -notmatch 'MSNdis|MSiSCSI|MSMCA|MSFC|MS_SM|MSStorage|MSLSA|MSSAM|MSSerial|MSTape|MSChanger|MSWmi|WmiMonitor|IPMI|FT_|Bcd' } | ForEach-Object {
    $g = $_.CimClassQualifiers['guid']
    Write-Output ('  ' + $_.CimClassName + '  guid=' + ($g ? $g.Value : '(无)') + '  super=' + $_.CimSuperClassName)
}
