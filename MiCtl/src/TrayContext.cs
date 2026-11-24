// 托盘上下文 —— NotifyIcon + 深色右键菜单 + 图标动态绘制
using System;
using System.Drawing;
using System.Windows.Forms;

namespace MiCtl
{
    /// <summary>深色菜单配色表</summary>
    internal sealed class DarkColorTable : ProfessionalColorTable
    {
        public override Color MenuItemSelected { get { return Color.FromArgb(46, 46, 62); } }
        public override Color MenuItemBorder { get { return Color.FromArgb(58, 58, 76); } }
        public override Color ToolStripDropDownBackground { get { return Color.FromArgb(32, 32, 44); } }
        public override Color ImageMarginGradientBegin { get { return Color.FromArgb(32, 32, 44); } }
        public override Color ImageMarginGradientMiddle { get { return Color.FromArgb(32, 32, 44); } }
        public override Color ImageMarginGradientEnd { get { return Color.FromArgb(32, 32, 44); } }
        public override Color MenuBorder { get { return Color.FromArgb(48, 48, 62); } }
        public override Color SeparatorDark { get { return Color.FromArgb(48, 48, 62); } }
        public override Color SeparatorLight { get { return Color.FromArgb(48, 48, 62); } }
        public override Color CheckBackground { get { return Theme.Accent; } }
        public override Color MenuItemSelectedGradientBegin { get { return Color.FromArgb(46, 46, 62); } }
        public override Color MenuItemSelectedGradientEnd { get { return Color.FromArgb(46, 46, 62); } }
        public override Color MenuItemPressedGradientBegin { get { return Color.FromArgb(40, 40, 54); } }
        public override Color MenuItemPressedGradientEnd { get { return Color.FromArgb(40, 40, 54); } }
    }

    public sealed class TrayContext : ApplicationContext
    {
        private readonly AppController _app;
        private readonly NotifyIcon _icon;
        private MainWindow _mainWindow;

        private ToolStripMenuItem _miQuiet, _miBalanced, _miFull, _miAuto;

        public TrayContext(AppController app)
        {
            _app = app;

            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Renderer = new ToolStripProfessionalRenderer(new DarkColorTable());
            menu.BackColor = Theme.Card;
            menu.ForeColor = Theme.TextMain;
            menu.ShowImageMargin = false;

            _miQuiet = Item("静谧模式", delegate { Switch(MifsClient.Profile.Quiet); });
            _miBalanced = Item("均衡模式", delegate { Switch(MifsClient.Profile.Balanced); });
            _miFull = Item("极速模式", delegate { Switch(MifsClient.Profile.FullSpeed); });

            _miAuto = Item("温度自动联动",
                delegate
                {
                    _app.SetAutoEnabled(!_app.Settings.AutoEnabled);
                    RefreshMenuChecks();
                });
            _miAuto.Checked = _app.Settings.AutoEnabled;

            ToolStripMenuItem open = Item("打开控制台", delegate { ShowMain(); });
            ToolStripMenuItem startup = null;
            startup = Item("开机自启", delegate
            {
                StartupManager.Toggle();
                startup.Checked = StartupManager.IsEnabled();
            });
            startup.Checked = StartupManager.IsEnabled();

            ToolStripMenuItem exit = Item("退出", delegate { ExitApp(); });

            menu.Items.Add(_miQuiet);
            menu.Items.Add(_miBalanced);
            menu.Items.Add(_miFull);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(_miAuto);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(open);
            menu.Items.Add(startup);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(exit);

            _icon = new NotifyIcon();
            _icon.Icon = MakeIcon(MifsClient.Profile.Balanced);
            _icon.Text = "MiCtl";
            _icon.ContextMenuStrip = menu;
            _icon.Visible = true;
            _icon.DoubleClick += delegate { ShowMain(); };

            RefreshMenuChecks();
            RefreshTooltip();

            _app.SensorsUpdated += delegate { RefreshTooltip(); };
            _app.ProfileChanged += delegate
            {
                RefreshMenuChecks();
                RefreshIcon();
                RefreshTooltip();
            };
        }

        private static ToolStripMenuItem Item(string text, EventHandler onClick)
        {
            ToolStripMenuItem mi = new ToolStripMenuItem(text, null, onClick);
            mi.ForeColor = Theme.TextMain;
            return mi;
        }

