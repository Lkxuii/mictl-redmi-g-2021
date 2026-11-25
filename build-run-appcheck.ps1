# 编译端到端检查 AppCheck：引用"新编译的 MiCtl.exe"，读出 AppController 算好的显示值
# 新版 MiCtl.exe 编译到 probe096\（临时目录），因为 out\MiCtl.exe 可能正被运行中的实例锁定
# 用法:
#   pwsh -File build-run-appcheck.ps1            只编译（普通权限）
#   pwsh -File build-run-appcheck.ps1 -Run       编译后提权运行（弹一次 UAC），结果写 probe\appcheck.txt
param([switch]$Run)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$p = Join-Path $root 'probe096'
New-Item -ItemType Directory -Force -Path $p | Out-Null

$csc = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$libs = Join-Path $root 'MiCtl\libs'

# 与 build.ps1 同一套资源（图标 + 版本信息 + manifest），编译期嵌入
$repo = $root
$miRoot = Join-Path $root 'MiCtl'
$resArg = $null
try {
    $py = (Get-Command python -ErrorAction Stop).Source
    $res = Join-Path $miRoot 'tools\app.res'
    & $py (Join-Path $miRoot 'tools\make_res.py') $res '1.1.0.0' (Join-Path $miRoot 'assets\app.ico') (Join-Path $miRoot 'src\app.manifest') | Write-Output
    if ((Test-Path $res) -and $LASTEXITCODE -eq 0) { $resArg = '/win32res:' + $res }
} catch { }

# 1) 新版 MiCtl.exe（与 build.ps1 相同的编译参数）
$srcFiles = @(Get-ChildItem (Join-Path $root 'MiCtl\src') -Filter *.cs | ForEach-Object { $_.FullName })
$a = New-Object System.Collections.Generic.List[string]
$a.Add('/nologo'); $a.Add('/target:winexe'); $a.Add('/platform:anycpu'); $a.Add('/optimize+')
$a.Add('/out:' + (Join-Path $p 'MiCtl.exe'))
if ($resArg) { $a.Add($resArg) } else { $a.Add('/win32manifest:' + (Join-Path $root 'MiCtl\src\app.manifest')) }
$a.Add('/r:' + (Join-Path $libs 'LibreHardwareMonitorLib.dll'))
$a.Add('/r:System.Management.dll'); $a.Add('/r:System.Windows.Forms.dll'); $a.Add('/r:System.Drawing.dll'); $a.Add('/r:System.Core.dll')
foreach ($s in $srcFiles) { $a.Add($s) }
& $csc $a.ToArray()
if ($LASTEXITCODE -ne 0) { throw "MiCtl.exe 编译失败 ($LASTEXITCODE)" }

# 2) 运行时依赖 + AppCheck
Copy-Item (Join-Path $root 'MiCtl\out\*.dll') $p -Force
$refOpt = '/r:' + (Join-Path $p 'MiCtl.exe')
& $csc /nologo /target:exe /platform:anycpu ('/out:' + (Join-Path $p 'AppCheck.exe')) $refOpt /r:System.Core.dll (Join-Path $root 'probe\AppCheck.cs')
if ($LASTEXITCODE -ne 0) { throw "AppCheck 编译失败 ($LASTEXITCODE)" }
Write-Output 'BUILD OK (probe096\MiCtl.exe + AppCheck.exe)'

if ($Run) {
    $res = Join-Path $root 'probe\appcheck.txt'
    Remove-Item $res -ErrorAction SilentlyContinue
    Start-Process -Verb RunAs -Wait -WindowStyle Hidden -FilePath (Join-Path $p 'AppCheck.exe') -ArgumentList ('"' + $res + '"')
    if (Test-Path $res) { Get-Content $res -Encoding UTF8 } else { Write-Output '检查程序未产出结果文件' }
}
