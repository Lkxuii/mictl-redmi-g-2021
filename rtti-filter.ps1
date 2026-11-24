# 过滤 RTTI 类名：触控板/Win键/Fn/风扇/Inventec 相关
$lines = Get-Content 'D:\ZCode\.zcode\workspace\d5dbea00-ec3e-4c33-a2a1-69fb62e36ba5\rev\rtti.txt'
$hit = $lines | Where-Object { $_ -match 'Touchpad|WindowsKey|FnKey|Fan|Inventec|Quanta|A7|Led|Keyboard|SystemPerformance|GraphicMode|AcType|DownKey|RefreshRate' }
$clean = $hit | ForEach-Object { ($_ -split '\.\?AV', 2)[1] }
$clean | Sort-Object -Unique | ForEach-Object { Write-Output $_ }
Write-Output ''
Write-Output ('命中类名: ' + $clean.Count + ' 条（去重前）')
