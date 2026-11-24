// 入口 —— 单实例 + 托盘常驻
using System;
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
