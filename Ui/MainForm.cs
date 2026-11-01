using System.Drawing;
using System.Drawing.Drawing2D;
using System.Diagnostics;
using DualSenseApp.Config;
using DualSenseApp.Engine;
using DualSenseApp.Hid;
using DualSenseApp.Mapping;
using DualSenseApp.Native;
using DualSenseApp.Services;

namespace DualSenseApp.Ui
{
    public sealed class MainForm : Form
    {
        private readonly AppConfig _config;
        private readonly ControllerEngine _engine;
        private readonly ProfileWatcher _profiles;
        private readonly System.Windows.Forms.Timer _hudTimer = new();

        private HotkeyService? _hotkeys;
        private TrayService? _tray;
        private bool _reallyExit;
        private bool _draggingVibration;
        private Rectangle _hoveredButton;
        private Rectangle _flashingButton;
        private float _hoverGlow;
        private float _clickFlash;
        private float _glowPhase;
        private PadState _pad = new();
        private int _lastBatteryPercent;
        private bool _lastBatteryCharging;
        private bool _hasBatteryReading;
        private Bitmap? _bgImage;
        private string? _bgImagePath;

        private readonly Font _fontTitle = new("Segoe UI", 12f, FontStyle.Bold);
        private readonly Font _fontSmall = new("Consolas", 8f, FontStyle.Bold);
        private readonly Font _fontSub = new("Segoe UI", 9f, FontStyle.Bold);
        private readonly Font _fontSymbol = new("Segoe UI Symbol", 10f, FontStyle.Bold);
        private readonly Font _fontLarge = new("Segoe UI", 14f, FontStyle.Bold);

        // Header buttons - TOP RIGHT CORNER
        private readonly Rectangle _rectClose = new(850, 12, 40, 32);
        private readonly Rectangle _rectMin = new(802, 12, 40, 32);
        private readonly Rectangle _rectGitHub = new(320, 12, 100, 32);
        private readonly Rectangle _rectYouTube = new(430, 12, 120, 32);
        private readonly Rectangle _rectThemeBtn = new(560, 12, 230, 32);

        // Main status panel
        private readonly Rectangle _rectStatus = new(24, 54, 852, 80);
        private readonly Rectangle _rectConnection = new(544, 50, 140, 56);
        private readonly Rectangle _rectLed = new(698, 50, 150, 56);

        // Mode button - LARGE
        private readonly Rectangle _rectModeBtn = new(24, 144, 852, 60);

        // Driver installation section
        private readonly Rectangle _rectUnlockBtn = new(24, 214, 210, 38);
        private readonly Rectangle _rectHidHideBtn = new(242, 214, 210, 38);
        private readonly Rectangle _rectInstallVigem = new(460, 214, 210, 38);
        private readonly Rectangle _rectBattery = new(678, 214, 198, 38);

        // Profile & Trigger controls
        private readonly Rectangle _rectProfile = new(24, 262, 420, 38);
        private readonly Rectangle _rectTrigL = new(452, 262, 100, 38);
        private readonly Rectangle _rectTrigR = new(560, 262, 100, 38);
        private readonly Rectangle _rectVibToggle = new(668, 262, 208, 38);

        // Stick Sensitivity section
        private readonly Rectangle _rectStickLLabel = new(24, 310, 250, 24);
        private readonly Rectangle _rectStickLSlider = new(24, 336, 560, 32);
        private readonly Rectangle _rectStickRLabel = new(592, 310, 250, 24);
        private readonly Rectangle _rectStickRSlider = new(592, 336, 284, 32);

        // Deadzone & Vibration section
        private readonly Rectangle _rectDeadzoneLabel = new(24, 378, 250, 24);
        private readonly Rectangle _rectDeadzoneSlider = new(24, 404, 560, 32);
        private readonly Rectangle _rectVibrationLabel = new(592, 378, 250, 24);
        private readonly Rectangle _rectVibrationSlider = new(592, 404, 284, 32);

        // Touchpad & Startup section
        private readonly Rectangle _rectTouchpadMode = new(24, 446, 560, 38);
        private readonly Rectangle _rectStartup = new(592, 446, 284, 38);

        // Controller display area - LARGE for input visualization
        private readonly Rectangle _rectInputDisplay = new(24, 494, 852, 380);

        // Stats/Info section bottom
        private readonly Rectangle _rectControllerInfo = new(24, 884, 852, 96);

        public MainForm()
        {
            _config = ConfigStore.Load();
            _engine = new ControllerEngine(_config);
            _profiles = new ProfileWatcher(_config);
            InitializeWindow();
            ApplyThemeAccent();
            _engine.Changed += OnEngineChanged;
            _profiles.ProfileApplied += _ =>
            {
                _engine.XboxModeActive = _config.ActiveProfile.XboxMode;
                _engine.UniversalModeActive = _config.ActiveProfile.UniversalMode;
                _engine.SetLightbarColor(_config.ResolveLightbar(_engine.LightbarFallback));
                ConfigStore.Save(_config);
                BeginInvokeInvalidate();
            };
            _profiles.Start();
            HidHideService.Apply(false, null);
            _engine.ApplyHidHide();
            _engine.Start();
        }

        private void ShowLedColorPicker()
        {
            using var picker = new LedColorForm(CurrentTheme, _config.ResolveLightbar(_engine.LightbarFallback));
            picker.ColorChanged += color =>
            {
                var profile = _config.ActiveProfile;
                profile.LightbarR = color.R;
                profile.LightbarG = color.G;
                profile.LightbarB = color.B;
                _engine.SetLightbarColor(color);
                Invalidate();
            };
            picker.ShowDialog(this);
            ConfigStore.Save(_config);
        }

