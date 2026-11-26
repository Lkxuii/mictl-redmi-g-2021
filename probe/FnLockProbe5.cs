// fn10 payload → 动作 映射穷举：记录所有 EC 事件（id + value），画出 fn10 每个 payload 触发什么。
// 已知：{1} = Fn 解锁（EC id7 value0）；{0} = 触控板切换（用户实测，EC 应推 id6——此前探针只盯 id7 漏了）。
// 本探针记录 HID_EVENT20 的全部事件（type/id/value），payload {0}..{6} 各测两次（toggle 类动作第二次应恢复）。
// 需要 PawnIO/管理员。用法：FnLockProbe5.exe <输出文件>（控制台保持可见，约 60 秒）
using System;
using System.Collections.Generic;
using System.IO;
using System.Management;
using System.Text;
using System.Threading;
using MiCtl;

class FnLockProbe5
{
    private static readonly List<string> Events = new List<string>();
    private static DateTime _t0 = DateTime.Now;

    static int Main(string[] args)
    {
        string outFile = args.Length > 0 ? args[0] : "fnlock-probe5.txt";
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
        sb.AppendLine("EC 全事件订阅: 已启动。基线观察 2 秒：");
        Console.WriteLine("基线观察 2 秒……");
        Thread.Sleep(2000);
        lock (Events) foreach (string l in Events) sb.AppendLine(l);

        byte[][] payloads =
        {
            new byte[] { 0 },
            new byte[] { 0 },
            new byte[] { 1 },
            new byte[] { 1 },
            new byte[] { 2 },
            new byte[] { 3 },
            new byte[] { 4 },
            new byte[] { 5 },
            new byte[] { 6 },
        };

        foreach (byte[] p in payloads)
        {
            string hex = Hex(p);
            Console.WriteLine("");
            Console.WriteLine(">>> SET fn10 [" + hex + "] ……（观察 3 秒，注意触控板/键盘灯/音量等变化）");
            sb.AppendLine("");
            sb.AppendLine(">>> SET fn10 [" + hex + "]，观察 3 秒：");
            DateTime t0 = DateTime.Now;
            bool ok = mifs.Set(MifsClient.Fn.KbdType, p);
            sb.AppendLine("    SET 返回: " + ok);
            int before = Events.Count;
            Thread.Sleep(3000);
            // 本步期间的新事件
            for (int i = before; i < Events.Count; i++) sb.AppendLine(Events[i]);
            if (Events.Count == before) sb.AppendLine("    （无 EC 事件）");
        }

        sb.AppendLine("");
        sb.AppendLine("结束时全部事件回放：");
        lock (Events) foreach (string l in Events) sb.AppendLine(l);
        watcher.Stop();
        watcher.Dispose();
        mifs.Dispose();
        File.WriteAllText(outFile, sb.ToString(), new UTF8Encoding(false));
        Console.WriteLine("");
        Console.WriteLine("完成，结果已写入 " + outFile);
        Thread.Sleep(2500);
        return 0;
    }

    private static string Hex(byte[] p)
    {
        StringBuilder sb = new StringBuilder();
        foreach (byte b in p) sb.Append(b.ToString("X2")).Append(' ');
        return sb.ToString().TrimEnd();
    }
}
