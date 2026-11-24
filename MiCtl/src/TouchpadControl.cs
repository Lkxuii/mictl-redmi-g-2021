// 触控板控制 —— 通过 pnputil 禁用/启用精密触控板 HID 设备
// 本机 BIOS 不支持 MIFS fn12，改用 Windows 设备层控制
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Management;

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

        private static int RunPnP(string args)
        {
            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = "pnputil.exe";
            psi.Arguments = args;
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.RedirectStandardOutput = true;
            using (Process p = Process.Start(psi))
            {
                p.StandardOutput.ReadToEnd();
                p.WaitForExit(8000);
                return p.ExitCode;
            }
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
                    string verb = disable ? "/disable-device" : "/enable-device";
                    int rc = RunPnP(verb + " \"" + id + "\"");
                    if (rc == 0) ok++;
                }
                return ok > 0;
            }
            catch
            {
                return false;
            }
        }
    }
}
