# 扫 KeyboardMonitor.dll / GamingBoxHost.exe / main.js 的触控板与热键实现线索
$gb = 'C:\Program Files (x86)\Timi Personal Computing\GamingBox\1.4.0.851'
$out = New-Object System.Collections.Generic.List[string]

function ScanText([string]$file, [string[]]$keywords, [int]$ctx = 150) {
    if (-not (Test-Path $file)) { $out.Add('MISS: ' + $file); return }
    $bytes = [IO.File]::ReadAllBytes($file)
    $ascii = [Text.Encoding]::ASCII.GetString($bytes)
    $utf16 = [Text.Encoding]::Unicode.GetString($bytes)
    $out.Add('===== ' + (Split-Path $file -Leaf) + ' =====')
    foreach ($kw in $keywords) {
        $ea = [regex]::Escape($kw)
        $ma = [regex]::Matches($ascii, '.{0,' + $ctx + '}' + $ea + '.{0,' + $ctx + '}')
        $mu = [regex]::Matches($utf16, '.{0,' + $ctx + '}' + $ea + '.{0,' + $ctx + '}')
        foreach ($src in @(@('ASCII', $ma), @('UTF16', $mu))) {
            if ($src[1].Count -gt 0) {
                $out.Add('--- [' + $kw + '/' + $src[0] + '] x' + $src[1].Count + ' ---')
                $src[1] | Select-Object -First 3 | ForEach-Object {
                    $out.Add('  ' + ($_.Value -replace '[\x00-\x1F]', '·'))
                }
            }
        }
    }
}

ScanText (Join-Path $gb 'KeyboardMonitor.dll') @('touchpad', 'Touchpad', 'TouchPad', 'touch', 'Fn', 'hotkey', 'Hotkey', 'VK_', 'disable', 'Disable', 'a7', 'A7') 120
ScanText (Join-Path $gb 'GamingBoxHost.exe') @('touchpad', 'Touchpad', 'TouchPad', 'MSFT0001', 'disable-device', 'pnputil', 'SetupDi', 'DICS_ENABLE', 'DICS_DISABLE', 'SPDRP') 150

# main.js: set_a7_touchpad_status / set_touchpad_status 上下文
$jsText = [IO.File]::ReadAllText((Join-Path $gb 'dist\main.js'))
foreach ($kw in @('set_a7_touchpad_status', 'set_touchpad_status', 'get_a7_touchpad_status', 'touchpad_status')) {
    $ea = [regex]::Escape($kw)
    $m = [regex]::Matches($jsText, '.{0,200}' + $ea + '.{0,250}')
    if ($m.Count -gt 0) {
        $out.Add('--- [js:' + $kw + '] x' + $m.Count + ' ---')
        $m | Select-Object -First 2 | ForEach-Object { $out.Add('  ' + ($_.Value -replace '\r?\n', ' ¶ ')) }
    }
}

$out | Out-File -FilePath 'D:\ZCode\.zcode\workspace\d5dbea00-ec3e-4c33-a2a1-69fb62e36ba5\scan-tp-result.txt' -Encoding utf8
Write-Output ('扫描完成 -> scan-tp-result.txt (' + $out.Count + ' 行)')
