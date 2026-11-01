using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.Runtime.InteropServices;

namespace DualSenseApp.Ui
{
    public sealed class LedColorForm : Form
    {
        private static readonly Rectangle ColorField = new(18, 60, 394, 248);
        private static readonly Rectangle HueTrack = new(18, 322, 394, 14);
        private static readonly Rectangle CloseButton = new(386, 14, 26, 26);
        private static readonly Rectangle CopyButton = new(306, 350, 36, 30);
        private readonly AnimeTheme _theme;
        private readonly TextBox _hexInput;
        private Bitmap? _colorFieldBitmap;
        private Bitmap? _hueBitmap;
        private float _hue;
        private float _saturation;
        private float _value;
        private bool _updatingHexInput;
        private PickerDragTarget _dragTarget;

        public Color SelectedColor { get; private set; }
        public event Action<Color>? ColorChanged;

        public LedColorForm(AnimeTheme theme, Color initialColor)
        {
            _theme = theme;
            SelectedColor = initialColor;
            ColorToHsv(initialColor, out _hue, out _saturation, out _value);
            Text = "CONTROLLER LED";
            ClientSize = new Size(430, 398);
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            BackColor = theme.BgDark;
            DoubleBuffered = true;
            KeyPreview = true;

            using (var path = CreateChamferRect(new Rectangle(0, 0, ClientSize.Width - 1, ClientSize.Height - 1), 12))
                Region = new Region(path);

            _hexInput = new TextBox
            {
                BorderStyle = BorderStyle.None,
                BackColor = theme.BgPanel,
                ForeColor = theme.TextMain,
                Font = new Font("Consolas", 11f, FontStyle.Bold),
                Location = new Point(58, 355),
                Size = new Size(238, 22),
                MaxLength = 7,
                Text = ToHex(initialColor)
            };
            _hexInput.TextChanged += OnHexInputChanged;
            _hexInput.Leave += (_, _) => SetHexInputText(ToHex(SelectedColor));
            Controls.Add(_hexInput);

            UpdateColorFieldBitmap();
            _hueBitmap = CreateHueBitmap(HueTrack.Width, HueTrack.Height);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(_theme.BgDark);

            using (var backgroundBrush = new SolidBrush(_theme.BgDark))
                g.FillRectangle(backgroundBrush, ClientRectangle);
            using (var panelPen = new Pen(Color.FromArgb(170, _theme.SecondaryNeon), 1.5f))
                g.DrawRectangle(panelPen, 1, 1, Width - 3, Height - 3);

            using var titleBrush = new SolidBrush(_theme.TextMain);
            using var accentBrush = new SolidBrush(_theme.SecondaryNeon);
            using var dimBrush = new SolidBrush(_theme.TextDim);
            using var titleFont = new Font("Segoe UI", 11f, FontStyle.Bold);
            using var smallFont = new Font("Consolas", 8f, FontStyle.Bold);

            g.FillRectangle(accentBrush, 18, 17, 4, 18);
            g.DrawString("CONTROLLER // LED COLOR", titleFont, titleBrush, 30, 15);
            using (var closePen = new Pen(_theme.PrimaryNeon, 1.5f))
            {
                g.DrawLine(closePen, 394, 21, 404, 31);
                g.DrawLine(closePen, 404, 21, 394, 31);
            }

            if (_colorFieldBitmap != null)
                g.DrawImage(_colorFieldBitmap, ColorField);
            using (var fieldBorder = new Pen(Color.FromArgb(180, _theme.TextMain), 1f))
                g.DrawRectangle(fieldBorder, ColorField);

            Point colorMarker = GetColorMarker();
            using (var markerShadow = new Pen(Color.FromArgb(190, _theme.BgDark), 4f))
                g.DrawEllipse(markerShadow, colorMarker.X - 7, colorMarker.Y - 7, 14, 14);
            using (var markerBorder = new Pen(Color.White, 2f))
                g.DrawEllipse(markerBorder, colorMarker.X - 6, colorMarker.Y - 6, 12, 12);

            if (_hueBitmap != null)
                g.DrawImage(_hueBitmap, HueTrack);
            using (var hueBorder = new Pen(Color.FromArgb(150, _theme.TextMain), 1f))
                g.DrawRectangle(hueBorder, HueTrack);

            int hueX = HueTrack.Left + (int)Math.Round(_hue / 360f * (HueTrack.Width - 1));
            using (var hueShadow = new Pen(Color.FromArgb(220, _theme.BgDark), 4f))
                g.DrawEllipse(hueShadow, hueX - 7, HueTrack.Top - 3, 14, 20);
            using (var hueMarker = new Pen(_theme.TextMain, 2f))
                g.DrawEllipse(hueMarker, hueX - 6, HueTrack.Top - 2, 12, 18);

            using (var inputBackground = new SolidBrush(_theme.BgPanel))
                g.FillRectangle(inputBackground, 18, 348, 324, 34);
            using (var inputBorder = new Pen(Color.FromArgb(140, _theme.SecondaryNeon), 1f))
                g.DrawRectangle(inputBorder, 18, 348, 324, 34);
            using (var swatch = new SolidBrush(SelectedColor))
                g.FillEllipse(swatch, 27, 355, 20, 20);
            DrawThemeButton(g, CopyButton, "▢", _theme.TextMain);

            using (var hexBrush = new SolidBrush(_theme.TextDim))
                g.DrawString("HEX", smallFont, hexBrush, 354, 359);
            using (var footerBrush = new SolidBrush(_theme.TextDim))
                g.DrawString("LIVE PREVIEW  //  CHANGES APPLY IMMEDIATELY", smallFont, footerBrush, 18, 386);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left)
                return;

