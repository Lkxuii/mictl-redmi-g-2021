// 应用协调器 —— 轮询线程：传感器采样 + 档位同步 + 自动切档执行
using System;
using System.Collections.Generic;
using System.Threading;

namespace MiCtl
{
    public sealed class AppController : IDisposable
    {
        public MifsClient Mifs { get; private set; }
        public SensorService Sensors { get; private set; }
        public AppSettings Settings { get; private set; }
        public FanSpeedWatcher FanWatcher { get; private set; }
        public WinKeyHook WinHook { get; private set; }
        public MifsClient.FanTempStatus FanStatus { get; private set; }

        /// <summary>触控板在设备层是否被禁用（pnputil 那一层；键盘快捷键那一层走 EC 事件，见 FanSpeedWatcher）</summary>
        public bool TouchpadDeviceDisabled { get; private set; }
        /// <summary>开机自启计划任务是否存在。启动后在后台线程读一次，之后由 RefreshDeviceStates() 刷新</summary>
        public bool StartupEnabled { get; private set; }
        private bool _startupLoaded;

        // ---- 低频采集（都由轮询线程或线程池执行，界面线程只读）----
        /// <summary>内存占用前几名的进程（同名合并），每 4 秒刷新</summary>
        public List<ProcessEntry> Processes { get; private set; }
        public int ProcessCount { get; private set; }
        /// <summary>无线连接信息（netsh），每 16 秒刷新</summary>
        public WifiInfo Wifi { get; private set; }
        /// <summary>最近一次延迟检测</summary>
        public PingResult LastPing { get; private set; }
        private bool _pingRunning;
        public bool PingRunning { get { return _pingRunning; } }
        private int _tickIndex;

        public MifsClient.Profile? CurrentProfile { get; private set; }

        /// <summary>
        /// 显示与自动联动实际使用的温度（°C）：固件上报（fn9，与游戏盒子同源）优先，
        /// 其次 LHM/NVML；两者都读不到为 null
        /// </summary>
        public double? CpuTemp { get; private set; }
        public double? GpuTemp { get; private set; }

        private Thread _worker;
        private volatile bool _stop;
        private DateTime _lastAutoChange = DateTime.MinValue;

        public event Action SensorsUpdated;
        public event Action ProfileChanged;
        public event Action ProfileSwitchResult; // 手动切换完成（成功/失败均通知 UI 刷新）

        public void Start()
        {
            Settings = AppSettings.Load();
            Mifs = new MifsClient();
            Sensors = new SensorService();
            FanWatcher = new FanSpeedWatcher();
            FanWatcher.Start();
            WinHook = new WinKeyHook();

            // EC 状态事件（触控板/Fn 锁状态变化）：立即刷新状态并通知 UI
            FanWatcher.StateChanged += delegate
            {
                Raise(ProfileChanged);   // Fn 锁等状态
                Raise(SensorsUpdated);   // 触控板 EC 状态
            };

            if (Mifs.Available)
                CurrentProfile = Mifs.GetProfile();

            _stop = false;
            _worker = new Thread(WorkerLoop);
            _worker.IsBackground = true;
            _worker.Start();
        }

        private void WorkerLoop()
        {
            while (!_stop)
            {
                try
                {
                    Tick();
                }
                catch { }
                Thread.Sleep(2000);
            }
        }

        private void Tick()
        {
            // 内存、网速不依赖传感器库是否打开，所以总是轮询（Poll 内部自己判断）
            Sensors.Poll();

            // 风扇转速 + 固件上报的温度（fn9 轮询，游戏盒子同源数据）
            if (Mifs.Available)
                FanStatus = Mifs.GetFanTempStatus();

            // 显示/联动用的温度：固件上报优先，其次传感器库（LHM/NVML）
            CpuTemp = FanStatus != null && FanStatus.CpuTemp > 0
                ? (double?)FanStatus.CpuTemp : Sensors.CpuTemp;
            GpuTemp = FanStatus != null && FanStatus.GpuTemp > 0
                ? (double?)FanStatus.GpuTemp : Sensors.GpuTemp;

            // 同步实际档位（覆盖 Fn+K / GamingBox 等外部切换）
            if (Mifs.Available)
            {
                MifsClient.Profile? p = Mifs.GetProfile();
                if (p.HasValue && p.Value != CurrentProfile)
                {
                    CurrentProfile = p.Value;
                    Raise(ProfileChanged);
                }
                // 注：本机 MIFS 固件只响应 fn8 档位 / fn9 风扇温度 / fn10 / fn11（全枚举验证）；
                //     Fn 锁状态变化由 EC 事件推送，风扇转速不在事件里、只能 fn9 轮询

                // 自动切档
                if (Settings.AutoEnabled && CurrentProfile.HasValue)
                {
                    double? temp = MaxTemp();
                    bool onBattery = System.Windows.Forms.SystemInformation.PowerStatus.PowerLineStatus
                                     != System.Windows.Forms.PowerLineStatus.Online;
                    MifsClient.Profile? target = AutoProfile.Evaluate(
                        temp, CurrentProfile.Value, _lastAutoChange, onBattery,
                        Settings.LowThreshold, Settings.HighThreshold);
                    if (target.HasValue)
                    {
                        if (Mifs.SetProfile(target.Value))
                        {
                            _lastAutoChange = DateTime.Now;
                            CurrentProfile = Mifs.GetProfile() ?? target;
                            Raise(ProfileChanged);
                        }
                    }
                }
            }

            // 设备层状态：WMI 查询放在后台线程，UI 线程只读属性（要在通知界面之前刷新）
            RefreshDeviceStates(true);

            // 低频采集：进程每 4 秒、WiFi 每 16 秒、延迟启动后一次之后每分钟（ping 在线程池里做）
            _tickIndex++;
            try
            {
                if (_tickIndex % 2 == 1) RefreshProcessesNow();
                if (_tickIndex % 8 == 2) Wifi = SystemInfo.QueryWifi();
                if (_tickIndex == 1 || _tickIndex % 30 == 1) StartPing();
            }
            catch { }

            Raise(SensorsUpdated);
        }

