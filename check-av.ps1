# 检查已安装杀软与 Defender 状态
Write-Output '=== SecurityCenter2 注册的杀软 ==='
try {
    Get-CimInstance -Namespace root/SecurityCenter2 -ClassName AntiVirusProduct | ForEach-Object {
        Write-Output ('  ' + $_.displayName + '  (路径: ' + $_.productState + ')')
    }
} catch { Write-Output ('  读取失败: ' + $_.Exception.Message) }

Write-Output '=== Defender 状态 ==='
try {
    $mp = Get-MpComputerStatus -ErrorAction Stop
    Write-Output ('  实时保护: ' + $mp.RealTimeProtectionEnabled)
    Write-Output ('  防病毒已启用: ' + $mp.AntivirusEnabled)
} catch { Write-Output ('  ' + $_.Exception.Message) }

Write-Output '=== 近期威胁检测（可能记录了拦截） ==='
try {
    Get-MpThreatDetection -ErrorAction Stop | Select-Object -First 5 | ForEach-Object {
        Write-Output ('  ' + $_.InitialDetectionTime + ' | ' + $_.ProcessName + ' | ' + ($_.Resources -join ','))
    }
} catch { Write-Output '  无记录或读取失败' }
