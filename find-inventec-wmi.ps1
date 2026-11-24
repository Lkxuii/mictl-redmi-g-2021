# 在全量字符串中找 Inventec 的 WMI 类名/命名空间/方法名
$root = 'D:\ZCode\.zcode\workspace\d5dbea00-ec3e-4c33-a2a1-69fb62e36ba5'

Write-Output '=== strings-u16 中含关键词的行 ==='
$lines = Get-Content ($root + '\rev\strings-u16.txt')
$hit = $lines | Where-Object { $_ -match 'TMA|Inventec|WMIEvent|_WMI|WMI_|AWMI|TMAWMI|Select|SELECT|root\\' }
$hit | ForEach-Object { Write-Output $_ }

Write-Output ''
Write-Output '=== strings-ascii 中含关键词的行 ==='
$lines2 = Get-Content ($root + '\rev\strings-ascii.txt')
$hit2 = $lines2 | Where-Object { $_ -match 'TMA|Inventec|WMIEvent|_WMI|WMI_|AWMI|root\\|Event2' }
$hit2 | ForEach-Object { Write-Output $_ }
