$out = New-Object System.Collections.Generic.List[string]
$out.Add('IsAdmin=' + (New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator))

function Call-Mi {
    param([byte]$op, [byte]$fn, [byte[]]$payload)
    $in = New-Object byte[] 32
    $in[1] = $op
    $in[3] = $fn
    if ($payload) {
        for ($i = 0; $i -lt $payload.Length -and $i -lt 28; $i++) { $in[4 + $i] = $payload[$i] }
    }
    # 只传 in 参数 InData；OutData/Reserved 都是 out
    $r = Invoke-CimMethod -Namespace root\WMI -ClassName MICommonInterface -MethodName MiInterface -Arguments @{ InData = [byte[]]$in }
    return $r
}

function HexView { param($b, [int]$n)
    if ($null -eq $b) { return '(null)' }
    ($b[0..([Math]::Min($n, $b.Length) - 1)] | ForEach-Object { $_.ToString('X2') }) -join ' '
}

$tests = @(
    @{ fn = 13; name = 'FAN_SPEEDS' },
    @{ fn = 22; name = 'CPU_THERMOMETER' },
    @{ fn = 8;  name = 'SYSTEM_PER_MODE' },
    @{ fn = 19; name = 'SYSTEM_AC_TYPE' },
    @{ fn = 23; name = 'CPU_POWER' },
    @{ fn = 21; name = 'MAX_FAN_SPEED' },
    @{ fn = 20; name = 'MAX_FAN_SWITCH' },
    @{ fn = 9;  name = 'GPU_MODE' },
    @{ fn = 11; name = 'FN_LOCK' },
    @{ fn = 12; name = 'TP_LOCK' },
    @{ fn = 10; name = 'KBD_TYPE' }
)

foreach ($t in $tests) {
    try {
        $r = Call-Mi 250 $t.fn $null
        $out.Add(("fn{0,-2} {1,-16} rc={2}  raw: {3}" -f $t.fn, $t.name, $r.ReturnValue, (HexView $r.OutData 28)))
    } catch {
        $out.Add(("fn{0,-2} {1,-16} EXCEPTION: {2}" -f $t.fn, $t.name, $_.Exception.Message))
    }
}

try {
    $r = Call-Mi 250 13 $null
    $o = $r.OutData
    if ($o) { $out.Add(("=> CPU fan: " + ($o[0] + 256 * $o[1]) + " RPM   GPU fan: " + ($o[2] + 256 * $o[3]) + " RPM   SYS: " + ($o[6] + 256 * $o[7]) + " RPM")) }
} catch { $out.Add('fan parse fail') }

$out | Out-File -FilePath 'D:\ZCode\.zcode\workspace\d5dbea00-ec3e-4c33-a2a1-69fb62e36ba5\probe-elevated-result.txt' -Encoding utf8