            if (CloseButton.Contains(e.Location))
            {
                Close();
                return;
            }

            if (CopyButton.Contains(e.Location))
            {
                Clipboard.SetText(ToHex(SelectedColor));
                return;
            }

            if (ColorField.Contains(e.Location))
            {
                _dragTarget = PickerDragTarget.ColorField;
                Capture = true;
                UpdateColorFromField(e.Location);
            }
            else if (HueTrack.Contains(e.Location))
            {
                _dragTarget = PickerDragTarget.Hue;
                Capture = true;
                UpdateHueFromMouse(e.X);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (e.Button == MouseButtons.Left)
            {
                if (_dragTarget == PickerDragTarget.ColorField)
                    UpdateColorFromField(e.Location);
                else if (_dragTarget == PickerDragTarget.Hue)
                    UpdateHueFromMouse(e.X);
            }

            Cursor = CloseButton.Contains(e.Location) ||
                     CopyButton.Contains(e.Location) ||
                     ColorField.Contains(e.Location) ||
                     HueTrack.Contains(e.Location)
                ? Cursors.Hand
                : Cursors.Default;
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            _dragTarget = PickerDragTarget.None;
            Capture = false;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Escape || e.KeyCode == Keys.Enter)
            {
                Close();
                e.Handled = true;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _colorFieldBitmap?.Dispose();
                _hueBitmap?.Dispose();
                _hexInput.Font.Dispose();
            }
            base.Dispose(disposing);
        }

        private void UpdateColorFromField(Point point)
        {
            _saturation = Math.Clamp((point.X - ColorField.Left) / (float)(ColorField.Width - 1), 0f, 1f);
            _value = 1f - Math.Clamp((point.Y - ColorField.Top) / (float)(ColorField.Height - 1), 0f, 1f);
            UpdateSelectedColor();
        }

        private void UpdateHueFromMouse(int mouseX)
        {
            _hue = Math.Clamp(
                (mouseX - HueTrack.Left) / (float)(HueTrack.Width - 1) * 360f,
                0f,
                360f);
            UpdateColorFieldBitmap();
            UpdateSelectedColor();
        }

        private void UpdateSelectedColor()
        {
            Color color = ColorFromHsv(_hue, _saturation, _value);
            if (color.ToArgb() == SelectedColor.ToArgb())
                return;

            SelectedColor = color;
            SetHexInputText(ToHex(color));
            Invalidate();
            ColorChanged?.Invoke(color);
        }

        private void OnHexInputChanged(object? sender, EventArgs e)
        {
            if (_updatingHexInput)
                return;

            string value = _hexInput.Text.Trim().TrimStart('#');
            if (value.Length != 6 ||
                !int.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int rgb))
                return;

            Color color = Color.FromArgb(255, (rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);
            ColorToHsv(color, out _hue, out _saturation, out _value);
            UpdateColorFieldBitmap();
            UpdateSelectedColor();
        }

        private void SetHexInputText(string value)
        {
            _updatingHexInput = true;
            _hexInput.Text = value;
            _hexInput.SelectionStart = _hexInput.Text.Length;
            _updatingHexInput = false;
        }

        private void UpdateColorFieldBitmap()
        {
            _colorFieldBitmap?.Dispose();
            _colorFieldBitmap = CreateColorFieldBitmap(ColorField.Width, ColorField.Height, _hue);
        }

        private Point GetColorMarker()
        {
            return new Point(
                ColorField.Left + (int)Math.Round(_saturation * (ColorField.Width - 1)),
                ColorField.Top + (int)Math.Round((1f - _value) * (ColorField.Height - 1)));
        }

