// fn10 双向验证：SET {0} 与 SET {1} 各观察 EC 事件并请用户按 F1 确认行为，
// 钉死 fn10 payload 与 Fn 锁定/解锁的对应关系。
// 需要 PawnIO/管理员。用法：FnLockProbe4.exe <输出文件>（控制台保持可见）
using System;
using System.IO;
using System.Text;
using System.Threading;
using MiCtl;

class FnLockProbe4
{
    static int Main(string[] args)
    {
        string outFile = args.Length > 0 ? args[0] : "fnlock-probe4.txt";
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

        int seen = w.FnLockEventValue ?? -999;

        Console.WriteLine("");
        Console.WriteLine("====================================================");
        Console.WriteLine(" 第 1 步：发送 SET fn10 = 0。");
        Console.WriteLine(" 请按一下 F1，观察是正常 F1（锁定）还是音量/静音（解锁）。");
        Console.WriteLine("====================================================");
        DateTime t0 = DateTime.Now;
        sb.AppendLine(">>> SET fn10 = {0}，观察 8 秒：");
        bool ok = mifs.Set(MifsClient.Fn.KbdType, new byte[] { 0 });
        sb.AppendLine("    SET 返回: " + ok);
        Watch(sb, w, ref seen, t0, 8000);

        Console.WriteLine("");
        Console.WriteLine("====================================================");
        Console.WriteLine(" 第 2 步：发送 SET fn10 = 1。");
        Console.WriteLine(" 请再按一下 F1，对比行为是否变化。");
        Console.WriteLine("====================================================");
        t0 = DateTime.Now;
        sb.AppendLine(">>> SET fn10 = {1}，观察 8 秒：");
        ok = mifs.Set(MifsClient.Fn.KbdType, new byte[] { 1 });
        sb.AppendLine("    SET 返回: " + ok);
        Watch(sb, w, ref seen, t0, 8000);

        sb.AppendLine("");
        sb.AppendLine("GET fn10 复查: data0=" + (mifs.Get(MifsClient.Fn.KbdType) ?? new byte[32])[0]);
        sb.AppendLine("GET fn11 复查: data0=" + (mifs.Get(MifsClient.Fn.FnLock) ?? new byte[32])[0]);
        sb.AppendLine("结束时事件缓存 value: " + Fmt(w.FnLockEventValue));

        w.Dispose();
        mifs.Dispose();
        File.WriteAllText(outFile, sb.ToString(), new UTF8Encoding(false));
        Console.WriteLine("");
        Console.WriteLine("完成，结果已写入 " + outFile);
        Thread.Sleep(2000);
        return 0;
    }

    private static void Watch(StringBuilder sb, FanSpeedWatcher w, ref int seen, DateTime t0, int ms)
    {
        for (int i = 0; i < ms / 200; i++)
        {
            Thread.Sleep(200);
            int cur = w.FnLockEventValue ?? -999;
            if (cur != seen)
            {
                seen = cur;
                string line = "    ★ [" + (DateTime.Now - t0).TotalSeconds.ToString("0.0") + "s] EC 事件 id7 -> value " + cur;
                sb.AppendLine(line);
                Console.WriteLine(line);
            }
        }
    }

    private static string Fmt(int? v)
    {
        return v.HasValue ? v.Value.ToString() : "(空)";
    }
}
