# 枚举可能的触控板设备（PnP）
Write-Output '=== 鼠标/触控板类设备 ==='
Get-CimInstance Win32_PointingDevice | ForEach-Object {
    Write-Output ('  ' + $_.Name + ' | 状态=' + $_.Status + ' | PNP=' + $_.PNPDeviceID)
}
Write-Output ''
Write-Output '=== 名称含 Touch 的 PnP 设备 ==='
Get-CimInstance Win32_PnPEntity | Where-Object { $_.Name -match 'touch|触控' } | ForEach-Object {
    Write-Output ('  ' + $_.Name + ' | 状态=' + $_.Status + ' | PNP=' + $_.DeviceID)
}
Write-Output ''
Write-Output '=== HID 类设备（前 25 个）==='
Get-CimInstance Win32_PnPEntity | Where-Object { $_.PNPDeviceID -like 'HID\*' } | Select-Object -First 25 | ForEach-Object {
    Write-Output ('  ' + $_.Name + ' | 状态=' + $_.Status + ' | PNP=' + $_.DeviceID)
}
