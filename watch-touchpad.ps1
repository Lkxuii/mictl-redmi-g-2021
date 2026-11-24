# 触控板状态实时监听（30 秒）—— 请在此期间用键盘快捷键切换触控板
Write-Output '监听 30 秒，请现在用键盘快捷键切换触控板...'
$prev = $null
for ($i = 0; $i -lt 60; $i++) {
    $devs = Get-CimInstance Win32_PnPEntity | Where-Object { $_.DeviceID -like 'HID\MSFT0001*' }
    $state = ($devs | ForEach-Object { $_.DeviceID.Substring(0, 20) + ':' + $_.ConfigManagerErrorCode }) -join ' | '
    if ($prev -ne $state) {
        Write-Output (Get-Date -Format 'HH:mm:ss.fff')
        $devs | ForEach-Object { Write-Output ('   ' + $_.Name + ' | err=' + $_.ConfigManagerErrorCode + ' | ' + $_.DeviceID) }
        $prev = $state
    }
    Start-Sleep -Milliseconds 500
}
Write-Output '监听结束'
