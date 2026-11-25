// 界面状态快照 —— 主窗口某一刻要显示的全部数据（纯数据，不含 IO）。
// 真实运行时由 AppController 的轮询线程产生；离屏预览工具用 Sample()/Empty() 填充。
using System;
using System.Collections.Generic;

namespace MiCtl
{
    /// <summary>网络页要显示的全部信息</summary>
    public sealed class NetSnapshot
    {
        public bool HasLink;
        public string NicName, NicDesc, Ip, Gateway;
        public List<string> Others = new List<string>();

        public bool WifiConnected;
        public string Ssid = "";
        public string RadioType = "";
        public int? SignalPct;
        public int? RxMbps;

        public double DownKbps, UpKbps;
        public double SessionDownMB, SessionUpMB;

        public bool PingRunning;
        public int? PingGatewayMs, PingPublicMs;
        public DateTime? PingAt;

        public string NicText
        {
            get
            {
                if (!HasLink) return "未找到可用的网络连接";
                return NicName + "  " + NicDesc;
            }
        }

        public string WifiText
        {
            get
            {
                if (!WifiConnected) return "未连接无线网络";
                List<string> parts = new List<string>();
                if (Ssid.Length > 0) parts.Add(Ssid);
                if (RadioType.Length > 0) parts.Add(RadioType);
                if (SignalPct.HasValue) parts.Add("信号 " + SignalPct.Value + "%");
                if (RxMbps.HasValue) parts.Add(RxMbps.Value + " Mbps");
                return parts.Count > 0 ? string.Join("  ·  ", parts.ToArray()) : "已连接";
            }
        }

        public string PingText
        {
            get
            {
                if (PingRunning && !PingAt.HasValue) return "正在检测…";
                List<string> parts = new List<string>();
                if (PingGatewayMs.HasValue) parts.Add("网关 " + PingGatewayMs.Value + " ms");
                if (PingPublicMs.HasValue) parts.Add("公网 " + PingPublicMs.Value + " ms");
                if (parts.Count == 0) return PingAt.HasValue ? "没有响应" : "尚未检测";
                return string.Join("  ·  ", parts.ToArray());
            }
        }
    }

    public sealed class UiState
    {
        public bool MifsAvailable;
        public string MifsError;
        public MifsClient.Profile? Profile;

        public double? CpuTemp, GpuTemp, CpuPower, GpuPower, GpuLoad;
        public int? FanA, FanB;
        public double DownKbps, UpKbps;       // 兼容字段，与 Net.DownKbps/UpKbps 相同
        public string TempSource;              // "固件" / "传感器库" / ""

        public double? CpuLoad;                // %
        public double? CpuClockMHz;            // 各核平均
        public double? GpuClockMHz;
        public double? GpuMemUsedMB, GpuMemTotalMB;

        public double MemLoadPct, MemUsedGB, MemAvailGB, MemTotalGB;
        public double? MemSpeedMHz;

        public double? BatteryLevel, BatteryRateW, BatteryHealthPct;
        public string PowerText = "";          // "已接通电源" / "使用电池"

        public List<ProcessEntry> Processes = new List<ProcessEntry>();
        public int ProcessCount;

        public NetSnapshot Net = new NetSnapshot();

        public bool AutoEnabled;
        public double LowThreshold = 65, HighThreshold = 78;

        public bool TouchpadDeviceDisabled;    // 设备层（本程序通过 pnputil 控制的那一层）
        public int? TouchpadEcValue;           // 键盘快捷键那一层（EC 事件），null = 启动后还没收到过事件
        public bool WinKeyLocked;
        public int? FnLockValue;               // null = 未知；0 = 解锁（F1–F12 出音量等）、1 = 锁定（正常 F1）——语义已确认，value 对应待对照
        public int? GpuMode;                   // null = 未知；0 = 混合模式，1 = 独显直连

        public bool StartupEnabled;

