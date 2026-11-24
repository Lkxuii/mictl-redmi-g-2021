// LHM 传感器诊断：dump 全部硬件与传感器 + WinRing0 服务状态
using System;
using System.IO;
using System.ServiceProcess;
using System.Text;
using LibreHardwareMonitor.Hardware;

class LhmDiag
{
    static int Main(string[] args)
    {
        string outFile = args.Length > 0 ? args[0] : "lhm-diag.txt";
        StringBuilder sb = new StringBuilder();

        sb.AppendLine("=== WinRing0 服务状态 ===");
        foreach (ServiceController sc in ServiceController.GetServices())
        {
            if (sc.ServiceName.IndexOf("WinRing", StringComparison.OrdinalIgnoreCase) >= 0 ||
                sc.DisplayName.IndexOf("WinRing", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                sb.AppendLine("  service: " + sc.ServiceName + " | " + sc.DisplayName + " | " + sc.Status);
            }
        }
        // 驱动文件位置
        string lhmDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LibreHardwareMonitor");
        sb.AppendLine("LHM dir: " + lhmDir + " exists=" + Directory.Exists(lhmDir));
        if (Directory.Exists(lhmDir))
            foreach (string f in Directory.GetFiles(lhmDir))
                sb.AppendLine("  file: " + Path.GetFileName(f) + " " + new FileInfo(f).Length);

        sb.AppendLine("");
        sb.AppendLine("=== 传感器树 ===");
        try
        {
            Computer c = new Computer
            {
                IsCpuEnabled = true,
                IsGpuEnabled = true,
                IsMemoryEnabled = false,
                IsMotherboardEnabled = true,
                IsStorageEnabled = false,
                IsNetworkEnabled = false,
                IsControllerEnabled = false,
                IsPsuEnabled = false,
                IsBatteryEnabled = false
            };
            c.Open();

            DumpHardware(sb, c.Hardware, 0);
            c.Close();
        }
        catch (Exception ex)
        {
            sb.AppendLine("EXCEPTION: " + ex);
        }
        File.WriteAllText(outFile, sb.ToString());
        return 0;
    }

    static void DumpHardware(StringBuilder sb, System.Collections.Generic.IList<IHardware> hws, int depth)
    {
        string pad = new string(' ', depth * 2);
        foreach (IHardware hw in hws)
        {
            hw.Update();
            sb.AppendLine(pad + "[" + hw.HardwareType + "] " + hw.Name + "  id=" + hw.Identifier);
            foreach (ISensor s in hw.Sensors)
            {
                sb.AppendLine(pad + "  " + s.SensorType + " | " + s.Name + " | " +
                    (s.Value.HasValue ? s.Value.Value.ToString("0.##") : "(null)"));
            }
            DumpHardware(sb, hw.SubHardware, depth + 1);
        }
    }
}