        private void InitializeWindow()
        {
            Text = "DS4Kiri // NEURAL LINK [by Kirito1337x]";
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application;
            Size = new Size(900, 1000);
            FormBorderStyle = FormBorderStyle.None;  // Keeps round corners
            StartPosition = FormStartPosition.CenterScreen;
            DoubleBuffered = true;
            BackColor = CurrentTheme.BgDark;




            using var path = CreateChamferRect(new Rectangle(0, 0, Width, Height), 18);
            Region = new Region(path);

            _hudTimer.Interval = 33;
            _hudTimer.Tick += (_, _) =>
            {
                _glowPhase += 0.16f;
                _hoverGlow = _hoveredButton.IsEmpty
                    ? Math.Max(0f, _hoverGlow - 0.16f)
                    : Math.Min(1f, _hoverGlow + 0.16f);
                _clickFlash = Math.Max(0f, _clickFlash - 0.10f);
                if (_clickFlash == 0f)
                    _flashingButton = Rectangle.Empty;

                _pad = _engine.Snapshot();
                if (_pad.Valid)
                {
                    if (_pad.Kind == SonyPadKind.Generic)
                    {
                        _lastBatteryPercent = 0;
                        _lastBatteryCharging = false;
                        _hasBatteryReading = false;
                    }
                    else
                    {
                        _lastBatteryPercent = Math.Clamp(_pad.BatteryPercent, 0, 100);
                        _lastBatteryCharging = _pad.Charging;
                        _hasBatteryReading = true;
                    }
                }
                Invalidate();
            };
            _hudTimer.Start();

            Load += (_, _) =>
            {
                _hotkeys = new HotkeyService(Handle);
                _hotkeys.Register(_config.Hotkeys);
                _tray = new TrayService(this, ToggleXbox, CycleProfile, ExitApp);
            };
            FormClosing += OnAppClosing;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            _hotkeys?.Register(_config.Hotkeys);
        }

        protected override void WndProc(ref Message m)
        {
            const int WM_NCHITTEST = 0x0084;
            const int HTCAPTION = 2;

            if (m.Msg == WM_NCHITTEST)
            {
                base.WndProc(ref m);
                Point clientPos = PointToClient(Cursor.Position);

                // Check if mouse is over any interactive control area
                if (IsOverInteractiveArea(clientPos))
                {
                    return; // Let Windows handle it normally
                }

                // Allow dragging from any non-interactive area
                m.Result = (IntPtr)HTCAPTION;
                return;
            }

            if (m.Msg == NativeMethods.WmHotkey)
            {
                int id = m.WParam.ToInt32();
                if (id == HotkeyService.IdToggleXbox) ToggleXbox();
                else if (id == HotkeyService.IdCycleProfile) CycleProfile();
            }

            base.WndProc(ref m);
        }

        private bool IsOverInteractiveArea(Point p)
        {
            // All interactive button/slider rectangles
            if (_rectClose.Contains(p)) return true;
            if (_rectMin.Contains(p)) return true;
            if (_rectGitHub.Contains(p)) return true;
            if (_rectYouTube.Contains(p)) return true;
            if (_rectConnection.Contains(p)) return true;
            if (_rectLed.Contains(p)) return true;
            if (_rectThemeBtn.Contains(p)) return true;
            if (_rectModeBtn.Contains(p)) return true;
            if (_rectUnlockBtn.Contains(p)) return true;
            if (_rectHidHideBtn.Contains(p)) return true;
            if (_rectInstallVigem.Contains(p)) return true;
            if (_rectProfile.Contains(p)) return true;
            if (_rectTrigL.Contains(p)) return true;
            if (_rectTrigR.Contains(p)) return true;
            if (_rectVibToggle.Contains(p)) return true;
            if (_rectStickLSlider.Contains(p)) return true;
            if (_rectStickRSlider.Contains(p)) return true;
            if (_rectDeadzoneSlider.Contains(p)) return true;
            if (_rectVibrationSlider.Contains(p)) return true;
            if (_rectTouchpadMode.Contains(p)) return true;
            if (_rectStartup.Contains(p)) return true;

            return false;
        }

        private AnimeTheme CurrentTheme => AnimeTheme.All[Math.Clamp(_config.ThemeIndex, 0, AnimeTheme.All.Length - 1)];

        private void ApplyThemeAccent()
        {
            var t = CurrentTheme;
            BackColor = t.BgDark;
            _engine.LightbarFallback = t.PrimaryNeon;
        }

        private void OnEngineChanged() => BeginInvokeInvalidate();

        private void BeginInvokeInvalidate()
        {
            if (IsDisposed || !IsHandleCreated)
                return;
            try
            {
                BeginInvoke(() =>
                {
                    if (!IsDisposed)
                        Invalidate();
                });
            }
            catch { }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left)
                return;

            _flashingButton = FindButtonAt(e.Location);
            _clickFlash = _flashingButton.IsEmpty ? 0f : 1f;
            Invalidate();

