// 主窗口 —— 无边框深色"仪表"界面
// 结构：
//   标题栏
//   仪表卡片：CPU / GPU / 内存三个环 + CPU、GPU 各一行数据（负载 · 频率 · 功耗 · 风扇）
//   页签：控制 / 系统 / 网络（窗口高度随页签变化）
//     控制：性能档位 / 温度自动联动 / 快捷控制 / GPU 模式 / 开机自启 / 状态行
//     系统：内存与电池概况 / 内存占用前 5 的进程（可结束）/ 打开任务管理器
//     网络：上下行速率 / 网卡 / 接入点 / 本地 IP / 延迟 / 其它地址
// 主窗口只读 UiState、只通过 AppController 的公开方法执行动作；AppController 为 null 时是离屏预览模式（见 probe/UiPreview.cs）。
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Management;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace MiCtl
{
    public sealed class MainWindow : Form
    {
        // ---- 逻辑像素（96 DPI 基准，经 Theme.S() 缩放）----
        private const int W = 400, M = 20, CW = 360;
        private const int CardY = 60, CardH = 188;
        private const int TabY = 266, TabH = 36;
        private const int PageY = 316;
        private const int RowH = 38;                       // 控制页列表行高
        private const int ToggleRight = W - M - 44;
        private static readonly int[] PageHeights = { 440, 274, 252 };   // 控制 / 系统 / 网络

        private static readonly string[] PageNames = { "控制", "系统", "网络" };
        private static readonly MifsClient.Profile[] ProfileOrder =
        {
            MifsClient.Profile.Quiet, MifsClient.Profile.Balanced, MifsClient.Profile.FullSpeed
        };
        private static readonly string[] GpuModeNames = { "混合模式", "独显直连" };
        private const int ProcRows = 5;
        private const int ProcRowH = 30;
        private const int ProcRow0 = 102;

        private readonly AppController _app;
        private UiState _state = new UiState();
        private bool _suppress;
        private int? _gpuModeDetected;

        // 仪表
        private GaugeRing _gaugeCpu, _gaugeGpu, _gaugeMem;
        private Label _lblCpuLoad, _lblCpuClock, _lblCpuPwr, _lblCpuFan;
        private Label _lblGpuLoad, _lblGpuClock, _lblGpuPwr, _lblGpuFan;

        // 页签与页面
        private TabStrip _tabs;
        private Panel _pageControl, _pageSystem, _pageNetwork;

        // 控制页
        private Segmented _segProfiles;
        private ToggleSwitch _tglAuto, _tglTouchpad, _tglWinKey, _tglStartup;
        private Stepper _stpLow, _stpHigh;
        private StatusPill _pillTouchpadEc, _pillFnLock;
        private GhostButton _btnUnlockFn;
        private ToolTip _tips;
        private Label _lblGpuModeValue;
        private Label _lblFnNote, _lblGpuModeNote, _lblStatus;

        // 系统页
        private Label _lblMemDetail, _lblBattDetail, _lblProcCount, _lblProcEmpty;
        private TextButton _btnTaskMgr;
        private readonly Label[] _procName = new Label[ProcRows];
        private readonly Label[] _procMem = new Label[ProcRows];
        private readonly GhostButton[] _procKill = new GhostButton[ProcRows];
        private readonly Label[] _procLine = new Label[ProcRows];

        // 网络页
        private Label _lblDownNum, _lblDownUnit, _lblUpNum, _lblUpUnit, _lblSession;
        private Label _lblNetNic, _lblNetWifi, _lblNetIp, _lblNetPing, _lblNetOthers;
        private Panel _rowNetWifi, _rowNetOthers;
        private Panel _rowTmp;
        private TextButton _btnPing;

        [DllImport("user32.dll")] private static extern bool ReleaseCapture();
        [DllImport("user32.dll")] private static extern int SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);
        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HTCAPTION = 0x2;

        public MainWindow(AppController app)
        {
            _app = app;

            Text = "MiCtl";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Theme.Bg;
            DoubleBuffered = true;
            KeyPreview = true;
            Font = Theme.F(Theme.Cn, 13);

            BuildUi();

            _gpuModeDetected = DetectGpuMode();

            if (_app != null)
            {
                _app.SensorsUpdated += OnAppUpdated;
                _app.ProfileChanged += OnAppUpdated;
            }
            FormClosed += delegate
            {
                if (_app == null) return;
                _app.SensorsUpdated -= OnAppUpdated;
                _app.ProfileChanged -= OnAppUpdated;
            };

            SetPage(0);
            ApplyState(_app != null ? UiState.Capture(_app) : UiState.Empty());
        }

        // ---- 无边框拖动 ----
        private void DragWindow(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(Handle, WM_NCLBUTTONDOWN, HTCAPTION, 0);
            }
        }

        // ---------------- 小工具 ----------------
        private static Label Lbl(string text, string family, float px, Color color, int x, int y)
        {
            Label l = new Label();
            l.Text = text;
            l.Font = Theme.F(family, px);
            l.ForeColor = color;
            l.AutoSize = true;
            l.BackColor = Color.Transparent;
            l.UseMnemonic = false;
            l.Location = Theme.P(x, y);
            return l;
        }

        /// <summary>定宽、超长显示省略号的标签</summary>
        private static Label LblEllipsis(string text, float px, Color color, int x, int y, int w)
        {
            Label l = Lbl(text, Theme.Cn, px, color, x, y);
            l.AutoSize = false;
            l.AutoEllipsis = true;
            l.Size = Theme.Sz(w, 18);
            return l;
        }

        private static void PlaceRight(Control c, int rightX, int y)
        {
            c.Left = Theme.S(rightX) - c.Width;
            c.Top = Theme.S(y);
        }

        private static void AddDivider(Control parent, int x, int y, int w)
        {
            Label line = new Label();
            line.AutoSize = false;
            line.Size = new Size(Theme.S(w), 1);          // 分隔线固定 1 物理像素
            line.Location = new Point(Theme.S(x), Theme.S(y));
            line.BackColor = Theme.CardBorder;
            parent.Controls.Add(line);
        }

        /// <summary>详情行：一个行容器（可整行隐藏）+ 标题 + 定宽值</summary>
        private static void MakeRow(Control parent, string title, int y, int valueX, int valueW,
            out Panel row, out Label value)
        {
            row = new Panel();
            row.Bounds = Theme.R(0, y, W, ProcRowH);
            row.BackColor = Theme.Bg;
            parent.Controls.Add(row);
            row.Controls.Add(Lbl(title, Theme.Cn, 11, Theme.TextDim, M, 8));
            value = LblEllipsis("", 11.5f, Theme.TextMain, valueX, 7, valueW);
            row.Controls.Add(value);
        }

        // ---------------- UI 构建 ----------------
        private void BuildUi()
        {
            _tips = new ToolTip();

            // ===== 标题栏 =====
            Panel bar = new Panel();
            bar.Bounds = Theme.R(0, 0, W, 48);
            bar.BackColor = Theme.Bg;
            bar.MouseDown += delegate (object s, MouseEventArgs e) { DragWindow(e); };
            bar.MouseMove += delegate (object s, MouseEventArgs e) { if (e.Button == MouseButtons.Left) DragWindow(e); };
            Controls.Add(bar);
            bar.Controls.Add(Lbl("MiCtl", Theme.UiSemibold, 15, Theme.TextMain, 20, 14));
            bar.Controls.Add(Lbl("Redmi G 2021", Theme.Ui, 12, Theme.TextDim, 66, 17));

            IconButton btnMin = new IconButton("\uE921");   // ChromeMinimize
            btnMin.Bounds = Theme.R(W - 88, 8, 36, 32);
            btnMin.Click += delegate { WindowState = FormWindowState.Minimized; };
            IconButton btnClose = new IconButton("\uE8BB");  // ChromeClose
            btnClose.Bounds = Theme.R(W - 48, 8, 36, 32);
            btnClose.Click += delegate { Hide(); };
            bar.Controls.Add(btnMin);
            bar.Controls.Add(btnClose);

            BuildDashboard();
            BuildTabs();
            BuildControlPage();
            BuildSystemPage();
            BuildNetworkPage();
        }

        // ===== 仪表卡片：三个环 + CPU / GPU 各一行数据 =====
        private void BuildDashboard()
        {
            RoundedCard card = new RoundedCard();
            card.Bounds = Theme.R(M, CardY, CW, CardH);
            Controls.Add(card);

            _gaugeCpu = new GaugeRing("CPU");
            _gaugeCpu.Bounds = Theme.R(16, 8, 100, 114);
            card.Controls.Add(_gaugeCpu);
            _gaugeGpu = new GaugeRing("GPU");
            _gaugeGpu.Bounds = Theme.R(129, 8, 100, 114);
            card.Controls.Add(_gaugeGpu);
            _gaugeMem = new GaugeRing("内存");
            _gaugeMem.SetMode("%", false, Theme.Blue);
            _gaugeMem.Bounds = Theme.R(242, 8, 100, 114);
            card.Controls.Add(_gaugeMem);

            AddDivider(card, 20, 128, CW - 40);

            // 列：标签 20 | 负载 56 | 频率 100 | 功耗 184 | 风扇 232（等宽数字，列自然对齐）
            card.Controls.Add(Lbl("CPU", Theme.Cn, 11, Theme.TextDim, 20, 138));
            _lblCpuLoad = Lbl("--", Theme.Mono, 12, Theme.TextMain, 56, 137);
            _lblCpuClock = Lbl("--", Theme.Mono, 12, Theme.TextMain, 100, 137);
            _lblCpuPwr = Lbl("--", Theme.Mono, 12, Theme.TextMain, 184, 137);
            _lblCpuFan = Lbl("--", Theme.Mono, 12, Theme.TextMain, 232, 137);
            card.Controls.Add(_lblCpuLoad);
            card.Controls.Add(_lblCpuClock);
            card.Controls.Add(_lblCpuPwr);
            card.Controls.Add(_lblCpuFan);

            card.Controls.Add(Lbl("GPU", Theme.Cn, 11, Theme.TextDim, 20, 158));
            _lblGpuLoad = Lbl("--", Theme.Mono, 12, Theme.TextMain, 56, 157);
            _lblGpuClock = Lbl("--", Theme.Mono, 12, Theme.TextMain, 100, 157);
            _lblGpuPwr = Lbl("--", Theme.Mono, 12, Theme.TextMain, 184, 157);
            _lblGpuFan = Lbl("--", Theme.Mono, 12, Theme.TextMain, 232, 157);
            card.Controls.Add(_lblGpuLoad);
            card.Controls.Add(_lblGpuClock);
            card.Controls.Add(_lblGpuPwr);
            card.Controls.Add(_lblGpuFan);
        }

        // ===== 页签 =====
        private void BuildTabs()
        {
            _tabs = new TabStrip(PageNames);
            _tabs.Bounds = Theme.R(M, TabY, CW, TabH);
            _tabs.SelectedChanged += delegate { SetPage(_tabs.SelectedIndex); };
            Controls.Add(_tabs);

            int maxH = 0;
            foreach (int h in PageHeights) if (h > maxH) maxH = h;
            _pageControl = NewPage(maxH);
            _pageSystem = NewPage(maxH);
            _pageNetwork = NewPage(maxH);
        }

        private Panel NewPage(int h)
        {
            Panel p = new Panel();
            p.Bounds = Theme.R(0, PageY, W, h);
            p.BackColor = Theme.Bg;
            Controls.Add(p);
            return p;
        }

        private void SetPage(int i)
        {
            if (_tabs == null) return;
            if (i < 0 || i >= PageHeights.Length) i = 0;
            _pageControl.Visible = i == 0;
            _pageSystem.Visible = i == 1;
            _pageNetwork.Visible = i == 2;
            ClientSize = Theme.Sz(W, PageY + PageHeights[i]);
        }

        /// <summary>切到某一页（预览工具用）</summary>
        public void SelectPage(int i)
        {
            _tabs.SelectedIndex = i;
        }

        // ===== 控制页 =====
        private void BuildControlPage()
        {
            Panel p = _pageControl;
            int y;

            // 性能档位
            p.Controls.Add(Lbl("性能档位", Theme.Cn, 11, Theme.TextSub, 22, 0));
            _segProfiles = new Segmented(new[] { "静谧", "均衡", "极速" },
                new[] { Theme.Green, Theme.Blue, Theme.Red }, false);
            _segProfiles.Bounds = Theme.R(M, 20, CW, 44);
            _segProfiles.SelectedChanged += OnProfileSegmentChanged;
            p.Controls.Add(_segProfiles);

            // 温度自动联动
            p.Controls.Add(Lbl("温度自动联动", Theme.Cn, 13, Theme.TextMain, M, 80));
            p.Controls.Add(Lbl("按温度在三档之间切换", Theme.Cn, 11, Theme.TextDim, 124, 83));
            _tglAuto = new ToggleSwitch();
            _tglAuto.Bounds = Theme.R(ToggleRight, 78, 44, 24);
            _tglAuto.CheckedChanged += delegate
            {
                if (_suppress || _app == null) return;
                _app.SetAutoEnabled(_tglAuto.Checked);
                ApplyLive();
            };
            p.Controls.Add(_tglAuto);

            p.Controls.Add(Lbl("静谧阈值", Theme.Cn, 11, Theme.TextSub, M, 122));
            _stpLow = new Stepper(50, 95, 65);
            _stpLow.Bounds = Theme.R(78, 115, 88, 30);
            _stpLow.ValueChanged += delegate { ThresholdChanged(); };
            p.Controls.Add(_stpLow);
            p.Controls.Add(Lbl("°C", Theme.Ui, 11.5f, Theme.TextSub, 172, 122));

            p.Controls.Add(Lbl("极速阈值", Theme.Cn, 11, Theme.TextSub, 206, 122));
            _stpHigh = new Stepper(50, 95, 78);
            _stpHigh.Bounds = Theme.R(268, 115, 88, 30);
            _stpHigh.ValueChanged += delegate { ThresholdChanged(); };
            p.Controls.Add(_stpHigh);
            p.Controls.Add(Lbl("°C", Theme.Ui, 11.5f, Theme.TextSub, 362, 122));

            // 快捷控制
            p.Controls.Add(Lbl("快捷控制", Theme.Cn, 11, Theme.TextSub, 22, 166));

            y = 188;
            p.Controls.Add(Lbl("禁用触控板", Theme.Cn, 13, Theme.TextMain, M, y + 11));
            _pillTouchpadEc = new StatusPill();
            _pillTouchpadEc.Location = Theme.P(M + 92, y + 8);
            _tips.SetToolTip(_pillTouchpadEc, "触控板被键盘快捷键（Fn+F9）锁定。\n按 Fn+F9 可解锁；MiCtl 无法软件解除这一层。");
            p.Controls.Add(_pillTouchpadEc);
            _tglTouchpad = new ToggleSwitch();
            _tglTouchpad.Bounds = Theme.R(ToggleRight, y + 7, 44, 24);
            _tglTouchpad.CheckedChanged += delegate
            {
                if (_suppress || _app == null) return;
                bool wantDisable = _tglTouchpad.Checked;
                if (!TouchpadControl.SetDisabled(wantDisable))
                    MessageBox.Show("触控板" + (wantDisable ? "禁用" : "启用") + "未生效：系统拒绝了更改。\n启用方向若仍无效，可能触控板被键盘快捷键（Fn+F9）锁定。", "MiCtl",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                // 启用方向：总是用 fn10 热键动作把 EC 层也设为启用（幂等动作；EC 缓存值可能过期，
                // 只在缓存==0 时才发会漏掉被外部切到禁用的情况）。
                if (!_tglTouchpad.Checked)
                    _app.SetTouchpadOn();
                _app.RefreshDeviceStates();
                ApplyLive();
            };
            p.Controls.Add(_tglTouchpad);
            AddDivider(p, M, y + RowH, CW);

            y += RowH;
            p.Controls.Add(Lbl("禁用 Win 键", Theme.Cn, 13, Theme.TextMain, M, y + 11));
            p.Controls.Add(Lbl("游戏时防误触", Theme.Cn, 11, Theme.TextDim, M + 92, y + 12));
            _tglWinKey = new ToggleSwitch();
            _tglWinKey.Bounds = Theme.R(ToggleRight, y + 7, 44, 24);
            _tglWinKey.CheckedChanged += delegate
            {
                if (_suppress || _app == null) return;
                if (!_app.SetWinKeyLock(_tglWinKey.Checked))
                    MessageBox.Show("Win 键钩子安装失败。", "MiCtl",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                ApplyLive();
            };
            p.Controls.Add(_tglWinKey);
            AddDivider(p, M, y + RowH, CW);

            y += RowH;
            // Fn 锁：软件只能解锁（fn10 SET {1}，探针验证有效）；"锁定"方向本机固件没有 MIFS 通道
            //（SET 全枚举只有 fn8/fn10/fn11 三个活码，fn10 {0} 是触控板动作），锁定请按 Fn+Esc。
            string fnTip =
                "Fn+Esc 切换；解锁也可点右侧按钮（固件 fn10 功能码）。\n" +
                "解锁时：F1 静音 · F2/F3 音量 · F4/F5 亮度 · F6 投影 · F9 触控板 · F10 键盘灯 · F11 截屏 · F12 插入\n" +
                "锁定时：F1–F12 为功能键（软件无法锁定，请按 Fn+Esc）";
            p.Controls.Add(Lbl("Fn 锁", Theme.Cn, 13, Theme.TextMain, M, y + 11));
            _pillFnLock = new StatusPill();
            _pillFnLock.Location = Theme.P(M + 92, y + 8);
            _tips.SetToolTip(_pillFnLock, fnTip);
            p.Controls.Add(_pillFnLock);
            _lblFnNote = Lbl("锁定 = 功能键；解锁 = 媒体键", Theme.Cn, 11, Theme.TextDim, M + 92, y + 12);
            _tips.SetToolTip(_lblFnNote, fnTip);
            p.Controls.Add(_lblFnNote);
            _btnUnlockFn = new GhostButton("解锁 Fn");
            _btnUnlockFn.Bounds = Theme.R(W - M - 88, y + 5, 88, 28);
            _tips.SetToolTip(_btnUnlockFn, "把 Fn 切到解锁（F1–F12 出媒体功能）。\n锁定方向固件未开放，请按 Fn+Esc。");
            _btnUnlockFn.Click += delegate
            {
                if (_app == null) return;
                if (!_app.UnlockFn())
                {
                    MessageBox.Show("Fn 解锁失败：固件接口没有响应。", "MiCtl",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                ApplyLive();
            };
            p.Controls.Add(_btnUnlockFn);

            // GPU 模式：只显示当前模式（A7S 的固件未开放软件切换——fn9 SET 被拒，游戏盒子该功能也仅 A7SR）
            p.Controls.Add(Lbl("GPU 模式", Theme.Cn, 13, Theme.TextMain, M, 320));
            _lblGpuModeValue = Lbl("", Theme.Cn, 11.5f, Theme.TextMain, 92, 322);
            p.Controls.Add(_lblGpuModeValue);
            _lblGpuModeNote = Lbl("切换需重启进 BIOS（本机固件未开放软件切换）", Theme.Cn, 11, Theme.TextDim, 92, 342);
            p.Controls.Add(_lblGpuModeNote);

            // 开机自启 + 状态行
            p.Controls.Add(Lbl("开机自启", Theme.Cn, 13, Theme.TextMain, M, 372));
            p.Controls.Add(Lbl("登录时以管理员身份启动", Theme.Cn, 11, Theme.TextDim, 92, 375));
            _tglStartup = new ToggleSwitch();
            _tglStartup.Bounds = Theme.R(ToggleRight, 369, 44, 24);
            _tglStartup.CheckedChanged += delegate
            {
                if (_suppress || _app == null) return;
                StartupManager.Toggle();
                _app.RefreshDeviceStates();
                ApplyLive();
            };
            p.Controls.Add(_tglStartup);

            _lblStatus = Lbl("", Theme.Cn, 10.5f, Theme.TextDim, M, 410);
            p.Controls.Add(_lblStatus);
        }

        // ===== 系统页：内存 / 电池 / 进程 =====
        private void BuildSystemPage()
        {
            Panel p = _pageSystem;
            p.Controls.Add(Lbl("内存", Theme.Cn, 13, Theme.TextMain, M, 6));
            _lblMemDetail = LblEllipsis("", 11, Theme.TextDim, 64, 9, 316);
            p.Controls.Add(_lblMemDetail);

            p.Controls.Add(Lbl("电池", Theme.Cn, 13, Theme.TextMain, M, 34));
            _lblBattDetail = LblEllipsis("", 11, Theme.TextDim, 64, 37, 316);
            p.Controls.Add(_lblBattDetail);

            AddDivider(p, M, 62, CW);

            p.Controls.Add(Lbl("进程", Theme.Cn, 11, Theme.TextSub, 22, 76));
            _lblProcCount = Lbl("", Theme.Cn, 11, Theme.TextDim, 58, 76);
            p.Controls.Add(_lblProcCount);
            _btnTaskMgr = new TextButton("打开任务管理器");
            _btnTaskMgr.Size = Theme.Sz(_btnTaskMgr.PreferredWidth(), 26);
            PlaceRight(_btnTaskMgr, W - M, 70);
            _btnTaskMgr.Click += delegate
            {
                SystemInfo.OpenTaskManager();
            };
            p.Controls.Add(_btnTaskMgr);

            _lblProcEmpty = Lbl("读不到进程列表", Theme.Cn, 11.5f, Theme.TextDim, M, ProcRow0 + 8);
            _lblProcEmpty.Visible = false;
            p.Controls.Add(_lblProcEmpty);

            for (int i = 0; i < ProcRows; i++)
            {
                int y = ProcRow0 + i * ProcRowH;
                int idx = i;
                _procName[i] = LblEllipsis("", 13, Theme.TextMain, M, y + 7, 168);
                p.Controls.Add(_procName[i]);
                _procMem[i] = Lbl("--", Theme.Mono, 12, Theme.TextSub, 0, y + 8);
                p.Controls.Add(_procMem[i]);
                _procKill[i] = new GhostButton("结束");
                _procKill[i].Danger = true;
                _procKill[i].Bounds = Theme.R(312, y + 3, 68, 24);
                _procKill[i].Click += delegate { KillAt(idx); };
                p.Controls.Add(_procKill[i]);
                if (i < ProcRows - 1)
                {
                    _procLine[i] = new Label();
                    _procLine[i].AutoSize = false;
                    _procLine[i].Size = new Size(Theme.S(CW), 1);
                    _procLine[i].Location = new Point(Theme.S(M), Theme.S(y + ProcRowH - 1));
                    _procLine[i].BackColor = Theme.CardBorder;
                    p.Controls.Add(_procLine[i]);
                }
            }
        }

        // ===== 网络页：速率 / 网卡 / 接入点 / IP / 延迟 =====
        private void BuildNetworkPage()
        {
            Panel p = _pageNetwork;
            p.Controls.Add(Lbl("\u2193", Theme.Ui, 15, Theme.TextSub, M, 18));
            _lblDownNum = Lbl("--", Theme.Mono, 28, Theme.TextMain, M + 18, 6);
            p.Controls.Add(_lblDownNum);
            _lblDownUnit = Lbl("", Theme.Cn, 12, Theme.TextSub, 0, 0);
            p.Controls.Add(_lblDownUnit);

            p.Controls.Add(Lbl("\u2191", Theme.Ui, 15, Theme.TextSub, 200, 18));
            _lblUpNum = Lbl("--", Theme.Mono, 28, Theme.TextMain, 218, 6);
            p.Controls.Add(_lblUpNum);
            _lblUpUnit = Lbl("", Theme.Cn, 12, Theme.TextSub, 0, 0);
            p.Controls.Add(_lblUpUnit);

            _lblSession = Lbl("", Theme.Cn, 11, Theme.TextDim, M, 52);
            p.Controls.Add(_lblSession);

            AddDivider(p, M, 74, CW);

            int y = 86;
            MakeRow(p, "网卡", y, 84, 296, out _rowTmp, out _lblNetNic);
            y += ProcRowH;
            MakeRow(p, "接入点", y, 84, 296, out _rowNetWifi, out _lblNetWifi);
            y += ProcRowH;
            MakeRow(p, "本地 IP", y, 84, 296, out _rowTmp, out _lblNetIp);
            y += ProcRowH;
            MakeRow(p, "延迟", y, 84, 218, out _rowTmp, out _lblNetPing);
            _btnPing = new TextButton("重新检测");
            _btnPing.Size = Theme.Sz(_btnPing.PreferredWidth(), 26);
            PlaceRight(_btnPing, W - M, y + 2);
            _btnPing.Click += delegate
            {
                if (_app == null) return;
                _app.StartPing();
                ApplyLive();
            };
            p.Controls.Add(_btnPing);
            y += ProcRowH;
            MakeRow(p, "其它地址", y, 84, 296, out _rowNetOthers, out _lblNetOthers);
        }

        // ---------------- 状态 → 界面 ----------------
        /// <summary>把一份状态画到界面上（真实运行与离屏预览共用）</summary>
        public void ApplyState(UiState s)
        {
            if (s == null) return;
            _state = s;
            _suppress = true;
            try
            {
                // 仪表：CPU/GPU 环是温度，内存环是使用率
                _gaugeCpu.SetValue(s.CpuTemp, Watts(s.CpuPower));
                _gaugeGpu.SetValue(s.GpuTemp, Watts(s.GpuPower));
                bool hasMem = s.MemTotalGB > 0;
                _gaugeMem.SetValue(hasMem ? (double?)s.MemLoadPct : null,
                    hasMem ? s.MemUsedGB.ToString("0.#") + " GB" : "");

                // 每个硬件一行：负载 · 频率 · 功耗 · 风扇
                _lblCpuLoad.Text = Pct(s.CpuLoad);
                _lblCpuClock.Text = s.CpuClockMHz.HasValue ? (s.CpuClockMHz.Value / 1000.0).ToString("0.#") + " GHz" : "--";
                _lblCpuPwr.Text = WattsOrDash(s.CpuPower);
                _lblCpuFan.Text = Rpm(s.FanA);
                _lblGpuLoad.Text = Pct(s.GpuLoad);
                _lblGpuClock.Text = s.GpuClockMHz.HasValue ? s.GpuClockMHz.Value.ToString("0") + " MHz" : "--";
                _lblGpuPwr.Text = WattsOrDash(s.GpuPower);
                _lblGpuFan.Text = Rpm(s.FanB);

                // 档位
                int pi = -1;
                if (s.Profile.HasValue) pi = Array.IndexOf(ProfileOrder, s.Profile.Value);
                _segProfiles.SelectedIndex = pi;
                _segProfiles.Enabled = s.MifsAvailable;

                // 自动联动
                _tglAuto.Checked = s.AutoEnabled;
                _tglAuto.Enabled = s.MifsAvailable;
                _stpLow.SetValueQuiet((decimal)s.LowThreshold);
                _stpHigh.SetValueQuiet((decimal)s.HighThreshold);
                _stpLow.Enabled = s.AutoEnabled && s.MifsAvailable;
                _stpHigh.Enabled = s.AutoEnabled && s.MifsAvailable;

                // 触控板：开关表示"实际可用性" = !(设备禁用 || 键盘快捷键锁定)。
                // 两层互相独立：MiCtl 管设备层（pnputil），键盘快捷键走 EC（Fn+F9）。
                bool ecLocked = s.TouchpadEcValue.HasValue && s.TouchpadEcValue.Value == 0;
                _tglTouchpad.Checked = s.TouchpadDeviceDisabled || ecLocked;
                if (ecLocked) _pillTouchpadEc.Set("快捷键已锁定", Theme.Orange);
                _pillTouchpadEc.Visible = ecLocked;

                _tglWinKey.Checked = s.WinKeyLocked;

                // Fn 锁（已对照确认：value 1 = 锁定/F1–F12 功能键，value 0 = 解锁/媒体键）。
                // 软件切换只有解锁方向（fn10 SET {1}）；锁定方向固件未开放（SET 全枚举验证），请按 Fn+Esc。
                // 解锁按钮发出后的 3 秒确认窗口内不回写，等 EC 事件到位。
                int fnv = s.FnLockValue ?? -1;
                if (!_app.FnLockPending)
                {
                    _pillFnLock.Set(fnv == 1 ? "已锁定" : (fnv == 0 ? "已解锁" : "未检测到"),
                        fnv == 1 ? Theme.Green : (fnv == 0 ? Theme.Orange : Theme.TextDim));
                    _btnUnlockFn.Visible = fnv == 1;
                }
                _lblFnNote.Left = _pillFnLock.Right + Theme.S(10);
                _btnUnlockFn.Enabled = s.MifsAvailable;
                if (_app.FnLockFailed)
                {
                    _app.ClearFnLockFailed();
                    MessageBox.Show("Fn 解锁未生效：固件没有确认状态变化。", "MiCtl",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }

                // GPU 模式：只显示（A7S 的固件未开放软件切换——fn9 SET 被拒，游戏盒子该功能也仅 A7SR）
                if (s.GpuMode.HasValue) _gpuModeDetected = s.GpuMode;
                _lblGpuModeValue.Text = _gpuModeDetected.HasValue
                    ? GpuModeNames[_gpuModeDetected.Value]
                    : "无法识别";

                _tglStartup.Checked = s.StartupEnabled;

                if (s.MifsAvailable)
                {
                    string src = string.IsNullOrEmpty(s.TempSource) ? "" : "，温度与转速来自" + s.TempSource;
                    _lblStatus.Text = "固件接口已连接" + src;
                }
                else
                {
                    string err = s.MifsError ?? "";
                    if (err.Length > 60) err = err.Substring(0, 60) + "…";
                    _lblStatus.Text = "固件接口不可用" + (err.Length > 0 ? "：" + err : "");
                }

                ApplySystemPage(s);
                ApplyNetworkPage(s);
            }
            finally
            {
                _suppress = false;
            }
        }

        private void ApplySystemPage(UiState s)
        {
            if (s.MemTotalGB > 0)
            {
                _lblMemDetail.Text = s.MemUsedGB.ToString("0.#") + " / " + s.MemTotalGB.ToString("0.#") + " GB 已用"
                    + "  ·  可用 " + s.MemAvailGB.ToString("0.#") + " GB"
                    + (s.MemSpeedMHz.HasValue ? "  ·  " + s.MemSpeedMHz.Value.ToString("0") + "MHz" : "");
            }
            else
            {
                _lblMemDetail.Text = "读不到内存信息";
            }

            string batt = s.BatteryLevel.HasValue ? s.BatteryLevel.Value.ToString("0") + "%" : "--";
            if (!string.IsNullOrEmpty(s.PowerText)) batt += "  ·  " + s.PowerText;
            if (s.BatteryRateW.HasValue && s.BatteryRateW.Value >= 1)
                batt += "  ·  " + (s.PowerText == "已接通电源" ? "充电 " : "放电 ") + s.BatteryRateW.Value.ToString("0.#") + " W";
            if (s.BatteryHealthPct.HasValue) batt += "  ·  健康度 " + s.BatteryHealthPct.Value.ToString("0") + "%";
            _lblBattDetail.Text = batt;

            List<ProcessEntry> list = s.Processes ?? new List<ProcessEntry>();
            _lblProcCount.Text = s.ProcessCount > 0 ? s.ProcessCount + " 个" : "";
            _lblProcEmpty.Visible = list.Count == 0;
            for (int i = 0; i < ProcRows; i++)
            {
                bool has = i < list.Count;
                _procName[i].Visible = has;
                _procMem[i].Visible = has;
                _procKill[i].Visible = has;
                if (i < ProcRows - 1)
                    _procLine[i].Visible = has && i + 1 < list.Count;
                if (!has) continue;
                ProcessEntry e = list[i];
                _procName[i].Text = e.Name;
                _procMem[i].Text = e.MemoryText;
                PlaceRight(_procMem[i], 296, ProcRow0 + i * ProcRowH + 8);
            }
        }

        private void ApplyNetworkPage(UiState s)
        {
            NetSnapshot n = s.Net ?? new NetSnapshot();
            SetSpeed(_lblDownNum, _lblDownUnit, n.HasLink ? (double?)n.DownKbps : null, M + 18, 6);
            SetSpeed(_lblUpNum, _lblUpUnit, n.HasLink ? (double?)n.UpKbps : null, 218, 6);

            _lblSession.Text = n.HasLink
                ? "本次运行  \u2193 " + MegaBytes(n.SessionDownMB) + "   \u2191 " + MegaBytes(n.SessionUpMB)
                : "";

            _lblNetNic.Text = n.HasLink ? n.NicName + "  " + n.NicDesc : "未找到可用的网络连接";

            bool showWifi = n.WifiConnected;
            _rowNetWifi.Visible = showWifi;
            if (showWifi) _lblNetWifi.Text = n.WifiText;

            _lblNetIp.Text = n.HasLink ? n.Ip : "--";

            _lblNetPing.Text = n.PingText;
            _btnPing.Enabled = !n.PingRunning;

            bool showOthers = n.Others != null && n.Others.Count > 0;
            _rowNetOthers.Visible = showOthers;
            if (showOthers) _lblNetOthers.Text = string.Join("      ", n.Others.ToArray());
        }

        private void SetSpeed(Label num, Label unit, double? kbps, int x, int y)
        {
            num.Left = Theme.S(x);
            num.Top = Theme.S(y);
            if (!kbps.HasValue)
            {
                num.Text = "--";
                unit.Text = "";
            }
            else if (kbps.Value >= 1024)
            {
                num.Text = (kbps.Value / 1024).ToString("0.#");
                unit.Text = "MB/s";
            }
            else
            {
                num.Text = kbps.Value.ToString("0");
                unit.Text = "KB/s";
            }
            unit.Left = num.Right + Theme.S(6);
            unit.Top = num.Bottom - unit.Height - Theme.S(3);
        }

        // ---------------- 格式化 ----------------
        private static string Watts(double? w)
        {
            return w.HasValue ? w.Value.ToString("0") + " W" : "";
        }

        private static string WattsOrDash(double? w)
        {
            return w.HasValue ? w.Value.ToString("0") + " W" : "--";
        }

        private static string Pct(double? v)
        {
            return v.HasValue ? v.Value.ToString("0") + "%" : "--";
        }

        private static string Rpm(int? v)
        {
            return v.HasValue ? v.Value.ToString("0") + " RPM" : "--";
        }

        private static string MegaBytes(double mb)
        {
            return mb >= 1024 ? (mb / 1024).ToString("0.##") + " GB" : mb.ToString("0.#") + " MB";
        }

        // ---------------- 后台事件 → UI 线程 ----------------
        private void OnAppUpdated()
        {
            if (IsDisposed || !IsHandleCreated) return;
            try { BeginInvoke((Action)ApplyLive); }
            catch (InvalidOperationException) { }
        }

        private void ApplyLive()
        {
            if (_app != null) ApplyState(UiState.Capture(_app));
        }

        // ---------------- 用户操作 ----------------
        private void OnProfileSegmentChanged(object sender, EventArgs e)
        {
            if (_suppress || _app == null) return;
            int i = _segProfiles.SelectedIndex;
            if (i < 0 || i >= ProfileOrder.Length) return;
            if (!_app.SwitchProfile(ProfileOrder[i]))
            {
                MessageBox.Show("档位切换失败：固件接口没有响应。", "MiCtl",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            // 以固件回读的真实档位为准（失败时分段会滑回原档位）
            ApplyLive();
        }

        private void ThresholdChanged()
        {
            if (_suppress || _app == null) return;
            double low = (double)_stpLow.Value;
            double high = (double)_stpHigh.Value;
            if (low >= high) low = high - 1;          // 保持 静谧阈值 < 极速阈值
            _app.Settings.LowThreshold = low;
            _app.Settings.HighThreshold = high;
            _app.SaveSettings();
            ApplyLive();
        }

        private void KillAt(int i)
        {
            if (_app == null) return;
            List<ProcessEntry> list = _state.Processes ?? new List<ProcessEntry>();
            if (i < 0 || i >= list.Count) return;
            ProcessEntry e = list[i];
            if (SystemInfo.IsCritical(e.Name))
            {
                MessageBox.Show(e.Name + " 是系统关键进程，不能在这里结束。", "MiCtl",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            string msg = "结束 " + e.Name + "？" +
                (e.Pids.Count > 1 ? "\n\n它有 " + e.Pids.Count + " 个进程，占用 " + e.MemoryText + "。" : "\n\n它占用 " + e.MemoryText + "。") +
                "\n该程序里没有保存的内容会丢失。";
            if (MessageBox.Show(msg, "MiCtl", MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                return;
            string err = _app.KillProcesses(e);
            if (err != null)
            {
                MessageBox.Show(err, "MiCtl", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            ApplyLive();
        }

        /// <summary>
        /// 通过系统实际显卡状态识别 GPU 模式：混合模式下 AMD 集显作为适配器可见；独显直连时消失。
        /// 这是推断，没有独立验证过。
        /// </summary>
        private static int? DetectGpuMode()
        {
            try
            {
                bool hasAmdIgpu = false;
                using (ManagementObjectSearcher searcher =
                    new ManagementObjectSearcher("SELECT Name, Status FROM Win32_VideoController"))
                {
                    foreach (ManagementObject o in searcher.Get())
                    {
                        string name = (o["Name"] as string) ?? "";
                        string status = (o["Status"] as string) ?? "";
                        if (name.IndexOf("Radeon", StringComparison.OrdinalIgnoreCase) >= 0 &&
                            status.IndexOf("ok", StringComparison.OrdinalIgnoreCase) >= 0)
                            hasAmdIgpu = true;
                    }
                }
                return hasAmdIgpu ? 0 : 1;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>预览用：把所有控件的过渡动画直接停到终点</summary>
        public void FinishAnimations()
        {
            FinishAnimationsCore(this);
        }

        private static void FinishAnimationsCore(Control parent)
        {
            foreach (Control c in parent.Controls)
            {
                GaugeRing gr = c as GaugeRing;
                if (gr != null) gr.Jump();
                Segmented sg = c as Segmented;
                if (sg != null) sg.Jump();
                ToggleSwitch ts = c as ToggleSwitch;
                if (ts != null) ts.Jump();
                TabStrip tb = c as TabStrip;
                if (tb != null) tb.Jump();
                FinishAnimationsCore(c);
            }
        }

        // ---- ESC 隐藏到托盘 ----
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape)
            {
                Hide();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }
    }
}
