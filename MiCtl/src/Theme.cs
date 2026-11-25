// 主题与绘制基础 —— 配色、DPI 缩放、字体缓存、GDI+ 辅助
// 所有布局常量按 96 DPI 的"逻辑像素"书写，经 S()/R()/Sz() 换算成物理像素；
// 字体统一用 GraphicsUnit.Pixel，于是屏幕渲染与离屏预览（probe/UiPreview）可以逐像素复现。
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;
using System.Windows.Forms;

namespace MiCtl
{
    public static class Theme
    {
        // ---------- 缩放 ----------
        /// <summary>物理像素 / 逻辑像素。Program 启动时按屏幕 DPI 设置；预览工具可设为任意值。</summary>
        public static float Scale = 1f;

        public static int S(int v) { return (int)Math.Round(v * Scale); }
        public static float Sf(float v) { return v * Scale; }
        public static Point P(int x, int y) { return new Point(S(x), S(y)); }
        public static Size Sz(int w, int h) { return new Size(S(w), S(h)); }
        public static Rectangle R(int x, int y, int w, int h) { return new Rectangle(S(x), S(y), S(w), S(h)); }

        // ---------- 配色（深色） ----------
        public static readonly Color Bg          = Color.FromArgb(0x12, 0x12, 0x16);   // 窗口底
        public static readonly Color Card        = Color.FromArgb(0x1B, 0x1B, 0x22);   // 卡片底
        public static readonly Color CardTop     = Color.FromArgb(0x20, 0x20, 0x29);   // 卡片顶部（渐变）
        public static readonly Color CardHover   = Color.FromArgb(0x23, 0x23, 0x2C);   // 输入底 / 悬停
        public static readonly Color CardRaised  = Color.FromArgb(0x2E, 0x2E, 0x3A);   // 中性选中块（设置项）
        public static readonly Color CardBorder  = Color.FromArgb(0x2C, 0x2C, 0x37);   // 描边 / 分隔线
        public static readonly Color RingTrack   = Color.FromArgb(0x2A, 0x2A, 0x34);   // 仪表环底轨

        public static readonly Color Accent      = Color.FromArgb(0xFF, 0x7A, 0x2F);   // 唯一强调色：动作
        public static readonly Color OnAccent    = Color.FromArgb(0x15, 0x15, 0x1A);   // 强调色上的文字

        public static readonly Color TextMain    = Color.FromArgb(0xED, 0xED, 0xF2);
        public static readonly Color TextSub     = Color.FromArgb(0xA0, 0xA0, 0xAE);
        public static readonly Color TextDim     = Color.FromArgb(0x66, 0x66, 0x73);

        public static readonly Color Green       = Color.FromArgb(0x4F, 0xD1, 0xA5);   // 静谧 / 正常
        public static readonly Color Yellow      = Color.FromArgb(0xFF, 0xC9, 0x4D);   // 偏热
        public static readonly Color Orange      = Color.FromArgb(0xFF, 0x8A, 0x3D);   // 热 / 激活
        public static readonly Color Red         = Color.FromArgb(0xFF, 0x5D, 0x5D);   // 极速 / 过热
        public static readonly Color Blue        = Color.FromArgb(0x5C, 0xA8, 0xFF);   // 均衡

        public static readonly Color TrackOn     = Accent;
        public static readonly Color TrackOff    = Color.FromArgb(0x2C, 0x2C, 0x37);

        // ---------- 颜色工具 ----------
        public static Color WithAlpha(Color c, int alpha)
        {
            return Color.FromArgb(Math.Max(0, Math.Min(255, alpha)), c.R, c.G, c.B);
        }

        public static Color Lerp(Color a, Color b, float t)
        {
            if (t <= 0f) return a;
            if (t >= 1f) return b;
            return Color.FromArgb(
                (int)Math.Round(a.R + (b.R - a.R) * t),
                (int)Math.Round(a.G + (b.G - a.G) * t),
                (int)Math.Round(a.B + (b.B - a.B) * t));
        }

        /// <summary>温度 → 状态色：40°C 青绿，62°C 暖黄，78°C 橙，92°C 红，之间线性插值</summary>
        public static Color TempColor(double t)
        {
            if (t <= 40) return Green;
            if (t <= 62) return Lerp(Green, Yellow, (float)((t - 40) / 22.0));
            if (t <= 78) return Lerp(Yellow, Orange, (float)((t - 62) / 16.0));
            if (t <= 92) return Lerp(Orange, Red, (float)((t - 78) / 14.0));
            return Red;
        }

        public static Color ProfileColor(MifsClient.Profile p)
        {
            switch (p)
            {
                case MifsClient.Profile.Quiet: return Green;
                case MifsClient.Profile.Balanced: return Blue;
                default: return Red;
            }
        }

        // ---------- 字体 ----------
        // family: "ui" = Segoe UI（拉丁/数字），"ui-sb" = Segoe UI Semibold，"cn" = Microsoft YaHei UI，"mono" = Consolas
        public const string Ui = "ui";
        public const string UiSemibold = "ui-sb";
        public const string Cn = "cn";
        public const string Mono = "mono";
        public const string Icons = "icons";