        /// <summary>从运行中的控制器取一次快照。只读内存属性，不做任何 IO。</summary>
        public static UiState Capture(AppController app)
        {
            UiState s = new UiState();
            if (app == null) return s;

            s.MifsAvailable = app.Mifs != null && app.Mifs.Available;
            s.MifsError = app.Mifs != null ? app.Mifs.InitError : null;
            s.Profile = app.CurrentProfile;

            s.CpuTemp = app.CpuTemp;
            s.GpuTemp = app.GpuTemp;
            if (app.FanStatus != null)
            {
                s.FanA = app.FanStatus.CpuRpm;
                s.FanB = app.FanStatus.GpuRpm;
            }
            if (app.Sensors != null)
            {
                SensorService se = app.Sensors;
                s.GpuPower = se.GpuPower;
                s.GpuLoad = se.GpuLoad;
                s.CpuPower = se.CpuPower;
                s.CpuLoad = se.CpuLoad;
                s.CpuClockMHz = se.CpuClockMHz;
                s.GpuClockMHz = se.GpuClockMHz;
                s.GpuMemUsedMB = se.GpuMemUsedMB;
                s.GpuMemTotalMB = se.GpuMemTotalMB;
                s.MemLoadPct = se.MemLoadPct;
                s.MemUsedGB = se.MemUsedGB;
                s.MemAvailGB = se.MemAvailGB;
                s.MemTotalGB = se.MemTotalGB;
                s.MemSpeedMHz = se.MemSpeedMHz;
                s.BatteryLevel = se.BatteryLevel;
                s.BatteryRateW = se.BatteryRateW;
                s.BatteryHealthPct = se.BatteryHealthPct;

                s.Net.HasLink = !string.IsNullOrEmpty(se.LocalIPv4);
                s.Net.NicName = se.NicName;
                s.Net.NicDesc = se.NicDesc;
                s.Net.Ip = se.LocalIPv4;
                s.Net.Gateway = se.GatewayIPv4;
                if (se.OtherIPv4 != null) s.Net.Others = se.OtherIPv4;
                s.Net.DownKbps = se.DownKbps;
                s.Net.UpKbps = se.UpKbps;
                s.Net.SessionDownMB = se.SessionDownMB;
                s.Net.SessionUpMB = se.SessionUpMB;
                s.DownKbps = se.DownKbps;
                s.UpKbps = se.UpKbps;
            }
            if (app.Mifs != null && app.Mifs.Available)
                s.TempSource = app.FanStatus != null ? "固件" : "传感器库";

            if (app.Wifi != null)
            {
                s.Net.WifiConnected = app.Wifi.Connected;
                s.Net.Ssid = app.Wifi.Ssid ?? "";
                s.Net.RadioType = app.Wifi.RadioType ?? "";
                s.Net.SignalPct = app.Wifi.SignalPct;
                s.Net.RxMbps = app.Wifi.RxMbps;
            }
            s.Net.PingRunning = app.PingRunning;
            PingResult ping = app.LastPing;
            if (ping != null)
            {
                s.Net.PingAt = ping.At;
                s.Net.PingGatewayMs = ping.GatewayMs;
                s.Net.PingPublicMs = ping.PublicMs;
            }

            try
            {
                System.Windows.Forms.PowerLineStatus line = System.Windows.Forms.SystemInformation.PowerStatus.PowerLineStatus;
                s.PowerText = line == System.Windows.Forms.PowerLineStatus.Online ? "已接通电源" : "使用电池";
            }
            catch
            {
                s.PowerText = "";
            }

            if (app.Processes != null)
            {
                s.Processes = app.Processes;
                s.ProcessCount = app.ProcessCount;
            }

            if (app.Settings != null)
            {
                s.AutoEnabled = app.Settings.AutoEnabled;
                s.LowThreshold = app.Settings.LowThreshold;
                s.HighThreshold = app.Settings.HighThreshold;
            }

            s.TouchpadDeviceDisabled = app.TouchpadDeviceDisabled;
            if (app.FanWatcher != null)
            {
                s.TouchpadEcValue = app.FanWatcher.TouchpadEventValue;
                s.FnLockValue = app.FanWatcher.FnLockEventValue;
            }
            s.WinKeyLocked = app.WinHook != null && app.WinHook.Enabled;
            s.StartupEnabled = app.StartupEnabled;
            return s;
        }

