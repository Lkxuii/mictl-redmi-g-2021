// 入口 —— 单实例 + DPI 缩放初始化 + 托盘常驻
using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace MiCtl
{
    internal static class Program
    {
        private static Mutex _mutex;

        [STAThread]
        private static void Main()
        {
            bool createdNew;
            _mutex = new Mutex(true, "MiCtl_SingleInstance", out createdNew);
            if (!createdNew)
            {
                MessageBox.Show("MiCtl 已在运行（请查看任务栏托盘）", "MiCtl",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // 进程在 manifest 里声明为 DPI 感知；按屏幕 DPI 把逻辑像素换算成物理像素。
            // 字体用像素单位，所有尺寸统一经 Theme.S() 缩放，避免系统位图拉伸导致的模糊。
            try
            {
                using (Graphics g = Graphics.FromHwnd(IntPtr.Zero))
                {
                    Theme.Scale = Math.Max(1f, g.DpiX / 96f);
                }
            }
            catch
            {
                Theme.Scale = 1f;
            }

            AppController app = new AppController();
            try
            {
                app.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show("初始化失败：\r\n" + ex.Message, "MiCtl",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            TrayContext tray = new TrayContext(app);
            Application.Run(tray);
            app.Dispose();
            GC.KeepAlive(_mutex);
        }
    }
}