        private static readonly Dictionary<string, Font> _fonts = new Dictionary<string, Font>();
        private static string FamilyOf(string key)
        {
            switch (key)
            {
                case UiSemibold: return "Segoe UI Semibold";
                case Cn: return "Microsoft YaHei UI";
                case Mono: return "Consolas";
                case Icons: return "Segoe MDL2 Assets";
                default: return "Segoe UI";
            }
        }

        /// <summary>取字体（按逻辑像素大小，自动乘缩放；进程内缓存，不要 Dispose 返回值）</summary>
        public static Font F(string family, float px)
        {
            return F(family, px, FontStyle.Regular);
        }

        public static Font F(string family, float px, FontStyle style)
        {
            string key = family + "|" + px.ToString(CultureInfo.InvariantCulture) + "|" + (int)style + "|" + Scale.ToString(CultureInfo.InvariantCulture);
            Font f;
            if (_fonts.TryGetValue(key, out f)) return f;
            f = new Font(FamilyOf(family), Sf(px), style, GraphicsUnit.Pixel);
            _fonts[key] = f;
            return f;
        }

        // ---------- 绘制辅助 ----------
        /// <summary>统一画质：抗锯齿 + ClearType。所有自绘控件在 OnPaint 开头调用。</summary>
        public static void Prepare(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        }

        /// <summary>圆角矩形路径（浮点，供描边与填充共用）</summary>
        public static GraphicsPath Round(float x, float y, float w, float h, float r)
        {
            GraphicsPath p = new GraphicsPath();
            if (w <= 0 || h <= 0) { p.AddRectangle(new RectangleF(x, y, Math.Max(0, w), Math.Max(0, h))); return p; }
            float d = r * 2f;
            if (d > w) d = w;
            if (d > h) d = h;
            p.AddArc(x, y, d, d, 180, 90);
            p.AddArc(x + w - d, y, d, d, 270, 90);
            p.AddArc(x + w - d, y + h - d, d, d, 0, 90);
            p.AddArc(x, y + h - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static GraphicsPath Round(Rectangle r, float radius)
        {
            return Round(r.X, r.Y, r.Width, r.Height, radius);
        }

        /// <summary>填充圆角面（可选垂直渐变、可选 1px 描边）</summary>
        public static void FillRound(Graphics g, Rectangle r, int radius, Color top, Color bottom, Color? border)
        {
            using (GraphicsPath p = Round(r.X, r.Y, r.Width, r.Height, Sf(radius)))
            using (Brush b = bottom == top
                ? (Brush)new SolidBrush(top)
                : new LinearGradientBrush(new Rectangle(r.X, r.Y, r.Width, Math.Max(1, r.Height)), top, bottom, LinearGradientMode.Vertical))
            {
                g.FillPath(b, p);
                if (border.HasValue)
                {
                    // 路径整体收进半个像素，让 1px 描边落在像素中心，得到实色边线
                    using (GraphicsPath s = Round(r.X + 0.5f, r.Y + 0.5f, r.Width - 1f, r.Height - 1f, Sf(radius)))
                    using (Pen pen = new Pen(border.Value, 1f))
                        g.DrawPath(pen, s);
                }
            }
        }

        public static void StrokeRound(Graphics g, Rectangle r, int radius, Color color)
        {
            using (GraphicsPath s = Round(r.X + 0.5f, r.Y + 0.5f, r.Width - 1f, r.Height - 1f, Sf(radius)))
            using (Pen pen = new Pen(color, 1f))
                g.DrawPath(pen, s);
        }

        /// <summary>水平 1px 分隔线（落在像素中心）</summary>
        public static void HLine(Graphics g, int x, int y, int width, Color color)
        {
            using (Pen pen = new Pen(color, 1f))
                g.DrawLine(pen, S(x), S(y) + 0.5f, S(x + width), S(y) + 0.5f);
        }

        // ---------- 文本（GDI，锐利；与 Label 的渲染一致） ----------
        public const TextFormatFlags CenterFlags =
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
            TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;

        public static void TextCenter(Graphics g, string text, Font font, Color color, Rectangle box)
        {
            TextRenderer.DrawText(g, text, font, box, color, CenterFlags);
        }

        public static void TextLeft(Graphics g, string text, Font font, Color color, int x, int y)
        {
            Rectangle box = new Rectangle(S(x), S(y), S(4096), S(4096));
            TextRenderer.DrawText(g, text, font, box, color,
                TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.SingleLine |
                TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
        }

        public static void TextRight(Graphics g, string text, Font font, Color color, int rightX, int y)
        {
            Size sz = TextRenderer.MeasureText(g, text, font, new Size(int.MaxValue, int.MaxValue),
                TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
            Rectangle box = new Rectangle(S(rightX) - sz.Width, S(y), sz.Width, sz.Height);
            TextRenderer.DrawText(g, text, font, box, color,
                TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.SingleLine |
                TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
        }

        private const TextFormatFlags MeasureFlags =
            TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;

        public static Size Measure(Graphics g, string text, Font font)
        {
            if (g == null)
                return TextRenderer.MeasureText(text, font, new Size(int.MaxValue, int.MaxValue), MeasureFlags);
            return TextRenderer.MeasureText(g, text, font, new Size(int.MaxValue, int.MaxValue), MeasureFlags);
        }

        /// <summary>数值格式化；读不到时显示 fallback</summary>
        public static string Num(double? v, string fmt, string fallback)
        {
            return v.HasValue ? v.Value.ToString(fmt) : fallback;
        }
    }
}
