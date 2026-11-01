using System.Drawing;
using System.Drawing.Drawing2D;
using DualSenseApp.Hid;
using DualSenseApp.Ui;

namespace DualSenseApp.Mapping
{
    /// <summary>
    /// Disegna una mappa del controller con lo stato dei comandi in tempo reale.
    /// Il layout e' calcolato in coordinate relative all'area passata, in modo
    /// che nessun elemento si sovrapponga.
    /// </summary>
    public static class InputVisualizer
    {
        private static readonly Font FontLabel = new("Consolas", 7.5f, FontStyle.Bold);
        private static readonly Font FontGlyph = new("Segoe UI Symbol", 11f, FontStyle.Bold);
        private static readonly Font FontSection = new("Consolas", 8f, FontStyle.Bold);

        private static readonly StringFormat Centered = new()
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center
        };

        public static void DrawControllerLayout(Graphics g, Rectangle area, PadState pad, AnimeTheme theme)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Riga 1: barre trigger a sinistra e destra
            DrawTriggerBar(g, new Rectangle(area.X + 24, area.Y + 24, 170, 20), pad.L2, "L2", theme);
            DrawTriggerBar(g, new Rectangle(area.Right - 194, area.Y + 24, 170, 20), pad.R2, "R2", theme);

            // Riga 2: dorsali
            DrawPill(g, new Rectangle(area.X + 24, area.Y + 56, 80, 26), "L1", pad.L1, theme);
            DrawPill(g, new Rectangle(area.X + 110, area.Y + 56, 80, 26), "L3", pad.L3, theme);
            DrawPill(g, new Rectangle(area.Right - 190, area.Y + 56, 80, 26), "R3", pad.R3, theme);
            DrawPill(g, new Rectangle(area.Right - 104, area.Y + 56, 80, 26), "R1", pad.R1, theme);

            // Riga 3: dpad - touchpad - tasti frontali
            int rowY = area.Y + 150;
            DrawDPad(g, new Point(area.X + 96, rowY), 30, pad, theme);
            DrawFaceButtons(g, new Point(area.Right - 96, rowY), 32, pad, theme);
            DrawTouchpad(g, new Rectangle(area.X + 300, area.Y + 100, 252, 100), pad, theme);

            // Riga 4: stick analogici
            int stickY = area.Bottom - 104;
            DrawAnalogStick(g, new Point(area.X + 128, stickY), 54, pad.LX, pad.LY, "LEFT STICK", pad.L3, theme);
            DrawAnalogStick(g, new Point(area.Right - 128, stickY), 54, pad.RX, pad.RY, "RIGHT STICK", pad.R3, theme);