            if (_rectClose.Contains(e.Location)) { ExitApp(); return; }
            if (_rectMin.Contains(e.Location))
            {
                if (_config.MinimizeToTray)
                    _tray?.HideToTray();
                else
                    WindowState = FormWindowState.Minimized;
                return;
            }
            if (_rectThemeBtn.Contains(e.Location))
            {
                _config.ThemeIndex = (_config.ThemeIndex + 1) % AnimeTheme.All.Length;
                ApplyThemeAccent();
                ConfigStore.Save(_config);
                Invalidate();
                return;
            }
            if (_rectGitHub.Contains(e.Location))
            {
                OpenSocialLink("https://github.com/Kirito1337x");
                return;
            }
            if (_rectYouTube.Contains(e.Location))
            {
                OpenSocialLink("https://www.youtube.com/@Kirito1337x");
                return;
            }
            if (_rectLed.Contains(e.Location))
            {
                ShowLedColorPicker();
                return;
            }
            if (_rectModeBtn.Contains(e.Location)) { ToggleXbox(); return; }
            if (_rectUnlockBtn.Contains(e.Location))
            {
                _config.AutoHidHide = false;
                ConfigStore.Save(_config);
                _engine.ApplyHidHide(forceAdmin: true);
                Invalidate();
                return;
            }
            if (_rectHidHideBtn.Contains(e.Location))
            {
                _config.AutoHidHide = !_config.AutoHidHide;
                ConfigStore.Save(_config);
                _engine.ApplyHidHide(forceAdmin: true);
                Invalidate();
                return;
            }
            if (!_engine.IsInstalling)
            {
                if (_rectInstallVigem.Contains(e.Location))
                {
                    _ = InstallDriversFromButtonAsync();
                    return;
                }
            }
            if (_rectProfile.Contains(e.Location)) { CycleProfile(); return; }
            if (_rectTrigL.Contains(e.Location))
            {
                CycleTrigger(left: true);
                return;
            }
            if (_rectTrigR.Contains(e.Location))
            {
                CycleTrigger(left: false);
                return;
            }
            if (_rectVibToggle.Contains(e.Location))
            {
                ToggleVibration();
                return;
            }
            if (_rectStickLSlider.Contains(e.Location))
            {
                UpdateSliderValue(e.X, _rectStickLSlider, (val) => _config.ActiveProfile.StickSensitivityL = val, 150);
                _draggingVibration = true;
                return;
            }
            if (_rectStickRSlider.Contains(e.Location))
            {
                UpdateSliderValue(e.X, _rectStickRSlider, (val) => _config.ActiveProfile.StickSensitivityR = val, 150);
                _draggingVibration = true;
                return;
            }
            if (_rectDeadzoneSlider.Contains(e.Location))
            {
                UpdateSliderValue(e.X, _rectDeadzoneSlider, (val) => _config.ActiveProfile.DeadzonePercent = val, 25);
                _draggingVibration = true;
                return;
            }
            if (_rectVibrationSlider.Contains(e.Location))
            {
                UpdateSliderValue(e.X, _rectVibrationSlider, (val) => _config.ActiveProfile.VibrationIntensity = val, 150);
                _draggingVibration = true;
                return;
            }
            if (_rectTouchpadMode.Contains(e.Location))
            {
                CycleTouchpadMode();
                return;
            }
            if (_rectStartup.Contains(e.Location))
            {
                _config.StartWithWindows = !_config.StartWithWindows;
                StartupService.Apply(_config.StartWithWindows);
                ConfigStore.Save(_config);
                Invalidate();
                return;
            }
            if (_rectConnection.Contains(e.Location))
            {
                if (_engine.IsConnected)
                    _engine.Disconnect();
                else
                    _engine.Connect();
                Invalidate();
                return;
            }
        }

