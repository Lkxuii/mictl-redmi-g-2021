$root = 'D:\ZCode\.zcode\workspace\d5dbea00-ec3e-4c33-a2a1-69fb62e36ba5'
$outOpt = '/out:' + $root + '\probe\LhmDiag.exe'
$ref1 = '/r:' + $root + '\MiCtl\libs\LibreHardwareMonitorLib.dll'
$src = $root + '\probe\LhmDiag.cs'
& 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe' /nologo $outOpt $ref1 /r:System.ServiceProcess.dll $src
if ($LASTEXITCODE -eq 0) { Write-Output 'DIAG BUILD OK' } else { Write-Output 'DIAG BUILD FAILED' }
