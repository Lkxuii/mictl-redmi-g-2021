// 触控板 EC 层修复 + 状态确认：SET fn10 {0}（触控板启用）并监听 EC 回推的值。
// 需要 PawnIO/管理员。用法：TouchpadOn.exe <输出文件>
using System;
using System.Collections.Generic;
using System.IO;
using System.Management;
using System.Text;
using System.Threading;
using MiCtl;

class TouchpadOn
{
    private static readonly List<string> Events = new List<string>();
    private static DateTime _t0 = DateTime.Now;

    static int Main(string[] args)
    {
        string outFile = args.Length > 0 ? args[0] : "touchpad-on.txt";
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

        Console.WriteLine("发送 SET fn10 {0}（触控板启用）……");
        sb.AppendLine(">>> SET fn10 {0}，观察 6 秒：");
        DateTime t0 = DateTime.Now;
        bool ok = mifs.Set(MifsClient.Fn.KbdType, new byte[] { 0 });
        sb.AppendLine("    SET 返回: " + ok);
        int before = Events.Count;
        Thread.Sleep(6000);
        for (int i = before; i < Events.Count; i++) sb.AppendLine(Events[i]);
        if (Events.Count == before) sb.AppendLine("    （无 EC 事件）");

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
}