        private void Switch(MifsClient.Profile p)
        {
            if (!_app.Mifs.Available)
            {
                MessageBox.Show("MIFS 接口不可用：" + _app.Mifs.InitError, "MiCtl",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            _app.SwitchProfile(p);
        }

        private void RefreshMenuChecks()
        {
            MifsClient.Profile? p = _app.CurrentProfile;
            _miQuiet.Checked = p == MifsClient.Profile.Quiet;
            _miBalanced.Checked = p == MifsClient.Profile.Balanced;
            _miFull.Checked = p == MifsClient.Profile.FullSpeed;
            _miAuto.Checked = _app.Settings.AutoEnabled;
        }

        private void RefreshTooltip()
        {
            string cpu = _app.CpuTemp.HasValue ? _app.CpuTemp.Value.ToString("0") : "--";
            string gpu = _app.GpuTemp.HasValue ? _app.GpuTemp.Value.ToString("0") : "--";
            _icon.Text = "MiCtl [" + ProfileName(_app.CurrentProfile) + "]  CPU " + cpu + "°C  GPU " + gpu + "°C";
            if (_icon.Text.Length > 63) _icon.Text = _icon.Text.Substring(0, 63);
        }

        private string ProfileName(MifsClient.Profile? p)
        {
            if (!p.HasValue) return "未知";
            switch (p.Value)
            {
                case MifsClient.Profile.Quiet: return "静谧";
                case MifsClient.Profile.Balanced: return "均衡";
                case MifsClient.Profile.FullSpeed: return "极速";
                default: return "未知";
            }
        }

        private void RefreshIcon()
        {
            try { _icon.Icon.Dispose(); } catch { }
            _icon.Icon = MakeIcon(_app.CurrentProfile ?? MifsClient.Profile.Balanced);
        }

        // 运行时绘制托盘图标：档位颜色圆点，自动联动时外加圆环
        private Icon MakeIcon(MifsClient.Profile p)
        {
            Color c;
            switch (p)
            {
                case MifsClient.Profile.Quiet: c = Theme.Green; break;
                case MifsClient.Profile.Balanced: c = Theme.Blue; break;
                case MifsClient.Profile.FullSpeed: c = Theme.Red; break;
                default: c = Color.FromArgb(158, 158, 158); break;
            }
            using (Bitmap bmp = new Bitmap(16, 16))
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                using (SolidBrush b = new SolidBrush(c))
                    g.FillEllipse(b, 2, 2, 12, 12);
                if (_app.Settings.AutoEnabled)
                using (Pen pen = new Pen(Color.White, 1.6f))
                    g.DrawEllipse(pen, 1, 1, 14, 14);
                using (Font f = new Font("Segoe UI", 7f, FontStyle.Bold))
                using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    g.DrawString("M", f, Brushes.White, new RectangleF(0, 0, 16, 16), sf);
                return Icon.FromHandle(bmp.GetHicon());
            }
        }

        private void ShowMain()
        {
            if (_mainWindow == null || _mainWindow.IsDisposed)
                _mainWindow = new MainWindow(_app);
            _mainWindow.Show();
            _mainWindow.Activate();
        }

        private void ExitApp()
        {
            _icon.Visible = false;
            _icon.Dispose();
            Application.Exit();
        }
    }

    /// <summary>开机自启：计划任务（最高权限，免 UAC）</summary>
    public static class StartupManager
    {
        private const string TaskName = "MiCtl_AutoStart";

        public static bool IsEnabled()
        {
            try
            {
                System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo();
                psi.FileName = "schtasks";
                psi.Arguments = "/Query /TN \"" + TaskName + "\"";
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true;
                using (System.Diagnostics.Process p = System.Diagnostics.Process.Start(psi))
                {
                    p.WaitForExit(3000);
                    return p.ExitCode == 0;
                }
            }
            catch { return false; }
        }

        public static void Toggle()
        {
            try
            {
                if (IsEnabled())
                {
                    Run("schtasks /Delete /TN \"" + TaskName + "\" /F");
                }
                else
                {
                    string exe = System.Reflection.Assembly.GetExecutingAssembly().Location;
                    Run("schtasks /Create /TN \"" + TaskName + "\" /TR \"\\\"" + exe + "\\\"\" /SC ONLOGON /RL HIGHEST /F");
                }
            }
            catch { }
        }

        private static void Run(string args)
        {
            System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo();
            psi.FileName = "cmd.exe";
            psi.Arguments = "/c " + args;
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            using (System.Diagnostics.Process p = System.Diagnostics.Process.Start(psi))
                p.WaitForExit(5000);
        }
    }
}
