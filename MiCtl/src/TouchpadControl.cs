// 触控板控制 —— 设备层禁用/启用精密触控板 HID 设备。
// 实现：cfgmgr32 的 CM_Locate_DevNode/CM_Disable_DevNode/CM_Enable_DevNode，进程内直接调用。
// 早期版本用 pnputil 子进程，但未签名的 MiCtl 启动 pnputil 禁用设备会被火绒的行为防御拦下（拒绝访问），
// 而 CM_ API 无子进程特征。若 CM_ API 也被拦，只能靠把 MiCtl.exe 加入火绒的行为白名单。
// 本机 BIOS 不支持 MIFS fn12，EC 层的触控板锁只能键盘 Fn+F9（fn10 只有"启用"方向）。
using System;
using System.Collections.Generic;
using System.Management;
using System.Runtime.InteropServices;
using System.Threading;

namespace MiCtl
{
    public static class TouchpadControl
    {
        /// <summary>
        /// 枚举内置精密触控板的设备实例 ID（HID\MSFT0001 为微软精密触控板标准标识）。
        /// 只取触摸板功能集合（COL02）与鼠标集合（COL01），不影响外接鼠标。
        /// </summary>
        public static List<string> GetTouchpadInstanceIds()
        {
            List<string> ids = new List<string>();
            try
            {
                using (ManagementObjectSearcher searcher =
                    new ManagementObjectSearcher("SELECT DeviceID, Name, ConfigManagerErrorCode FROM Win32_PnPEntity"))
                {
                    foreach (ManagementObject o in searcher.Get())
                    {
                        string id = (o["DeviceID"] as string) ?? "";
                        string name = (o["Name"] as string) ?? "";
                        if (id.StartsWith("HID\\MSFT0001", StringComparison.OrdinalIgnoreCase) &&
                            (name.IndexOf("触摸板", StringComparison.Ordinal) >= 0 ||
                             name.IndexOf("touch pad", StringComparison.OrdinalIgnoreCase) >= 0 ||
                             name.IndexOf("touchpad", StringComparison.OrdinalIgnoreCase) >= 0 ||
                             (id.Contains("&COL01") || id.Contains("&COL02"))))
                        {
                            ids.Add(id);
                        }
                    }
                }
            }
            catch { }
            return ids;
        }

