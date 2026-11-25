# 从游戏盒子前端 main.js 里抽出后端通道名与监控相关的代码片段 → rev/gb-frontend-channels.txt
# 不需要管理员权限。用法: pwsh -File extract-gb-channels.ps1
param(
    [string]$Js = 'C:\Program Files (x86)\Timi Personal Computing\GamingBox\1.4.0.851\dist\main.js',
    [string]$Out = ''
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
if (-not $Out) { $Out = Join-Path $root 'rev\gb-frontend-channels.txt' }
$t = [System.IO.File]::ReadAllText($Js)
$sb = New-Object System.Text.StringBuilder
[void]$sb.AppendLine("file: $Js")
[void]$sb.AppendLine("length: $($t.Length)")

# 已知通道名 / 关键字的上下文
foreach ($k in 'set_a7_fn_key_status', 'kill_process', 'cpu_usage', 'gpu_usage', 'frequency', 'ssid') {
    [void]$sb.AppendLine('')
    [void]$sb.AppendLine("=== $k ===")
    foreach ($m in [regex]::Matches($t, [regex]::Escape($k), 'IgnoreCase')) {
        $i = $m.Index
        [void]$sb.AppendLine("--- @ $i")
        [void]$sb.AppendLine($t.Substring([Math]::Max(0, $i - 220), [Math]::Min(520, $t.Length - [Math]::Max(0, $i - 220))))
    }
}

# snake_case 字符串字面量（通道名一般长这样）
$pat = '["'']([a-z][a-z0-9]*(?:_[a-z0-9]+){1,6})["'']'
$g = [regex]::Matches($t, $pat) | ForEach-Object { $_.Groups[1].Value } | Group-Object | Sort-Object Name
[void]$sb.AppendLine('')
[void]$sb.AppendLine("=== snake_case 字面量 ($($g.Count)) ===")
foreach ($x in $g) { [void]$sb.AppendLine(('{0,3}  {1}' -f $x.Count, $x.Name)) }

# 可能的桥接对象（contextBridge 暴露的 api 名）
$pat2 = 'contextBridge\.exposeInMainWorld\(\s*["'']([A-Za-z0-9_]+)["'']'
$m3 = [regex]::Matches($t, $pat2)
[void]$sb.AppendLine('')
[void]$sb.AppendLine("=== exposeInMainWorld ($($m3.Count)) ===")
foreach ($x in $m3) {
    $i = $x.Index
    [void]$sb.AppendLine($x.Groups[1].Value + '  @ ' + $i)
    [void]$sb.AppendLine($t.Substring($i, [Math]::Min(1500, $t.Length - $i)))
}

[System.IO.File]::WriteAllText($Out, $sb.ToString(), (New-Object System.Text.UTF8Encoding($false)))
Write-Output "written $Out"