        private async Task InstallDriversFromButtonAsync()
        {
            try
            {
                await _engine.InstallDriversAsync(true, false);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    ex.GetBaseException().Message,
                    "Driver installation failed",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            Rectangle hoveredButton = FindButtonAt(e.Location);
            if (hoveredButton != _hoveredButton)
            {
                _hoveredButton = hoveredButton;
                Invalidate();
            }

            // Handle slider dragging
            if (_draggingVibration)
            {
                var profile = _config.ActiveProfile;
                if (_rectStickLSlider.Contains(e.Location))
                    UpdateSliderValue(e.X, _rectStickLSlider, (val) => profile.StickSensitivityL = val, 150);
                else if (_rectStickRSlider.Contains(e.Location))
                    UpdateSliderValue(e.X, _rectStickRSlider, (val) => profile.StickSensitivityR = val, 150);
                else if (_rectDeadzoneSlider.Contains(e.Location))
                    UpdateSliderValue(e.X, _rectDeadzoneSlider, (val) => profile.DeadzonePercent = val, 25);
                else if (_rectVibrationSlider.Contains(e.Location))
                    UpdateSliderValue(e.X, _rectVibrationSlider, (val) => profile.VibrationIntensity = val, 150);
            }

            bool hand = _rectClose.Contains(e.Location) || _rectMin.Contains(e.Location) ||
                        _rectGitHub.Contains(e.Location) || _rectYouTube.Contains(e.Location) ||
                        _rectThemeBtn.Contains(e.Location) || _rectModeBtn.Contains(e.Location) ||
                        _rectUnlockBtn.Contains(e.Location) || _rectHidHideBtn.Contains(e.Location) ||
                        _rectInstallVigem.Contains(e.Location) ||
                        _rectProfile.Contains(e.Location) ||
                        _rectTrigL.Contains(e.Location) || _rectTrigR.Contains(e.Location) || _rectVibToggle.Contains(e.Location) ||
                        _rectTouchpadMode.Contains(e.Location) || _rectStartup.Contains(e.Location) ||
                        _rectStickLSlider.Contains(e.Location) || _rectStickRSlider.Contains(e.Location) ||
                        _rectDeadzoneSlider.Contains(e.Location) || _rectVibrationSlider.Contains(e.Location) ||
                        _rectConnection.Contains(e.Location) || _rectLed.Contains(e.Location);
            Cursor = hand ? Cursors.Hand : Cursors.Default;
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hoveredButton = Rectangle.Empty;
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            _draggingVibration = false;
            ConfigStore.Save(_config);
        }

        private void UpdateSliderValue(int mouseX, Rectangle sliderRect, Action<int> setValue, int maxValue)
        {
            float ratio = Math.Max(0, Math.Min(1, (mouseX - sliderRect.X) / (float)sliderRect.Width));
            int value = Math.Clamp((int)Math.Round(ratio * maxValue), 0, maxValue);
            setValue(value);
            Invalidate();
        }

        private void ToggleXbox()
        {
            if (_engine.UniversalModeActive)
            {
                _engine.UniversalModeActive = false;
                _engine.XboxModeActive = true;
            }
            else if (_engine.XboxModeActive)
            {
                _engine.XboxModeActive = false;
            }
            else
            {
                _engine.UniversalModeActive = true;
            }

            _config.ActiveProfile.UniversalMode = _engine.UniversalModeActive;
            _config.ActiveProfile.XboxMode = _engine.XboxModeActive;
            ConfigStore.Save(_config);
            _engine.ApplyHidHide();
            Invalidate();
        }

        private void CycleProfile()
        {
            var p = _config.NextProfile();
            _engine.XboxModeActive = p.XboxMode;
            _engine.UniversalModeActive = p.UniversalMode;
            ConfigStore.Save(_config);
            _engine.ApplyHidHide();
            Invalidate();
        }

        private void CycleTrigger(bool left)
        {
            var p = _config.ActiveProfile;
            if (left)
                p.TriggerPresetL = NextTrigger(p.TriggerPresetL);
            else
                p.TriggerPresetR = NextTrigger(p.TriggerPresetR);
            ConfigStore.Save(_config);
            Invalidate();
        }

        private static TriggerPreset NextTrigger(TriggerPreset current)
        {
            int n = ((int)current + 1) % Enum.GetValues<TriggerPreset>().Length;
            return (TriggerPreset)n;
        }

        private void CycleTouchpadMode()
        {
            var p = _config.ActiveProfile;
            p.TouchpadMode = p.TouchpadMode switch
            {
                TouchpadMode.Off => TouchpadMode.Button,
                TouchpadMode.Button => TouchpadMode.Mouse,
                _ => TouchpadMode.Off
            };
            ConfigStore.Save(_config);
            Invalidate();
        }

        private bool _vibrationEnabled = true;

        private void ToggleVibration()
        {
            _vibrationEnabled = !_vibrationEnabled;
            Invalidate();
        }

        private void ExitApp()
        {
            _reallyExit = true;
            Close();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var t = CurrentTheme;

            // Draw background: image or solid color
            if (!string.IsNullOrEmpty(t.BackgroundImage))
            {
                string fullPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, t.BackgroundImage);
                if (System.IO.File.Exists(fullPath) && (_bgImage == null || _bgImagePath != fullPath))
                {
                    _bgImage?.Dispose();
                    try { _bgImage = new Bitmap(fullPath); } catch { _bgImage = null; }
                    _bgImagePath = fullPath;
                }
                if (_bgImage != null)
                {
                    g.DrawImage(_bgImage, 0, 0, Width, Height);
                    // Dark overlay for readability
                    using var overlay = new SolidBrush(Color.FromArgb(140, t.BgDark));
                    g.FillRectangle(overlay, 0, 0, Width, Height);
                }
                else
                {
                    g.Clear(t.BgDark);
                }
            }
            else
            {
                _bgImage?.Dispose();
                _bgImage = null;
                _bgImagePath = null;
                g.Clear(t.BgDark);
            }

            using (var gridPen = new Pen(Color.FromArgb(12, t.PrimaryNeon), 1))
            {
                for (int x = 0; x < Width; x += 26) g.DrawLine(gridPen, x, 0, x, Height);
                for (int y = 0; y < Height; y += 26) g.DrawLine(gridPen, 0, y, Width, y);
            }

            using var brushPrimary = new SolidBrush(t.PrimaryNeon);
            using var brushSecondary = new SolidBrush(t.SecondaryNeon);
            using var brushText = new SolidBrush(t.TextMain);
            using var brushDim = new SolidBrush(t.TextDim);

            g.FillRectangle(brushPrimary, 24, 15, 6, 18);
            const string appName = "DS4Kiri //";
            g.DrawString(appName, _fontTitle, brushText, 35, 15);
            float authorX = 35 + g.MeasureString(appName, _fontTitle).Width + 8;
            g.DrawString("KIRITO1337X", _fontTitle, brushSecondary, authorX, 15);

            DrawSocialButton(g, _rectGitHub, "GITHUB", t, DrawGitHubIcon);
            DrawSocialButton(g, _rectYouTube, "YOUTUBE", t, DrawYouTubeIcon);
            DrawCyberButton(
                g,
                _rectThemeBtn,
                t.Name,
                _fontSmall,
                Color.FromArgb(255, 8, 10, 18),
                t.SecondaryNeon,
                t.TextMain);
            DrawCyberButton(g, _rectMin, "_", _fontSmall, t.BgPanel, t.TextDim, t.TextMain);
            DrawCyberButton(g, _rectClose, "X", _fontSmall, Color.FromArgb(60, 15, 30), t.PrimaryNeon, t.PrimaryNeon);

            var statusRect = new Rectangle(24, 50, 512, 56);
            Color connBorder = _engine.IsConnected ? Color.FromArgb(0, 220, 100) : Color.FromArgb(255, 50, 50);
            DrawChamferPanel(g, statusRect, t.BgPanel, connBorder, 8);
            Color ledColor = _engine.IsConnected ? Color.FromArgb(0, 255, 100) : Color.FromArgb(255, 40, 40);
            using (var ledBrush = new SolidBrush(ledColor))
                g.FillEllipse(ledBrush, 40, 66, 12, 12);
            g.DrawString(_engine.StatusText, _fontTitle, brushText, 60, 61);
            string bat = !_engine.IsConnected
                ? ""
                : _pad.Kind == SonyPadKind.Generic
                    ? "BAT N/A  "
                    : $"BAT {(_pad.Charging ? "CHG " : "")}{_pad.BatteryPercent}%  ";
            g.DrawString(bat + _engine.ConnText, _fontSmall, brushDim, 61, 83);

            bool connected = _engine.IsConnected;
            DrawCyberButton(
                g,
                _rectConnection,
                connected ? "DISCONNECT" : "CONNECT",
                _fontSub,
                connected ? Color.FromArgb(60, 180, 20, 20) : Color.FromArgb(35, t.SecondaryNeon),
                connected ? Color.FromArgb(255, 60, 60) : t.SecondaryNeon,
                connected ? Color.FromArgb(255, 80, 80) : t.SecondaryNeon);
            Color lightbarColor = _config.ResolveLightbar(t.PrimaryNeon);
            DrawCyberButton(
                g,
                _rectLed,
                "LED COLOR",
                _fontSmall,
                Color.FromArgb(150, t.BgDark),
                lightbarColor,
                lightbarColor);
            using (var ledPreview = new SolidBrush(lightbarColor))
                g.FillEllipse(ledPreview, _rectLed.X + 25, _rectLed.Y + 22, 12, 12);

            string modeTitle = _engine.UniversalModeActive
                ? "UNIVERSAL: HID → XBOX VIRTUAL"
                : _engine.XboxModeActive
                    ? "OVERDRIVE: XBOX 360 EMULATION"
                    : "PASSTHROUGH: NATIVE PLAYSTATION";
            Color modeBorder = _engine.UniversalModeActive || _engine.XboxModeActive ? t.PrimaryNeon : t.SecondaryNeon;
            Color modeFill = _engine.UniversalModeActive || _engine.XboxModeActive
                ? Color.FromArgb(45, t.PrimaryNeon)
                : Color.FromArgb(45, t.SecondaryNeon);
            DrawCyberButton(g, _rectModeBtn, modeTitle, _fontTitle, modeFill, modeBorder, t.TextMain);

            DrawCyberButton(g, _rectUnlockBtn, "RESET HIDHIDE", _fontSub, t.BgPanel, t.TextDim, t.TextMain);
            string cloakLabel = _config.AutoHidHide ? "STEALTH CLOAK: ON" : "STEALTH CLOAK: OFF";
            Color cloakColor = _config.AutoHidHide ? t.SecondaryNeon : t.TextDim;
            DrawCyberButton(g, _rectHidHideBtn, cloakLabel, _fontSub, t.BgPanel, cloakColor, cloakColor);

            string vigemTxt = _engine.VigemInstalled ? "VIGEMBUS: READY" : "INSTALL VIGEMBUS";
            Color vigemCol = _engine.VigemInstalled ? t.SecondaryNeon : t.PrimaryNeon;
            DrawCyberButton(g, _rectInstallVigem, vigemTxt, _fontSub, t.BgPanel, vigemCol, vigemCol);

            bool hidOk = HidHideService.Installed;
            string hidTxt = hidOk ? "HIDHIDE: READY" : "INSTALL HIDHIDE";
            Color hidCol = hidOk ? t.SecondaryNeon : t.PrimaryNeon;
            DrawCyberButton(g, _rectHidHideBtn, hidTxt, _fontSub, t.BgPanel, hidCol, hidCol);

            DrawBatteryIndicator(g, _rectBattery, t);

            var profile = _config.ActiveProfile;
            DrawCyberButton(g, _rectProfile, $"PROFILE: {profile.Name.ToUpperInvariant()}", _fontSub, t.BgPanel, t.SecondaryNeon, t.TextMain);
            DrawCyberButton(g, _rectTrigL, $"L2: {profile.TriggerPresetL}", _fontSmall, t.BgPanel, t.PrimaryNeon, t.TextMain);
            DrawCyberButton(g, _rectTrigR, $"R2: {profile.TriggerPresetR}", _fontSmall, t.BgPanel, t.PrimaryNeon, t.TextMain);
            
            // Vibration toggle
            string vibTxt = _vibrationEnabled ? "VIBRATION: ON" : "VIBRATION: OFF";
            Color vibCol = _vibrationEnabled ? t.SecondaryNeon : t.TextDim;
            DrawCyberButton(g, _rectVibToggle, vibTxt, _fontSub, t.BgPanel, vibCol, vibCol);

            // Stick Sensitivity Labels
            g.DrawString("STICK L SENSITIVITY", _fontSmall, brushSecondary, _rectStickLLabel.X, _rectStickLLabel.Y);
            DrawCyberSlider(g, _rectStickLSlider, profile.StickSensitivityL, 150, t);
            g.DrawString($"{profile.StickSensitivityL}%", _fontSmall, brushSecondary, _rectStickLSlider.Right - 40, _rectStickLLabel.Y);

            g.DrawString("STICK R SENSITIVITY", _fontSmall, brushSecondary, _rectStickRLabel.X, _rectStickRLabel.Y);
            DrawCyberSlider(g, _rectStickRSlider, profile.StickSensitivityR, 150, t);
            g.DrawString($"{profile.StickSensitivityR}%", _fontSmall, brushSecondary, _rectStickRSlider.Right - 40, _rectStickRLabel.Y);

            // Deadzone & Vibration Labels
            g.DrawString("DEADZONE THRESHOLD", _fontSmall, brushSecondary, _rectDeadzoneLabel.X, _rectDeadzoneLabel.Y);
            DrawCyberSlider(g, _rectDeadzoneSlider, profile.DeadzonePercent, 25, t);
            g.DrawString($"{profile.DeadzonePercent}%", _fontSmall, brushSecondary, _rectDeadzoneSlider.Right - 40, _rectDeadzoneLabel.Y);

            g.DrawString("VIBRATION INTENSITY", _fontSmall, brushSecondary, _rectVibrationLabel.X, _rectVibrationLabel.Y);
            DrawCyberSlider(g, _rectVibrationSlider, profile.VibrationIntensity, 150, t);
            g.DrawString($"{profile.VibrationIntensity}%", _fontSmall, brushSecondary, _rectVibrationSlider.Right - 40, _rectVibrationLabel.Y);

            // Touchpad & Startup
            string tpMode = profile.TouchpadMode.ToString().ToUpper();
            DrawCyberButton(g, _rectTouchpadMode, $"TOUCHPAD: {tpMode}", _fontSub, t.BgPanel, t.SecondaryNeon, t.TextMain);
            string startTxt = _config.StartWithWindows ? "STARTUP: ON" : "STARTUP: OFF";
            Color startCol = _config.StartWithWindows ? t.SecondaryNeon : t.TextDim;
            DrawCyberButton(g, _rectStartup, startTxt, _fontSub, t.BgPanel, startCol, startCol);

            // Input Visualizer - Draw controller layout
            Color lowerPanelFill = Color.FromArgb(45, t.BgDark);
            DrawChamferPanel(g, _rectInputDisplay, lowerPanelFill, Color.FromArgb(90, t.PrimaryNeon), 12);
            InputVisualizer.DrawControllerLayout(g, _rectInputDisplay, _pad, t);

            // Controller Info Section
            DrawChamferPanel(g, _rectControllerInfo, lowerPanelFill, Color.FromArgb(90, t.PrimaryNeon), 12);
            string connStatus = _engine.IsConnected ? "🟢 CONNECTED" : "🔴 DISCONNECTED";
            string vibStatus = _pad.Kind == SonyPadKind.Generic
                ? "N/A"
                : _vibrationEnabled ? "🔊 ON" : "🔇 OFF";
            string mode = _engine.UniversalModeActive ? "UNIVERSAL MODE" : _engine.XboxModeActive ? "XBOX MODE" : "PLAYSTATION MODE";
            string batteryLabel = _pad.Kind == SonyPadKind.Generic ? "N/A" : $"{_pad.BatteryPercent}%";
            g.DrawString($"{connStatus} | VIBRATION {vibStatus} | {mode} | BAT {batteryLabel}", 
                _fontSmall, brushSecondary, _rectControllerInfo.X + 12, _rectControllerInfo.Y + 12);
            g.DrawString($"LX:{_pad.LX:D3} LY:{_pad.LY:D3} | RX:{_pad.RX:D3} RY:{_pad.RY:D3} | L2:{_pad.L2:D3} R2:{_pad.R2:D3}", 
                _fontSmall, brushSecondary, _rectControllerInfo.X + 12, _rectControllerInfo.Y + 35);
            g.DrawString($"Buttons: {(_pad.Square ? "SQ " : "")} {(_pad.Cross ? "X " : "")} {(_pad.Circle ? "O " : "")} {(_pad.Triangle ? "TR " : "")} | L1:{(_pad.L1 ? "✓" : "✗")} R1:{(_pad.R1 ? "✓" : "✗")} | Dpad:{(_pad.DpadUp ? "↑" : "")}{(_pad.DpadDown ? "↓" : "")}{(_pad.DpadLeft ? "←" : "")}{(_pad.DpadRight ? "→" : "")}", 
                _fontSmall, brushSecondary, _rectControllerInfo.X + 12, _rectControllerInfo.Y + 58);

            var footerRect = new Rectangle(24, 912, 852, 48);
            DrawChamferPanel(g, footerRect, lowerPanelFill, Color.FromArgb(110, t.SecondaryNeon), 6);
            var sfCenter = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString("Ctrl+Shift+F9 Xbox  |  F10 Profile  |  Mouse Sliders to Adjust Values  // KIRITO1337X", _fontSmall, brushSecondary, footerRect, sfCenter);

            using var borderPath = CreateChamferRect(new Rectangle(1, 1, Width - 3, Height - 3), 18);
            using var borderPen = new Pen(t.PrimaryNeon, 2f);
            g.DrawPath(borderPen, borderPath);

            if (!_hoveredButton.IsEmpty && _hoverGlow > 0f)
            {
                float pulse = 0.78f + 0.22f * ((MathF.Sin(_glowPhase) + 1f) / 2f);
                DrawButtonGlow(g, _hoveredButton, GetButtonGlowColor(_hoveredButton, t), _hoverGlow * pulse);
            }
            if (!_flashingButton.IsEmpty && _clickFlash > 0f)
                DrawButtonGlow(g, _flashingButton, GetButtonGlowColor(_flashingButton, t), _clickFlash);
        }

