// 传感器服务 —— LibreHardwareMonitor 封装 + 内存 + 电池 + 网速
// CPU: 温度与 Package 功耗需要内核驱动（本机 WinRing0 被拦截，实际走 PawnIO 2.1.0）；负载与频率不需要
// GPU: NVML 温度/功耗/负载/核心频率/显存（免内核驱动）
// 内存: GlobalMemoryStatusEx；内存频率: WMI（启动时读一次）；电池: 传感器库的 Battery 硬件
// 内核驱动不可用时库返回 0 而不是 null：这里按"读不到"处理，其余指标不受影响
using System;
using System.Collections.Generic;
using System.Management;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Threading;
using LibreHardwareMonitor.Hardware;

namespace MiCtl
{
    public sealed class SensorService : IDisposable
    {
        [StructLayout(LayoutKind.Sequential)]
        private class MEMORY_INFO
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GlobalMemoryStatusEx([In, Out] MEMORY_INFO mi);

        private Computer _computer;
        private readonly object _lock = new object();

        public bool Opened { get; private set; }
        public string OpenError { get; private set; }

        // ---- 传感器库 ----
        public double? CpuTemp { get; private set; }
        public double? GpuTemp { get; private set; }
        public double? GpuPower { get; private set; }
        public double? GpuLoad { get; private set; }
        public double? CpuPower { get; private set; }
        public double? CpuLoad { get; private set; }        // %，"CPU Total"
        public double? CpuClockMHz { get; private set; }    // 各核平均时钟，"Cores (Average)"
        public double? GpuClockMHz { get; private set; }    // GPU 核心时钟
        public double? GpuMemUsedMB { get; private set; }
        public double? GpuMemTotalMB { get; private set; }

        // ---- 电池 ----
        public double? BatteryLevel { get; private set; }   // %
        public double? BatteryRateW { get; private set; }   // 充放电功率（绝对值）
        public double? BatteryHealthPct { get; private set; }

        // ---- 内存 ----
        public double MemLoadPct { get; private set; }
        public double MemUsedGB { get; private set; }
        public double MemAvailGB { get; private set; }
        public double MemTotalGB { get; private set; }
        public double? MemSpeedMHz { get; private set; }    // 启动时读一次

        // ---- 网络 ----
        public double DownKbps { get; private set; }
        public double UpKbps { get; private set; }
        public double SessionDownMB { get; private set; }
        public double SessionUpMB { get; private set; }
        public string NicName { get; private set; }
        public string NicDesc { get; private set; }
        public string LocalIPv4 { get; private set; }
        public string GatewayIPv4 { get; private set; }
        public List<string> OtherIPv4 { get; private set; }
        private string _lastNicId;
        private long _lastRecv = -1, _lastSent = -1;
        private long _baseRecv = -1, _baseSent = -1;
        private DateTime _lastNetAt = DateTime.MinValue;

