// 键盘层探针：低级键盘钩子记录按键的 vkCode / 扫描码 / 是否注入。
// 目的：判别 Fn"解锁态"下按 F1，硬件发的是 F1 扫描码（媒体键由游戏盒子软件模拟，MiCtl 可复刻）
// 还是直接发静音/媒体键扫描码（EC 硬件行为，需要 EC 写通道才能切换）。
// 不需要管理员。用法：KeyProbe.exe <输出文件>（控制台保持可见，共记录 16 秒）
// 注意：低级键盘钩子的回调经由安装线程的消息机制投递，线程必须泵消息（PeekMessage 循环），
//       否则系统会因超时绕过钩子（表现为记录不到任何按键）。
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

class KeyProbe
{
    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public UIntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public int ptX;
        public int ptY;
    }

    private delegate IntPtr LowLevelHookProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelHookProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);

    [DllImport("user32.dll")]
    private static extern bool PeekMessage(out MSG msg, IntPtr hWnd, uint min, uint max, uint remove);

    [DllImport("user32.dll")]
    private static extern bool TranslateMessage(ref MSG msg);

    [DllImport("user32.dll")]
    private static extern IntPtr DispatchMessage(ref MSG msg);

    private static readonly List<string> Log = new List<string>();
    private static IntPtr _hook;
    private static DateTime _t0 = DateTime.Now;

    private static string VkName(uint vk)
    {
        switch (vk)
        {
            case 0x70: return "F1";
            case 0x71: return "F2";
            case 0x72: return "F3";
            case 0x73: return "F4";
            case 0x74: return "F5";
            case 0x75: return "F6";
            case 0x76: return "F7";
            case 0x77: return "F8";
            case 0x78: return "F9";
            case 0x79: return "F10";
            case 0x7A: return "F11";
            case 0x7B: return "F12";
            case 0xAD: return "VOLUME_MUTE(静音)";
            case 0xAE: return "VOLUME_DOWN(音量减)";
            case 0xAF: return "VOLUME_UP(音量加)";
            case 0xB0: return "MEDIA_NEXT";
            case 0xB1: return "MEDIA_PREV";
            case 0xB2: return "MEDIA_STOP";
            case 0xB3: return "MEDIA_PLAY_PAUSE";
            default: return "";
        }
    }

    private static IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int msg = wParam.ToInt32();
            if (msg == 0x0100 || msg == 0x0104)   // KEYDOWN / SYSKEYDOWN
            {
                KBDLLHOOKSTRUCT k = (KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(KBDLLHOOKSTRUCT));
                bool injected = (k.flags & 0x10) != 0;   // LLKHF_INJECTED
                string line = string.Format(
                    "  [{0,5:0.0}s] {1,-3} vk=0x{2:X2} {3} scan=0x{4:X4} injected={5}",
                    (DateTime.Now - _t0).TotalSeconds,
                    msg == 0x0104 ? "SYS" : "DN",
                    k.vkCode,
                    VkName(k.vkCode),
                    k.scanCode | ((k.flags & 0x01) != 0 ? 0xE000u : 0),
                    injected);
                lock (Log) Log.Add(line);
                Console.WriteLine(line);
            }
        }
        return CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    static int Main(string[] args)
    {
        string outFile = args.Length > 0 ? args[0] : "key-probe.txt";
        _hook = SetWindowsHookEx(13, HookProc, GetModuleHandle(null), 0);
        if (_hook == IntPtr.Zero)
        {
            Console.WriteLine("钩子安装失败: " + Marshal.GetLastWin32Error());
            return 1;
        }
        _t0 = DateTime.Now;
        Console.WriteLine("====================================================");
        Console.WriteLine(" 键盘层探针已启动，共记录 16 秒。");
        Console.WriteLine(" 0~8 秒：请按一下 F1（游戏盒子当前状态下）。");
        Console.WriteLine(" 8~16 秒：如果期间你切换了游戏盒子的 Fn 开关，请再按一下 F1。");
        Console.WriteLine("====================================================");

        DateTime end = DateTime.Now.AddSeconds(16);
        MSG msg;
        bool half = false;
        while (DateTime.Now < end)
        {
            while (PeekMessage(out msg, IntPtr.Zero, 0, 0, 1))   // PM_REMOVE
            {
                TranslateMessage(ref msg);
                DispatchMessage(ref msg);
            }
            if (!half && (end - DateTime.Now).TotalSeconds <= 8)
            {
                half = true;
                Console.WriteLine("---- 8 秒已过，如切换了开关请再按一次 F1 ----");
            }
            Thread.Sleep(30);
        }
        UnhookWindowsHookEx(_hook);

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("记录时间: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        lock (Log)
        {
            foreach (string l in Log) sb.AppendLine(l);
            sb.AppendLine("共 " + Log.Count + " 条按键");
        }
        File.WriteAllText(outFile, sb.ToString(), new UTF8Encoding(false));
        Console.WriteLine("");
        Console.WriteLine("完成，结果已写入 " + outFile);
        Thread.Sleep(2000);
        return 0;
    }
}
