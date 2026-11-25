// fn11 payload 形态穷举：不同 payload 的 SET 各观察 3 秒，看哪种能触发 EC 事件 id7（状态真的变了）
// 背景：单字节 {0}/{1} 已实测无效；游戏盒子 Host 也实现了 MIFS，payload 形态可能不同。
// 需要 PawnIO/管理员。用法：FnLockProbe2.exe <输出文件>
using System;
using System.IO;
using System.Text;
using System.Threading;
using MiCtl;

class FnLockProbe2
{
    static int Main(string[] args)
    {
        string outFile = args.Length > 0 ? args[0] : "fnlock-probe2.txt";
        StringBuilder sb = new StringBuilder();
        MifsClient mifs = new MifsClient();
        sb.AppendLine("MIFS 可用: " + mifs.Available);
        if (!mifs.Available)
        {
            Console.WriteLine(sb.ToString());
            File.WriteAllText(outFile, sb.ToString(), new UTF8Encoding(false));
            return 1;
        }
        FanSpeedWatcher w = new FanSpeedWatcher();
        w.Start();
        sb.AppendLine("EC 事件订阅: " + w.Started + "  启动缓存 value: " + Fmt(w.FnLockEventValue));
        Console.WriteLine("开始穷举 fn11 的 payload 形态（约 25 秒，无需操作）……");

        byte[][] payloads =
        {
            new byte[] { 0 },
            new byte[] { 1 },
            new byte[] { 0, 1, 0, 0 },
            new byte[] { 1, 0, 0, 0 },
            new byte[] { 0, 0, 0, 0 },
            new byte[] { 2 },
            new byte[] { 0x10 },
        };

        int seen = w.FnLockEventValue ?? -999;
        foreach (byte[] p in payloads)
        {
            string hex = Hex(p);
            sb.AppendLine("");
            sb.AppendLine(">>> SET fn11 payload [" + hex + "]，观察 3 秒：");
            Console.WriteLine("SET fn11 [" + hex + "] …");
            DateTime t0 = DateTime.Now;
            bool ok = mifs.Set(MifsClient.Fn.FnLock, p);
            sb.AppendLine("    SET 返回: " + ok);
            for (int i = 0; i < 15; i++)
            {
                Thread.Sleep(200);
                int cur = w.FnLockEventValue ?? -999;
                if (cur != seen)
                {
                    seen = cur;
                    string line = "    ★ [" + (DateTime.Now - t0).TotalSeconds.ToString("0.0") + "s] EC 事件 id7 -> value " + cur + "  （payload [" + hex + "] 生效！）";
                    sb.AppendLine(line);
                    Console.WriteLine(line);
                }
            }
            sb.AppendLine("    GET fn11 复查: " + Hex(mifs.Get(MifsClient.Fn.FnLock)));
        }

        sb.AppendLine("");
        sb.AppendLine("结束时事件缓存 value: " + Fmt(w.FnLockEventValue));
        w.Dispose();
        mifs.Dispose();
        File.WriteAllText(outFile, sb.ToString(), new UTF8Encoding(false));
        Console.WriteLine("完成，结果已写入 " + outFile);
        Thread.Sleep(1500);
        return 0;
    }

    private static string Fmt(int? v)
    {
        return v.HasValue ? v.Value.ToString() : "(空)";
    }

    private static string Hex(byte[] p)
    {
        StringBuilder sb = new StringBuilder();
        foreach (byte b in p) sb.Append(b.ToString("X2")).Append(' ');
        return sb.ToString().TrimEnd();
    }
}
