// 读取 LHM Ring0 内部失败报告
using System;
using System.IO;
using System.Reflection;
using System.Text;
using LibreHardwareMonitor.Hardware;

class LhmDiag2
{
    static int Main(string[] args)
    {
        string outFile = args.Length > 0 ? args[0] : "lhm-diag3.txt";
        StringBuilder sb = new StringBuilder();

        Computer c = new Computer
        {
            IsCpuEnabled = true,
            IsGpuEnabled = true,
            IsMemoryEnabled = false,
            IsMotherboardEnabled = false,
            IsStorageEnabled = false,
            IsNetworkEnabled = false,
            IsControllerEnabled = false,
            IsPsuEnabled = false,
            IsBatteryEnabled = false
        };
        c.Open();

        Assembly asm = typeof(Computer).Assembly;
        Type ring0 = asm.GetType("LibreHardwareMonitor.Hardware.Ring0");
        sb.AppendLine("Ring0 类型: " + (ring0 == null ? "(null)" : ring0.FullName));
        if (ring0 != null)
        {
            PropertyInfo isOpen = ring0.GetProperty("IsOpen");
            if (isOpen != null)
                sb.AppendLine("Ring0.IsOpen = " + isOpen.GetValue(null));

            MethodInfo getReport = ring0.GetMethod("GetReport", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (getReport != null)
            {
                object rep = getReport.Invoke(null, null);
                sb.AppendLine("--- Ring0 Report ---");
                sb.AppendLine(rep == null ? "(null)" : rep.ToString());
            }
            else
            {
                // 尝试私有字段 _report
                FieldInfo fr = ring0.GetField("_report", BindingFlags.NonPublic | BindingFlags.Static);
                if (fr != null)
                {
                    object rep = fr.GetValue(null);
                    sb.AppendLine("--- Ring0 _report (field) ---");
                    sb.AppendLine(rep == null ? "(null)" : rep.ToString());
                }
                else sb.AppendLine("GetReport/_report 都不可访问");
            }
        }

        c.Close();
        File.WriteAllText(outFile, sb.ToString());
        return 0;
    }
}
