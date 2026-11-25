// Fn 锁软件切换可行性探针：fn11 SET 之后，观察 EC 事件（id 7）是否推送、请用户按 F1 确认行为是否变化
// 背景：fn11 GET 在本机恒为 1（不反映状态），此前从未验证过 SET 是否真的切换。
// 需要 PawnIO/管理员。用法：FnLockProbe.exe <输出文件>（控制台窗口保持可见，里面有操作提示）
using System;
using System.IO;
using System.Text;
using System.Threading;
using MiCtl;

class FnLockProbe
{
    static int Main(string[] args)
    {
        string outFile = args.Length > 0 ? args[0] : "fnlock-probe.txt";
        StringBuilder sb = new StringBuilder();
        MifsClient mifs = new MifsClient();
        sb.AppendLine("MIFS 可用: " + mifs.Available + (mifs.Available ? "" : "  (" + mifs.InitError + ")"));
        if (!mifs.Available)
        {
            Console.WriteLine(sb.ToString());
            File.WriteAllText(outFile, sb.ToString(), new UTF8Encoding(false));
            return 1;
        }
        FanSpeedWatcher w = new FanSpeedWatcher();
        w.Start();
        sb.AppendLine("EC 事件订阅: " + w.Started);
        sb.AppendLine("启动时事件缓存 value: " + Fmt(w.FnLockEventValue));
        sb.AppendLine("GET fn11: " + Hex(mifs.Get(MifsClient.Fn.FnLock)));

        Console.WriteLine("");
        Console.WriteLine("==================================================");
        Console.WriteLine(" 发送 SET fn11 = 0。请【现在按一下 F1】，");
        Console.WriteLine(" 看是静音/音量（解锁）还是正常 F1（锁定）。");
        Console.WriteLine(" 12 秒后会发送 SET fn11 = 1，请【再按一次 F1】。");
        Console.WriteLine("==================================================");

        sb.AppendLine("");
        sb.AppendLine(">>> SET fn11 = 0 已发送，观察 12 秒：");
        int seen = w.FnLockEventValue ?? -999;
        DateTime t0 = DateTime.Now;
        bool ok0 = mifs.Set(MifsClient.Fn.FnLock, new byte[] { 0 });
        sb.AppendLine("    SET 返回: " + ok0);
        Watch(sb, w, ref seen, t0, 12000);
        sb.AppendLine("GET fn11 复查: " + Hex(mifs.Get(MifsClient.Fn.FnLock)));

        Console.WriteLine("");
        Console.WriteLine("==================================================");
        Console.WriteLine(" 发送 SET fn11 = 1。请【再按一次 F1】看行为。");
        Console.WriteLine("==================================================");

        sb.AppendLine("");
        sb.AppendLine(">>> SET fn11 = 1 已发送，观察 12 秒：");
        t0 = DateTime.Now;
        bool ok1 = mifs.Set(MifsClient.Fn.FnLock, new byte[] { 1 });
        sb.AppendLine("    SET 返回: " + ok1);
        Watch(sb, w, ref seen, t0, 12000);
        sb.AppendLine("GET fn11 复查: " + Hex(mifs.Get(MifsClient.Fn.FnLock)));
        sb.AppendLine("结束时事件缓存 value: " + Fmt(w.FnLockEventValue));

        w.Dispose();
        mifs.Dispose();
        File.WriteAllText(outFile, sb.ToString(), new UTF8Encoding(false));
        Console.WriteLine("");
        Console.WriteLine("完成，结果已写入 " + outFile + "。窗口即将关闭。");
        Thread.Sleep(2500);
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
                string line = "    [" + (DateTime.Now - t0).TotalSeconds.ToString("0.0") + "s] EC 事件 id7 -> value " + cur;
                sb.AppendLine(line);
                Console.WriteLine(line);
            }
        }
    }

    private static string Fmt(int? v)
    {
        return v.HasValue ? v.Value.ToString() : "(空)";
    }

    private static string Hex(byte[] d)
    {
        if (d == null) return "(null)";
        StringBuilder sb = new StringBuilder();
        foreach (byte b in d) sb.Append(b.ToString("X2")).Append(' ');
        return sb.ToString();
    }
}