        public SensorService()
        {
            OtherIPv4 = new List<string>();
            try
            {
                _computer = new Computer
                {
                    IsCpuEnabled = true,
                    IsGpuEnabled = true,
                    IsMemoryEnabled = false,      // 需要 RAMSPDToolkit-NDD.dll，内存改走系统接口
                    IsMotherboardEnabled = false,
                    IsStorageEnabled = false,
                    IsNetworkEnabled = false,
                    IsControllerEnabled = false,
                    IsPsuEnabled = false,
                    IsBatteryEnabled = true
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
            MemSpeedMHz = ReadMemorySpeedMHz();
        }

        private static double? ReadMemorySpeedMHz()
        {
            try
            {
                double? best = null;
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(
                    "SELECT ConfiguredClockSpeed FROM Win32_PhysicalMemory"))
                {
                    foreach (ManagementObject o in searcher.Get())
                    {
                        object v = o["ConfiguredClockSpeed"];
                        if (v != null)
                        {
                            double d = Convert.ToDouble(v);
                            if (d > 0 && (!best.HasValue || d > best.Value)) best = d;
                        }
                    }
                }
                return best;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>轮询全部传感器并刷新属性值</summary>
        public void Poll()
        {
            double? cpu = null, gpu = null, gpuP = null, gpuL = null, cpuP = null;
            double? cpuLoad = null, cpuClock = null, gpuClock = null, gpuMemUsed = null, gpuMemTotal = null;
            double? battLevel = null, battRate = null, designed = null, fullCharge = null;
            lock (_lock)
            {
                if (_computer != null)
                {
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
                                    else if (s.SensorType == SensorType.Load && s.Name == "CPU Total")
                                    {
                                        cpuLoad = s.Value.Value;
                                    }
                                    else if (s.SensorType == SensorType.Clock && s.Name == "Cores (Average)")
                                    {
                                        cpuClock = s.Value.Value;
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
                                    else if (s.SensorType == SensorType.Power && s.Name != null &&
                                             s.Name.IndexOf("package", StringComparison.OrdinalIgnoreCase) >= 0)
                                    {
                                        gpuP = s.Value.Value;
                                    }
                                    else if (s.SensorType == SensorType.Load && s.Name != null &&
                                             s.Name.IndexOf("core", StringComparison.OrdinalIgnoreCase) >= 0)
                                    {
                                        gpuL = s.Value.Value;
                                    }
                                    else if (s.SensorType == SensorType.Clock && s.Name == "GPU Core")
                                    {
                                        gpuClock = s.Value.Value;
                                    }
                                    else if (s.SensorType == SensorType.SmallData && s.Name == "GPU Memory Used")
                                    {
                                        gpuMemUsed = s.Value.Value;
                                    }
                                    else if (s.SensorType == SensorType.SmallData && s.Name == "GPU Memory Total")
                                    {
                                        gpuMemTotal = s.Value.Value;
                                    }
                                }
                            }
                            else if (hw.HardwareType == HardwareType.Battery)
                            {
                                foreach (ISensor s in hw.Sensors)
                                {
                                    if (!s.Value.HasValue) continue;
                                    if (s.SensorType == SensorType.Level && s.Name == "Charge Level")
                                        battLevel = s.Value.Value;
                                    else if (s.SensorType == SensorType.Power && s.Name == "Charge/Discharge Rate")
                                        battRate = Math.Abs(s.Value.Value) / 1000.0;     // mW → W
                                    else if (s.SensorType == SensorType.Energy && s.Name == "Designed Capacity")
                                        designed = s.Value.Value;
                                    else if (s.SensorType == SensorType.Energy && s.Name == "Fully-Charged Capacity")
                                        fullCharge = s.Value.Value;
                                }
                            }
                        }
                        catch { }
                    }
                }
            }
            // 传感器库在驱动不可用时返回 0 而不是 null，按"读不到"处理
            CpuTemp = cpu > 0.5 ? cpu : null;
            GpuTemp = gpu > 0.5 ? gpu : null;
            GpuPower = gpuP > 0.05 ? gpuP : null;
            GpuLoad = gpuL;
            CpuPower = cpuP > 0.05 ? cpuP : null;
            CpuLoad = cpuLoad >= 0 ? cpuLoad : null;
            CpuClockMHz = cpuClock > 0 ? cpuClock : null;
            GpuClockMHz = gpuClock > 0 ? gpuClock : null;
            GpuMemUsedMB = gpuMemUsed >= 0 ? gpuMemUsed : null;
            GpuMemTotalMB = gpuMemTotal > 0 ? gpuMemTotal : null;
            BatteryLevel = battLevel >= 0 ? battLevel : null;
            BatteryRateW = battRate >= 0 ? battRate : null;
            BatteryHealthPct = (designed > 0 && fullCharge > 0) ? (fullCharge / designed) * 100.0 : (double?)null;

            ReadMemory();
            PollNetwork();
        }

        private void ReadMemory()
        {
            try
            {
                MEMORY_INFO mi = new MEMORY_INFO();
                mi.dwLength = (uint)Marshal.SizeOf(typeof(MEMORY_INFO));
                if (!GlobalMemoryStatusEx(mi)) return;
                MemLoadPct = mi.dwMemoryLoad;
                MemTotalGB = mi.ullTotalPhys / 1073741824.0;
                MemAvailGB = mi.ullAvailPhys / 1073741824.0;
                MemUsedGB = MemTotalGB - MemAvailGB;
            }
            catch { }
        }

