# 扫描 main.js 的全部 window.register_* 回调与主机接口
$gb = 'C:\Program Files (x86)\Timi Personal Computing\GamingBox\1.4.0.851'
$jsText = [IO.File]::ReadAllText((Join-Path $gb 'dist\main.js'))

Write-Output '===== window.register_* 回调 ====='
$m = [regex]::Matches($jsText, 'window\.([A-Za-z_][A-Za-z0-9_]*)')
$names = $m | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique
$names | ForEach-Object { Write-Output ('  window.' + $_) }

Write-Output ''
Write-Output '===== register_fan_speed_changed 上下文 ====='
$pat = 'register_fan_speed_changed.{0,400}'
$m = [regex]::Matches($jsText, $pat)
$m | Select-Object -First 3 | ForEach-Object { Write-Output ($_.Value -replace '\r?\n', ' ¶ '); Write-Output '  ---' }

Write-Output ''
Write-Output '===== 转速/风扇相关字段名 ====='
foreach ($kw in @('cpuFan', 'gpuFan', 'fanRpm', 'fan_rpm', 'speedChanged', 'fanSpeedChanged', 'currSpeed', 'cpuSpeed', 'gpuSpeed', 'FAN_SPEED')) {
    $p2 = '.{0,80}' + [regex]::Escape($kw) + '.{0,120}'
    $m2 = [regex]::Matches($jsText, $p2)
    if ($m2.Count -gt 0) {
        Write-Output ('--- [' + $kw + '] x' + $m2.Count + ' ---')
        $m2 | Select-Object -First 2 | ForEach-Object { Write-Output ('  ' + ($_.Value -replace '\r?\n', ' ¶ ')) }
    }
}
