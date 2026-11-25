// EC 状态事件监听 —— 订阅 MIFS WMI 事件（HID_EVENT20）
// 事件结构：{ u8 event_type; u8 event_id; u8 value_low; u8 value_high; u8 reserved[4]; }
//   event_type 1 = 状态通知；event id:
//     5  键盘背光亮度变化      6  触控板状态变化
//     7  Fn 锁状态变化（语义与 value 对应均已确认：锁定 = 正常 F1，解锁 = F1–F12 出音量/亮度；
//        value 1 = 锁定、value 0 = 解锁，2026-11-26 对照确认）
//     15 性能档位变化
//     25 刷新率变化            26 CPU 风扇转速（16bit RPM，低字节在前）
//     32 GPU 风扇转速          33 Win 键锁状态变化
// 本机实测（一份 68 个事件的日志）：只出现过 id 6 / 7 / 10，没有收到过风扇转速事件（26/32），
// 所以风扇转速靠 fn9 轮询（见 MifsClient.GetFanTempStatus），这里的 26/32 分支只是保留。
using System;
using System.Management;
using System.Threading;

namespace MiCtl
{
    public sealed class FanSpeedWatcher : IDisposable
    {
        private ManagementEventWatcher _watcher;
        private readonly object _lock = new object();

        public int? CpuRpm { get; private set; }
        public int? GpuRpm { get; private set; }
        public int? FnLockEventValue { get; private set; }      // Fn 锁（EC 侧）
        public int? TouchpadEventValue { get; private set; }    // 触控板（EC 侧）
        public int? WinLockEventValue { get; private set; }     // Win 键锁（EC 侧）
        public bool Started { get; private set; }
        public DateTime LastEventAt { get; private set; }

        /// <summary>任何状态事件到达（风扇/触控板/Fn 锁/Win 锁）</summary>
        public event Action StateChanged;

        public void Start()
        {
            lock (_lock)
            {
                if (Started) return;
                try
                {
                    ManagementScope scope = new ManagementScope(@"root\WMI");
                    scope.Connect();
                    _watcher = new ManagementEventWatcher(scope, new WqlEventQuery("SELECT * FROM HID_EVENT20"));
                    _watcher.EventArrived += OnEvent;
                    _watcher.Start();
                    Started = true;
                }
                catch
                {
                    Started = false;
                    _watcher = null;
                }
            }
        }

        private void OnEvent(object sender, EventArrivedEventArgs e)
        {
            try
            {
                bool stateChanged = false;
                foreach (PropertyData pd in e.NewEvent.Properties)
                {
                    byte[] b = pd.Value as byte[];
                    if (b == null || b.Length < 8) continue;

                    byte eventType = b[0];
                    byte eventId = b[1];
                    if (eventType != 1) continue;

                    switch (eventId)
                    {
                        case 26: // CPU 风扇转速
                            CpuRpm = b[2] | (b[3] << 8);
                            stateChanged = true;
                            break;
                        case 32: // GPU 风扇转速
                            GpuRpm = b[2] | (b[3] << 8);
                            stateChanged = true;
                            break;
                        case 6: // 触控板状态（键盘快捷键切换）
                            TouchpadEventValue = b[2];
                            stateChanged = true;
                            break;
                        case 7: // Fn 锁状态（Fn+Esc 切换）
                            FnLockEventValue = b[2];
                            stateChanged = true;
                            break;
                        case 33: // Win 键锁状态
                            WinLockEventValue = b[2];
                            stateChanged = true;
                            break;
                    }
                }
                if (stateChanged)
                {
                    LastEventAt = DateTime.Now;
                    if (StateChanged != null) StateChanged();
                }
            }
            catch { }
        }

        public void Dispose()
        {
            lock (_lock)
            {
                if (_watcher != null)
                {
                    try { _watcher.Stop(); _watcher.Dispose(); } catch { }
                    _watcher = null;
                }
                Started = false;
            }
        }
    }
}
