// 界面离屏预览 —— 把 MainWindow 按指定缩放画成 PNG。
// MiCtl 以管理员运行，截图 / UI Automation 都读不到它的窗口；这个工具用 null 控制器构造主窗口、
// 灌入一份样例 UiState，再用 DrawToBitmap 离屏渲染，得到与屏幕渲染逐像素一致的画面。
// 用法：UiPreview.exe <scale> <out.png> [sample|empty] [page]
//   page: 0 = 控制（默认），1 = 系统，2 = 网络
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Windows.Forms;

namespace MiCtl
{
    public static class UiPreview
    {
        [STAThread]
        public static int Main(string[] args)
        {
            float scale = args.Length > 0 ? float.Parse(args[0], CultureInfo.InvariantCulture) : 1f;
            string outPath = args.Length > 1 ? args[1] : "ui-preview.png";
            string mode = args.Length > 2 ? args[2] : "sample";
            int page = args.Length > 3 ? int.Parse(args[3], CultureInfo.InvariantCulture) : 0;

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Theme.Scale = scale;

            using (MainWindow w = new MainWindow(null))
            {
                w.ApplyState(mode == "empty" ? UiState.Empty() : UiState.Sample());
                if (page != 0) w.SelectPage(page);
                w.PerformLayout();
                w.FinishAnimations();

                // 子控件只有在窗口可见时才会创建句柄；把窗口放到屏幕外显示一下再离屏渲染
                w.ShowInTaskbar = false;
                w.StartPosition = FormStartPosition.Manual;
                w.Location = new Point(-20000, -20000);
                w.Show();
                Application.DoEvents();
                w.FinishAnimations();

                using (Bitmap bmp = new Bitmap(w.Width, w.Height, PixelFormat.Format32bppArgb))
                {
                    w.DrawToBitmap(bmp, new Rectangle(0, 0, w.Width, w.Height));
                    string full = Path.GetFullPath(outPath);
                    bmp.Save(full, ImageFormat.Png);
                    Console.WriteLine("saved " + full + " " + bmp.Width + "x" + bmp.Height);
                }
                w.Close();
            }
            return 0;
        }
    }
}
