Add-Type -ReferencedAssemblies System.Management -TypeDefinition @'
using System;
using System.Management;

public static class Mifs2
{
    static ManagementClass mc;
    static Mifs2()
    {
        ManagementScope scope = new ManagementScope(@"root\WMI");
        scope.Connect();
        ManagementPath path = new ManagementPath("MICommonInterface");
        mc = new ManagementClass(scope, path, null);
    }

    public static string DumpMethod()
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        ManagementBaseObject mp = mc.GetMethodParameters("MiInterface");
        foreach (PropertyData pd in mp.Properties)
        {
            sb.Append("Param: " + pd.Name + " Type=" + pd.Type + " IsArray=" + pd.IsArray + " IsLocal=" + pd.IsLocal + " Origin=" + pd.Origin + "\r\n");
            foreach (QualifierData q in pd.Qualifiers)
                sb.Append("    qual: [" + q.Name + "] = " + q.Value + " (" + q.CimType + ")\r\n");
        }
        foreach (QualifierData q in mc.Methods["MiInterface"].Qualifiers)
            sb.Append("MethodQual: [" + q.Name + "] = " + q.Value + "\r\n");
        return sb.ToString();
    }

    public static byte[] Call(byte op, byte fn, byte[] payload, out int ret)
    {
        ManagementBaseObject inParams = mc.GetMethodParameters("MiInterface");
        byte[] buf = new byte[32];
        buf[1] = op;
        buf[3] = fn;
        if (payload != null)
            Array.Copy(payload, 0, buf, 4, Math.Min(payload.Length, 28));
        inParams.SetPropertyValue("InData", buf);
        inParams.SetPropertyValue("Reserved", (UInt16)0);
        ManagementBaseObject outParams = mc.InvokeMethod("MiInterface", inParams, null);
        ret = Convert.ToInt32(outParams["ReturnValue"]);
        object o = outParams["OutData"];
        if (o is byte[]) return (byte[])o;
        return null;
    }

    // 显式三参数版本
    public static byte[] CallExplicit(byte op, byte fn, byte[] payload, bool setOut, out int ret)
    {
        ManagementBaseObject inParams = mc.GetMethodParameters("MiInterface");
        byte[] buf = new byte[32];
        buf[1] = op;
        buf[3] = fn;
        if (payload != null)
            Array.Copy(payload, 0, buf, 4, Math.Min(payload.Length, 28));
        inParams.SetPropertyValue("InData", buf);
        if (setOut)
        {
            byte[] ob = new byte[30];
            inParams.SetPropertyValue("OutData", ob);
        }
        inParams.SetPropertyValue("Reserved", (UInt16)0);
        ManagementBaseObject outParams = mc.InvokeMethod("MiInterface", inParams, null);
        ret = Convert.ToInt32(outParams["ReturnValue"]);
        object o = outParams["OutData"];
        if (o is byte[]) return (byte[])o;
        return null;
    }
}
'@

Write-Output '=== 方法元数据 ==='
[Mifs2]::DumpMethod()

Write-Output '=== 显式参数调用 fn13 ==='
$ret = 0
try {
    $o = [Mifs2]::CallExplicit(250, 13, $null, $false, [ref]$ret)
    Write-Output ("rc=" + $ret + " out: " + (($o | ForEach-Object { $_.ToString('X2') }) -join ' '))
} catch { Write-Output ('ERROR: ' + $_.Exception.Message) }

Write-Output '=== 带out参数调用 fn13 ==='
try {
    $o = [Mifs2]::CallExplicit(250, 13, $null, $true, [ref]$ret)
    Write-Output ("rc=" + $ret + " out: " + (($o | ForEach-Object { $_.ToString('X2') }) -join ' '))
} catch { Write-Output ('ERROR: ' + $_.Exception.Message) }
