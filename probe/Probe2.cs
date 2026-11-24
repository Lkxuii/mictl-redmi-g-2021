// MIFS WMI 多变体调用测试 - 打印详细 HRESULT
using System;
using System.IO;
using System.Management;
using System.Text;

class Probe2
{
    static string lastHex;

    static byte[] BuildBuf(byte op, byte fn)
    {
        byte[] buf = new byte[32];
        buf[1] = op;
        buf[3] = fn;
        return buf;
    }

    static void TryCall(ManagementClass mc, string label, byte[] inData, bool withOut, ManagementObject inst)
    {
        try
        {
            ManagementBaseObject target = inst ?? (ManagementBaseObject)mc;
            ManagementBaseObject inParams = target.ClassPath != null && inst == null
                ? mc.GetMethodParameters("MiInterface")
                : inst.GetMethodParameters("MiInterface");
            inParams["InData"] = inData;
            if (withOut)
            {
                inParams["OutData"] = new byte[30];
                inParams["Reserved"] = (UInt16)0;
            }
            ManagementBaseObject outParams;
            if (inst == null)
                outParams = mc.InvokeMethod("MiInterface", inParams, null);
            else
                outParams = inst.InvokeMethod("MiInterface", inParams, null);
            int rc = outParams["ReturnValue"] == null ? -999 : Convert.ToInt32(outParams["ReturnValue"]);
            byte[] o = outParams["OutData"] as byte[];
            object resv = outParams["Reserved"];
            string hex = o == null ? "(null)" : BitConverter.ToString(o, 0, Math.Min(o.Length, 32)).Replace('-', ' ');
            lastHex = hex;
            Console.WriteLine(label + ": OK rc=" + rc + " Reserved=" + (resv ?? "(null)") + " out=" + hex);
        }
        catch (ManagementException me)
        {
            Console.WriteLine(label + ": MgmtException code=" + me.ErrorCode + " hr=0x" + me.HResult.ToString("X8") + " msg=" + me.Message);
        }
        catch (Exception ex)
        {
            Console.WriteLine(label + ": " + ex.GetType().Name + " " + ex.Message);
        }
    }

    static int Main(string[] args)
    {
        string outFile = args.Length > 0 ? args[0] : "probe2-result.txt";
        StringBuilder sb = new StringBuilder();
        TextWriter old = Console.Out;
        Console.SetOut(new StringWriter(sb));

        Console.WriteLine("IsAdmin=" + new System.Security.Principal.WindowsPrincipal(System.Security.Principal.WindowsIdentity.GetCurrent()).IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator));

        ManagementScope scope = new ManagementScope(@"root\WMI");
        scope.Connect();
        ManagementClass mc = new ManagementClass(scope, new ManagementPath("MICommonInterface"), null);

        byte[] buf32 = BuildBuf(250, 13);

        Console.WriteLine("--- 类调用变体 ---");
        TryCall(mc, "[A class in=32B]", buf32, false, null);
        TryCall(mc, "[B class in=32B+out30+res]", buf32, true, null);
        byte[] buf31 = new byte[31]; buf31[1] = 250; buf31[3] = 13;
        TryCall(mc, "[E class in=31B]", buf31, false, null);
        byte[] buf28 = new byte[28]; buf28[1] = 250; buf28[3] = 13;
        TryCall(mc, "[F class in=28B]", buf28, false, null);

        Console.WriteLine("--- 实例调用变体 ---");
        ManagementObject target = null;
        foreach (ManagementObject inst in mc.GetInstances()) { target = inst; break; }
        if (target != null)
        {
            Console.WriteLine("instance: " + target.Path.Path);
            TryCall(mc, "[C inst in=32B]", buf32, false, target);
            TryCall(mc, "[D inst in=32B+out30+res]", buf32, true, target);
        }

        // 语义测试：读各功能码
        Console.WriteLine("--- 语义读取（若 A 变体成功） ---");
        byte[][] fns = { new byte[]{13}, new byte[]{22}, new byte[]{8}, new byte[]{19}, new byte[]{23}, new byte[]{21}, new byte[]{20}, new byte[]{9} };
        string[] names = { "FAN_SPEEDS", "CPU_THERM", "PER_MODE", "AC_TYPE", "CPU_POWER", "MAX_FAN_SPEED", "MAX_FAN_SWITCH", "GPU_MODE" };
        for (int i = 0; i < fns.Length; i++)
        {
            byte[] b = BuildBuf(250, fns[i][0]);
            TryCall(mc, "[GET fn" + fns[i][0] + " " + names[i] + "]", b, false, null);
        }

        Console.SetOut(old);
        File.WriteAllText(outFile, sb.ToString());
        return 0;
    }
}
