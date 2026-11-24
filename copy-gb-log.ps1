# 复制被锁定的 GamingBox debug.log 并扫描风扇/WMI 调用日志
$src = 'C:\Program Files (x86)\Timi Personal Computing\GamingBox\1.4.0.851\debug.log'
$dst = 'D:\ZCode\.zcode\workspace\d5dbea00-ec3e-4c33-a2a1-69fb62e36ba5\rev\gb-debug.log'

# 用共享读模式复制
$fsIn = [IO.File]::Open($src, 'Open', 'Read', [IO.FileShare]::ReadWrite -bor [IO.FileShare]::Delete)
$fsOut = [IO.File]::Create($dst)
$fsIn.CopyTo($fsOut)
$fsOut.Close()
$fsIn.Close()
Write-Output ('复制完成: ' + (Get-Item $dst).Length + ' 字节')

$text = [IO.File]::ReadAllText($dst)
Write-Output ('行数约: ' + ($text -split "`n").Count)

Write-Output ''
Write-Output '=== 风扇/RPM 相关日志行 ==='
$lines = $text -split "`r?`n"
$hit = $lines | Where-Object { $_ -match 'fan|Fan|FAN|rpm|RPM|转速|speed|Speed' } | Select-Object -First 40
if ($hit) { $hit | ForEach-Object { Write-Output ('  ' + $_.Trim()) } } else { Write-Output '  (无匹配)' }

Write-Output ''
Write-Output '=== MiInterface / InData 调用日志 ==='
$hit2 = $lines | Where-Object { $_ -match 'MiInterface|InData|OutData|MiCommon|MIFS' } | Select-Object -First 30
if ($hit2) { $hit2 | ForEach-Object { Write-Output ('  ' + $_.Trim()) } } else { Write-Output '  (无匹配)' }
