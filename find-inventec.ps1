# 找 Inventec WMI 接口：wmi-classes 搜索 + GamingBoxHost GUID 提取
$root = 'D:\ZCode\.zcode\workspace\d5dbea00-ec3e-4c33-a2a1-69fb62e36ba5'

Write-Output '=== wmi-classes.txt 中搜 Inventec ==='
Select-String -Path ($root + '\wmi-classes.txt') -Pattern 'inventec' -SimpleMatch | ForEach-Object { Write-Output ('  ' + $_.Line) }

Write-Output ''
Write-Output '=== GamingBoxHost.exe 中的 GUID ==='
$b = [IO.File]::ReadAllBytes('C:\Program Files (x86)\Timi Personal Computing\GamingBox\1.4.0.851\GamingBoxHost.exe')
$ascii = [Text.Encoding]::ASCII.GetString($b)
$m = [regex]::Matches($ascii, '[0-9A-Fa-f]{8}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{12}')
$g = $m | ForEach-Object { $_.Value } | Sort-Object -Unique
$g | ForEach-Object { Write-Output ('  ' + $_) }

Write-Output ''
Write-Output '=== UTF16 GUID ==='
$utf16 = [Text.Encoding]::Unicode.GetString($b)
$m2 = [regex]::Matches($utf16, '[0-9A-Fa-f]{8}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{12}')
$g2 = $m2 | ForEach-Object { $_.Value } | Sort-Object -Unique
$g2 | ForEach-Object { Write-Output ('  ' + $_) }
