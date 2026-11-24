# 追踪 main.js 中风扇数据流：get_fan_status/get_a7_fan_status/RPM 字段
$gb = 'C:\Program Files (x86)\Timi Personal Computing\GamingBox\1.4.0.851'
$jsText = [IO.File]::ReadAllText((Join-Path $gb 'dist\main.js'))

foreach ($kw in @('get_fan_status', 'get_a7_fan_status', 'register_fan_speed_changed', 'register_fan_mode_changed', 'set_fan_mode', 'RPM', 'rpm')) {
    $ea = [regex]::Escape($kw)
    $m = [regex]::Matches($jsText, '.{0,250}' + $ea + '.{0,300}')
    Write-Output ('===== [' + $kw + '] x' + $m.Count + ' =====')
    $seen = @{}
    foreach ($mm in $m) {
        $s = $mm.Value -replace '\r?\n', ' ¶ '
        if (-not $seen.ContainsKey($s)) {
            $seen[$s] = 1
            Write-Output ('  ' + $s)
            Write-Output ''
        }
        if ($seen.Count -ge 3) { break }
    }
}
