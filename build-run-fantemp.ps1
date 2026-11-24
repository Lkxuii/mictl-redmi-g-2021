# 编译并提权运行风扇/温度对照诊断
$root = 'D:\ZCode\.zcode\workspace\d5dbea00-ec3e-4c33-a2a1-69fb62e36ba5'
$p = Join-Path $root 'probe096'
New-Item -ItemType Directory -Force -Path $p | Out-Null
# 用 MiCtl\out 里的 0.9.6 运行时依赖（probe 目录里是旧版 0.9.4）
Copy-Item (Join-Path $root 'MiCtl\out\*.dll') $p -Force

$outOpt = '/out:' + $p + '\FanTempDiag.exe'
$refOpt = '/r:' + $root + '\MiCtl\libs\LibreHardwareMonitorLib.dll'
& 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe' /nologo $outOpt $refOpt /r:System.Management.dll ($root + '\probe\FanTempDiag.cs')
if ($LASTEXITCODE -ne 0) { Write-Output 'BUILD FAILED'; exit 1 }
Write-Output 'BUILD OK'

Remove-Item ($root + '\probe\fantemp-diag.txt') -ErrorAction SilentlyContinue
Start-Process -Verb RunAs -Wait -WindowStyle Hidden -FilePath ($p + '\FanTempDiag.exe') -ArgumentList ($root + '\probe\fantemp-diag.txt')

$res = $root + '\probe\fantemp-diag.txt'
if (Test-Path $res) { Get-Content $res -Encoding UTF8 } else { Write-Output '诊断未产出结果文件' }
