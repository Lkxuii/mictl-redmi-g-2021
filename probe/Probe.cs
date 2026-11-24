// MIFS WMI 探测器 - .NET Framework 4.x
// 用法: Probe.exe <output_file>
using System;
using System.IO;
using System.Management;
using System.Security.Principal;
using System.Text;

class Probe
{
    static void Call(ManagementClass mc, byte op, byte fn, byte[] payload, string name, StringBuilder sb)
    {
        byte[] buf = new byte[32];
        buf[1] = op;
        buf[3] = fn;
        if (payload != null)
            Array.Copy(payload, 0, buf, 4, Math.Min(payload.Length, 28));
        try
        {
            // 类级调用
            ManagementBaseObject inParams = mc.GetMethodParameters("MiInterface");
            inParams["InData"] = buf;
            ManagementBaseObject outParams = mc.InvokeMethod("MiInterface", inParams, null);
            if (outParams == null)
            {
                sb.AppendLine(name + ": outParams null");
                return;
            }
            int rc = Convert.ToInt32(outParams["ReturnValue"]);
            byte[] o = outParams["OutData"] as byte[];
            object resv = outParams["Reserved"];
            string hex = o == null ? "(null)" : BitConverter.ToString(o, 0, Math.Min(o.Length, 32)).Replace('-', ' ');
            sb.AppendLine(name + ": rc=" + rc + " Reserved=" + (resv ?? "(null)") + " out=" + hex);
        }
        catch (Exception ex)
        {
            sb.AppendLine(name + ": EXCEPTION " + ex.GetType().Name + " " + ex.Message +
                (ex.InnerException != null ? " | inner: " + ex.InnerException.Message : ""));
        }
    }

    static void Main(string[] args)
    {
        string outFile = args.Length > 0 ? args[0] : "probe-result.txt";
        StringBuilder sb = new StringBuilder();

        sb.AppendLine("IsAdmin=" + new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator));

        ManagementScope scope = new ManagementScope(@"root\WMI");
        scope.Connect();

        ManagementPath path = new ManagementPath("MICommonInterface");
        ManagementClass mc = new ManagementClass(scope, path, null);

        // 枚举实例
        try
        {
            ManagementObjectCollection insts = mc.GetInstances();
            sb.AppendLine("InstanceCount=" + insts.Count);
            foreach (ManagementObject inst in insts)
            {
                sb.AppendLine("  instance: " + inst.Path.Path);
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine("GetInstances EXCEPTION: " + ex.Message);
        }

        // 测试一：类级调用
        Call(mc, 250, 13, null, "[class-call fn13 FAN_SPEEDS]", sb);
        Call(mc, 250, 22, null, "[class-call fn22 CPU_THERM]", sb);
        Call(mc, 250, 8, null, "[class-call fn8 PER_MODE]", sb);
        Call(mc, 250, 19, null, "[class-call fn19 AC_TYPE]", sb);
        Call(mc, 250, 23, null, "[class-call fn23 CPU_POWER]", sb);
        Call(mc, 250, 21, null, "[class-call fn21 MAX_FAN_SPEED]", sb);
        Call(mc, 250, 20, null, "[class-call fn20 MAX_FAN_SWITCH]", sb);
        Call(mc, 250, 9, null, "[class-call fn9 GPU_MODE]", sb);
        Call(mc, 250, 11, null, "[class-call fn11 FN_LOCK]", sb);
        Call(mc, 250, 12, null, "[class-call fn12 TP_LOCK]", sb);
        Call(mc, 250, 10, null, "[class-call fn10 KBD_TYPE]", sb);

        // 测试二：实例级调用
        try
        {
            ManagementObjectCollection insts = mc.GetInstances();
            ManagementObject target = null;
            foreach (ManagementObject inst in insts) { target = inst; break; }
            if (target != null)
            {
                byte[] buf = new byte[32];
                buf[1] = 250;
                buf[3] = 13;
                ManagementBaseObject inParams = target.GetMethodParameters("MiInterface");
                inParams["InData"] = buf;
                ManagementBaseObject outParams = target.InvokeMethod("MiInterface", inParams, null);
                int rc = Convert.ToInt32(outParams["ReturnValue"]);
                byte[] o = outParams["OutData"] as byte[];
                string hex = o == null ? "(null)" : BitConverter.ToString(o, 0, Math.Min(o.Length, 32)).Replace('-', ' ');
                sb.AppendLine("[inst-call fn13]: rc=" + rc + " out=" + hex);
            }
            else sb.AppendLine("[inst-call]: no instance");
        }
        catch (Exception ex)
        {
            sb.AppendLine("[inst-call] EXCEPTION: " + ex.Message);
        }

        File.WriteAllText(outFile, sb.ToString(), Encoding.UTF8);
    }
}
