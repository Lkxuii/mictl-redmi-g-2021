// MIFS SET 全枚举：之前只枚举过 GET（4 个响应码），"只写"功能码从未扫过。
// 目的：找出所有 SET 有效（返回 True）的功能码，再逐个观察能否产生 Fn 锁定（EC id7 value1）。
// 风险控制：快速扫只发 payload{0}；结束后恢复 fn8 档位。SET 类操作都是用户可恢复的硬件设置。
// 需要 PawnIO/管理员。用法：FnLockProbe6.exe <输出文件>
using System;
using System.Collections.Generic;
using System.IO;
using System.Management;
using System.Text;
using System.Threading;
using MiCtl;

class FnLockProbe6
{
    private static readonly List<string> Events = new List<string>();
    private static DateTime _t0 = DateTime.Now;

    static int Main(string[] args)
    {
        string outFile = args.Length > 0 ? args[0] : "set-enum.txt";
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

        // 记录当前档位，结束后恢复
        byte[] fn8Before = mifs.Get(MifsClient.Fn.PerMode);
        sb.AppendLine("当前档位 fn8: " + Hex(fn8Before));

        Console.WriteLine("第 1 步：快速扫描 fn0-255 的 SET（payload {0}），约 20 秒……");
        sb.AppendLine("");
        sb.AppendLine("=== 第 1 步：SET {0} 快速扫描（fn 0-255）===");
        List<int> alive = new List<int>();
        for (int fn = 0; fn <= 255; fn++)
        {
            bool ok = mifs.Set((byte)fn, new byte[] { 0 });
            if (ok)
            {
                alive.Add(fn);
                string line = "    fn" + fn + " (0x" + fn.ToString("X2") + ") SET {0} 成功";
                sb.AppendLine(line);
                Console.WriteLine(line);
            }
            if (fn % 32 == 0) Console.WriteLine("  …扫到 fn" + fn);
        }
        sb.AppendLine("SET 有效的功能码: " + (alive.Count > 0 ? string.Join(", ", alive.ConvertAll(f => f.ToString()).ToArray()) : "(无)"));

        Console.WriteLine("第 2 步：对 SET 有效的功能码发 payload {1}，观察能否产生 Fn 锁定（EC id7 value1）……");
        sb.AppendLine("");
        sb.AppendLine("=== 第 2 步：SET {1} + 观察 EC 事件（找 id7 value1 = Fn 锁定）===");
        foreach (int fn in alive)
        {
            if (fn == 8) continue;   // 档位不动
            string head = ">>> SET fn" + fn + " {1}，观察 3 秒：";
            sb.AppendLine(head);
            Console.WriteLine(head);
            DateTime t0 = DateTime.Now;
            bool ok = mifs.Set((byte)fn, new byte[] { 1 });
            sb.AppendLine("    SET 返回: " + ok);
            int before = Events.Count;
            Thread.Sleep(3000);
            for (int i = before; i < Events.Count; i++) sb.AppendLine(Events[i]);
            if (Events.Count == before) sb.AppendLine("    （无 EC 事件）");
            // 若此步切了 Fn 到解锁（id7 value0），再发 payload {0} 尝试锁定并观察
            lock (Events)
            {
                bool sawUnlock = false;
                for (int i = before; i < Events.Count; i++)
                    if (Events[i].Contains("id=7 value=0")) sawUnlock = true;
                if (sawUnlock)
                {
                    sb.AppendLine("    >>> 该功能码可解锁 Fn！尝试 payload {0} 锁定：");
                    Console.WriteLine("    尝试 payload {0} 锁定……");
                    DateTime t1 = DateTime.Now;
                    bool ok2 = mifs.Set((byte)fn, new byte[] { 0 });
                    sb.AppendLine("    SET 返回: " + ok2);
                    int b2 = Events.Count;
                    Thread.Sleep(3000);
                    for (int i = b2; i < Events.Count; i++) sb.AppendLine(Events[i]);
                    if (Events.Count == b2) sb.AppendLine("    （无 EC 事件——锁定方向可能不生效）");
                }
            }
        }

        // 恢复档位
        if (fn8Before != null)
        {
            byte p = fn8Before[4];
            mifs.Set(MifsClient.Fn.PerMode, new byte[] { p });
            sb.AppendLine("已恢复档位 fn8 -> " + p);
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

    private static string Hex(byte[] d)
    {
        if (d == null) return "(null)";
        StringBuilder sb = new StringBuilder();
        foreach (byte b in d) sb.Append(b.ToString("X2")).Append(' ');
        return sb.ToString().TrimEnd();
    }
}
