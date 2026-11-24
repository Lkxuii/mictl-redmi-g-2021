// 传感器服务 —— LibreHardwareMonitor 封装
// CPU: AMD SMN 温度（Tctl/Tdie），需 WinRing0 内核驱动（LHM 内嵌）
// GPU: NVML 温度/功耗/负载（免内核驱动）
// WinRing0 加载失败时优雅降级：GPU 温度仍可用
using System;
using System.Threading;
using LibreHardwareMonitor.Hardware;

namespace MiCtl
{
    public sealed class SensorService : IDisposable
    {
        private Computer _computer;
        private readonly object _lock = new object();

        public bool Opened { get; private set; }
        public string OpenError { get; private set; }

        public double? CpuTemp { get; private set; }
        public double? GpuTemp { get; private set; }
        public double? GpuPower { get; private set; }
        public double? GpuLoad { get; private set; }
        public double? CpuPower { get; private set; }

        // 网速（KB/s，所有活动物理网卡求和，回环/隧道除外）
        public double DownKbps { get; private set; }
        public double UpKbps { get; private set; }
        private long _lastRecv = -1;
        private long _lastSent = -1;
        private DateTime _lastNetAt = DateTime.MinValue;

        public SensorService()
        {
            try
            {
                _computer = new Computer
                {
                    IsCpuEnabled = true,
                    IsGpuEnabled = true,
                    IsMemoryEnabled = false,
                    IsMotherboardEnabled = false,
                    IsStorageEnabled = false,
                    IsNetworkEnabled = false,
                    IsControllerEnabled = false,
                    IsPsuEnabled = false,
                    IsBatteryEnabled = false
                };
                _computer.Open();
                Opened = true;
            }
            catch (Exception ex)
            {
                Opened = false;
                OpenError = ex.Message;
                _computer = null;
            }
        }

        /// <summary>轮询全部传感器并刷新属性值</summary>
        public void Poll()
        {
            double? cpu = null, gpu = null, gpuP = null, gpuL = null, cpuP = null;
            lock (_lock)
            {
                if (_computer == null) return;
                foreach (IHardware hw in _computer.Hardware)
                {
                    try
                    {
                        hw.Update();
                        if (hw.HardwareType == HardwareType.Cpu)
                        {
                            foreach (ISensor s in hw.Sensors)
                            {
                                if (!s.Value.HasValue) continue;
                                if (s.SensorType == SensorType.Temperature)
                                {
                                    // 取 CPU 各温度源的最大值（Tctl/Tdie/CCD）
                                    if (!cpu.HasValue || s.Value.Value > cpu.Value) cpu = s.Value.Value;
                                }
                                else if (s.SensorType == SensorType.Power && s.Name != null &&
                                         s.Name.IndexOf("package", StringComparison.OrdinalIgnoreCase) >= 0)
                                {
                                    cpuP = s.Value.Value;
                                }
                            }
                        }
                        else if (hw.HardwareType == HardwareType.GpuNvidia ||
                                 hw.HardwareType == HardwareType.GpuAmd ||
                                 hw.HardwareType == HardwareType.GpuIntel)
                        {
                            foreach (ISensor s in hw.Sensors)
                            {
                                if (!s.Value.HasValue) continue;
                                if (s.SensorType == SensorType.Temperature)
                                {
                                    // 只认 "GPU Core"（与 nvidia-smi / 任务管理器是同一个传感器）。
                                    // 不能让其它温度传感器覆盖：GPU Hot Spot 空闲时就比 Core 高 3~4°C
                                    if (s.Name == "GPU Core")
                                        gpu = s.Value.Value;
                                    else if (!gpu.HasValue)
                                        gpu = s.Value.Value;
                                }
                                else if (s.SensorType == SensorType.Power &&
                                         s.Name != null && s.Name.IndexOf("package", StringComparison.OrdinalIgnoreCase) >= 0)
                                {
                                    gpuP = s.Value.Value;
                                }
                                else if (s.SensorType == SensorType.Load &&
                                         s.Name != null && s.Name.IndexOf("core", StringComparison.OrdinalIgnoreCase) >= 0)
                                {
                                    gpuL = s.Value.Value;
                                }
                            }
                        }

                        // 子硬件（部分 GPU 传感器在子硬件上）
                        foreach (IHardware sub in hw.SubHardware)
                        {
                            try
                            {
                                sub.Update();
                                foreach (ISensor s in sub.Sensors)
                                {
                                    if (!s.Value.HasValue) continue;
                                    if (s.SensorType == SensorType.Temperature && gpu == null)
                                        gpu = s.Value.Value;
                                }
                            }
                            catch { }
                        }
                    }
                    catch { }
                }
            }
            // 传感器库在驱动不可用时返回 0 而不是 null，按"读不到"处理
            CpuTemp = cpu > 0.5 ? cpu : null;
            GpuTemp = gpu > 0.5 ? gpu : null;
            GpuPower = gpuP > 0.05 ? gpuP : null;
            GpuLoad = gpuL;
            CpuPower = cpuP > 0.05 ? cpuP : null;

            PollNetwork();
        }

        private void PollNetwork()
        {
            try
            {
                long recv = 0, sent = 0;
                System.Net.NetworkInformation.NetworkInterface[] nics =
                    System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces();
                foreach (System.Net.NetworkInformation.NetworkInterface nic in nics)
                {
                    if (nic.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up)
                        continue;
                    if (nic.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Loopback ||
                        nic.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Tunnel)
                        continue;
                    System.Net.NetworkInformation.IPv4InterfaceStatistics st = nic.GetIPv4Statistics();
                    recv += st.BytesReceived;
                    sent += st.BytesSent;
                }
                DateTime now = DateTime.Now;
                if (_lastRecv >= 0)
                {
                    double sec = (now - _lastNetAt).TotalSeconds;
                    if (sec > 0.3)
                    {
                        DownKbps = Math.Max(0, (recv - _lastRecv) / sec / 1024.0);
                        UpKbps = Math.Max(0, (sent - _lastSent) / sec / 1024.0);
                    }
                }
                _lastRecv = recv;
                _lastSent = sent;
                _lastNetAt = now;
            }
            catch { }
        }

        public void Dispose()
        {
            lock (_lock)
            {
                if (_computer != null)
                {
                    try { _computer.Close(); } catch { }
                    _computer = null;
                }
                Opened = false;
            }
        }
    }
}