        private void DrawStickRadar(Graphics g, Point center, int radius, int rawX, int rawY, string label, AnimeTheme t, int deadzone)
        {
            Rectangle r = new(center.X - radius, center.Y - radius, radius * 2, radius * 2);
            using var bgBrush = new SolidBrush(Color.FromArgb(14, 12, 22));
            using var ringPen = new Pen(Color.FromArgb(90, t.SecondaryNeon), 1.5f);
            using var crossPen = new Pen(Color.FromArgb(45, t.SecondaryNeon), 1f) { DashStyle = DashStyle.Dot };
            g.FillEllipse(bgBrush, r);
            g.DrawEllipse(ringPen, r);
            g.DrawLine(crossPen, center.X - radius, center.Y, center.X + radius, center.Y);
            g.DrawLine(crossPen, center.X, center.Y - radius, center.X, center.Y + radius);

            int dzRadius = Math.Max(3, (int)(radius * (deadzone / 100f)));
            using var dzPen = new Pen(Color.FromArgb(100, t.PrimaryNeon), 1f);
            g.DrawEllipse(dzPen, center.X - dzRadius, center.Y - dzRadius, dzRadius * 2, dzRadius * 2);

            float normX = (rawX - 128) / 128f;
            float normY = (rawY - 128) / 128f;
            int dotX = center.X + (int)(normX * (radius - 6));
            int dotY = center.Y + (int)(normY * (radius - 6));
            using var linePen = new Pen(t.PrimaryNeon, 1.5f);
            g.DrawLine(linePen, center.X, center.Y, dotX, dotY);
            using var dotBrush = new SolidBrush(t.SecondaryNeon);
            g.FillEllipse(dotBrush, dotX - 5, dotY - 5, 10, 10);
            using var textBrush = new SolidBrush(t.TextDim);
            g.DrawString(label, _fontSmall, textBrush, center.X - 22, center.Y - radius - 17);
        }

