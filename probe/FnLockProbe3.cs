// fn10（WMI_FN_KBD_TYPE，"键盘类型"）payload 穷举：观察 EC 事件 id7 与 GET 读数变化
// fn10 本机 GET 恒为 0；fn11 已实测 SET 不生效。fn10 的 SET 从未验证过。
// 需要 PawnIO/管理员。用法：FnLockProbe3.exe <输出文件>
using System;
using System.IO;
using System.Text;
using System.Threading;
using MiCtl;

class FnLockProbe3
{
    static int Main(string[] args)
    {
        string outFile = args.Length > 0 ? args[0] : "fnlock-probe3.txt";
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
        Console.WriteLine("穷举 fn10 的 SET（约 20 秒，无需操作）……");

        byte[][] payloads =
        {
            new byte[] { 0 },
            new byte[] { 1 },
            new byte[] { 2 },
            new byte[] { 0, 1 },
            new byte[] { 1, 0 },
            new byte[] { 0, 0 },
        };

        int seen = w.FnLockEventValue ?? -999;
        foreach (byte[] p in payloads)
        {
            string hex = Hex(p);
            sb.AppendLine("");
            sb.AppendLine(">>> SET fn10 payload [" + hex + "]，观察 3 秒：");
            Console.WriteLine("SET fn10 [" + hex + "] …");
            DateTime t0 = DateTime.Now;
            bool ok = mifs.Set(MifsClient.Fn.KbdType, p);
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
            byte[] g10 = mifs.Get(MifsClient.Fn.KbdType);
            sb.AppendLine("    GET fn10 复查: " + (g10 == null ? "(null)" : "data0=" + g10[0]));
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
