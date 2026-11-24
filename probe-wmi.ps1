# 只读探测 MICommonInterface (Bitland MIFS WMI)
function Call-Mi {
    param([byte]$op, [byte]$fn, [byte[]]$payload)
    $in = New-Object byte[] 32
    $in[1] = $op
    $in[3] = $fn
    if ($payload) {
        for ($i = 0; $i -lt $payload.Length -and $i -lt 28; $i++) { $in[4 + $i] = $payload[$i] }
    }
    $r = Invoke-CimMethod -Namespace root\WMI -ClassName MICommonInterface -MethodName MiInterface -Arguments @{ InData = [byte[]]$in; Reserved = [UInt16]0 }
    return $r
}

function HexView { param([byte[]]$b, [int]$n)
    if ($null -eq $b) { return "(null)" }
    ($b[0..([Math]::Min($n, $b.Length) - 1)] | ForEach-Object { $_.ToString('X2') }) -join ' '
}

Write-Output '=== GET fn13 FAN_SPEEDS ==='
try {
    $r = Call-Mi 250 13 $null
    Write-Output ("rc=" + $r.ReturnValue)
    Write-Output ("out: " + (HexView $r.OutData 16))
    if ($r.OutData) {
        Write-Output ("CPU fan: " + ($r.OutData[0] + 256 * $r.OutData[1]) + " RPM")
        Write-Output ("GPU fan: " + ($r.OutData[2] + 256 * $r.OutData[3]) + " RPM")
        Write-Output ("SYS    : " + ($r.OutData[6] + 256 * $r.OutData[7]) + " RPM")
    }
} catch { Write-Output ("ERROR: " + $_.Exception.Message) }

Write-Output '=== GET fn22 CPU_THERMOMETER ==='
try {
    $r = Call-Mi 250 22 $null
    Write-Output ("rc=" + $r.ReturnValue + "  temp=" + $r.OutData[0] + " C")
} catch { Write-Output ("ERROR: " + $_.Exception.Message) }

Write-Output '=== GET fn8 SYSTEM_PER_MODE ==='
try {
    $r = Call-Mi 250 8 $null
    Write-Output ("rc=" + $r.ReturnValue + "  profile=" + $r.OutData[0] + " (0=Bal,1=Perf,2=Quiet,3=Full)")
} catch { Write-Output ("ERROR: " + $_.Exception.Message) }

Write-Output '=== GET fn19 SYSTEM_AC_TYPE ==='
try {
    $r = Call-Mi 250 19 $null
    Write-Output ("rc=" + $r.ReturnValue + "  ac=" + $r.OutData[0])
} catch { Write-Output ("ERROR: " + $_.Exception.Message) }

Write-Output '=== GET fn23 CPU_POWER ==='
try {
    $r = Call-Mi 250 23 $null
    Write-Output ("rc=" + $r.ReturnValue + "  raw: " + (HexView $r.OutData 12))
} catch { Write-Output ("ERROR: " + $_.Exception.Message) }

Write-Output '=== GET fn21 MAX_FAN_SPEED ==='
try {
    $r = Call-Mi 250 21 $null
    Write-Output ("rc=" + $r.ReturnValue + "  raw: " + (HexView $r.OutData 12))
} catch { Write-Output ("ERROR: " + $_.Exception.Message) }

Write-Output '=== GET fn20 MAX_FAN_SWITCH ==='
try {
    $r = Call-Mi 250 20 $null
    Write-Output ("rc=" + $r.ReturnValue + "  raw: " + (HexView $r.OutData 12))
} catch { Write-Output ("ERROR: " + $_.Exception.Message) }
