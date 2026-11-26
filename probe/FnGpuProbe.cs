// fn9 SET 行为探针：fn9 GET 在本机是风扇/温度状态包（Linux 驱动注释的 GPU_MODE 语义不符），
// fn9 SET 是否有 GPU 模式/其它语义从未系统验证。本探针对 {0}/{1}/{2} 各发一次，
// 每次后立即对比 fn9 GET 的风扇/温度是否变化（确认无副作用）并记录 EC 事件。
// 需要 PawnIO/管理员。用法：FnGpuProbe.exe <输出文件>
using System;
using System.Collections.Generic;
using System.IO;
using System.Management;
using System.Text;
using System.Threading;
using MiCtl;

class FnGpuProbe
{
    private static readonly List<string> Events = new List<string>();
    private static DateTime _t0 = DateTime.Now;

    static int Main(string[] args)
    {
        string outFile = args.Length > 0 ? args[0] : "fngpu-probe.txt";
        StringBuilder sb = new StringBuilder();
        MifsClient mifs = new MifsClient();
        sb.AppendLine("MIFS 可用: " + mifs.Available);
        if (!mifs.Available)
        {
            Console.WriteLine(sb.ToString());
            File.WriteAllText(outFile, sb.ToString(), new UTF8Encoding(false));
            return 1;
        }

        ManagementScope scope = new ManagementScope(@"root\WMI");
        scope.Connect();
        ManagementEventWatcher watcher = new ManagementEventWatcher(scope, new WqlEventQuery("SELECT * FROM HID_EVENT20"));
        watcher.EventArrived += delegate(object s, EventArrivedEventArgs e)
        {
            try
            {
                foreach (PropertyData pd in e.NewEvent.Properties)
                {
                    byte[] b = pd.Value as byte[];
                    if (b == null || b.Length < 8) continue;
                    string line = string.Format("    [{0,6:0.0}s] EC 事件 type={1} id={2} value={3}",
                        (DateTime.Now - _t0).TotalSeconds, b[0], b[1], b[2]);
                    lock (Events) Events.Add(line);
                    Console.WriteLine(line);
                }
            }
            catch { }
        };
        watcher.Start();
        _t0 = DateTime.Now;

        sb.AppendLine("fn9 GET 基线: " + Hex(mifs.Get(MifsClient.Fn.GpuMode)));

        byte[][] payloads = { new byte[] { 0 }, new byte[] { 1 }, new byte[] { 2 } };
        foreach (byte[] p in payloads)
        {
            string hex = Hex(p);
            sb.AppendLine("");
            sb.AppendLine(">>> SET fn9 payload [" + hex + "]，观察 4 秒：");
            Console.WriteLine("SET fn9 [" + hex + "] ……");
            byte[] before = mifs.Get(MifsClient.Fn.GpuMode);
            sb.AppendLine("    SET 前 fn9 GET: " + Hex(before));
            DateTime t0 = DateTime.Now;
            bool ok = mifs.Set(MifsClient.Fn.GpuMode, p);
            sb.AppendLine("    SET 返回: " + ok);
            int beforeEvents = Events.Count;
            Thread.Sleep(2000);
            for (int i = beforeEvents; i < Events.Count; i++) sb.AppendLine(Events[i]);
            if (Events.Count == beforeEvents) sb.AppendLine("    （无 EC 事件）");
            byte[] after = mifs.Get(MifsClient.Fn.GpuMode);
            sb.AppendLine("    SET 后 fn9 GET: " + Hex(after));
            sb.AppendLine("    GET 是否变化: " + (Hex(before) != Hex(after) ? "是" : "否"));
        }

        sb.AppendLine("");
        sb.AppendLine("结束时全部事件：");
        lock (Events) foreach (string l in Events) sb.AppendLine(l);
        watcher.Stop();
        watcher.Dispose();
        mifs.Dispose();
        File.WriteAllText(outFile, sb.ToString(), new UTF8Encoding(false));
        Console.WriteLine("完成，结果已写入 " + outFile);
        Thread.Sleep(2000);
        return 0;
    }

    private static string Hex(byte[] d)
    {
        if (d == null) return "(null)";
        StringBuilder sb = new StringBuilder();
        foreach (byte b in d) sb.Append(b.ToString("X2")).Append(' ');
        return sb.ToString().TrimEnd();
    }
}
