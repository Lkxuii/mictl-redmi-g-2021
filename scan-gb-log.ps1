# 扫描 GamingBox 的日志与前端 JS 中的风扇/功能码线索
$gb = 'C:\Program Files (x86)\Timi Personal Computing\GamingBox\1.4.0.851'

Write-Output '===== debug.log 关键行 ====='
$log = Join-Path $gb 'debug.log'
$bytes = [IO.File]::ReadAllBytes($log)
$text = [Text.Encoding]::UTF8.GetString($bytes)
foreach ($kw in @('MiInterface', 'InData', 'fan', 'Fan', 'FAN', 'speed', 'Speed', '0x0D', 'fn13', 'func')) {
    $pat = '.{0,80}' + [regex]::Escape($kw) + '.{0,120}'
    $m = [regex]::Matches($text, $pat)
    if ($m.Count -gt 0) {
        Write-Output ('--- [' + $kw + '] x' + $m.Count + ' ---')
        $m | Select-Object -First 3 | ForEach-Object { Write-Output ('  ' + ($_.Value -replace '\r?\n', ' ¶ ')) }
    }
}

Write-Output ''
Write-Output '===== main.js 关键字段 ====='
$js = Join-Path $gb 'dist\main.js'
$jsText = [IO.File]::ReadAllText($js)
Write-Output ('main.js 大小: ' + $jsText.Length)
foreach ($kw in @('fanSpeed', 'fan_speed', 'fanRpm', 'rpm', 'FanSpeed', 'CpuFan', 'GpuFan', 'fanSpeeds', 'getFan', 'MICommonInterface', 'MiInterface', 'InData')) {
    $pat = '.{0,100}' + [regex]::Escape($kw) + '.{0,150}'
    $m = [regex]::Matches($jsText, $pat)
    if ($m.Count -gt 0) {
        Write-Output ('--- [' + $kw + '] x' + $m.Count + ' ---')
        $m | Select-Object -First 2 | ForEach-Object { Write-Output ('  ' + ($_.Value -replace '\r?\n', ' ¶ ')) }
    }
}
