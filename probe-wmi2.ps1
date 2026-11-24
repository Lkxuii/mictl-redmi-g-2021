# 用 System.Management 老式 WMI API 调用 MiInterface
$ErrorActionPreference = 'Continue'
$mc = New-Object System.Management.ManagementClass '\\.\root\WMI:MICommonInterface'
$mc.Scope.Options.EnablePrivileges = $true
$inParams = $mc.GetMethodParameters('MiInterface')
Write-Output ('输入参数字段: ' + (($inParams.Properties | ForEach-Object { $_.Name + ':' + $_.Type }) -join ', '))

function Call-Mi {
    param([byte]$op, [byte]$fn, [byte[]]$payload)
    $in = $mc.GetMethodParameters('MiInterface')
    $buf = New-Object byte[] 32
    $buf[1] = $op
    $buf[3] = $fn
    if ($payload) {
        for ($i = 0; $i -lt $payload.Length -and $i -lt 28; $i++) { $buf[4 + $i] = $payload[$i] }
    }
    $in['InData'] = $buf
    if ($in.Properties['Reserved']) { $in['Reserved'] = [UInt16]0 }
    $out = $mc.InvokeMethod('MiInterface', $in, $null)
    return $out
}

function HexView { param($b, [int]$n)
    if ($null -eq $b) { return '(null)' }
    ($b[0..([Math]::Min($n, $b.Length) - 1)] | ForEach-Object { $_.ToString('X2') }) -join ' '
}

Write-Output '=== GET fn13 FAN_SPEEDS ==='
try {
    $r = Call-Mi 250 13 $null
    Write-Output ('ReturnValue=' + $r['ReturnValue'])
    Write-Output ('out: ' + (HexView $r['OutData'] 16))
    $o = $r['OutData']
    if ($o) {
        Write-Output ('CPU fan: ' + ($o[0] + 256 * $o[1]) + ' RPM')
        Write-Output ('GPU fan: ' + ($o[2] + 256 * $o[3]) + ' RPM')
    }
} catch { Write-Output ('ERROR: ' + $_.Exception.Message) }

Write-Output '=== GET fn22 CPU_THERMOMETER ==='
try {
    $r = Call-Mi 250 22 $null
    $o = $r['OutData']
    Write-Output ('rc=' + $r['ReturnValue'] + '  temp=' + $o[0] + ' C')
} catch { Write-Output ('ERROR: ' + $_.Exception.Message) }

Write-Output '=== GET fn8 SYSTEM_PER_MODE ==='
try {
    $r = Call-Mi 250 8 $null
    $o = $r['OutData']
    Write-Output ('rc=' + $r['ReturnValue'] + '  profile=' + $o[0] + ' (0=Bal,1=Perf,2=Quiet,3=Full)')
} catch { Write-Output ('ERROR: ' + $_.Exception.Message) }

Write-Output '=== GET fn19 SYSTEM_AC_TYPE ==='
try {
    $r = Call-Mi 250 19 $null
    $o = $r['OutData']
    Write-Output ('rc=' + $r['ReturnValue'] + '  ac=' + $o[0])
} catch { Write-Output ('ERROR: ' + $_.Exception.Message) }

Write-Output '=== GET fn23 CPU_POWER ==='
try {
    $r = Call-Mi 250 23 $null
    Write-Output ('rc=' + $r['ReturnValue'] + '  raw: ' + (HexView $r['OutData'] 12))
} catch { Write-Output ('ERROR: ' + $_.Exception.Message) }

Write-Output '=== GET fn21 MAX_FAN_SPEED ==='
try {
    $r = Call-Mi 250 21 $null
    Write-Output ('rc=' + $r['ReturnValue'] + '  raw: ' + (HexView $r['OutData'] 12))
} catch { Write-Output ('ERROR: ' + $_.Exception.Message) }

Write-Output '=== GET fn20 MAX_FAN_SWITCH ==='
try {
    $r = Call-Mi 250 20 $null
    Write-Output ('rc=' + $r['ReturnValue'] + '  raw: ' + (HexView $r['OutData'] 12))
} catch { Write-Output ('ERROR: ' + $_.Exception.Message) }