        private void DrawTriggerBar(Graphics g, Rectangle rect, int value, string label, AnimeTheme t)
        {
            using var bgBrush = new SolidBrush(Color.FromArgb(14, 12, 22));
            using var borderPen = new Pen(Color.FromArgb(90, t.SecondaryNeon), 1.5f);
            g.FillRectangle(bgBrush, rect);
            int fillHeight = (int)((value / 255f) * rect.Height);
            if (fillHeight > 0)
            {
                Rectangle fillRect = new(rect.X + 2, rect.Bottom - fillHeight, rect.Width - 3, fillHeight);
                using var grad = new LinearGradientBrush(rect, t.PrimaryNeon, t.SecondaryNeon, LinearGradientMode.Vertical);
                g.FillRectangle(grad, fillRect);
            }
            g.DrawRectangle(borderPen, rect);
            using var textBrush = new SolidBrush(t.TextMain);
            g.DrawString(label, _fontSmall, textBrush, rect.X + 2, rect.Y - 17);
        }

        private void DrawActionButtonNode(Graphics g, Point center, string symbol, bool pressed, AnimeTheme t)
        {
            Rectangle r = new(center.X - 13, center.Y - 13, 26, 26);
            Color fill = pressed ? t.PrimaryNeon : Color.FromArgb(18, 16, 28);
            Color border = pressed ? t.SecondaryNeon : Color.FromArgb(90, t.TextDim);
            using var b = new SolidBrush(fill);
            using var p = new Pen(border, 1.5f);
            g.FillEllipse(b, r);
            g.DrawEllipse(p, r);
            using var tb = new SolidBrush(t.TextMain);
            var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString(symbol, symbol.Length > 1 ? _fontSub : _fontSymbol, tb, r, sf);
        }

