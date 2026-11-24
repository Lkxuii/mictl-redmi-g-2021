# 复制 LHM 核心依赖到 MiCtl\libs
$root = 'D:\ZCode\.zcode\workspace\d5dbea00-ec3e-4c33-a2a1-69fb62e36ba5'
$core = @(
    'LibreHardwareMonitorLib.dll', 'HidSharp.dll', 'System.Text.Json.dll',
    'System.Memory.dll', 'System.Buffers.dll', 'System.Numerics.Vectors.dll',
    'System.Runtime.CompilerServices.Unsafe.dll', 'Microsoft.Bcl.AsyncInterfaces.dll',
    'System.Threading.Tasks.Extensions.dll', 'System.Collections.Immutable.dll',
    'System.Reflection.Metadata.dll', 'System.IO.Pipelines.dll'
)
foreach ($f in $core) {
    $src = Join-Path $root ('lhm-extract\' + $f)
    if (Test-Path $src) {
        Copy-Item $src (Join-Path $root 'MiCtl\libs') -Force
        Write-Output ('OK  ' + $f)
    } else {
        Write-Output ('MISS ' + $f)
    }
}
