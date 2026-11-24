Add-Type -ReferencedAssemblies @('System.Management', 'System.ComponentModel.Primitives') -TypeDefinition @'
using System;
using System.Management;

public static class Mifs
{
    static ManagementClass mc;
    static Mifs()
    {
        ManagementScope scope = new ManagementScope(@"root\WMI");
        scope.Connect();
        ManagementPath path = new ManagementPath("MICommonInterface");
        mc = new ManagementClass(scope, path, null);
    }

    // 只传 in 参数：InData(32B)。Reserved/OutData 都是 out，不能赋值。
    public static byte[] Call(byte op, byte fn, byte[] payload, out int ret)
    {
        ManagementBaseObject inParams = mc.GetMethodParameters("MiInterface");
        byte[] buf = new byte[32];
        buf[1] = op;
        buf[3] = fn;
        if (payload != null)
            Array.Copy(payload, 0, buf, 4, Math.Min(payload.Length, 28));
        inParams.SetPropertyValue("InData", buf);
        ManagementBaseObject outParams = mc.InvokeMethod("MiInterface", inParams, null);
        ret = Convert.ToInt32(outParams["ReturnValue"]);
        object o = outParams["OutData"];
        if (o is byte[]) return (byte[])o;
        return null;
    }
}
'@

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
    @{ fn = 20; name = 'MAX_FAN_SWITCH' }
)

foreach ($t in $tests) {
    $ret = 0
    try {
        $o = [Mifs]::Call(250, $t.fn, $null, [ref]$ret)
        Write-Output ("fn{0,-2} {1,-16} rc={2}  raw: {3}" -f $t.fn, $t.name, $ret, (HexView $o 16))
    } catch {
        Write-Output ("fn{0,-2} {1,-16} EXCEPTION: {2}" -f $t.fn, $t.name, $_.Exception.Message)
    }
}

$ret = 0
$o = [Mifs]::Call(250, 13, $null, [ref]$ret)
if ($o) {
    Write-Output ("=> CPU fan: " + ($o[0] + 256 * $o[1]) + " RPM   GPU fan: " + ($o[2] + 256 * $o[3]) + " RPM")
}