        private static void DrawCyberSlider(Graphics g, Rectangle rect, int val, int max, AnimeTheme t)
        {
            int trackY = rect.Y + rect.Height / 2 - 3;
            Rectangle track = new(rect.X, trackY, rect.Width, 6);
            using var bgBrush = new SolidBrush(t.BgPanel);
            using var borderPen = new Pen(Color.FromArgb(80, t.SecondaryNeon), 1);
            g.FillRectangle(bgBrush, track);
            g.DrawRectangle(borderPen, track);
            int fillW = (int)((val / (float)max) * rect.Width);
            if (fillW > 0)
            {
                using var fillBrush = new SolidBrush(t.PrimaryNeon);
                g.FillRectangle(fillBrush, rect.X, trackY, fillW, 6);
            }
            int knobX = rect.X + fillW - 6;
            Rectangle knob = new(Math.Clamp(knobX, rect.X, rect.Right - 12), rect.Y + 2, 12, rect.Height - 4);
            using var knobBrush = new SolidBrush(t.SecondaryNeon);
            g.FillRectangle(knobBrush, knob);
        }

        private static void DrawChamferPanel(Graphics g, Rectangle rect, Color fill, Color border, int cut)
        {
            using var path = CreateChamferRect(rect, cut);
            using var brush = new SolidBrush(fill);
            using var pen = new Pen(border, 1.5f);
            g.FillPath(brush, path);
            g.DrawPath(pen, path);
        }

        private static void DrawCyberButton(Graphics g, Rectangle rect, string text, Font font, Color fill, Color border, Color textColor)
        {
            DrawChamferPanel(g, rect, fill, border, 7);
            using var tb = new SolidBrush(textColor);
            var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString(text, font, tb, rect, sf);
        }

        private Rectangle FindButtonAt(Point point)
        {
            Rectangle[] buttons =
            {
                _rectClose, _rectMin, _rectGitHub, _rectYouTube, _rectThemeBtn,
                _rectConnection, _rectLed, _rectModeBtn, _rectUnlockBtn,
                _rectHidHideBtn, _rectInstallVigem, _rectProfile, _rectTrigL,
                _rectTrigR, _rectVibToggle, _rectTouchpadMode, _rectStartup,
                _rectStickLSlider, _rectStickRSlider, _rectDeadzoneSlider,
                _rectVibrationSlider
            };

            foreach (Rectangle button in buttons)
            {
                if (button.Contains(point))
                    return button;
            }
            return Rectangle.Empty;
        }

        private Color GetButtonGlowColor(Rectangle button, AnimeTheme theme)
        {
            if (button == _rectClose)
                return theme.PrimaryNeon;
            if (button == _rectConnection && _engine.IsConnected)
                return Color.FromArgb(255, 75, 75);
            if (button == _rectLed)
                return _config.ResolveLightbar(theme.PrimaryNeon);
            return theme.SecondaryNeon;
        }

        private static void DrawButtonGlow(Graphics g, Rectangle button, Color color, float intensity)
        {
            if (intensity <= 0f)
                return;

            int alpha = (int)Math.Clamp(Math.Round(intensity * 36f), 0, 36);
            using var outerPen = new Pen(Color.FromArgb(alpha, color), 12f);
            using var middlePen = new Pen(Color.FromArgb(Math.Min(90, alpha * 2), color), 6f);
            using var innerPen = new Pen(Color.FromArgb(Math.Min(180, alpha * 4), color), 2f);
            using var outerPath = CreateChamferRect(Rectangle.Inflate(button, 4, 4), 9);
            using var middlePath = CreateChamferRect(Rectangle.Inflate(button, 2, 2), 8);
            using var innerPath = CreateChamferRect(button, 7);
            g.DrawPath(outerPen, outerPath);
            g.DrawPath(middlePen, middlePath);
            g.DrawPath(innerPen, innerPath);
        }