            // Riga 4 centro: tasti di sistema
            int sysX = area.X + 300;
            int sysY = area.Bottom - 138;
            DrawPill(g, new Rectangle(sysX, sysY, 120, 26), "CREATE", pad.Create, theme);
            DrawPill(g, new Rectangle(sysX + 132, sysY, 120, 26), "OPTIONS", pad.Options, theme);
            DrawPill(g, new Rectangle(sysX, sysY + 34, 120, 26), "PS", pad.Ps, theme);
            DrawPill(g, new Rectangle(sysX + 132, sysY + 34, 120, 26), "MUTE", pad.Mute, theme);
            DrawPill(g, new Rectangle(sysX, sysY + 68, 252, 26), "TOUCHPAD CLICK", pad.TouchpadClick, theme);
        }

        private static void DrawPill(Graphics g, Rectangle r, string label, bool pressed, AnimeTheme theme)
        {
            Color fill = pressed ? Color.FromArgb(150, theme.PrimaryNeon) : Color.FromArgb(40, theme.SecondaryNeon);
            Color border = pressed ? theme.PrimaryNeon : Color.FromArgb(110, theme.SecondaryNeon);

            using var path = RoundedRect(r, r.Height / 2);
            using var brush = new SolidBrush(fill);
            using var pen = new Pen(border, 1.5f);
            g.FillPath(brush, path);
            g.DrawPath(pen, path);

            using var text = new SolidBrush(pressed ? theme.TextMain : theme.TextDim);
            g.DrawString(label, FontLabel, text, r, Centered);
        }

        private static void DrawTriggerBar(Graphics g, Rectangle r, int value, string label, AnimeTheme theme)
        {
            int clamped = Math.Clamp(value, 0, 255);

            using var labelBrush = new SolidBrush(theme.TextDim);
            g.DrawString($"{label} {clamped * 100 / 255,3}%", FontSection, labelBrush, r.X, r.Y - 16);

            var track = new Rectangle(r.X, r.Y, r.Width, r.Height);
            using var bg = new SolidBrush(Color.FromArgb(40, theme.SecondaryNeon));
            using var pen = new Pen(Color.FromArgb(110, theme.SecondaryNeon), 1.5f);
            using var path = RoundedRect(track, r.Height / 2);
            g.FillPath(bg, path);

            int fillWidth = clamped * r.Width / 255;
            if (fillWidth > r.Height)
            {
                var fillRect = new Rectangle(r.X, r.Y, fillWidth, r.Height);
                using var fillPath = RoundedRect(fillRect, r.Height / 2);
                using var fillBrush = new SolidBrush(theme.PrimaryNeon);
                g.FillPath(fillBrush, fillPath);
            }

            g.DrawPath(pen, path);
        }

        private static void DrawFaceButtons(Graphics g, Point center, int spread, PadState pad, AnimeTheme theme)
        {
            DrawRoundButton(g, new Point(center.X, center.Y - spread), 17, "\u25B3", pad.Triangle, theme);
            DrawRoundButton(g, new Point(center.X - spread, center.Y), 17, "\u25A1", pad.Square, theme);
            DrawRoundButton(g, new Point(center.X + spread, center.Y), 17, "\u25CB", pad.Circle, theme);
            DrawRoundButton(g, new Point(center.X, center.Y + spread), 17, "\u2715", pad.Cross, theme);

            using var labelBrush = new SolidBrush(theme.TextDim);
            g.DrawString("FACE", FontSection, labelBrush,
                new Rectangle(center.X - 40, center.Y + spread + 20, 80, 14), Centered);
        }

        private static void DrawRoundButton(Graphics g, Point center, int radius, string glyph, bool pressed, AnimeTheme theme)
        {
            var r = new Rectangle(center.X - radius, center.Y - radius, radius * 2, radius * 2);
            using var brush = new SolidBrush(pressed ? Color.FromArgb(170, theme.PrimaryNeon) : Color.FromArgb(40, theme.SecondaryNeon));
            using var pen = new Pen(pressed ? theme.PrimaryNeon : Color.FromArgb(110, theme.SecondaryNeon), 1.5f);
            g.FillEllipse(brush, r);
            g.DrawEllipse(pen, r);

            using var text = new SolidBrush(pressed ? theme.TextMain : theme.TextDim);
            g.DrawString(glyph, FontGlyph, text, r, Centered);
        }

        private static void DrawDPad(Graphics g, Point center, int spread, PadState pad, AnimeTheme theme)
        {
            DrawRoundButton(g, new Point(center.X, center.Y - spread), 15, "\u25B2", pad.DpadUp, theme);
            DrawRoundButton(g, new Point(center.X - spread, center.Y), 15, "\u25C0", pad.DpadLeft, theme);
            DrawRoundButton(g, new Point(center.X + spread, center.Y), 15, "\u25B6", pad.DpadRight, theme);
            DrawRoundButton(g, new Point(center.X, center.Y + spread), 15, "\u25BC", pad.DpadDown, theme);

            using var labelBrush = new SolidBrush(theme.TextDim);
            g.DrawString("D-PAD", FontSection, labelBrush,
                new Rectangle(center.X - 40, center.Y + spread + 20, 80, 14), Centered);
        }

        private static void DrawAnalogStick(Graphics g, Point center, int radius, int rawX, int rawY, string label, bool pressed, AnimeTheme theme)
        {
            var r = new Rectangle(center.X - radius, center.Y - radius, radius * 2, radius * 2);

            using var bg = new SolidBrush(Color.FromArgb(200, 14, 12, 22));
            using var ring = new Pen(pressed ? theme.PrimaryNeon : Color.FromArgb(110, theme.SecondaryNeon), 1.5f);
            using var cross = new Pen(Color.FromArgb(45, theme.SecondaryNeon), 1f) { DashStyle = DashStyle.Dot };
            g.FillEllipse(bg, r);
            g.DrawEllipse(ring, r);
            g.DrawLine(cross, center.X - radius, center.Y, center.X + radius, center.Y);
            g.DrawLine(cross, center.X, center.Y - radius, center.X, center.Y + radius);

            float nx = Math.Clamp((rawX - 128) / 128f, -1f, 1f);
            float ny = Math.Clamp((rawY - 128) / 128f, -1f, 1f);
            int dotX = center.X + (int)(nx * (radius - 10));
            int dotY = center.Y + (int)(ny * (radius - 10));

            using var trail = new Pen(Color.FromArgb(120, theme.PrimaryNeon), 1.5f);
            g.DrawLine(trail, center.X, center.Y, dotX, dotY);
            using var dot = new SolidBrush(theme.SecondaryNeon);
            g.FillEllipse(dot, dotX - 6, dotY - 6, 12, 12);

            using var labelBrush = new SolidBrush(theme.TextDim);
            g.DrawString(label, FontSection, labelBrush,
                new Rectangle(center.X - radius, center.Y + radius + 6, radius * 2, 14), Centered);
            g.DrawString($"X {rawX,3}  Y {rawY,3}", FontLabel, labelBrush,
                new Rectangle(center.X - radius, center.Y + radius + 22, radius * 2, 14), Centered);
        }

        private static void DrawTouchpad(Graphics g, Rectangle r, PadState pad, AnimeTheme theme)
        {
            using var bg = new SolidBrush(Color.FromArgb(200, 14, 12, 22));
            using var pen = new Pen(pad.TouchpadClick ? theme.PrimaryNeon : Color.FromArgb(110, theme.SecondaryNeon), 1.5f);
            using var path = RoundedRect(r, 10);
            g.FillPath(bg, path);
            g.DrawPath(pen, path);

            DrawTouchPoint(g, r, pad.Touch1Active, pad.Touch1X, pad.Touch1Y, theme.PrimaryNeon);
            DrawTouchPoint(g, r, pad.Touch2Active, pad.Touch2X, pad.Touch2Y, theme.SecondaryNeon);

            using var labelBrush = new SolidBrush(theme.TextDim);
            g.DrawString("TOUCHPAD", FontSection, labelBrush, r.X + 8, r.Y + 6);
        }

        private static void DrawTouchPoint(Graphics g, Rectangle pad, bool active, int x, int y, Color color)
        {
            if (!active)
                return;

            // Risoluzione touchpad Sony: 1920 x 1080
            int px = pad.X + (int)(Math.Clamp(x, 0, 1920) / 1920f * pad.Width);
            int py = pad.Y + (int)(Math.Clamp(y, 0, 1080) / 1080f * pad.Height);

            using var brush = new SolidBrush(color);
            using var halo = new Pen(Color.FromArgb(120, color), 1.5f);
            g.FillEllipse(brush, px - 5, py - 5, 10, 10);
            g.DrawEllipse(halo, px - 10, py - 10, 20, 20);
        }

        private static GraphicsPath RoundedRect(Rectangle r, int radius)
        {
            int d = Math.Max(2, radius * 2);
            var path = new GraphicsPath();
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
