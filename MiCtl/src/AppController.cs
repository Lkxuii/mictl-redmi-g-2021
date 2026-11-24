// 应用协调器 —— 轮询线程：传感器采样 + 档位同步 + 自动切档执行
using System;
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

            // EC 状态事件（触控板/Fn 锁/Win 锁/风扇转速变化）：立即刷新状态并通知 UI
            FanWatcher.StateChanged += delegate
            {
                Raise(ProfileChanged);   // 档位 / Fn 锁等状态
                Raise(SensorsUpdated);   // 触控板开关 / 风扇转速
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
            if (Sensors.Opened) Sensors.Poll();

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
                //     触控板与 Fn 锁的状态变化由 EC 事件推送，风扇转速不在事件里、只能 fn9 轮询

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

            Raise(SensorsUpdated);
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
            // 手动切换后重置自动防抖计时，避免立刻被自动逻辑改回去
            _lastAutoChange = DateTime.Now.AddSeconds(-AutoProfile.MinHoldSeconds);
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
