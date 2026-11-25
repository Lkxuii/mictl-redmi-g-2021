// 系统信息 —— 不经过传感器库的那部分：进程、WiFi、延迟
// 全部是 Windows 自带接口，不需要内核驱动。耗时操作都由 AppController 放在后台线程调用。
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace MiCtl
{
    /// <summary>一个进程条目：同名进程合并成一行（浏览器、Electron 应用都是多进程）</summary>
    public sealed class ProcessEntry
    {
        public string Name;                 // 含 .exe
        public long WorkingSet;             // 字节，同名进程求和
        public List<int> Pids = new List<int>();

        public string MemoryText
        {
            get
            {
                double mb = WorkingSet / 1048576.0;
                return mb >= 1024 ? (mb / 1024).ToString("0.00") + " GB" : mb.ToString("0") + " MB";
            }
        }
    }

    public sealed class WifiInfo
    {
        public bool Connected;
        public string Ssid = "";
        public int? SignalPct;
        public string RadioType = "";
        public int? RxMbps;
    }

    public sealed class PingResult
    {
        public DateTime At;
        public int? GatewayMs;
        public int? PublicMs;
        public string Error;
    }

    public static class SystemInfo
    {
        // 结束这些进程会让系统不稳定甚至蓝屏，拒绝在这里结束
        private static readonly string[] Critical =
        {
            "system", "idle", "registry", "smss", "csrss", "wininit", "winlogon", "services", "lsass",
            "dwm", "fontdrvhost", "mictl", "secure system", "memory compression", "svchost"
        };

        public static bool IsCritical(string nameWithExe)
        {
            if (nameWithExe == null) return false;
            string n = nameWithExe.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                ? nameWithExe.Substring(0, nameWithExe.Length - 4)
                : nameWithExe;
            foreach (string c in Critical)
            {
                if (string.Equals(c, n, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        /// <summary>按内存占用取前 topN 个进程（同名合并）。total 是系统进程总数。</summary>
        public static List<ProcessEntry> TopProcesses(int topN, out int total)
        {
            Process[] all;
            try { all = Process.GetProcesses(); }
            catch { total = 0; return new List<ProcessEntry>(); }
            total = all.Length;

            Dictionary<string, ProcessEntry> map = new Dictionary<string, ProcessEntry>(StringComparer.OrdinalIgnoreCase);
            foreach (Process p in all)
            {
                try
                {
                    // 系统关键进程不能结束，也不该占着榜单的位置（svchost 一合并就是上 GB）
                    if (IsCritical(p.ProcessName))
                    {
                        try { p.Dispose(); } catch { }
                        continue;
                    }
                    long ws = 0;
                    try { ws = p.WorkingSet64; } catch { }
                    string name = p.ProcessName + ".exe";
                    ProcessEntry e;
                    if (!map.TryGetValue(name, out e))
                    {
                        e = new ProcessEntry();
                        e.Name = name;
                        map[name] = e;
                    }
                    e.WorkingSet += ws;
                    e.Pids.Add(p.Id);
                }
                catch { }
                finally
                {
                    try { p.Dispose(); } catch { }
                }
            }

            List<ProcessEntry> list = new List<ProcessEntry>(map.Values);
            list.Sort(delegate (ProcessEntry a, ProcessEntry b) { return b.WorkingSet.CompareTo(a.WorkingSet); });
            if (list.Count > topN) list.RemoveRange(topN, list.Count - topN);
            return list;
        }

        /// <summary>结束一个条目下的全部进程。成功返回 null，失败返回给用户看的原因。</summary>
        public static string Kill(ProcessEntry e)
        {
            if (e == null) return "没有可结束的进程";
            if (IsCritical(e.Name)) return "这是系统关键进程，不能在这里结束。";
            int failed = 0;
            foreach (int pid in e.Pids)
            {
                try
                {
                    using (Process p = Process.GetProcessById(pid))
                    {
                        if (string.Equals(p.ProcessName + ".exe", e.Name, StringComparison.OrdinalIgnoreCase))
                        {
                            p.Kill();
                            p.WaitForExit(3000);
                        }
                    }
                }
                catch { failed++; }
            }
            if (failed == 0) return null;
            return failed + " 个进程没能结束（权限不足，或者已经退出）。";
        }

        public static void OpenTaskManager()
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo("taskmgr.exe");
                psi.UseShellExecute = true;
                Process.Start(psi);
            }
            catch { }
        }

        // netsh 的输出随系统显示语言本地化，标签按中英文都认
        private static readonly Regex RxState = new Regex(@"^\s*(?:状态|State)\s*:\s*(.+)$",
            RegexOptions.Multiline | RegexOptions.IgnoreCase);
        private static readonly Regex RxSsid = new Regex(@"^\s*SSID\s*:\s*(.+)$",
            RegexOptions.Multiline | RegexOptions.IgnoreCase);
        private static readonly Regex RxSignal = new Regex(@"^\s*(?:信号|Signal)\s*:\s*(\d+)\s*%",
            RegexOptions.Multiline | RegexOptions.IgnoreCase);
        private static readonly Regex RxRadio = new Regex(@"^\s*(?:无线电类型|Radio type)\s*:\s*(.+)$",
            RegexOptions.Multiline | RegexOptions.IgnoreCase);
        private static readonly Regex RxRate = new Regex(@"^\s*(?:接收速率|Receive rate)\s*\(Mbps\)\s*:\s*(\d+)",
            RegexOptions.Multiline | RegexOptions.IgnoreCase);

        /// <summary>当前无线连接信息。netsh 是外部进程（约 100~200 ms），不要高频调用。</summary>
        public static WifiInfo QueryWifi()
        {
            WifiInfo w = new WifiInfo();
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo("netsh", "wlan show interfaces");
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true;
                psi.StandardOutputEncoding = Encoding.GetEncoding(936);   // 系统代码页
                string text;
                using (Process p = Process.Start(psi))
                {
                    text = p.StandardOutput.ReadToEnd();
                    p.WaitForExit(5000);
                }

                Match m = RxState.Match(text);
                string state = m.Success ? m.Groups[1].Value.Trim() : "";
                w.Connected = state.IndexOf("已连接", StringComparison.Ordinal) >= 0 ||
                              state.IndexOf("connected", StringComparison.OrdinalIgnoreCase) >= 0;
                m = RxSsid.Match(text);
                if (m.Success) w.Ssid = m.Groups[1].Value.Trim();
                m = RxSignal.Match(text);
                if (m.Success) w.SignalPct = int.Parse(m.Groups[1].Value.Trim());
                m = RxRadio.Match(text);
                if (m.Success) w.RadioType = m.Groups[1].Value.Trim();
                m = RxRate.Match(text);
                if (m.Success) w.RxMbps = int.Parse(m.Groups[1].Value.Trim());
            }
            catch { }
            return w;
        }

        /// <summary>ping 网关 4 次、公网 3 次取平均。同步阻塞，放后台线程调用。</summary>
        public static PingResult Ping(string gateway)
        {
            PingResult r = new PingResult();
            r.At = DateTime.Now;
            try
            {
                using (System.Net.NetworkInformation.Ping ping = new System.Net.NetworkInformation.Ping())
                {
                    if (!string.IsNullOrEmpty(gateway))
                        r.GatewayMs = Average(ping, gateway, 4);
                    r.PublicMs = Average(ping, "223.5.5.5", 3);
                }
            }
            catch (Exception ex)
            {
                r.Error = ex.Message;
            }
            return r;
        }

        private static int? Average(System.Net.NetworkInformation.Ping ping, string host, int count)
        {
            long total = 0;
            int ok = 0;
            for (int i = 0; i < count; i++)
            {
                try
                {
                    PingReply reply = ping.Send(host, 1500);
                    if (reply.Status == IPStatus.Success)
                    {
                        total += reply.RoundtripTime;
                        ok++;
                    }
                }
                catch { }
                if (i < count - 1) Thread.Sleep(150);
            }
            if (ok == 0) return null;
            return (int)Math.Round(total / (double)ok);
        }
    }
}