        /// <summary>触控板当前是否被禁用（ConfigManagerErrorCode=22 为 CM 已禁用）</summary>
        public static bool IsDisabled()
        {
            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(
                    "SELECT DeviceID, ConfigManagerErrorCode FROM Win32_PnPEntity WHERE DeviceID LIKE 'HID\\MSFT0001%'"))
                {
                    foreach (ManagementObject o in searcher.Get())
                    {
                        uint err = Convert.ToUInt32(o["ConfigManagerErrorCode"]);
                        if (err == 22) return true; // 任一集合被禁用即视为禁用
                    }
                }
            }
            catch { }
            return false;
        }

        [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
        private static extern int CM_Locate_DevNodeW(out IntPtr pdnDevInst, string pDeviceID, uint ulFlags);

        [DllImport("cfgmgr32.dll")]
        private static extern int CM_Disable_DevNode(IntPtr dnDevInst, uint ulFlags);

        [DllImport("cfgmgr32.dll")]
        private static extern int CM_Enable_DevNode(IntPtr dnDevInst, uint ulFlags);

        private const uint CM_LOCATE_DEVNODE_NORMAL = 0x00000000;

        // ---- SetupDi 路径（设备管理器同款：SetClassInstallParams + CallClassInstaller）----
        [StructLayout(LayoutKind.Sequential)]
        private struct SP_DEVINFO_DATA { public int cbSize; public Guid classGuid; public IntPtr devInst; }

        [StructLayout(LayoutKind.Sequential)]
        private struct SP_CLASSINSTALL_HEADER { public int cbSize; public uint devInst; }

        [StructLayout(LayoutKind.Sequential)]
        private struct SP_PROPCHANGE_PARAMS { public SP_CLASSINSTALL_HEADER header; public uint stateChange; public uint scope; }

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern IntPtr SetupDiCreateDeviceInfoList(IntPtr classGuid, IntPtr hwndParent);

        [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool SetupDiOpenDeviceInfoW(IntPtr deviceInfoSet, string deviceInstanceId, IntPtr hwndParent, uint openFlags, ref SP_DEVINFO_DATA deviceInfoData);

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiSetClassInstallParams(IntPtr deviceInfoSet, ref SP_DEVINFO_DATA deviceInfoData, ref SP_PROPCHANGE_PARAMS propertyChangeParams, int paramsSize);

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiCallClassInstaller(uint installFunction, IntPtr deviceInfoSet, ref SP_DEVINFO_DATA deviceInfoData);

        [DllImport("setupapi.dll")]
        private static extern bool SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);

        private const uint DIF_PROPERTYCHANGE = 0x12;
        private const uint DICS_ENABLE = 1;
        private const uint DICS_DISABLE = 2;
        private const uint DICS_FLAG_CONFIGSPECIFIC = 2;

        /// <summary>SetupDi 路径：设备管理器同款的双步流程（SetClassInstallParams + CallClassInstaller）</summary>
        private static bool SetDeviceEnabledSetupDi(string deviceId, bool enable)
        {
            IntPtr set = SetupDiCreateDeviceInfoList(IntPtr.Zero, IntPtr.Zero);
            if (set == IntPtr.Zero || set == new IntPtr(-1)) return false;
            try
            {
                SP_DEVINFO_DATA did = new SP_DEVINFO_DATA();
                did.cbSize = Marshal.SizeOf(typeof(SP_DEVINFO_DATA));
                if (!SetupDiOpenDeviceInfoW(set, deviceId, IntPtr.Zero, 0, ref did)) return false;

                SP_CLASSINSTALL_HEADER hdr = new SP_CLASSINSTALL_HEADER();
                hdr.cbSize = Marshal.SizeOf(typeof(SP_CLASSINSTALL_HEADER));
                hdr.devInst = (uint)did.devInst.ToInt64();
                SP_PROPCHANGE_PARAMS pcp = new SP_PROPCHANGE_PARAMS();
                pcp.header = hdr;
                pcp.stateChange = enable ? DICS_ENABLE : DICS_DISABLE;
                pcp.scope = DICS_FLAG_CONFIGSPECIFIC;
                if (!SetupDiSetClassInstallParams(set, ref did, ref pcp, Marshal.SizeOf(typeof(SP_PROPCHANGE_PARAMS)))) return false;
                if (!SetupDiCallClassInstaller(DIF_PROPERTYCHANGE, set, ref did)) return false;
                return true;
            }
            finally
            {
                SetupDiDestroyDeviceInfoList(set);
            }
        }

        /// <summary>CM_ 路径（cfgmgr32）：SetupDi 被拒时的回退</summary>
        private static bool SetDeviceEnabledCm(string deviceId, bool enable)
        {
            IntPtr dn;
            int cr = CM_Locate_DevNodeW(out dn, deviceId, CM_LOCATE_DEVNODE_NORMAL);
            if (cr != 0) return false;                       // CR_SUCCESS = 0
            cr = enable ? CM_Enable_DevNode(dn, 0) : CM_Disable_DevNode(dn, 0);
            return cr == 0;
        }

        /// <summary>对单个设备实例执行禁用/启用：先 SetupDi（设备管理器同款），被拒再回退 CM_</summary>
        private static bool SetDeviceEnabled(string deviceId, bool enable)
        {
            if (SetDeviceEnabledSetupDi(deviceId, enable)) return true;
            return SetDeviceEnabledCm(deviceId, enable);
        }

        public static bool SetDisabled(bool disable)
        {
            try
            {
                List<string> ids = GetTouchpadInstanceIds();
                if (ids.Count == 0) return false;
                int ok = 0;
                foreach (string id in ids)
                {
                    if (SetDeviceEnabled(id, !disable)) ok++;
                }
                if (ok == 0) return false;
                // 设备状态的切换是异步生效的：复查实际状态，没切过去就算失败
                //（否则开关会显示"已启用"但触控板不动）。
                Thread.Sleep(1500);
                bool now = IsDisabled();
                return disable == now;
            }
            catch
            {
                return false;
            }
        }
    }
}
