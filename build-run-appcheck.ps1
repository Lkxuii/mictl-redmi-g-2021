# 编译并提权运行 AppCheck：用 MiCtl 编译产物里的 AppController 跑几个轮询周期，读出它算好的显示值
$root = 'D:\ZCode\.zcode\workspace\d5dbea00-ec3e-4c33-a2a1-69fb62e36ba5'
$p = Join-Path $root 'probe096'
New-Item -ItemType Directory -Force -Path $p | Out-Null
# 与正式产物同一套文件：新编译的 MiCtl.exe + out 里的运行时依赖
Copy-Item (Join-Path $root 'MiCtl\out\MiCtl.exe') $p -Force
Copy-Item (Join-Path $root 'MiCtl\out\*.dll') $p -Force

$outOpt = '/out:' + $p + '\AppCheck.exe'
$refOpt = '/r:' + $p + '\MiCtl.exe'
& 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe' /nologo $outOpt $refOpt ($root + '\probe\AppCheck.cs')
if ($LASTEXITCODE -ne 0) { Write-Output 'BUILD FAILED'; exit 1 }
Write-Output 'BUILD OK'

$res = $root + '\probe\appcheck.txt'
Remove-Item $res -ErrorAction SilentlyContinue
Start-Process -Verb RunAs -Wait -WindowStyle Hidden -FilePath ($p + '\AppCheck.exe') -ArgumentList $res

if (Test-Path $res) { Get-Content $res -Encoding UTF8 } else { Write-Output '检查程序未产出结果文件' }
