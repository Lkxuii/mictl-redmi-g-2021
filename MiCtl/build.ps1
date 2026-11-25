# MiCtl 编译脚本 —— csc.exe (.NET Framework 4.8)
# 用法: pwsh -File build.ps1
# 依赖: MiCtl/libs/ 下的 LibreHardwareMonitorLib 0.9.6 及其运行时 DLL；缺失时自动从 GitHub Releases 下载
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$src = Join-Path $root 'src'
$libs = Join-Path $root 'libs'
$out = Join-Path $root 'out'
New-Item -ItemType Directory -Force -Path $out | Out-Null

# ---- 依赖就位 ----
$lhmUrl = 'https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/releases/download/v0.9.6/LibreHardwareMonitor.zip'
$lhmFiles = @(
    'LibreHardwareMonitorLib.dll', 'HidSharp.dll', 'System.Text.Json.dll',
    'System.Memory.dll', 'System.Buffers.dll', 'System.Numerics.Vectors.dll',
    'System.Runtime.CompilerServices.Unsafe.dll', 'Microsoft.Bcl.AsyncInterfaces.dll',
    'System.Threading.Tasks.Extensions.dll', 'System.Collections.Immutable.dll',
    'System.Reflection.Metadata.dll', 'System.IO.Pipelines.dll'
)

if (-not (Test-Path (Join-Path $libs 'LibreHardwareMonitorLib.dll'))) {
    Write-Output 'libs 缺失，正在下载 LibreHardwareMonitor 0.9.6 ...'
    New-Item -ItemType Directory -Force -Path $libs | Out-Null
    $tmp = Join-Path $env:TEMP ('lhm096_' + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Force -Path $tmp | Out-Null
    $zipPath = Join-Path $tmp 'lhm.zip'
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    Invoke-WebRequest $lhmUrl -OutFile $zipPath
    Expand-Archive $zipPath -DestinationPath $tmp -Force
    foreach ($f in $lhmFiles) {
        Copy-Item (Join-Path $tmp $f) $libs -Force
    }
    Remove-Item $tmp -Recurse -Force
    Write-Output '依赖下载完成'
}

$csc = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { throw "csc.exe 不存在" }

# ---- Win32 资源（图标 + 版本信息 + manifest），全部编译期嵌入 ----
# 为什么不用"编译后再改 exe"（UpdateResource）：本机的火绒会拦截"进程改写可执行文件"
# （拒绝访问），新出现的 exe 还会被实时监控按 Backdoor/Steam 自动隔离。
# .res 由 python 生成（tools/make_res.py）；python 不可用时退回纯 manifest。
$resArg = $null
try {
    $py = (Get-Command python -ErrorAction Stop).Source
    $res = Join-Path $root 'tools\app.res'
    & $py (Join-Path $root 'tools\make_res.py') $res '1.1.0.0' (Join-Path $root 'assets\app.ico') (Join-Path $src 'app.manifest') | Write-Output
    if ((Test-Path $res) -and $LASTEXITCODE -eq 0) { $resArg = '/win32res:' + $res }
} catch { }
if (-not $resArg) { Write-Output '警告：python 不可用，跳过图标与版本信息（只有 manifest）' }

$srcFiles = @(Get-ChildItem $src -Filter *.cs | ForEach-Object { $_.FullName })

$args = New-Object System.Collections.Generic.List[string]
$args.Add('/nologo')
$args.Add('/target:winexe')
$args.Add('/platform:anycpu')
$args.Add('/optimize+')
$args.Add('/out:' + (Join-Path $out 'MiCtl.exe'))
if ($resArg) { $args.Add($resArg) } else { $args.Add('/win32manifest:' + (Join-Path $src 'app.manifest')) }
$args.Add('/r:' + (Join-Path $libs 'LibreHardwareMonitorLib.dll'))
$args.Add('/r:System.Management.dll')
$args.Add('/r:System.Windows.Forms.dll')
$args.Add('/r:System.Drawing.dll')
$args.Add('/r:System.Core.dll')
foreach ($s in $srcFiles) { $args.Add($s) }

& $csc $args.ToArray()

if ($LASTEXITCODE -eq 0) {
    # 产物自检：安全软件可能把刚编译出的未签名 exe 实时隔离（csc 返回成功但文件不在）
    if (-not (Test-Path (Join-Path $out 'MiCtl.exe'))) {
        Write-Output 'BUILD OK 但 out\MiCtl.exe 不在：刚生成的产物极可能被安全软件实时监控隔离了（见 README「安全软件误报说明」）'
    }
    # 运行时依赖随产物输出
    foreach ($f in $lhmFiles) {
        if ($f -ne 'LibreHardwareMonitorLib.dll') {
            Copy-Item (Join-Path $libs $f) $out -Force -ErrorAction SilentlyContinue
        }
    }
    Copy-Item (Join-Path $libs 'LibreHardwareMonitorLib.dll') $out -Force
    Write-Output ("BUILD OK -> " + (Join-Path $out 'MiCtl.exe'))
} else {
    Write-Output "BUILD FAILED ($LASTEXITCODE)"
}
