// 端到端检查：直接调用 MiCtl.exe 里的 AppController，读出它算好、准备交给界面显示的值
// （不经过界面控件；界面本身需要提权的 UI Automation 才能读，这里不做）
using System;
using System.IO;
using System.Text;
using System.Threading;
using MiCtl;

class AppCheck
{
    static string Fmt(double? v) { return v.HasValue ? v.Value.ToString("0.#") : "--"; }

    static int Main(string[] args)
    {
        string outFile = args.Length > 0 ? args[0] : "appcheck.txt";
        StringBuilder sb = new StringBuilder();
        try
        {
            using (AppController app = new AppController())
            {
                app.Start();
                for (int i = 1; i <= 4; i++)
                {
                    Thread.Sleep(2200); // 一个轮询周期
                    sb.AppendLine("[第 " + i + " 次轮询后]");
                    sb.AppendLine("  界面将显示的温度 : CPU=" + Fmt(app.CpuTemp) + "°C  GPU=" + Fmt(app.GpuTemp) + "°C");
                    sb.AppendLine("  界面将显示的风扇 : " +
                        (app.FanStatus == null ? "(无数据，界面不显示风扇)" :
                            app.FanStatus.CpuRpm + " / " + app.FanStatus.GpuRpm + " RPM"));
                    sb.AppendLine("  固件包原始值     : " +
                        (app.FanStatus == null ? "(null)" :
                            "风扇A=" + app.FanStatus.CpuRpm + " 风扇B=" + app.FanStatus.GpuRpm +
                            " CPU=" + app.FanStatus.CpuTemp + "°C GPU=" + app.FanStatus.GpuTemp + "°C"));
                    sb.AppendLine("  传感器库原始值   : CPU=" + Fmt(app.Sensors.CpuTemp) + "°C  GPU=" + Fmt(app.Sensors.GpuTemp) +
                        "°C  GPU负载=" + Fmt(app.Sensors.GpuLoad) + "%  GPU功耗=" + Fmt(app.Sensors.GpuPower) +
                        "W  CPU功耗=" + Fmt(app.Sensors.CpuPower) + "W");
                }
                sb.AppendLine("");
                sb.AppendLine("固件接口可用: " + app.Mifs.Available + (app.Mifs.Available ? "" : "  (" + app.Mifs.InitError + ")"));
                sb.AppendLine("当前档位     : " + app.CurrentProfile);
                sb.AppendLine("事件监听已启动: " + (app.FanWatcher != null && app.FanWatcher.Started));
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine("FATAL: " + ex);
        }
        File.WriteAllText(outFile, sb.ToString(), Encoding.UTF8);
        return 0;
    }
}
