// 端到端检查：直接调用 AppController，读出它算好、准备交给界面显示的值
// （不经过界面控件；界面本身需要提权的 UI Automation 才能读，这里不做）
// 需要 PawnIO 可用（读 CPU 温度/功耗），以管理员运行。用法：AppCheck.exe <输出文件>
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using MiCtl;

class AppCheck
{
    static string Fmt(double? v) { return v.HasValue ? v.Value.ToString("0.#") : "--"; }
    static string FmtI(int? v) { return v.HasValue ? v.Value.ToString() : "--"; }

    static int Main(string[] args)
    {
        string outFile = args.Length > 0 ? args[0] : "appcheck.txt";
        StringBuilder sb = new StringBuilder();
        try
        {
            using (AppController app = new AppController())
            {
                app.Start();
                for (int i = 1; i <= 5; i++)
                {
                    Thread.Sleep(2200); // 一个轮询周期
                    SensorService se = app.Sensors;
                    sb.AppendLine("[第 " + i + " 次轮询后]");
                    sb.AppendLine("  界面将显示的温度 : CPU=" + Fmt(app.CpuTemp) + "°C  GPU=" + Fmt(app.GpuTemp) + "°C");
                    sb.AppendLine("  界面将显示的风扇 : " +
                        (app.FanStatus == null ? "(无数据，界面不显示风扇)" :
                            app.FanStatus.CpuRpm + " / " + app.FanStatus.GpuRpm + " RPM"));
                    sb.AppendLine("  固件包原始值     : " +
                        (app.FanStatus == null ? "(null)" :
                            "风扇A=" + app.FanStatus.CpuRpm + " 风扇B=" + app.FanStatus.GpuRpm +
                            " CPU=" + app.FanStatus.CpuTemp + "°C GPU=" + app.FanStatus.GpuTemp + "°C"));
                    sb.AppendLine("  CPU              : 温度=" + Fmt(se.CpuTemp) + "°C  负载=" + Fmt(se.CpuLoad) + "%  频率=" +
                        Fmt(se.CpuClockMHz) + " MHz  功耗=" + Fmt(se.CpuPower) + " W");
                    sb.AppendLine("  GPU              : 温度=" + Fmt(se.GpuTemp) + "°C  负载=" + Fmt(se.GpuLoad) + "%  核心频率=" +
                        Fmt(se.GpuClockMHz) + " MHz  功耗=" + Fmt(se.GpuPower) + " W  显存=" + Fmt(se.GpuMemUsedMB) + " / " +
                        Fmt(se.GpuMemTotalMB) + " MB");
                    sb.AppendLine("  内存             : " + Fmt(se.MemLoadPct) + "%  已用 " + Fmt(se.MemUsedGB) + " / " +
                        Fmt(se.MemTotalGB) + " GB  可用 " + Fmt(se.MemAvailGB) + " GB  频率 " + Fmt(se.MemSpeedMHz) + "MHz");
                    sb.AppendLine("  电池             : " + Fmt(se.BatteryLevel) + "%  功率 " + Fmt(se.BatteryRateW) + " W  健康度 " +
                        Fmt(se.BatteryHealthPct) + "%");
                    sb.AppendLine("  网络             : 网卡=" + (se.NicName ?? "(无)") + " [" + (se.NicDesc ?? "") + "]  IP=" +
                        (se.LocalIPv4 ?? "(无)") + "  网关=" + (se.GatewayIPv4 ?? "(无)") +
                        "  ↓" + Fmt(se.DownKbps) + " ↑" + Fmt(se.UpKbps) + " KB/s" +
                        (se.OtherIPv4.Count > 0 ? "  其它地址=" + string.Join("; ", se.OtherIPv4.ToArray()) : ""));
                    sb.AppendLine("  WiFi             : " + (app.Wifi == null ? "(本轮还没采集)" :
                        (app.Wifi.Connected ? "已连接" : "未连接") + "  SSID=" + app.Wifi.Ssid + "  信号=" + FmtI(app.Wifi.SignalPct) +
                        "%  " + app.Wifi.RadioType + "  " + FmtI(app.Wifi.RxMbps) + " Mbps"));
                    sb.AppendLine("  延迟             : " + (app.PingRunning ? "检测中 " : "") +
                        (app.LastPing == null ? "(还没有结果)" :
                            "网关 " + FmtI(app.LastPing.GatewayMs) + " ms  公网 " + FmtI(app.LastPing.PublicMs) + " ms" +
                            (string.IsNullOrEmpty(app.LastPing.Error) ? "" : "  错误=" + app.LastPing.Error)));
                    sb.AppendLine("  进程             : " + app.ProcessCount + " 个" +
                        (app.Processes == null ? " (还没采集)" : "  前 5: " +
                            string.Join(", ", app.Processes.Select(p => p.Name + " " + p.MemoryText + " x" + p.Pids.Count).ToArray())));
                }
                sb.AppendLine();
                sb.AppendLine("固件接口可用: " + app.Mifs.Available + (app.Mifs.Available ? "" : "  (" + app.Mifs.InitError + ")"));
                sb.AppendLine("当前档位     : " + app.CurrentProfile);
                sb.AppendLine("事件监听已启动: " + (app.FanWatcher != null && app.FanWatcher.Started));
                sb.AppendLine("开机自启任务 : " + app.StartupEnabled);
                sb.AppendLine("触控板设备禁用: " + app.TouchpadDeviceDisabled);
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine("FATAL: " + ex);
        }
        File.WriteAllText(outFile, sb.ToString(), new UTF8Encoding(false));
        return 0;
    }
}
