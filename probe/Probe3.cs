// 路径形式对 InvokeMethod 的影响测试
using System;
using System.IO;
using System.Management;
using System.Text;

class Probe3
{
    static StringBuilder sb = new StringBuilder();

    static void TryPath(ManagementScope scope, string label, string relPath, byte fn)
    {
        try
        {
            ManagementObject obj = new ManagementObject(scope, new ManagementPath(relPath), null);
            byte[] buf = new byte[32];
            buf[1] = 250;
            buf[3] = fn;
            ManagementBaseObject inParams = obj.GetMethodParameters("MiInterface");
            inParams["InData"] = buf;
            ManagementBaseObject outParams = obj.InvokeMethod("MiInterface", inParams, null);
            int rc = outParams["ReturnValue"] == null ? -999 : Convert.ToInt32(outParams["ReturnValue"]);
            byte[] o = outParams["OutData"] as byte[];
            string hex = o == null ? "(null)" : BitConverter.ToString(o, 0, Math.Min(o.Length, 32)).Replace('-', ' ');
            sb.AppendLine(label + ": OK rc=" + rc + " out=" + hex);
        }
        catch (ManagementException me)
        {
            sb.AppendLine(label + ": MgmtException " + me.ErrorCode + " hr=0x" + me.HResult.ToString("X8") + " " + me.Message);
        }
        catch (Exception ex)
        {
            sb.AppendLine(label + ": " + ex.GetType().Name + " " + ex.Message);
        }
    }

    static int Main(string[] args)
    {
        string outFile = args.Length > 0 ? args[0] : "probe3-result.txt";
        sb.AppendLine("IsAdmin=" + new System.Security.Principal.WindowsPrincipal(System.Security.Principal.WindowsIdentity.GetCurrent()).IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator));

        ManagementScope scope = new ManagementScope(@"root\WMI");
        scope.Connect();

        // 各种路径形式（C# 字符串里 \\ = 一个反斜杠）
        TryPath(scope, "[p1 单引号+单反斜杠]", @"MICommonInterface.InstanceName='ACPI\PNP0C14\MIFS_0'", 13);
        TryPath(scope, "[p2 单引号+双反斜杠转义]", @"MICommonInterface.InstanceName='ACPI\\PNP0C14\\MIFS_0'", 13);
        TryPath(scope, "[p3 双引号+双反斜杠]", "MICommonInterface.InstanceName=\"ACPI\\\\PNP0C14\\\\MIFS_0\"", 13);
        TryPath(scope, "[p4 类相对路径]", "MICommonInterface", 13);

        File.WriteAllText(outFile, sb.ToString());
        return 0;
    }
}
