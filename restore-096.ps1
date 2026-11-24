# 恢复 LHM 0.9.6 到 libs 并重新编译
$root = 'D:\ZCode\.zcode\workspace\d5dbea00-ec3e-4c33-a2a1-69fb62e36ba5'
$src = $root + '\lhm-extract'

$core = @(
    'LibreHardwareMonitorLib.dll', 'HidSharp.dll', 'System.Text.Json.dll',
    'System.Memory.dll', 'System.Buffers.dll', 'System.Numerics.Vectors.dll',
    'System.Runtime.CompilerServices.Unsafe.dll', 'Microsoft.Bcl.AsyncInterfaces.dll',
    'System.Threading.Tasks.Extensions.dll', 'System.Collections.Immutable.dll',
    'System.Reflection.Metadata.dll', 'System.IO.Pipelines.dll'
)
foreach ($f in $core) {
    Copy-Item ($src + '\' + $f) ($root + '\MiCtl\libs\') -Force
}
Write-Output 'libs 已恢复为 LHM 0.9.6'

# 重新编译
& pwsh -NoProfile -ExecutionPolicy Bypass -File ($root + '\MiCtl\build.ps1')