        private static bool IsVirtualNic(NetworkInterface nic)
        {
            NetworkInterfaceType t = nic.NetworkInterfaceType;
            if (t == NetworkInterfaceType.Loopback || t == NetworkInterfaceType.Tunnel) return true;
            int ti = (int)t;
            if (ti == 53 || ti == 131 || ti == 1) return true;     // propVirtual / tunnel / other
            string d = nic.Description ?? "";
            foreach (string k in new[] { "virtual", "wintun", "tap-", "tunnel", "vpn", "hyper-v", "vmware", "virtualbox", "bluetooth" })
            {
                if (d.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            return false;
        }

        private static bool HasIPv4(NetworkInterface nic, bool requireGateway, out string ipv4)
        {
            ipv4 = null;
            try
            {
                IPInterfaceProperties ip = nic.GetIPProperties();
                foreach (UnicastIPAddressInformation a in ip.UnicastAddresses)
                {
                    if (a.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && ipv4 == null)
                        ipv4 = a.Address.ToString();
                }
                if (requireGateway && ip.GatewayAddresses.Count == 0) return false;
                return ipv4 != null;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 真正上网的那块网卡：优先"已连接、非虚拟、有 IPv4 网关"。
        /// 只统计它的流量，避免 TUN 代理这类虚拟网卡把同一份流量再算一遍。
        /// </summary>
        private static NetworkInterface FindDefaultNic()
        {
            NetworkInterface[] nics = NetworkInterface.GetAllNetworkInterfaces();
            NetworkInterface fallback = null, anyWithIp = null;
            string tmp;
            foreach (NetworkInterface nic in nics)
            {
                if (nic.OperationalStatus != OperationalStatus.Up || nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                bool isVirtual = IsVirtualNic(nic);
                if (!isVirtual && HasIPv4(nic, true, out tmp)) return nic;
                if (!isVirtual && fallback == null && HasIPv4(nic, false, out tmp)) fallback = nic;
                if (anyWithIp == null && HasIPv4(nic, false, out tmp)) anyWithIp = nic;
            }
            return fallback ?? anyWithIp;
        }

        private void PollNetwork()
        {
            try
            {
                NetworkInterface nic = FindDefaultNic();
                List<string> others = new List<string>();
                if (nic == null)
                {
                    DownKbps = 0;
                    UpKbps = 0;
                    NicName = null;
                    NicDesc = null;
                    LocalIPv4 = null;
                    GatewayIPv4 = null;
                    OtherIPv4 = others;
                    return;
                }

                if (!string.Equals(_lastNicId, nic.Id, StringComparison.Ordinal))
                {
                    // 换了网卡：速率与累计都从新网卡重新起算
                    _lastNicId = nic.Id;
                    _lastRecv = -1;
                    _lastSent = -1;
                    _baseRecv = -1;
                    _baseSent = -1;
                }

                NicName = nic.Name;
                NicDesc = nic.Description;
                IPInterfaceProperties props = nic.GetIPProperties();
                string gw = null;
                foreach (GatewayIPAddressInformation g in props.GatewayAddresses)
                {
                    if (g.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork) { gw = g.Address.ToString(); break; }
                }
                GatewayIPv4 = gw;
                string localIp;
                HasIPv4(nic, false, out localIp);
                LocalIPv4 = localIp;

                foreach (NetworkInterface other in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (other.Id == nic.Id || other.OperationalStatus != OperationalStatus.Up) continue;
                    string ip;
                    if (HasIPv4(other, false, out ip) && ip != "127.0.0.1")
                        others.Add(other.Name + "  " + ip);
                }
                OtherIPv4 = others;

                IPv4InterfaceStatistics st = nic.GetIPv4Statistics();
                long recv = st.BytesReceived;
                long sent = st.BytesSent;
                DateTime now = DateTime.Now;
                if (_baseRecv < 0) { _baseRecv = recv; _baseSent = sent; }
                SessionDownMB = Math.Max(0, (recv - _baseRecv) / 1048576.0);
                SessionUpMB = Math.Max(0, (sent - _baseSent) / 1048576.0);
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
