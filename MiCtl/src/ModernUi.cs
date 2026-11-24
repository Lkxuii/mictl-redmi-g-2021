// 现代深色 UI 组件集 —— 纯 GDI+ 自绘
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace MiCtl
{
    /// <summary>主题配色</summary>
    public static class Theme
    {
        public static readonly Color Bg = Color.FromArgb(22, 22, 30);          // 窗口背景
        public static readonly Color Card = Color.FromArgb(32, 32, 44);        // 卡片背景
        public static readonly Color CardHover = Color.FromArgb(40, 40, 54);
        public static readonly Color CardBorder = Color.FromArgb(48, 48, 62);  // 卡片描边
        public static readonly Color Accent = Color.FromArgb(255, 122, 71);    // 品牌橙
        public static readonly Color TextMain = Color.FromArgb(235, 235, 243);
        public static readonly Color TextSub = Color.FromArgb(140, 140, 156);
        public static readonly Color TextDim = Color.FromArgb(96, 96, 110);
        public static readonly Color Green = Color.FromArgb(102, 204, 153);
        public static readonly Color Orange = Color.FromArgb(255, 167, 64);
        public static readonly Color Red = Color.FromArgb(255, 96, 96);
        public static readonly Color Blue = Color.FromArgb(98, 155, 245);
        public static readonly Color TrackOn = Color.FromArgb(255, 122, 71);
        public static readonly Color TrackOff = Color.FromArgb(64, 64, 80);

        public static GraphicsPath RoundRect(Rectangle r, int radius)
        {
            GraphicsPath p = new GraphicsPath();
            if (radius <= 0 || r.Width <= 0 || r.Height <= 0)
            {
                p.AddRectangle(r);
                return p;
            }
            int d = radius * 2;
            if (d > r.Width) d = r.Width;
            if (d > r.Height) d = r.Height;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        /// <summary>按温度取状态色</summary>
        public static Color TempColor(double t)
        {
            if (t >= 75) return Red;
            if (t >= 60) return Orange;
            return Green;
        }
    }

    /// <summary>圆角卡片容器</summary>
    public class RoundedCard : Panel
    {
        public int Radius { get; set; }
        public Color FillColor { get; set; }
        public bool DrawBorder { get; set; }

        public RoundedCard()
        {
            Radius = 12;
            FillColor = Theme.Card;
            DrawBorder = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Bg;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath p = Theme.RoundRect(r, Radius))
            {
                using (SolidBrush b = new SolidBrush(FillColor))
                    e.Graphics.FillPath(b, p);
                if (DrawBorder)
                using (Pen pen = new Pen(Theme.CardBorder, 1f))
                    e.Graphics.DrawPath(pen, p);
            }
        }
    }

    /// <summary>现代开关（toggle）</summary>
    public class ToggleSwitch : Control
    {
        private bool _checked;
        public event EventHandler CheckedChanged;

        public bool Checked
        {
            get { return _checked; }
            set
            {
                if (_checked == value) return;
                _checked = value;
                Invalidate();
                if (CheckedChanged != null) CheckedChanged(this, EventArgs.Empty);
            }
        }

        public ToggleSwitch()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            Size = new Size(46, 24);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            int trackH = 12;
            Rectangle track = new Rectangle(0, (Height - trackH) / 2, Width, trackH);
            using (GraphicsPath p = Theme.RoundRect(track, trackH / 2))
            using (SolidBrush b = new SolidBrush(_checked ? Theme.TrackOn : Theme.TrackOff))
                e.Graphics.FillPath(b, p);

            int ball = Height - 6;
            int bx = _checked ? Width - ball - 2 : 2;
            Rectangle ballRect = new Rectangle(bx, (Height - ball) / 2, ball, ball);
            using (SolidBrush b = new SolidBrush(Color.White))
                e.Graphics.FillEllipse(b, ballRect);
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            Checked = !Checked;
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            base.OnEnabledChanged(e);
            Invalidate();
        }
    }

    /// <summary>分段式档位选择器（三段胶囊）</summary>
    public class SegmentedProfiles : Control
    {
        private static readonly string[] Labels = { "静谧", "均衡", "极速" };
        private static readonly MifsClient.Profile[] Values =
            { MifsClient.Profile.Quiet, MifsClient.Profile.Balanced, MifsClient.Profile.FullSpeed };

        private static readonly Color[] SelColors =
            { Theme.Green, Theme.Blue, Theme.Red };

        private MifsClient.Profile? _selected;
        private int _hoverSeg = -1;

        public event Action<MifsClient.Profile> SegmentClicked;

        public MifsClient.Profile? Selected
        {
            get { return _selected; }
            set { _selected = value; Invalidate(); }
        }

        public SegmentedProfiles()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
        }

        private int SegmentAt(int x)
        {
            int w = Width / 3;
            if (x < w) return 0;
            if (x < w * 2) return 1;
            return 2;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);

            // 整体轨道
            using (GraphicsPath p = Theme.RoundRect(r, Height / 2))
            using (SolidBrush b = new SolidBrush(Theme.Card))
                e.Graphics.FillPath(b, p);
            using (Pen pen = new Pen(Theme.CardBorder, 1f))
                e.Graphics.DrawRectangle(pen, r);

            int segW = Width / 3;
            for (int i = 0; i < 3; i++)
            {
                bool sel = _selected.HasValue && Values[i] == _selected.Value;
                Rectangle seg = new Rectangle(i * segW + 2, 2, segW - 4, Height - 4);
                if (sel)
                {
                    using (GraphicsPath p = Theme.RoundRect(seg, seg.Height / 2))
                    using (SolidBrush b = new SolidBrush(SelColors[i]))
                        e.Graphics.FillPath(b, p);
                    TextRenderer.DrawText(e.Graphics, Labels[i], Font, seg, Color.White,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }
                else
                {
                    Color textColor = _hoverSeg == i ? Theme.TextMain : Theme.TextSub;
                    TextRenderer.DrawText(e.Graphics, Labels[i], Font, seg, textColor,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }
            }
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (e.Button == MouseButtons.Left)
            {
                int seg = SegmentAt(e.X);
                _selected = Values[seg];
                Invalidate();
                if (SegmentClicked != null) SegmentClicked(Values[seg]);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int seg = SegmentAt(e.X);
            if (seg != _hoverSeg)
            {
                _hoverSeg = seg;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hoverSeg = -1;
            Invalidate();
        }
    }

    /// <summary>状态圆点（温度指示）</summary>
    public class StatusDot : Control
    {
        private Color _color = Theme.Green;
        public Color DotColor
        {
            get { return _color; }
            set { _color = value; Invalidate(); }
        }

        public StatusDot()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            Size = new Size(10, 10);
            BackColor = Color.Transparent;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (SolidBrush b = new SolidBrush(_color))
                e.Graphics.FillEllipse(b, 0, 0, Width - 1, Height - 1);
        }
    }
}