        private void DrawSocialButton(
            Graphics g,
            Rectangle rect,
            string label,
            AnimeTheme theme,
            Action<Graphics, Rectangle, Color> drawIcon)
        {
            DrawChamferPanel(g, rect, Color.FromArgb(230, theme.BgDark), theme.SecondaryNeon, 6);
            var iconRect = new Rectangle(rect.X + 8, rect.Y + 7, 18, 18);
            drawIcon(g, iconRect, theme.TextMain);
            using var textBrush = new SolidBrush(theme.TextMain);
            var textRect = new Rectangle(iconRect.Right + 5, rect.Y, rect.Right - iconRect.Right - 8, rect.Height);
            var format = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
                FormatFlags = StringFormatFlags.NoWrap
            };
            g.DrawString(label, _fontSmall, textBrush, textRect, format);
        }

        private static void DrawGitHubIcon(Graphics g, Rectangle rect, Color color)
        {
            using var brush = new SolidBrush(color);
            g.FillEllipse(brush, rect.X + 2, rect.Y + 5, 14, 12);
            g.FillPolygon(brush, new[]
            {
                new Point(rect.X + 2, rect.Y + 9),
                new Point(rect.X + 2, rect.Y + 1),
                new Point(rect.X + 8, rect.Y + 5)
            });
            g.FillPolygon(brush, new[]
            {
                new Point(rect.X + 10, rect.Y + 5),
                new Point(rect.X + 16, rect.Y + 1),
                new Point(rect.X + 16, rect.Y + 9)
            });
            g.FillEllipse(brush, rect.X + 5, rect.Y + 13, 3, 5);
            g.FillEllipse(brush, rect.X + 11, rect.Y + 13, 3, 5);
        }

        private static void DrawYouTubeIcon(Graphics g, Rectangle rect, Color color)
        {
            var logoRect = new Rectangle(rect.X + 1, rect.Y + 3, 16, 12);
            using var logoBrush = new SolidBrush(Color.FromArgb(255, 255, 0, 0));
            using var path = CreateChamferRect(logoRect, 4);
            g.FillPath(logoBrush, path);
            using var playBrush = new SolidBrush(color);
            g.FillPolygon(playBrush, new[]
            {
                new Point(rect.X + 7, rect.Y + 6),
                new Point(rect.X + 7, rect.Y + 12),
                new Point(rect.X + 12, rect.Y + 9)
            });
        }

        private void OpenSocialLink(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    ex.Message,
                    "Unable to open link",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void DrawBatteryIndicator(Graphics g, Rectangle rect, AnimeTheme theme)
        {
            DrawChamferPanel(
                g,
                rect,
                Color.FromArgb(150, theme.BgDark),
                Color.FromArgb(110, theme.SecondaryNeon),
                7);

            bool connected = _engine.IsConnected;
            bool charging = connected && _lastBatteryCharging;
            string status = !_hasBatteryReading
                ? "NO BATTERY DATA"
                : charging
                    ? "CHARGING"
                    : connected
                        ? "BATTERY"
                        : "LAST READING";
            Color accent = _hasBatteryReading
                ? connected ? theme.SecondaryNeon : theme.TextDim
                : theme.TextDim;

            using (var labelBrush = new SolidBrush(accent))
                g.DrawString(status, _fontSmall, labelBrush, rect.X + 12, rect.Y + 4);

            var batteryBody = new Rectangle(rect.X + 12, rect.Y + 19, 112, 12);
            var batteryTip = new Rectangle(batteryBody.Right + 1, batteryBody.Y + 3, 4, 6);
            using var outlinePen = new Pen(Color.FromArgb(180, accent), 1.2f);
            using var emptyBrush = new SolidBrush(Color.FromArgb(90, theme.BgDark));
            using var tipBrush = new SolidBrush(accent);
            g.FillRectangle(emptyBrush, batteryBody);
            g.DrawRectangle(outlinePen, batteryBody);
            g.FillRectangle(tipBrush, batteryTip);

            if (_hasBatteryReading)
            {
                var inner = Rectangle.Inflate(batteryBody, -2, -2);
                int fillWidth = inner.Width * _lastBatteryPercent / 100;
                if (fillWidth > 0)
                {
                    var fillRect = new Rectangle(inner.X, inner.Y, fillWidth, inner.Height);
                    using var fillBrush = new LinearGradientBrush(
                        fillRect,
                        Color.FromArgb(connected ? 210 : 100, theme.PrimaryNeon),
                        Color.FromArgb(connected ? 230 : 130, theme.SecondaryNeon),
                        LinearGradientMode.Horizontal);
                    g.FillRectangle(fillBrush, fillRect);

                    if (charging && fillWidth > 10)
                    {
                        var graphicsState = g.Save();
                        try
                        {
                            g.SetClip(fillRect);
                            int shimmerWidth = Math.Min(18, fillWidth);
                            int animationTravel = fillWidth + shimmerWidth;
                            int shimmerX = inner.X +
                                (int)((Environment.TickCount64 / 35) % animationTravel) -
                                shimmerWidth;
                            using var shimmerBrush = new SolidBrush(
                                Color.FromArgb(150, theme.TextMain));
                            g.FillRectangle(
                                shimmerBrush,
                                shimmerX,
                                inner.Y,
                                shimmerWidth,
                                inner.Height);
                        }
                        finally
                        {
                            g.Restore(graphicsState);
                        }
                    }
                }
            }

            string level = _hasBatteryReading ? $"{_lastBatteryPercent}%" : "--%";
            using var levelBrush = new SolidBrush(_hasBatteryReading ? theme.TextMain : theme.TextDim);
            var levelRect = new Rectangle(rect.X + 132, rect.Y + 15, rect.Width - 140, 20);
            var levelFormat = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };
            g.DrawString(level, _fontSub, levelBrush, levelRect, levelFormat);
        }

        private static GraphicsPath CreateChamferRect(Rectangle r, int cut)
        {
            GraphicsPath path = new();
            path.AddPolygon(new[]
            {
                new Point(r.Left + cut, r.Top),
                new Point(r.Right - cut, r.Top),
                new Point(r.Right, r.Top + cut),
                new Point(r.Right, r.Bottom - cut),
                new Point(r.Right - cut, r.Bottom),
                new Point(r.Left + cut, r.Bottom),
                new Point(r.Left, r.Bottom - cut),
                new Point(r.Left, r.Top + cut)
            });
            path.CloseFigure();
            return path;
        }

        private void OnAppClosing(object? sender, FormClosingEventArgs e)
        {
            if (!_reallyExit && _config.MinimizeToTray && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                _tray?.HideToTray();
                return;
            }

            _hudTimer.Stop();
            _hotkeys?.Dispose();
            _profiles.Dispose();
            _engine.Changed -= OnEngineChanged;
            _engine.Stop();
            HidHideService.Apply(false, null);
            try { _tray?.Dispose(); } catch { }
            _fontTitle.Dispose();
            _fontSmall.Dispose();
            _fontSub.Dispose();
            _fontSymbol.Dispose();
        }
    }
}
