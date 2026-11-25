// 系统数据综合诊断：新功能要用的每项数据在本机到底读不读得到
//  1. LibreHardwareMonitor：CPU 负载 / 各核频率 / 功耗 / 温度，GPU 频率 / 负载 / 温度 / 功耗（采样 3 轮）
//  2. 内存：GlobalMemoryStatusEx
//  3. 性能计数器：CPU 使用率与频率的备选来源
//  4. 网络：网卡列表、IPv4、网关、netsh wlan（SSID / 信号）、ping 网关
//  5. 进程：数量、内存占用前 10
//  6. 电池：LHM Battery 硬件
// 需要 PawnIO 可用（读 CPU 温度/功耗），以管理员运行。
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using LibreHardwareMonitor.Hardware;

class SysProbe
{
    [StructLayout(LayoutKind.Sequential)]
    private class MEMORY_INFO
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx([In, Out] MEMORY_INFO mi);

    private static string F(float? v) { return v.HasValue ? v.Value.ToString("0.##") : "--"; }

    private static void DumpComputer(StringBuilder sb)
    {
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
            IsBatteryEnabled = true
        };
        c.Open();
        try
        {
            for (int round = 1; round <= 3; round++)
            {
                foreach (IHardware hw in c.Hardware)
                {
                    hw.Update();
                    if (hw.HardwareType != HardwareType.Cpu && hw.HardwareType != HardwareType.GpuNvidia &&
                        hw.HardwareType != HardwareType.Battery)
                        continue;
                    sb.AppendLine();
                    sb.AppendLine("[round " + round + "] [" + hw.HardwareType + "] " + hw.Name);
                    foreach (ISensor s in hw.Sensors)
                    {
                        if (hw.HardwareType == HardwareType.GpuNvidia &&
                            s.SensorType != SensorType.Clock && s.SensorType != SensorType.Load &&
                            s.SensorType != SensorType.Power && s.SensorType != SensorType.Temperature &&
                            s.SensorType != SensorType.SmallData)
                            continue;
                        sb.AppendLine("  " + s.SensorType + " | " + s.Name + " | " + F(s.Value));
                    }
                }
                Thread.Sleep(1500);
            }
        }
        finally { c.Close(); }
    }

    private static void DumpPerfCounters(StringBuilder sb)
    {
        sb.AppendLine();
        sb.AppendLine("=== 性能计数器 ===");
        try
        {
            PerformanceCounterCategory cat = new PerformanceCounterCategory("Processor Information");
            string inst = null;
            foreach (string n in cat.GetInstanceNames())
            {
                if (n.EndsWith("_Total")) { inst = n; break; }
            }
            sb.AppendLine("实例: " + inst);
            string[] names = { "% Processor Utility", "% Processor Time", "Processor Frequency", "% of Maximum Frequency", "% Processor Performance" };
            List<PerformanceCounter> list = new List<PerformanceCounter>();
            foreach (string n in names)
            {
                try { list.Add(new PerformanceCounter("Processor Information", n, inst)); }
                catch (Exception ex) { sb.AppendLine("  不可用: " + n + " (" + ex.Message + ")"); }
            }
            foreach (PerformanceCounter pc in list) pc.NextValue();
            Thread.Sleep(1100);
            foreach (PerformanceCounter pc in list)
            {
                try { sb.AppendLine("  " + pc.CounterName + " = " + pc.NextValue().ToString("0.##")); }
                catch (Exception ex) { sb.AppendLine("  " + pc.CounterName + " 读取失败: " + ex.Message); }
            }
        }
        catch (Exception ex) { sb.AppendLine("EXCEPTION: " + ex.Message); }
    }

    private static void DumpNetwork(StringBuilder sb)
    {
        sb.AppendLine();
        sb.AppendLine("=== 网卡 ===");
        IPAddress gateway = null;
        foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            IPInterfaceProperties ip = null;
            try { ip = nic.GetIPProperties(); } catch { }
            sb.AppendLine(nic.Name + " | " + nic.NetworkInterfaceType + " | " + nic.OperationalStatus + " | " + nic.Description);
            if (nic.OperationalStatus != OperationalStatus.Up || ip == null) continue;
            foreach (UnicastIPAddressInformation a in ip.UnicastAddresses)
            {
                if (a.Address.AddressFamily == AddressFamily.InterNetwork)
                {
                    sb.AppendLine("   IPv4 " + a.Address);
                    foreach (GatewayIPAddressInformation g in ip.GatewayAddresses)
                    {
                        sb.AppendLine("   网关 " + g.Address);
                        if (gateway == null) gateway = g.Address;
                    }
                }
            }
        }

        sb.AppendLine();
        sb.AppendLine("=== netsh wlan show interfaces ===");
        try
        {
            ProcessStartInfo psi = new ProcessStartInfo("netsh", "wlan show interfaces");
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.RedirectStandardOutput = true;
            psi.StandardOutputEncoding = Encoding.GetEncoding(936);   // 系统代码页（中文系统 netsh 输出）
            using (Process p = Process.Start(psi))
            {
                string o = p.StandardOutput.ReadToEnd();
                p.WaitForExit(5000);
                sb.AppendLine(o.Trim());
            }
        }
        catch (Exception ex) { sb.AppendLine("EXCEPTION: " + ex.Message); }

        sb.AppendLine();
        sb.AppendLine("=== ping 网关 ===");
        if (gateway == null) { sb.AppendLine("没有找到网关"); return; }
        try
        {
            using (Ping ping = new Ping())
            {
                for (int i = 0; i < 4; i++)
                {
                    PingReply r = ping.Send(gateway, 1500);
                    sb.AppendLine("  " + gateway + " -> " + r.Status + " " + (r.Status == IPStatus.Success ? r.RoundtripTime + " ms" : ""));
                    Thread.Sleep(200);
                }
                PingReply pub = ping.Send("223.5.5.5", 1500);
                sb.AppendLine("  223.5.5.5 -> " + pub.Status + " " + (pub.Status == IPStatus.Success ? pub.RoundtripTime + " ms" : ""));
            }
        }
        catch (Exception ex) { sb.AppendLine("EXCEPTION: " + ex.Message); }
    }

    private static void DumpProcesses(StringBuilder sb)
    {
        sb.AppendLine();
        sb.AppendLine("=== 进程 ===");
        List<Process> list = new List<Process>(Process.GetProcesses());
        sb.AppendLine("进程数: " + list.Count);
        list.Sort(delegate (Process a, Process b)
        {
            try { return b.WorkingSet64.CompareTo(a.WorkingSet64); }
            catch { return 0; }
        });
        Stopwatch sw = Stopwatch.StartNew();
        int shown = 0;
        foreach (Process p in list)
        {
            if (shown >= 10) break;
            try
            {
                sb.AppendLine("  " + p.ProcessName + ".exe | " + (p.WorkingSet64 / 1048576) + " MB | pid " + p.Id);
                shown++;
            }
            catch { }
        }
        sw.Stop();
        sb.AppendLine("枚举+读取耗时(含上面): " + sw.ElapsedMilliseconds + " ms");
    }

    static int Main(string[] args)
    {
        string outFile = args.Length > 0 ? args[0] : "sysprobe.txt";
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("时间: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        sb.AppendLine("管理员: " + (new System.Security.Principal.WindowsPrincipal(System.Security.Principal.WindowsIdentity.GetCurrent())
            .IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator)));
        try { DumpComputer(sb); }
        catch (Exception ex) { sb.AppendLine("LHM EXCEPTION: " + ex); }
        try
        {
            MEMORY_INFO mi = new MEMORY_INFO();
            mi.dwLength = (uint)Marshal.SizeOf(typeof(MEMORY_INFO));
            GlobalMemoryStatusEx(mi);
            sb.AppendLine();
            sb.AppendLine("=== 内存 ===");
            sb.AppendLine("使用率 " + mi.dwMemoryLoad + "%  总 " + (mi.ullTotalPhys / 1048576.0).ToString("0.##") + " GB  可用 " +
                (mi.ullAvailPhys / 1048576.0).ToString("0.##") + " GB  已用 " +
                ((mi.ullTotalPhys - mi.ullAvailPhys) / 1048576.0).ToString("0.##") + " GB");
        }
        catch (Exception ex) { sb.AppendLine("MEM EXCEPTION: " + ex.Message); }
        try { DumpPerfCounters(sb); }
        catch (Exception ex) { sb.AppendLine("PERF EXCEPTION: " + ex.Message); }
        try { DumpNetwork(sb); }
        catch (Exception ex) { sb.AppendLine("NET EXCEPTION: " + ex.Message); }
        try { DumpProcesses(sb); }
        catch (Exception ex) { sb.AppendLine("PROC EXCEPTION: " + ex.Message); }
        File.WriteAllText(outFile, sb.ToString(), new UTF8Encoding(false));
        return 0;
    }
}