        /// <summary>预览用：一组有代表性的运行中数据</summary>
        public static UiState Sample()
        {
            UiState s = new UiState();
            s.MifsAvailable = true;
            s.Profile = MifsClient.Profile.FullSpeed;
            s.CpuTemp = 64;
            s.GpuTemp = 49;
            s.CpuPower = 38.4;
            s.GpuPower = 96.2;
            s.GpuLoad = 13;
            s.CpuLoad = 16;
            s.CpuClockMHz = 3712;
            s.GpuClockMHz = 1567;
            s.GpuMemUsedMB = 2004;
            s.GpuMemTotalMB = 6144;
            s.FanA = 3028;
            s.FanB = 3026;
            s.MemLoadPct = 36;
            s.MemUsedGB = 11.53;
            s.MemAvailGB = 20.31;
            s.MemTotalGB = 31.84;
            s.MemSpeedMHz = 3200;
            s.BatteryLevel = 100;
            s.BatteryRateW = 0;
            s.BatteryHealthPct = 81.7;
            s.PowerText = "已接通电源";
            s.Processes = new List<ProcessEntry>
            {
                new ProcessEntry { Name = "firefox.exe", WorkingSet = 1016L * 1048576, Pids = new List<int> { 1, 2, 3 } },
                new ProcessEntry { Name = "AutoClaw2.exe", WorkingSet = 692L * 1048576, Pids = new List<int> { 4, 5 } },
                new ProcessEntry { Name = "Weixin.exe", WorkingSet = 403L * 1048576, Pids = new List<int> { 6 } },
                new ProcessEntry { Name = "steamwebhelper.exe", WorkingSet = 272L * 1048576, Pids = new List<int> { 7 } },
                new ProcessEntry { Name = "explorer.exe", WorkingSet = 230L * 1048576, Pids = new List<int> { 8 } },
            };
            s.ProcessCount = 291;
            s.Net.HasLink = true;
            s.Net.NicName = "WLAN";
            s.Net.NicDesc = "Intel(R) Wi-Fi 6E AX210 160MHz";
            s.Net.Ip = "192.168.1.146";
            s.Net.Gateway = "192.168.1.1";
            s.Net.Others = new List<string> { "vgate0  172.30.236.116" };
            s.Net.WifiConnected = true;
            s.Net.Ssid = "Nkxuii";
            s.Net.RadioType = "802.11ax";
            s.Net.SignalPct = 90;
            s.Net.RxMbps = 961;
            s.Net.DownKbps = 86;
            s.Net.UpKbps = 12.4 * 1024;
            s.Net.SessionDownMB = 128.4;
            s.Net.SessionUpMB = 46.9;
            s.Net.PingAt = DateTime.Now;
            s.Net.PingGatewayMs = 3;
            s.Net.PingPublicMs = 41;
            s.TempSource = "固件";
            s.AutoEnabled = true;
            s.LowThreshold = 65;
            s.HighThreshold = 78;
            s.TouchpadDeviceDisabled = false;
            s.TouchpadEcValue = 0;
            s.WinKeyLocked = true;
            s.FnLockValue = 0;
            s.GpuMode = 1;
            s.StartupEnabled = true;
            return s;
        }

        /// <summary>预览用：什么都还没读到、固件不可用</summary>
        public static UiState Empty()
        {
            UiState s = new UiState();
            s.PowerText = "使用电池";
            s.BatteryLevel = 63;
            s.BatteryRateW = 21.5;
            s.BatteryHealthPct = 81.7;
            return s;
        }
    }
}
