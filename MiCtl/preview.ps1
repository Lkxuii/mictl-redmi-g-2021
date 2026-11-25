# 界面离屏预览 —— 编译 probe/UiPreview.cs + MiCtl/src（不含 Program.cs）为 MiCtl/out/UiPreview.exe，并渲染 PNG
# 用法: pwsh -File preview.ps1 [-Scale 1.5] [-Mode sample|empty] [-Page 0|1|2|all] [-Out 路径.png]
#   Page: 0 = 控制，1 = 系统，2 = 网络，all = 三页各出一张
# MiCtl 本体是提权进程，截图读不到；这个工具不连硬件，只把主窗口按指定缩放画出来，供开发时目视。
param(
    [double]$Scale = 1.5,
    [ValidateSet('sample', 'empty')][string]$Mode = 'sample',
    [string]$Page = 'all',
    [string]$Out = ''
)
$ErrorActionPreference = 'Stop'
$inv = [System.Globalization.CultureInfo]::InvariantCulture
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$outDir = Join-Path $root 'out'
$libs = Join-Path $root 'libs'
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

if (-not (Test-Path (Join-Path $libs 'LibreHardwareMonitorLib.dll'))) {
    throw "libs 缺失，请先运行 pwsh -File build.ps1"
}

$exe = Join-Path $outDir 'UiPreview.exe'
$csc = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$srcFiles = @(Get-ChildItem (Join-Path $root 'src') -Filter *.cs |
    Where-Object { $_.Name -ne 'Program.cs' } | ForEach-Object { $_.FullName })

# 预览工具的资源（图标 + 版本信息 + 预览自己的 dpiAware manifest），编译期嵌入
$resArg = $null
try {
    $py = (Get-Command python -ErrorAction Stop).Source
    $res = Join-Path $root 'tools\uipreview.res'
    & $py (Join-Path $root 'tools\make_res.py') $res '1.1.0.0' (Join-Path $root 'assets\app.ico') (Join-Path (Split-Path -Parent $root) 'probe\uipreview.manifest') | Write-Output
    if ((Test-Path $res) -and $LASTEXITCODE -eq 0) { $resArg = '/win32res:' + $res }
} catch { }

$a = New-Object System.Collections.Generic.List[string]
$a.Add('/nologo'); $a.Add('/target:exe'); $a.Add('/platform:anycpu'); $a.Add('/optimize+')
$a.Add('/main:MiCtl.UiPreview')
if ($resArg) { $a.Add($resArg) } else { $a.Add('/win32manifest:' + (Join-Path (Split-Path -Parent $root) 'probe\uipreview.manifest')) }
$a.Add('/out:' + $exe)
$a.Add('/r:' + (Join-Path $libs 'LibreHardwareMonitorLib.dll'))
$a.Add('/r:System.Management.dll'); $a.Add('/r:System.Windows.Forms.dll'); $a.Add('/r:System.Drawing.dll'); $a.Add('/r:System.Core.dll')
foreach ($s in $srcFiles) { $a.Add($s) }
$a.Add((Join-Path (Split-Path -Parent $root) 'probe\UiPreview.cs'))

& $csc $a.ToArray()
if ($LASTEXITCODE -ne 0) { throw "UiPreview 编译失败 ($LASTEXITCODE)" }

$tag = $Scale.ToString('0.##', $inv) -replace '\.', '_'
$pageNames = @{ '0' = 'control'; '1' = 'system'; '2' = 'network' }
$pages = if ($Page -eq 'all') { @('0', '1', '2') } else { @($Page) }
foreach ($p in $pages) {
    if ($Out -and $pages.Count -eq 1) {
        $target = $Out
    } else {
        $target = Join-Path $outDir ("ui-preview-" + $Mode + "-" + $pageNames[$p] + "-" + $tag + "x.png")
    }
    & $exe ($Scale.ToString($inv)) $target $Mode $p
}