        /// <summary>重新取内存占用前 5 的进程（同名合并）。枚举全部进程约几十毫秒。</summary>
        public void RefreshProcessesNow()
        {
            try
            {
                int n;
                Processes = SystemInfo.TopProcesses(5, out n);
                ProcessCount = n;
            }
            catch { }
        }

        /// <summary>结束一个进程条目下的全部进程；失败时返回给用户看的原因</summary>
        public string KillProcesses(ProcessEntry e)
        {
            string err = SystemInfo.Kill(e);
            RefreshProcessesNow();
            return err;
        }

        /// <summary>延迟检测（ping 网关与公网），在线程池里跑，完成后发 SensorsUpdated</summary>
        public void StartPing()
        {
            if (_pingRunning) return;
            _pingRunning = true;
            string gateway = Sensors != null ? Sensors.GatewayIPv4 : null;
            ThreadPool.QueueUserWorkItem(delegate
            {
                PingResult r = SystemInfo.Ping(gateway);
                LastPing = r;
                _pingRunning = false;
                Raise(SensorsUpdated);
            });
        }

        /// <summary>
        /// 刷新触控板设备状态与自启任务状态。
        /// startupOnlyOnce = true 时只在第一次调用时查询计划任务（轮询线程用），用户切换自启后传 false 立即复查。
        /// </summary>
        public void RefreshDeviceStates(bool startupOnlyOnce = false)
        {
            try { TouchpadDeviceDisabled = TouchpadControl.IsDisabled(); }
            catch { }
            if (!startupOnlyOnce || !_startupLoaded)
            {
                _startupLoaded = true;
                try { StartupEnabled = StartupManager.IsEnabled(); }
                catch { }
            }
        }

        private double? MaxTemp()
        {
            double? t = CpuTemp;
            if (GpuTemp.HasValue && (!t.HasValue || GpuTemp.Value > t.Value))
                t = GpuTemp.Value;
            return t;
        }

        /// <summary>手动切换档位（UI/托盘调用）</summary>
        public bool SwitchProfile(MifsClient.Profile p)
        {
            if (!Mifs.Available) return false;
            bool ok = Mifs.SetProfile(p);
            MifsClient.Profile? now = Mifs.GetProfile();
            if (now.HasValue) CurrentProfile = now.Value;
            // 手动切换后从此刻起重新计 15 秒防抖：这段时间内自动联动不再改动档位
            _lastAutoChange = DateTime.Now;
            Raise(ProfileChanged);
            Raise(ProfileSwitchResult);
            return ok;
        }

        /// <summary>切换自动联动开关</summary>
        public void SetAutoEnabled(bool on)
        {
            Settings.AutoEnabled = on;
            Settings.Save();
            _lastAutoChange = DateTime.MinValue; // 打开后立即允许评估
        }

        public void SaveSettings()
        {
            Settings.Save();
        }

        /// <summary>切换 Win 键锁（键盘钩子）</summary>
        public bool SetWinKeyLock(bool on)
        {
            try
            {
                if (on) WinHook.Enable(); else WinHook.Disable();
                return WinHook.Enabled == on;
            }
            catch { return false; }
        }

        public void Raise(Action e)
        {
            Action h = e;
            if (h != null) h();
        }

        public void Dispose()
        {
            _stop = true;
            if (_worker != null) _worker.Join(3000);
            if (FanWatcher != null) FanWatcher.Dispose();
            if (WinHook != null) WinHook.Dispose();
            if (Sensors != null) Sensors.Dispose();
            if (Mifs != null) Mifs.Dispose();
        }
    }
}
