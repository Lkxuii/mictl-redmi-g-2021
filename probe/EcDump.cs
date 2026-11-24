// EC 全空间 dump + 转速寄存器定位
// 对比：游戏盒子显示 CPU 2398 RPM / GPU 2406 RPM 时，在 EC RAM 中找匹配的 16bit 模式
using System;
using System.IO;
using System.Text;
using System.Threading;
using LibreHardwareMonitor.PawnIo;

class EcDump
{
    static LpcAcpiEc ec;

    // 标准 ACPI EC 读（参照 LHM WindowsEmbeddedControllerIO）
    static bool ReadByteOp(byte register, out byte value)
    {
        const byte Command = 0x66, Data = 0x62;
        // WaitWrite: IBF 清
        for (int i = 0; i < 50; i++) { if ((ec.ReadPort(Command) & 0x02) == 0) goto write; Thread.Sleep(1); }
        value = 0; return false;
    write:
        ec.WritePort(Command, 0x80); // RD_EC
        for (int i = 0; i < 50; i++) { if ((ec.ReadPort(Command) & 0x02) == 0) goto addr; Thread.Sleep(1); }
        value = 0; return false;
    addr:
        ec.WritePort(Data, register);
        for (int i = 0; i < 50; i++)
        {
            byte st = ec.ReadPort(Command);
            if ((st & 0x02) == 0 && (st & 0x01) != 0) { value = ec.ReadPort(Data); return true; }
            Thread.Sleep(1);
        }
        value = 0; return false;
    }

    static byte ReadEc(byte register)
    {
        for (int i = 0; i < 5; i++)
        {
            byte v;
            if (ReadByteOp(register, out v)) return v;
        }
        return 0xFF;
    }

    static byte[] DumpAll()
    {
        byte[] d = new byte[256];
        for (int i = 0; i < 256; i++) d[i] = ReadEc((byte)i);
        return d;
    }

    static int Main(string[] args)
    {
        string outFile = args.Length > 0 ? args[0] : "ec-dump.txt";
        StringBuilder sb = new StringBuilder();
        try
        {
            ec = new LpcAcpiEc();
            sb.AppendLine("LpcAcpiEc 打开成功");

            // 三次 dump（间隔 2 秒），便于对比变化位
            byte[][] dumps = new byte[3][];
            for (int k = 0; k < 3; k++)
            {
                dumps[k] = DumpAll();
                sb.AppendLine("=== dump #" + (k + 1) + " ===");
                for (int row = 0; row < 16; row++)
                {
                    string hex = "";
                    for (int c = 0; c < 16; c++) hex += dumps[k][row * 16 + c].ToString("X2") + " ";
                    sb.AppendLine((row * 16).ToString("X2") + "0: " + hex);
                }
                if (k < 2) Thread.Sleep(2000);
            }

            // 转速匹配：游戏盒子当时显示约 CPU 2398 / GPU 2406 RPM（可能已变）
            // 找所有 16bit 组合在 1500-6000 范围且三次 dump 中变化或稳定的模式
            sb.AppendLine("");
            sb.AppendLine("=== 16bit RPM 模式候选（BE/LE，值 1200-6000） ===");
            for (int i = 0; i < 255; i++)
            {
                for (int endian = 0; endian < 2; endian++)
                {
                    bool allOk = true;
                    int[] vals = new int[3];
                    for (int k = 0; k < 3; k++)
                    {
                        int v = endian == 0
                            ? dumps[k][i] * 256 + dumps[k][i + 1]   // BE
                            : dumps[k][i] + dumps[k][i + 1] * 256;  // LE
                        if (v < 1200 || v > 6000) { allOk = false; break; }
                        vals[k] = v;
                    }
                    if (allOk)
                    {
                        sb.AppendLine("EC 0x" + i.ToString("X2") + (endian == 0 ? " BE: " : " LE: ") +
                            vals[0] + " / " + vals[1] + " / " + vals[2] + " RPM");
                    }
                }
            }

            // 变化位分析
            sb.AppendLine("");
            sb.AppendLine("=== 三次 dump 中变化的字节 ===");
            for (int i = 0; i < 256; i++)
            {
                if (dumps[0][i] != dumps[1][i] || dumps[1][i] != dumps[2][i])
                    sb.AppendLine("EC 0x" + i.ToString("X2") + ": " +
                        dumps[0][i].ToString("X2") + " -> " + dumps[1][i].ToString("X2") + " -> " + dumps[2][i].ToString("X2"));
            }

            ec.Close();
        }
        catch (Exception ex)
        {
            sb.AppendLine("FATAL: " + ex.ToString());
        }
        File.WriteAllText(outFile, sb.ToString());
        return 0;
    }
}
