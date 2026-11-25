// 自绘控件集 —— GDI+ 绘制。所有尺寸按 96 DPI 逻辑像素书写，经 Theme.S() 缩放。
// 文本统一走 GDI（TextRenderer），与 Label 渲染一致、小字号更锐利。
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace MiCtl
{
    /// <summary>自绘控件基类：双缓冲、透明背景、可选焦点、失焦/得焦重绘</summary>
    public abstract class PaintControl : Control
    {
        protected PaintControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor | ControlStyles.Selectable, true);
            BackColor = Color.Transparent;
        }

        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
        protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }

        /// <summary>键盘聚焦时画一圈细边，鼠标操作时不显示</summary>
        protected void DrawFocus(Graphics g, int radius)
        {
            if (Focused && ShowFocusCues)
                Theme.StrokeRound(g, new Rectangle(0, 0, Width - 1, Height - 1), radius,
                    Theme.WithAlpha(Theme.TextMain, 100));
        }
    }

    /// <summary>圆角面：卡片 / 分组底板（垂直渐变 + 1px 描边）</summary>
    public class RoundedCard : Panel
    {
        public int Radius = 16;
        public Color FillTop = Theme.CardTop;
        public Color FillBottom = Theme.Card;
        public bool DrawBorder = true;

        public RoundedCard()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Bg;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Theme.Prepare(e.Graphics);
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            Theme.FillRound(e.Graphics, r, Radius, FillTop, FillBottom,
                DrawBorder ? (Color?)Theme.CardBorder : null);
        }
    }

    /// <summary>标题栏图标按钮（Segoe MDL2 字形）</summary>
    public class IconButton : PaintControl
    {
        private readonly string _glyph;
        private bool _hover;

        public IconButton(string glyph)
        {
            _glyph = glyph;
            Cursor = Cursors.Hand;
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _hover = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _hover = false; Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Theme.Prepare(e.Graphics);
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            if (_hover)
                Theme.FillRound(e.Graphics, r, 8, Theme.WithAlpha(Color.White, 16), Theme.WithAlpha(Color.White, 16), null);
            Theme.TextCenter(e.Graphics, _glyph, Theme.F(Theme.Icons, 10),
                _hover ? Theme.TextMain : Theme.TextSub, r);
        }
    }

    /// <summary>
    /// 温度仪表环：270° 弧（缺口在底部），环内大数字 + 度符号，下方标签与副文本。
    /// 数值变化时 400ms 左右缓动到新值，动画结束后定时器自动停止。
    /// </summary>
    public class GaugeRing : PaintControl
    {
        private string _label = "";
        private string _sub = "";
        private string _unit = "°";             // 数字后的符号：° 或 %
        private bool _tempColor = true;          // true：颜色随温度变；false：固定色
        private Color _fixedColor = Theme.Blue;
        private bool _hasValue;
        private double _shown;
        private double _target;
        private Timer _timer;

        public GaugeRing(string label)
        {
            _label = label;
        }

        /// <summary>设定数值（°C）与环内副文本；value 为 null 时显示 "--"</summary>
        public void SetValue(double? value, string sub)
        {
            string ns = sub ?? "";
            bool subChanged = ns != _sub;
            _sub = ns;
            if (!value.HasValue)
            {
                _hasValue = false;
                StopTimer();
                Invalidate();
                return;
            }
            double nv = Math.Max(0, Math.Min(150, value.Value));
            if (!_hasValue)
            {
                _hasValue = true;
                _shown = nv;
                _target = nv;
                StopTimer();
                Invalidate();
                return;
            }
            if (Math.Abs(nv - _target) < 0.01)
            {
                // 温度没变但功耗变了：文字仍要重绘
                if (subChanged) Invalidate();
                return;
            }
            _target = nv;
            StartTimer();
        }

        /// <summary>预览用：跳到目标值并停止动画</summary>
        public void Jump()
        {
            if (_hasValue) _shown = _target;
            StopTimer();
            Invalidate();
        }

        /// <summary>
        /// 弧长始终是 值/100。温度环：单位 °、颜色随温度；百分比环（内存）：单位 %、固定色。
        /// </summary>
        public void SetMode(string unit, bool temperatureColor, Color fixedColor)
        {
            _unit = unit ?? "°";
            _tempColor = temperatureColor;
            _fixedColor = fixedColor;
            Invalidate();
        }

        private void StartTimer()
        {
            if (_timer == null)
            {
                _timer = new Timer();
                _timer.Interval = 16;
                _timer.Tick += OnTick;
            }
            _timer.Start();
        }

        private void StopTimer() { if (_timer != null) _timer.Stop(); }

        private void OnTick(object sender, EventArgs e)
        {
            double d = _target - _shown;
            if (Math.Abs(d) < 0.12)
            {
                _shown = _target;
                StopTimer();
            }
            else
            {
                _shown += d * 0.24;
            }
            Invalidate();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _timer != null) { _timer.Dispose(); _timer = null; }
            base.Dispose(disposing);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Theme.Prepare(g);

            // 硬件名称在环的正上方（环内只放数字和副文本，避免文字碰到弧线）
            int top = Theme.S(14);
            Theme.TextCenter(g, _label, Theme.F(Theme.Cn, 10.5f), Theme.TextSub,
                new Rectangle(0, 0, Width, top + Theme.S(2)));

            float stroke = Theme.Sf(8);
            RectangleF arc = new RectangleF(stroke / 2f + Theme.Sf(1), top + stroke / 2f + Theme.Sf(1),
                Width - stroke - Theme.Sf(2), Height - top - stroke - Theme.Sf(2));

            using (Pen track = new Pen(Theme.RingTrack, stroke))
            {
                track.StartCap = LineCap.Round;
                track.EndCap = LineCap.Round;
                g.DrawArc(track, arc, 135f, 270f);
            }

            Color color = !_hasValue ? Theme.TextDim : (_tempColor ? Theme.TempColor(_shown) : _fixedColor);
            if (_hasValue)
            {
                float frac = (float)Math.Max(0, Math.Min(1, _shown / 100.0));
                if (frac > 0.004f)
                {
                    using (Pen p = new Pen(color, stroke))
                    {
                        p.StartCap = LineCap.Round;
                        p.EndCap = LineCap.Round;
                        g.DrawArc(p, arc, 135f, 270f * frac);
                    }
                }
            }

            // 环内文字：数字（Semibold 34）+ 度符号（17，贴右上）；没有读数时只显示居中的 "--"
            string numText = _hasValue ? ((int)Math.Round(_shown)).ToString() : "--";
            Font fNum = Theme.F(Theme.UiSemibold, 34);
            Font fDeg = Theme.F(Theme.Ui, _unit == "%" ? 16f : 17f);
            Size numSz = Theme.Measure(g, numText, fNum);
            Size degSz = _hasValue ? Theme.Measure(g, _unit, fDeg) : Size.Empty;
            int totalW = numSz.Width + degSz.Width - (_hasValue ? Theme.S(2) : 0);
            int left = (Width - totalW) / 2;
            int cy = top + (Height - top) / 2;
            int numTop = cy - Theme.S(27);
            Color numColor = _hasValue ? Theme.TextMain : Theme.TextDim;
            Rectangle numBox = new Rectangle(left, numTop, numSz.Width, numSz.Height);
            TextRenderer.DrawText(g, numText, fNum, numBox, numColor,
                TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.SingleLine |
                TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
            if (_hasValue)
            {
                Rectangle degBox = new Rectangle(left + numSz.Width - Theme.S(2), numTop + Theme.S(1), degSz.Width, degSz.Height);
                TextRenderer.DrawText(g, _unit, fDeg, degBox, Theme.TextSub,
                    TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.SingleLine |
                    TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
            }

            // 环内下方：副文本（等宽、亮）
            Font fSub = Theme.F(Theme.Mono, 12);
            Size subSz = _sub.Length > 0 ? Theme.Measure(g, _sub, fSub) : Size.Empty;
            int rowTop = cy + Theme.S(17);
            int x = (Width - subSz.Width) / 2;
            if (subSz.Width > 0)
            {
                TextRenderer.DrawText(g, _sub, fSub, new Rectangle(x, rowTop - Theme.S(1), subSz.Width, subSz.Height),
                    Theme.TextMain, TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.SingleLine |
                    TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
            }
        }
    }

    /// <summary>
    /// 滑动分段选择器：底轨内一块滑块随选中项滑动。
    /// 两种选中样式：彩色（档位：颜色即状态语义，选中即生效，文字用深色保证对比）
    /// 与中性（设置项：选择不等于生效，还要点"应用"，所以不占用强调色）。
    /// </summary>
    public class Segmented : PaintControl
    {
        private readonly string[] _labels;
        private readonly Color[] _colors;
        private readonly bool _neutral;
        private int _selected = -1;
        private int _hover = -1;
        private int _animFrom = -1;
        private float _animT = 1f;
        private Timer _timer;

        public event EventHandler SelectedChanged;

        public Segmented(string[] labels, Color[] colors, bool neutral)
        {
            _labels = labels;
            _colors = colors;
            _neutral = neutral;
            Cursor = Cursors.Hand;
        }

        public int Count { get { return _labels.Length; } }

        public int SelectedIndex
        {
            get { return _selected; }
            set
            {
                if (value == _selected) return;
                if (_selected >= 0 && value >= 0)
                {
                    _animFrom = _selected;
                    _animT = 0f;
                    StartTimer();
                }
                else
                {
                    _animFrom = -1;
                    _animT = 1f;
                    StopTimer();
                }
                _selected = value;
                Invalidate();
                if (SelectedChanged != null) SelectedChanged(this, EventArgs.Empty);
            }
        }

        /// <summary>预览 / 批量赋值用：立即停在终点</summary>
        public void Jump()
        {
            _animT = 1f;
            _animFrom = -1;
            StopTimer();
            Invalidate();
        }

        private void StartTimer()
        {
            if (_timer == null)
            {
                _timer = new Timer();
                _timer.Interval = 16;
                _timer.Tick += OnTick;
            }
            _timer.Start();
        }

        private void StopTimer() { if (_timer != null) _timer.Stop(); }

        private void OnTick(object sender, EventArgs e)
        {
            _animT += 0.11f;
            if (_animT >= 1f)
            {
                _animT = 1f;
                StopTimer();
            }
            Invalidate();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _timer != null) { _timer.Dispose(); _timer = null; }
            base.Dispose(disposing);
        }

        private int SegmentAt(int x)
        {
            if (_labels.Length == 0) return -1;
            float pad = Theme.Sf(3), gap = Theme.Sf(2);
            float segW = (Width - pad * 2 - gap * (_labels.Length - 1)) / _labels.Length;
            float fx = x;
            for (int i = 0; i < _labels.Length; i++)
            {
                float sx = pad + i * (segW + gap);
                if (fx < sx + segW + gap / 2f) return i;
            }
            return _labels.Length - 1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int s = SegmentAt(e.X);
            if (s != _hover) { _hover = s; Invalidate(); }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hover = -1;
            Invalidate();
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (e.Button != MouseButtons.Left || !Enabled) return;
            int s = SegmentAt(e.X);
            if (s >= 0) SelectedIndex = s;
        }

        protected override bool IsInputKey(Keys keyData)
        {
            if (keyData == Keys.Left || keyData == Keys.Right) return true;
            return base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (!Enabled) return;
            if (e.KeyCode == Keys.Left && _selected > 0) { SelectedIndex = _selected - 1; e.Handled = true; }
            else if (e.KeyCode == Keys.Right && _selected < _labels.Length - 1) { SelectedIndex = _selected + 1; e.Handled = true; }
        }

        private static float EaseOut(float t)
        {
            float u = 1f - t;
            return 1f - u * u * u;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Theme.Prepare(g);
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            Theme.FillRound(g, r, 12, Theme.Card, Theme.Card, Theme.CardBorder);

            int n = _labels.Length;
            if (n == 0) return;
            float pad = Theme.Sf(3), gap = Theme.Sf(2);
            float segW = (r.Width - pad * 2 - gap * (n - 1)) / n;
            float innerH = r.Height - pad * 2;

            if (_selected >= 0 && _selected < n)
            {
                float toX = pad + _selected * (segW + gap);
                float fromX = (_animFrom >= 0 && _animFrom < n) ? pad + _animFrom * (segW + gap) : toX;
                float x = fromX + (toX - fromX) * EaseOut(_animT);
                Rectangle sel = new Rectangle((int)Math.Round(x), (int)Math.Round(pad),
                    (int)Math.Round(segW), (int)Math.Round(innerH));
                if (_neutral)
                {
                    Color fill = Enabled ? Theme.CardRaised : Theme.Lerp(Theme.CardRaised, Theme.Card, 0.65f);
                    Theme.FillRound(g, sel, 10, fill, fill, null);
                    if (Enabled) Theme.StrokeRound(g, sel, 10, Theme.WithAlpha(Theme.TextSub, 110));
                }
                else
                {
                    Color c = _colors[Math.Min(_selected, _colors.Length - 1)];
                    Theme.FillRound(g, sel, 10, c, c, null);
                }
            }

            for (int i = 0; i < n; i++)
            {
                Rectangle box = new Rectangle((int)Math.Round(pad + i * (segW + gap)), 0, (int)Math.Round(segW), r.Height);
                bool isSel = i == _selected;
                Color tc = isSel ? (_neutral ? Theme.TextMain : Theme.OnAccent)
                    : (!Enabled ? Theme.TextDim : (i == _hover ? Theme.TextMain : Theme.TextSub));
                if (!isSel && i == _hover && Enabled)
                {
                    // 悬停的未选中段：极淡的底色，提示可点
                    Rectangle hb = new Rectangle(box.X + Theme.S(1), (int)Math.Round(pad), box.Width - Theme.S(2), (int)Math.Round(innerH));
                    Theme.FillRound(g, hb, 10, Theme.WithAlpha(Color.White, 10), Theme.WithAlpha(Color.White, 10), null);
                }
                Theme.TextCenter(g, _labels[i],
                    isSel ? Theme.F(Theme.Cn, 13, FontStyle.Bold) : Theme.F(Theme.Cn, 13), tc, box);
            }
            DrawFocus(g, 12);
        }
    }

    /// <summary>开关：轨道随状态变色，球体 120ms 缓动</summary>
    public class ToggleSwitch : PaintControl
    {
        private bool _checked;
        private float _t;
        private Timer _timer;

        public event EventHandler CheckedChanged;

        public ToggleSwitch()
        {
            Size = Theme.Sz(44, 24);
            Cursor = Cursors.Hand;
        }

        public bool Checked
        {
            get { return _checked; }
            set
            {
                if (_checked == value) return;
                _checked = value;
                StartTimer();
                Invalidate();
                if (CheckedChanged != null) CheckedChanged(this, EventArgs.Empty);
            }
        }

        /// <summary>预览 / 批量赋值用：立即停在终点</summary>
        public void Jump()
        {
            _t = _checked ? 1f : 0f;
            StopTimer();
            Invalidate();
        }

        private void StartTimer()
        {
            if (_timer == null)
            {
                _timer = new Timer();
                _timer.Interval = 16;
                _timer.Tick += OnTick;
            }
            _timer.Start();
        }

        private void StopTimer() { if (_timer != null) _timer.Stop(); }

        private void OnTick(object sender, EventArgs e)
        {
            float target = _checked ? 1f : 0f;
            float d = target - _t;
            if (Math.Abs(d) < 0.03f)
            {
                _t = target;
                StopTimer();
            }
            else
            {
                _t += d * 0.32f;
            }
            Invalidate();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _timer != null) { _timer.Dispose(); _timer = null; }
            base.Dispose(disposing);
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            if (Enabled) Checked = !Checked;
        }

        protected override bool IsInputKey(Keys keyData)
        {
            if (keyData == Keys.Space || keyData == Keys.Enter) return true;
            return base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if ((e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter) && Enabled)
            {
                Checked = !Checked;
                e.Handled = true;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Theme.Prepare(g);
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            Color track = Theme.Lerp(Theme.TrackOff, Theme.TrackOn, _t);
            if (!Enabled) track = Theme.WithAlpha(track, 110);
            Theme.FillRound(g, r, 12, track, track, null);

            int ball = Height - Theme.S(6);
            int bx = Theme.S(3) + (int)Math.Round(_t * (Width - Theme.S(6) - ball));
            Rectangle ballRect = new Rectangle(bx, Theme.S(3), ball, ball);
            using (SolidBrush b = new SolidBrush(Enabled ? Theme.TextMain : Theme.WithAlpha(Theme.TextMain, 110)))
                g.FillEllipse(b, ballRect);
            DrawFocus(g, 12);
        }
    }

    /// <summary>状态标签（胶囊）：淡色底 + 同色文字，用于 Fn 锁、触控板快捷键状态等只读信息</summary>
    public class StatusPill : PaintControl
    {
        private string _text = "";
        private Color _color = Theme.TextDim;

        public StatusPill()
        {
            Size = Theme.Sz(10, 22);
        }

        public void Set(string text, Color color)
        {
            _text = text ?? "";
            _color = color;
            Size sz = Theme.Measure(null, _text, Theme.F(Theme.Cn, 11));
            Size = new Size(sz.Width + Theme.S(18), Theme.S(22));
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Theme.Prepare(e.Graphics);
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            Theme.FillRound(e.Graphics, r, 11, Theme.WithAlpha(_color, 30), Theme.WithAlpha(_color, 30),
                Theme.WithAlpha(_color, 90));
            Theme.TextCenter(e.Graphics, _text, Theme.F(Theme.Cn, 11), _color, r);
        }
    }

    /// <summary>步进器：[−] 值 [+]，替代系统的 NumericUpDown（原生控件无法配色）</summary>
    public class Stepper : PaintControl
    {
        private const int BtnW = 30;
        private decimal _value, _min, _max, _step = 1m;
        private int _hover;

        public event EventHandler ValueChanged;

        public Stepper(decimal min, decimal max, decimal value)
        {
            _min = min; _max = max;
            _value = Math.Min(max, Math.Max(min, value));
            Size = Theme.Sz(88, 30);
            Cursor = Cursors.Hand;
        }

        public decimal Value
        {
            get { return _value; }
            set
            {
                decimal v = Math.Min(_max, Math.Max(_min, value));
                if (v == _value) return;
                _value = v;
                Invalidate();
                if (ValueChanged != null) ValueChanged(this, EventArgs.Empty);
            }
        }

        public void SetValueQuiet(decimal value)
        {
            _value = Math.Min(_max, Math.Max(_min, value));
            Invalidate();
        }

        private int HitTest(int x)
        {
            if (x < Theme.S(BtnW)) return 1;
            if (x > Width - Theme.S(BtnW)) return 2;
            return 0;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int h = Enabled ? HitTest(e.X) : 0;
            if (h != _hover) { _hover = h; Invalidate(); }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hover = 0;
            Invalidate();
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (!Enabled || e.Button != MouseButtons.Left) return;
            int h = HitTest(e.X);
            if (h == 1) Value = _value - _step;
            else if (h == 2) Value = _value + _step;
        }

        protected override bool IsInputKey(Keys keyData)
        {
            if (keyData == Keys.Up || keyData == Keys.Down) return true;
            return base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (!Enabled) return;
            if (e.KeyCode == Keys.Up) { Value = _value + _step; e.Handled = true; }
            else if (e.KeyCode == Keys.Down) { Value = _value - _step; e.Handled = true; }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Theme.Prepare(g);
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            Theme.FillRound(g, r, 8, Theme.CardHover, Theme.CardHover, Theme.CardBorder);

            if (_hover > 0)
            {
                Rectangle hz = _hover == 1
                    ? new Rectangle(Theme.S(1), Theme.S(1), Theme.S(BtnW) - Theme.S(1), Height - Theme.S(2))
                    : new Rectangle(Width - Theme.S(BtnW), Theme.S(1), Theme.S(BtnW) - Theme.S(1), Height - Theme.S(2));
                Theme.FillRound(g, hz, 7, Theme.WithAlpha(Color.White, 12), Theme.WithAlpha(Color.White, 12), null);
            }

            Color glyph = Enabled ? Theme.TextSub : Theme.TextDim;
            Theme.TextCenter(g, "\u2212", Theme.F(Theme.Ui, 13), glyph,
                new Rectangle(0, 0, Theme.S(BtnW), Height));
            Theme.TextCenter(g, "+", Theme.F(Theme.Ui, 13), glyph,
                new Rectangle(Width - Theme.S(BtnW), 0, Theme.S(BtnW), Height));
            Theme.TextCenter(g, _value.ToString("0"), Theme.F(Theme.Mono, 13),
                Enabled ? Theme.TextMain : Theme.TextDim, r);
            DrawFocus(g, 8);
        }
    }

    /// <summary>主操作按钮：强调色底 + 深色文字（白字在橙底上只有 2.9:1，深字 6:1 以上）</summary>
    public class AccentButton : PaintControl
    {
        private readonly string _text;
        private bool _hover, _press;

        public AccentButton(string text)
        {
            _text = text;
            Cursor = Cursors.Hand;
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _hover = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _hover = false; _press = false; Invalidate(); }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left) { _press = true; Focus(); Invalidate(); }
        }
        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            _press = false;
            Invalidate();
        }

        protected override bool IsInputKey(Keys keyData)
        {
            if (keyData == Keys.Space || keyData == Keys.Enter) return true;
            return base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if ((e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter) && Enabled)
            {
                OnClick(EventArgs.Empty);
                e.Handled = true;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Theme.Prepare(g);
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            Color fill = Theme.Accent;
            Color tc;
            if (!Enabled)
            {
                // 禁用：中性灰面 + 暗字，不保留任何橙色（橙色只表示"可以点"）
                fill = Theme.CardHover;
                tc = Theme.Lerp(Theme.TextDim, Theme.TextSub, 0.5f);
            }
            else
            {
                tc = Theme.OnAccent;
                if (_press) fill = Theme.Lerp(Theme.Accent, Theme.OnAccent, 0.18f);
                else if (_hover) fill = Theme.Lerp(Theme.Accent, Theme.TextMain, 0.14f);
            }
            Theme.FillRound(g, r, 10, fill, fill, null);
            Theme.TextCenter(g, _text, Theme.F(Theme.Cn, 13), tc, r);
            DrawFocus(g, 10);
        }
    }

    /// <summary>页签条：文字标签 + 随选中项滑动的强调色下划线</summary>
    public class TabStrip : PaintControl
    {
        private readonly string[] _tabs;
        private readonly int[] _x;
        private readonly int[] _w;
        private int _selected;
        private int _hover = -1;
        private float _fromX, _toX, _fromW, _toW, _t = 1f;
        private Timer _timer;

        public event EventHandler SelectedChanged;

        public TabStrip(string[] tabs)
        {
            _tabs = tabs;
            _x = new int[tabs.Length];
            _w = new int[tabs.Length];
            Cursor = Cursors.Hand;
        }

        public int SelectedIndex
        {
            get { return _selected; }
            set
            {
                if (value == _selected) return;
                _fromX = _toX;
                _fromW = _toW;
                _toX = _x[Math.Max(0, Math.Min(_tabs.Length - 1, value))];
                _toW = _w[Math.Max(0, Math.Min(_tabs.Length - 1, value))];
                _t = 0f;
                _selected = value;
                StartTimer();
                Invalidate();
                if (SelectedChanged != null) SelectedChanged(this, EventArgs.Empty);
            }
        }

        public void Jump()
        {
            _t = 1f;
            StopTimer();
            Invalidate();
        }

        private void StartTimer()
        {
            if (_timer == null)
            {
                _timer = new Timer();
                _timer.Interval = 16;
                _timer.Tick += OnTick;
            }
            _timer.Start();
        }

        private void StopTimer() { if (_timer != null) _timer.Stop(); }

        private void OnTick(object sender, EventArgs e)
        {
            _t += 0.12f;
            if (_t >= 1f) { _t = 1f; StopTimer(); }
            Invalidate();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _timer != null) { _timer.Dispose(); _timer = null; }
            base.Dispose(disposing);
        }

        private void LayoutTabs(Graphics g)
        {
            int x = 0;
            for (int i = 0; i < _tabs.Length; i++)
            {
                Size sz = Theme.Measure(g, _tabs[i], Theme.F(Theme.Cn, 13));
                _x[i] = x;
                _w[i] = sz.Width;
                x += sz.Width + Theme.S(30);
            }
            // 首次布局时把下划线放到当前选中项
            if (_toW <= 0f && _tabs.Length > 0)
            {
                _fromX = _toX = _x[_selected];
                _fromW = _toW = _w[_selected];
            }
        }

        private int Hit(int px)
        {
            for (int i = 0; i < _tabs.Length; i++)
            {
                if (px < _x[i] + _w[i] + Theme.S(15)) return i;
            }
            return _tabs.Length - 1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int h = Hit(e.X);
            if (h != _hover) { _hover = h; Invalidate(); }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hover = -1;
            Invalidate();
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (e.Button == MouseButtons.Left) SelectedIndex = Hit(e.X);
        }

        protected override bool IsInputKey(Keys keyData)
        {
            if (keyData == Keys.Left || keyData == Keys.Right) return true;
            return base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Left && _selected > 0) { SelectedIndex = _selected - 1; e.Handled = true; }
            else if (e.KeyCode == Keys.Right && _selected < _tabs.Length - 1) { SelectedIndex = _selected + 1; e.Handled = true; }
        }

        private static float EaseOut(float t)
        {
            float u = 1f - t;
            return 1f - u * u * u;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Theme.Prepare(g);
            LayoutTabs(g);

            float k = EaseOut(_t);
            float cx = _fromX + (_toX - _fromX) * k;
            float cw = _fromW + (_toW - _fromW) * k;
            float y = Height - Theme.Sf(3);
            using (Pen pen = new Pen(Theme.Accent, Theme.Sf(2)))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                if (cw > Theme.Sf(4)) g.DrawLine(pen, cx + Theme.Sf(1), y, cx + cw - Theme.Sf(1), y);
            }

            for (int i = 0; i < _tabs.Length; i++)
            {
                Color tc = i == _selected || i == _hover ? Theme.TextMain : Theme.TextSub;
                Rectangle box = new Rectangle(_x[i], 0, _w[i], Height - Theme.S(8));
                TextRenderer.DrawText(g, _tabs[i], Theme.F(Theme.Cn, 13), box, tc,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine |
                    TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
            }
            DrawFocus(g, 6);
        }
    }

    /// <summary>描边小按钮，用于次要动作（"结束"）；Danger = true 时悬停变红</summary>
    public class GhostButton : PaintControl
    {
        private readonly string _text;
        private bool _hover, _press;
        public bool Danger;

        public GhostButton(string text)
        {
            _text = text;
            Cursor = Cursors.Hand;
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _hover = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _hover = false; _press = false; Invalidate(); }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left) { _press = true; Focus(); Invalidate(); }
        }
        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            _press = false;
            Invalidate();
        }

        protected override bool IsInputKey(Keys keyData)
        {
            if (keyData == Keys.Space || keyData == Keys.Enter) return true;
            return base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if ((e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter) && Enabled)
            {
                OnClick(EventArgs.Empty);
                e.Handled = true;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Theme.Prepare(g);
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            Color border = Theme.CardBorder;
            Color tc = Enabled ? Theme.TextSub : Theme.TextDim;
            Color? fill = null;
            if (Enabled && _hover)
            {
                if (Danger)
                {
                    fill = Theme.WithAlpha(Theme.Red, 40);
                    border = Theme.Red;
                    tc = Theme.Red;
                }
                else
                {
                    fill = Theme.WithAlpha(Color.White, 12);
                    border = Theme.WithAlpha(Theme.TextSub, 150);
                    tc = Theme.TextMain;
                }
                if (_press && fill.HasValue) fill = Theme.Lerp(fill.Value, Theme.TextMain, 0.12f);
            }
            if (fill.HasValue) Theme.FillRound(g, r, 8, fill.Value, fill.Value, null);
            Theme.StrokeRound(g, r, 8, border);
            Theme.TextCenter(g, _text, Theme.F(Theme.Cn, 11), tc, r);
            DrawFocus(g, 8);
        }
    }

    /// <summary>文字按钮（链接样式）：强调色文字，悬停提亮</summary>
    public class TextButton : PaintControl
    {
        private readonly string _text;
        private bool _hover;

        public TextButton(string text)
        {
            _text = text;
            Cursor = Cursors.Hand;
        }

        /// <summary>按文字宽度算出合适的按钮宽度</summary>
        public int PreferredWidth()
        {
            return Theme.Measure(null, _text, Theme.F(Theme.Cn, 11.5f)).Width + Theme.S(6);
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _hover = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _hover = false; Invalidate(); }

        protected override bool IsInputKey(Keys keyData)
        {
            if (keyData == Keys.Space || keyData == Keys.Enter) return true;
            return base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if ((e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter) && Enabled)
            {
                OnClick(EventArgs.Empty);
                e.Handled = true;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Theme.Prepare(e.Graphics);
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            if (_hover && Enabled)
                Theme.FillRound(e.Graphics, r, 6, Theme.WithAlpha(Color.White, 10), Theme.WithAlpha(Color.White, 10), null);
            Color tc = !Enabled ? Theme.TextDim
                : (_hover ? Theme.Lerp(Theme.Accent, Theme.TextMain, 0.4f) : Theme.Accent);
            Theme.TextCenter(e.Graphics, _text, Theme.F(Theme.Cn, 11.5f), tc, r);
            DrawFocus(e.Graphics, 6);
        }
    }
}
