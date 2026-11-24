// 主窗口 —— 深色无边框现代 UI
// 功能：档位分段选择 / 温度自动联动 / 温度+风扇转速+网速监控 / GPU 模式 / 快捷控制（触控板、Win 键）/ 开机自启
// 说明：Fn 锁由 Fn+Esc 硬件切换（仅显示状态）；键盘背光为键盘 Fn 映射控制，不做软件控制
using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace MiCtl
{
    public sealed class MainWindow : Form
    {
        private readonly AppController _app;

        // 温度区
        private Label _lblCpuTemp, _lblGpuTemp, _lblGpuDetail, _lblNetFan;
        private StatusDot _dotCpu, _dotGpu;

        // 档位与联动
        private SegmentedProfiles _segProfiles;
        private ToggleSwitch _tglAuto;
        private NumericUpDown _numLow, _numHigh;

        // GPU 模式
        private ComboBox _cboGpuMode;
        private Button _btnGpuApply;

        // 快捷控制
        private Label _lblFnLockState;
        private ToggleSwitch _tglTouchpad, _tglWinKey;

        // 其它
        private ToggleSwitch _tglStartup;
        private Label _lblStatus;
        private bool _suppressEvents;

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HTCAPTION = 0x2;

        public MainWindow(AppController app)
        {
            _app = app;

            Text = "MiCtl";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(400, 668);
            BackColor = Theme.Bg;
            Font = new Font("Microsoft YaHei UI", 9f);
            DoubleBuffered = true;
            KeyPreview = true;

            BuildUi();
            LoadState();

            _app.SensorsUpdated += OnSensorsUpdatedThread;
            _app.ProfileChanged += OnProfileChangedThread;
            FormClosed += delegate
            {
                _app.SensorsUpdated -= OnSensorsUpdatedThread;
                _app.ProfileChanged -= OnProfileChangedThread;
            };
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

        // ---------------- UI 构建 ----------------
        private void BuildUi()
        {
            // ===== 标题栏 =====
            Panel titleBar = new Panel();
            titleBar.Size = new Size(400, 46);
            titleBar.Location = new Point(0, 0);
            titleBar.BackColor = Theme.Bg;
            titleBar.MouseClick += delegate (object s, MouseEventArgs e) { DragWindow(e); };
            titleBar.MouseMove += delegate (object s, MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Left) DragWindow(e);
            };

            Label title = new Label();
            title.Text = "MiCtl";
            title.Font = new Font("Segoe UI Semibold", 11f);
            title.ForeColor = Theme.TextMain;
            title.Location = new Point(18, 11);
            title.AutoSize = true;
            title.BackColor = Color.Transparent;
            titleBar.Controls.Add(title);

            Label subtitle = new Label();
            subtitle.Text = "Redmi G 2021";
            subtitle.Font = new Font("Segoe UI", 8f);
            subtitle.ForeColor = Theme.TextDim;
            subtitle.Location = new Point(74, 15);
            subtitle.AutoSize = true;
            subtitle.BackColor = Color.Transparent;
            titleBar.Controls.Add(subtitle);

            Label btnMin = MakeTitleButton("─", 400 - 76);
            Label btnClose = MakeTitleButton("✕", 400 - 40);
            titleBar.Controls.Add(btnMin);
            titleBar.Controls.Add(btnClose);
            Controls.Add(titleBar);

            // ===== 温度卡片（含网速与风扇转速）=====
            RoundedCard cardTemp = new RoundedCard();
            cardTemp.Size = new Size(364, 150);
            cardTemp.Location = new Point(18, 54);
            Controls.Add(cardTemp);

            Label lblCpuTitle = new Label();
            lblCpuTitle.Text = "CPU";
            lblCpuTitle.Font = new Font("Segoe UI", 8.5f);
            lblCpuTitle.ForeColor = Theme.TextSub;
            lblCpuTitle.Location = new Point(30, 14);
            lblCpuTitle.AutoSize = true;
            lblCpuTitle.BackColor = Color.Transparent;
            cardTemp.Controls.Add(lblCpuTitle);

            _dotCpu = new StatusDot();
            _dotCpu.Location = new Point(60, 18);
            cardTemp.Controls.Add(_dotCpu);

            _lblCpuTemp = new Label();
            _lblCpuTemp.Text = "--";
            _lblCpuTemp.Font = new Font("Segoe UI Semibold", 26f);
            _lblCpuTemp.ForeColor = Theme.TextMain;
            _lblCpuTemp.Location = new Point(28, 30);
            _lblCpuTemp.AutoSize = true;
            _lblCpuTemp.BackColor = Color.Transparent;
            cardTemp.Controls.Add(_lblCpuTemp);

            Label lblGpuTitle = new Label();
            lblGpuTitle.Text = "GPU";
            lblGpuTitle.Font = new Font("Segoe UI", 8.5f);
            lblGpuTitle.ForeColor = Theme.TextSub;
            lblGpuTitle.Location = new Point(210, 14);
            lblGpuTitle.AutoSize = true;
            lblGpuTitle.BackColor = Color.Transparent;
            cardTemp.Controls.Add(lblGpuTitle);

            _dotGpu = new StatusDot();
            _dotGpu.Location = new Point(240, 18);
            cardTemp.Controls.Add(_dotGpu);

            _lblGpuTemp = new Label();
            _lblGpuTemp.Text = "--";
            _lblGpuTemp.Font = new Font("Segoe UI Semibold", 26f);
            _lblGpuTemp.ForeColor = Theme.TextMain;
            _lblGpuTemp.Location = new Point(208, 30);
            _lblGpuTemp.AutoSize = true;
            _lblGpuTemp.BackColor = Color.Transparent;
            cardTemp.Controls.Add(_lblGpuTemp);

            _lblGpuDetail = new Label();
            _lblGpuDetail.Text = "GPU 负载 --%  ·  功耗 --W  ·  CPU --W";
            _lblGpuDetail.Font = new Font("Segoe UI", 8.5f);
            _lblGpuDetail.ForeColor = Theme.TextSub;
            _lblGpuDetail.Location = new Point(30, 84);
            _lblGpuDetail.AutoSize = true;
            _lblGpuDetail.BackColor = Color.Transparent;
            cardTemp.Controls.Add(_lblGpuDetail);

            _lblNetFan = new Label();
            _lblNetFan.Text = "↓ --  ↑ --" + (HasFanData() ? "  ·  风扇 -- RPM" : "");
            _lblNetFan.Font = new Font("Segoe UI", 8.5f);
            _lblNetFan.ForeColor = Theme.TextSub;
            _lblNetFan.Location = new Point(30, 112);
            _lblNetFan.AutoSize = true;
            _lblNetFan.BackColor = Color.Transparent;
            cardTemp.Controls.Add(_lblNetFan);

            // ===== 档位 =====
            Label lblProfile = new Label();
            lblProfile.Text = "性能档位";
            lblProfile.Font = new Font("Segoe UI", 8.5f);
            lblProfile.ForeColor = Theme.TextSub;
            lblProfile.Location = new Point(22, 218);
            lblProfile.AutoSize = true;
            lblProfile.BackColor = Color.Transparent;
            Controls.Add(lblProfile);

            Label lblProfileNote = new Label();
            lblProfileNote.Text = "联动风扇曲线与功耗墙";
            lblProfileNote.Font = new Font("Segoe UI", 8f);
            lblProfileNote.ForeColor = Theme.TextDim;
            lblProfileNote.Location = new Point(90, 219);
            lblProfileNote.AutoSize = true;
            lblProfileNote.BackColor = Color.Transparent;
            Controls.Add(lblProfileNote);

            _segProfiles = new SegmentedProfiles();
            _segProfiles.Size = new Size(364, 46);
            _segProfiles.Location = new Point(18, 238);
            _segProfiles.Font = new Font("Microsoft YaHei UI", 10.5f);
            _segProfiles.SegmentClicked += delegate (MifsClient.Profile p)
            {
                bool ok = _app.SwitchProfile(p);
                if (!ok)
                {
                    MessageBox.Show("档位切换失败（MIFS 不可用）", "MiCtl",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                HighlightProfile();
            };
            Controls.Add(_segProfiles);

            // ===== 自动联动卡片 =====
            RoundedCard cardAuto = new RoundedCard();
            cardAuto.Size = new Size(364, 96);
            cardAuto.Location = new Point(18, 298);
            Controls.Add(cardAuto);

            Label lblAuto = new Label();
            lblAuto.Text = "温度自动联动";
            lblAuto.Font = new Font("Microsoft YaHei UI", 9.5f);
            lblAuto.ForeColor = Theme.TextMain;
            lblAuto.Location = new Point(20, 14);
            lblAuto.AutoSize = true;
            lblAuto.BackColor = Color.Transparent;
            cardAuto.Controls.Add(lblAuto);

            Label lblAutoNote = new Label();
            lblAutoNote.Text = "按温度在档位间自动切换";
            lblAutoNote.Font = new Font("Segoe UI", 8f);
            lblAutoNote.ForeColor = Theme.TextDim;
            lblAutoNote.Location = new Point(120, 17);
            lblAutoNote.AutoSize = true;
            lblAutoNote.BackColor = Color.Transparent;
            cardAuto.Controls.Add(lblAutoNote);

            _tglAuto = new ToggleSwitch();
            _tglAuto.Location = new Point(320, 12);
            _tglAuto.CheckedChanged += delegate
            {
                if (_suppressEvents) return;
                _app.SetAutoEnabled(_tglAuto.Checked);
                bool on = _tglAuto.Checked;
                _numLow.Enabled = on;
                _numHigh.Enabled = on;
            };
            cardAuto.Controls.Add(_tglAuto);

            Label lblLow = new Label();
            lblLow.Text = "静谧阈值";
            lblLow.Font = new Font("Segoe UI", 8.5f);
            lblLow.ForeColor = Theme.TextSub;
            lblLow.Location = new Point(20, 58);
            lblLow.AutoSize = true;
            lblLow.BackColor = Color.Transparent;
            cardAuto.Controls.Add(lblLow);

            _numLow = DarkNumeric(88, 55);
            cardAuto.Controls.Add(_numLow);
            Label lblLowUnit = new Label();
            lblLowUnit.Text = "°C";
            lblLowUnit.Font = new Font("Segoe UI", 8.5f);
            lblLowUnit.ForeColor = Theme.TextSub;
            lblLowUnit.Location = new Point(150, 58);
            lblLowUnit.AutoSize = true;
            lblLowUnit.BackColor = Color.Transparent;
            cardAuto.Controls.Add(lblLowUnit);

            Label lblHigh = new Label();
            lblHigh.Text = "极速阈值";
            lblHigh.Font = new Font("Segoe UI", 8.5f);
            lblHigh.ForeColor = Theme.TextSub;
            lblHigh.Location = new Point(196, 58);
            lblHigh.AutoSize = true;
            lblHigh.BackColor = Color.Transparent;
            cardAuto.Controls.Add(lblHigh);

            _numHigh = DarkNumeric(266, 60);
            cardAuto.Controls.Add(_numHigh);
            Label lblHighUnit = new Label();
            lblHighUnit.Text = "°C";
            lblHighUnit.Font = new Font("Segoe UI", 8.5f);
            lblHighUnit.ForeColor = Theme.TextSub;
            lblHighUnit.Location = new Point(328, 58);
            lblHighUnit.AutoSize = true;
            lblHighUnit.BackColor = Color.Transparent;
            cardAuto.Controls.Add(lblHighUnit);

            _numLow.ValueChanged += delegate
            {
                if (!_suppressEvents)
                {
                    _app.Settings.LowThreshold = (double)_numLow.Value;
                    _app.SaveSettings();
                }
            };
            _numHigh.ValueChanged += delegate
            {
                if (!_suppressEvents)
                {
                    _app.Settings.HighThreshold = (double)_numHigh.Value;
                    _app.SaveSettings();
                }
            };

            // ===== 快捷控制卡片 =====
            RoundedCard cardCtl = new RoundedCard();
            cardCtl.Size = new Size(364, 110);
            cardCtl.Location = new Point(18, 406);
            Controls.Add(cardCtl);

            Label lblCtlTitle = new Label();
            lblCtlTitle.Text = "快捷控制";
            lblCtlTitle.Font = new Font("Segoe UI", 8.5f);
            lblCtlTitle.ForeColor = Theme.TextSub;
            lblCtlTitle.Location = new Point(20, 10);
            lblCtlTitle.AutoSize = true;
            lblCtlTitle.BackColor = Color.Transparent;
            cardCtl.Controls.Add(lblCtlTitle);

            // Fn 锁状态（只读，Fn+Esc 硬件切换）
            Label lblFn = new Label();
            lblFn.Text = "Fn 锁";
            lblFn.Font = new Font("Microsoft YaHei UI", 9f);
            lblFn.ForeColor = Theme.TextMain;
            lblFn.Location = new Point(20, 36);
            lblFn.AutoSize = true;
            lblFn.BackColor = Color.Transparent;
            cardCtl.Controls.Add(lblFn);

            _lblFnLockState = new Label();
            _lblFnLockState.Text = "--（Fn+Esc 切换）";
            _lblFnLockState.Font = new Font("Segoe UI", 8.5f);
            _lblFnLockState.ForeColor = Theme.TextDim;
            _lblFnLockState.Location = new Point(90, 38);
            _lblFnLockState.AutoSize = true;
            _lblFnLockState.BackColor = Color.Transparent;
            cardCtl.Controls.Add(_lblFnLockState);

            // 触控板
            Label lblTp = new Label();
            lblTp.Text = "禁用触控板";
            lblTp.Font = new Font("Microsoft YaHei UI", 9f);
            lblTp.ForeColor = Theme.TextMain;
            lblTp.Location = new Point(20, 62);
            lblTp.AutoSize = true;
            lblTp.BackColor = Color.Transparent;
            cardCtl.Controls.Add(lblTp);

            _tglTouchpad = new ToggleSwitch();
            _tglTouchpad.Location = new Point(320, 60);
            _tglTouchpad.CheckedChanged += delegate
            {
                if (_suppressEvents) return;
                if (!TouchpadControl.SetDisabled(_tglTouchpad.Checked))
                {
                    _suppressEvents = true;
                    _tglTouchpad.Checked = !_tglTouchpad.Checked;
                    _suppressEvents = false;
                    MessageBox.Show("触控板控制失败（未找到设备或权限不足）", "MiCtl");
                }
            };
            cardCtl.Controls.Add(_tglTouchpad);

            // Win 键
            Label lblWin = new Label();
            lblWin.Text = "禁用 Win 键（游戏防误触）";
            lblWin.Font = new Font("Microsoft YaHei UI", 9f);
            lblWin.ForeColor = Theme.TextMain;
            lblWin.Location = new Point(20, 88);
            lblWin.AutoSize = true;
            lblWin.BackColor = Color.Transparent;
            cardCtl.Controls.Add(lblWin);

            _tglWinKey = new ToggleSwitch();
            _tglWinKey.Location = new Point(320, 86);
            _tglWinKey.CheckedChanged += delegate
            {
                if (_suppressEvents) return;
                if (!_app.SetWinKeyLock(_tglWinKey.Checked))
                {
                    _suppressEvents = true;
                    _tglWinKey.Checked = !_tglWinKey.Checked;
                    _suppressEvents = false;
                    MessageBox.Show("Win 键钩子安装失败", "MiCtl");
                }
            };
            cardCtl.Controls.Add(_tglWinKey);

            // ===== GPU 模式卡片 =====
            RoundedCard cardGpu = new RoundedCard();
            cardGpu.Size = new Size(364, 76);
            cardGpu.Location = new Point(18, 528);
            Controls.Add(cardGpu);

            Label lblGpuMode = new Label();
            lblGpuMode.Text = "GPU 模式";
            lblGpuMode.Font = new Font("Microsoft YaHei UI", 9.5f);
            lblGpuMode.ForeColor = Theme.TextMain;
            lblGpuMode.Location = new Point(20, 12);
            lblGpuMode.AutoSize = true;
            lblGpuMode.BackColor = Color.Transparent;
            cardGpu.Controls.Add(lblGpuMode);

            Label lblGpuModeNote = new Label();
            lblGpuModeNote.Text = "切换后需重启生效";
            lblGpuModeNote.Font = new Font("Segoe UI", 8f);
            lblGpuModeNote.ForeColor = Theme.TextDim;
            lblGpuModeNote.Location = new Point(96, 15);
            lblGpuModeNote.AutoSize = true;
            lblGpuModeNote.BackColor = Color.Transparent;
            cardGpu.Controls.Add(lblGpuModeNote);

            _cboGpuMode = DarkCombo(20, 40);
            cardGpu.Controls.Add(_cboGpuMode);

            _btnGpuApply = new Button();
            _btnGpuApply.Text = "应用";
            _btnGpuApply.Font = new Font("Microsoft YaHei UI", 9f);
            _btnGpuApply.FlatStyle = FlatStyle.Flat;
            _btnGpuApply.FlatAppearance.BorderSize = 0;
            _btnGpuApply.BackColor = Theme.Accent;
            _btnGpuApply.ForeColor = Color.White;
            _btnGpuApply.Location = new Point(280, 39);
            _btnGpuApply.Size = new Size(64, 28);
            _btnGpuApply.Cursor = Cursors.Hand;
            _btnGpuApply.Click += delegate { ApplyGpuMode(); };
            cardGpu.Controls.Add(_btnGpuApply);

            // ===== 自启 + 状态 =====
            Label lblStartup = new Label();
            lblStartup.Text = "开机自启（计划任务，免 UAC）";
            lblStartup.Font = new Font("Segoe UI", 8.5f);
            lblStartup.ForeColor = Theme.TextSub;
            lblStartup.Location = new Point(22, 614);
            lblStartup.AutoSize = true;
            lblStartup.BackColor = Color.Transparent;
            Controls.Add(lblStartup);

            _tglStartup = new ToggleSwitch();
            _tglStartup.Location = new Point(366 - 46, 610);
            _tglStartup.CheckedChanged += delegate
            {
                if (_suppressEvents) return;
                StartupManager.Toggle();
                _suppressEvents = true;
                _tglStartup.Checked = StartupManager.IsEnabled();
                _suppressEvents = false;
            };
            Controls.Add(_tglStartup);

            _lblStatus = new Label();
            _lblStatus.Font = new Font("Segoe UI", 8f);
            _lblStatus.ForeColor = Theme.TextDim;
            _lblStatus.Location = new Point(22, 644);
            _lblStatus.AutoSize = true;
            _lblStatus.BackColor = Color.Transparent;
            Controls.Add(_lblStatus);
        }

        private Label MakeTitleButton(string glyph, int x)
        {
            Label l = new Label();
            l.Text = glyph;
            l.Font = new Font("Segoe UI", 10f);
            l.ForeColor = Theme.TextSub;
            l.Size = new Size(38, 46);
            l.Location = new Point(x, 0);
            l.TextAlign = ContentAlignment.MiddleCenter;
            l.BackColor = Color.Transparent;
            l.Cursor = Cursors.Hand;
            l.MouseEnter += delegate { l.ForeColor = Theme.TextMain; };
            l.MouseLeave += delegate { l.ForeColor = Theme.TextSub; };
            l.Click += delegate
            {
                if (glyph == "✕")
                {
                    Hide(); // 隐藏到托盘
                }
                else
                {
                    WindowState = FormWindowState.Minimized;
                }
            };
            return l;
        }

        private static NumericUpDown DarkNumeric(int x, decimal val)
        {
            NumericUpDown n = new NumericUpDown();
            n.Minimum = 50; n.Maximum = 95;
            n.Location = new Point(x, 54);
            n.Width = 52;
            n.Font = new Font("Segoe UI", 9f);
            n.BorderStyle = BorderStyle.FixedSingle;
            n.BackColor = Theme.CardHover;
            n.ForeColor = Theme.TextMain;
            n.Value = val;
            return n;
        }

        private static ComboBox DarkCombo(int x, int y)
        {
            ComboBox c = new ComboBox();
            c.DropDownStyle = ComboBoxStyle.DropDownList;
            c.FlatStyle = FlatStyle.Flat;
            c.Location = new Point(x, y);
            c.Width = 240;
            c.Font = new Font("Microsoft YaHei UI", 9f);
            c.BackColor = Theme.CardHover;
            c.ForeColor = Theme.TextMain;
            c.DrawMode = DrawMode.OwnerDrawFixed;
            c.DrawItem += delegate (object s, DrawItemEventArgs e)
            {
                ComboBox cb = (ComboBox)s;
                e.DrawBackground();
                if (e.Index >= 0)
                {
                    Color tc = (e.State & DrawItemState.Selected) == DrawItemState.Selected
                        ? Theme.Accent : Theme.TextMain;
                    using (SolidBrush b = new SolidBrush(tc))
                        e.Graphics.DrawString(cb.Items[e.Index].ToString(), e.Font, b, e.Bounds);
                }
            };
            return c;
        }

        // 风扇转速：fn9 轮询（游戏盒子同源数据）；EC 事件（id 26/32）仅作补充
        private bool HasFanData()
        {
            return GetCpuRpm().HasValue || GetGpuRpm().HasValue;
        }

        private int? GetCpuRpm()
        {
            if (_app.FanStatus != null) return _app.FanStatus.CpuRpm;
            return _app.FanWatcher != null ? _app.FanWatcher.CpuRpm : null;
        }

        private int? GetGpuRpm()
        {
            if (_app.FanStatus != null) return _app.FanStatus.GpuRpm;
            return _app.FanWatcher != null ? _app.FanWatcher.GpuRpm : null;
        }

        // ---------------- 状态同步 ----------------
        private void LoadState()
        {
            _suppressEvents = true;
            try
            {
                _tglAuto.Checked = _app.Settings.AutoEnabled;
                _numLow.Value = (decimal)_app.Settings.LowThreshold;
                _numHigh.Value = (decimal)_app.Settings.HighThreshold;
                _numLow.Enabled = _tglAuto.Checked;
                _numHigh.Enabled = _tglAuto.Checked;

                if (!_app.Mifs.Available)
                {
                    _lblStatus.Text = "MIFS 不可用: " + _app.Mifs.InitError;
                    _segProfiles.Enabled = false;
                    _tglAuto.Enabled = false;
                }
                else
                {
                    _cboGpuMode.Items.Add("混合模式（双显卡）");
                    _cboGpuMode.Items.Add("独显直连");
                    // 当前模式按系统里集显是否可见来识别；识别不出就保持未选中，不去猜
                    int? gpuMode = DetectGpuMode();
                    if (gpuMode.HasValue)
                        _cboGpuMode.SelectedIndex = gpuMode.Value;

                    UpdateFnLockLabel();

                    _tglTouchpad.Checked = TouchpadControl.IsDisabled();
                    _tglStartup.Checked = StartupManager.IsEnabled();
                }
            }
            finally
            {
                _suppressEvents = false;
            }
            HighlightProfile();
            OnSensorsUi();
        }

        private void UpdateFnLockLabel()
        {
            // Fn 锁状态只来自 EC 事件（id 7，按 Fn+Esc 时推送）。
            // 0=开 / 1=关 这个映射是按用户对界面的反馈定的，还没有独立验证过；
            // fn11 的读取值在本机恒为 1，不能当状态用，所以这里不做回退。
            int? fn = _app.FanWatcher != null ? _app.FanWatcher.FnLockEventValue : (int?)null;
            if (!fn.HasValue)
            {
                _lblFnLockState.Text = "--（按 Fn+Esc 后显示）";
                _lblFnLockState.ForeColor = Theme.TextDim;
            }
            else
            {
                _lblFnLockState.Text = fn.Value == 0 ? "开（Fn+Esc 切换）" : "关（Fn+Esc 切换）";
                _lblFnLockState.ForeColor = fn.Value == 0 ? Theme.Orange : Theme.Green;
            }
        }

        /// <summary>
        /// 通过系统实际显卡状态识别 GPU 模式：
        /// 混合模式下 AMD 集显（Radeon Graphics）作为适配器可见；独显直连时消失。
        /// </summary>
        private static int? DetectGpuMode()
        {
            try
            {
                bool hasAmdIgpu = false;
                using (System.Management.ManagementObjectSearcher searcher =
                    new System.Management.ManagementObjectSearcher("SELECT Name, Status FROM Win32_VideoController"))
                {
                    foreach (System.Management.ManagementObject o in searcher.Get())
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

        private void HighlightProfile()
        {
            _segProfiles.Selected = _app.CurrentProfile;
        }

        private void ApplyGpuMode()
        {
            if (_cboGpuMode.SelectedIndex < 0) return;
            int mode = _cboGpuMode.SelectedIndex;
            string name = mode == 0 ? "混合模式" : "独显直连";
            if (MessageBox.Show(
                    "将向固件发送切换到「" + name + "」的指令。\n\n" +
                    "这个功能在本机还没有验证过是否真的生效（要重启才能看到结果）。\n确定发送吗？",
                    "MiCtl", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
            {
                if (_app.Mifs.SetGpuMode(mode))
                    MessageBox.Show(
                        "固件已接受指令。\n\n" +
                        "是否真的切换成功要重启后才能确认。如果重启后模式没变，" +
                        "说明这台机器不支持软件切换，请在 BIOS 里设置。",
                        "MiCtl", MessageBoxButtons.OK, MessageBoxIcon.Information);
                else
                    MessageBox.Show("指令未被固件接受。", "MiCtl",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
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

        // ---------------- 后台事件 marshaling ----------------
        private void OnSensorsUpdatedThread()
        {
            try { BeginInvoke((Action)OnSensorsUi); }
            catch { }
        }

        private void OnProfileChangedThread()
        {
            try { BeginInvoke((Action)delegate
            {
                HighlightProfile();
                UpdateFnLockLabel();
            }); }
            catch { }
        }

        private void OnSensorsUi()
        {
            // 触控板：EC 事件状态（value 0=禁用 / 1=启用，键盘快捷键切换）与设备禁用任一即视为禁用
            _suppressEvents = true;
            try
            {
                int? ecTp = _app.FanWatcher != null ? _app.FanWatcher.TouchpadEventValue : (int?)null;
                bool ecDisabled = ecTp.HasValue && ecTp.Value == 0;
                _tglTouchpad.Checked = ecDisabled || TouchpadControl.IsDisabled();
            }
            finally { _suppressEvents = false; }

            // CPU / GPU 温度：AppController 已选好来源（固件上报优先，其次传感器库）
            if (_app.CpuTemp.HasValue)
            {
                double t = _app.CpuTemp.Value;
                _lblCpuTemp.Text = t.ToString("0") + "°";
                _dotCpu.DotColor = Theme.TempColor(t);
            }
            else
            {
                _lblCpuTemp.Text = "--";
                _dotCpu.DotColor = Theme.TextDim;
            }

            if (_app.GpuTemp.HasValue)
            {
                double t = _app.GpuTemp.Value;
                _lblGpuTemp.Text = t.ToString("0") + "°";
                _dotGpu.DotColor = Theme.TempColor(t);
            }
            else
            {
                _lblGpuTemp.Text = "--";
                _dotGpu.DotColor = Theme.TextDim;
            }

            _lblGpuDetail.Text = "GPU 负载 " +
                (_app.Sensors.GpuLoad.HasValue ? _app.Sensors.GpuLoad.Value.ToString("0") + "%" : "--%") +
                "  ·  功耗 " +
                (_app.Sensors.GpuPower.HasValue ? _app.Sensors.GpuPower.Value.ToString("0.#") + "W" : "--W") +
                (_app.Sensors.CpuPower.HasValue
                    ? "  ·  CPU " + _app.Sensors.CpuPower.Value.ToString("0.#") + "W"
                    : "");

            // 状态行：如实写明数据来源
            if (_app.Mifs.Available)
                _lblStatus.Text = "固件接口已连接  ·  温度/转速来源：" +
                    (_app.FanStatus != null ? "固件（与游戏盒子同源）" : "传感器库");

            // 网速 + 风扇转速
            string netPart = "↓ " + FormatSpeed(_app.Sensors.DownKbps) +
                             "  ↑ " + FormatSpeed(_app.Sensors.UpKbps);
            string fanPart = "";
            if (HasFanData())
            {
                string cpu = GetCpuRpm().HasValue ? GetCpuRpm().Value.ToString() : "--";
                string gpu = GetGpuRpm().HasValue ? GetGpuRpm().Value.ToString() : "--";
                fanPart = "  ·  风扇 " + cpu + " / " + gpu + " RPM";
            }
            _lblNetFan.Text = netPart + fanPart;
        }

        private static string FormatSpeed(double kbps)
        {
            if (kbps >= 1024)
                return (kbps / 1024).ToString("0.#") + " MB/s";
            return kbps.ToString("0.#") + " KB/s";
        }
    }
}
