# 温度通道探测：ACPI 热区 + nvidia-smi
$out = New-Object System.Collections.Generic.List[string]

$out.Add('=== MSAcpi_ThermalZoneTemperature (root\wmi) ===')
try {
    Get-CimInstance -Namespace root\wmi -ClassName MSAcpi_ThermalZoneTemperature -ErrorAction Stop | ForEach-Object {
        $c = ($_.CurrentTemperature - 2732) / 10.0
        $out.Add(('  ' + $_.InstanceName + ' : ' + $c + ' C  (raw ' + $_.CurrentTemperature + ')'))
    }
} catch { $out.Add('  ERROR: ' + $_.Exception.Message) }

$out.Add('=== AMD 温度相关 WMI ===')
try {
    Get-CimInstance -Namespace root\cimv2 -ClassName Win32_TemperatureProbe -ErrorAction Stop | ForEach-Object {
        $out.Add(('  ' + $_.Name + ' : ' + $_.CurrentReading))
    }
} catch { $out.Add('  ERROR: ' + $_.Exception.Message) }

$out.Add('=== nvidia-smi ===')
$nsmi = "$env:SystemRoot\System32\nvidia-smi.exe"
if (-not (Test-Path $nsmi)) { $nsmi = 'nvidia-smi' }
try {
    $r = & $nsmi '--query-gpu=temperature.gpu,temperature.memory,utilization.gpu,power.draw,fan.speed' '--format=csv,noheader' 2>&1
    $out.Add('  ' + ($r -join ' | '))
} catch { $out.Add('  ERROR: ' + $_.Exception.Message) }

$out | Out-File -FilePath 'D:\ZCode\.zcode\workspace\d5dbea00-ec3e-4c33-a2a1-69fb62e36ba5\temp-result.txt' -Encoding utf8