        private static Bitmap CreateColorFieldBitmap(int width, int height, float hue)
        {
            var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            var bounds = new Rectangle(0, 0, width, height);
            BitmapData data = bitmap.LockBits(bounds, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            try
            {
                byte[] pixels = new byte[Math.Abs(data.Stride) * height];
                Color hueColor = ColorFromHsv(hue, 1f, 1f);
                for (int y = 0; y < height; y++)
                {
                    float value = 1f - y / (float)(height - 1);
                    int row = y * data.Stride;
                    for (int x = 0; x < width; x++)
                    {
                        float saturation = x / (float)(width - 1);
                        int offset = row + x * 4;
                        pixels[offset] = (byte)Math.Clamp((int)Math.Round(value * (255f + (hueColor.B - 255f) * saturation)), 0, 255);
                        pixels[offset + 1] = (byte)Math.Clamp((int)Math.Round(value * (255f + (hueColor.G - 255f) * saturation)), 0, 255);
                        pixels[offset + 2] = (byte)Math.Clamp((int)Math.Round(value * (255f + (hueColor.R - 255f) * saturation)), 0, 255);
                        pixels[offset + 3] = 255;
                    }
                }
                Marshal.Copy(pixels, 0, data.Scan0, pixels.Length);
            }
            finally
            {
                bitmap.UnlockBits(data);
            }
            return bitmap;
        }

        private static Bitmap CreateHueBitmap(int width, int height)
        {
            var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            using Graphics g = Graphics.FromImage(bitmap);
            using var brush = new LinearGradientBrush(
                new Rectangle(0, 0, width, height),
                Color.Red,
                Color.Magenta,
                LinearGradientMode.Horizontal);
            brush.InterpolationColors = new ColorBlend
            {
                Positions = new[] { 0f, 1f / 6f, 2f / 6f, 3f / 6f, 4f / 6f, 5f / 6f, 1f },
                Colors = new[] { Color.Red, Color.Yellow, Color.Lime, Color.Cyan, Color.Blue, Color.Magenta, Color.Red }
            };
            g.FillRectangle(brush, 0, 0, width, height);
            return bitmap;
        }

        private void DrawThemeButton(Graphics g, Rectangle rect, string text, Color accent)
        {
            using var fill = new SolidBrush(_theme.BgPanel);
            using var border = new Pen(Color.FromArgb(150, accent), 1f);
            using var path = CreateChamferRect(rect, 5);
            g.FillPath(fill, path);
            g.DrawPath(border, path);
            using var font = new Font("Segoe UI Symbol", 12f, FontStyle.Bold);
            using var textBrush = new SolidBrush(accent);
            var format = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };
            g.DrawString(text, font, textBrush, rect, format);
        }

        private static string ToHex(Color color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";

        private static Color ColorFromHsv(float hue, float saturation, float value)
        {
            hue = (hue % 360f + 360f) % 360f;
            float chroma = value * saturation;
            float x = chroma * (1f - Math.Abs(hue / 60f % 2f - 1f));
            float match = value - chroma;
            (float r, float g, float b) = hue switch
            {
                < 60f => (chroma, x, 0f),
                < 120f => (x, chroma, 0f),
                < 180f => (0f, chroma, x),
                < 240f => (0f, x, chroma),
                < 300f => (x, 0f, chroma),
                _ => (chroma, 0f, x)
            };
            return Color.FromArgb(
                (int)Math.Round((r + match) * 255f),
                (int)Math.Round((g + match) * 255f),
                (int)Math.Round((b + match) * 255f));
        }

        private static void ColorToHsv(Color color, out float hue, out float saturation, out float value)
        {
            float r = color.R / 255f;
            float g = color.G / 255f;
            float b = color.B / 255f;
            float max = Math.Max(r, Math.Max(g, b));
            float min = Math.Min(r, Math.Min(g, b));
            float delta = max - min;

            if (delta == 0f)
                hue = 0f;
            else if (max == r)
                hue = 60f * (((g - b) / delta) % 6f);
            else if (max == g)
                hue = 60f * ((b - r) / delta + 2f);
            else
                hue = 60f * ((r - g) / delta + 4f);

            if (hue < 0f)
                hue += 360f;
            saturation = max == 0f ? 0f : delta / max;
            value = max;
        }

        private static GraphicsPath CreateChamferRect(Rectangle rect, int cut)
        {
            var path = new GraphicsPath();
            path.AddPolygon(new[]
            {
                new Point(rect.Left + cut, rect.Top),
                new Point(rect.Right - cut, rect.Top),
                new Point(rect.Right, rect.Top + cut),
                new Point(rect.Right, rect.Bottom - cut),
                new Point(rect.Right - cut, rect.Bottom),
                new Point(rect.Left + cut, rect.Bottom),
                new Point(rect.Left, rect.Bottom - cut),
                new Point(rect.Left, rect.Top + cut)
            });
            path.CloseFigure();
            return path;
        }

        private enum PickerDragTarget
        {
            None,
            ColorField,
            Hue
        }
    }
}
