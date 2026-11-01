using System.Drawing;
using System.Text.Json.Serialization;
using System.Windows.Forms;

namespace DualSenseApp.Config
{
    public enum TouchpadMode
    {
        Off,
        Mouse,
        Button
    }

    public enum TouchpadClick
    {
        Back,
        Guide
    }

    public enum TriggerPreset
    {
        Off,
        Resist,
        Pulse,
        Rigid,
        Machine
    }

    public sealed class HotkeyConfig
    {
        public bool Ctrl { get; set; } = true;
        public bool Shift { get; set; } = true;
        public bool Alt { get; set; }
        public int ToggleXbox { get; set; } = (int)Keys.F9;
        public int CycleProfile { get; set; } = (int)Keys.F10;
    }

    public sealed class ButtonRemapping
    {
        // PS → Xbox Button Mapping
        public int SquareMap { get; set; } = 0;      // 0=X, 1=A, 2=B, 3=Y
        public int CrossMap { get; set; } = 1;
        public int CircleMap { get; set; } = 2;
        public int TriangleMap { get; set; } = 3;
        public int L1Map { get; set; } = 4;          // 4=LB
        public int R1Map { get; set; } = 5;          // 5=RB
        public int L3Map { get; set; } = 8;          // 8=LS, 9=RS
        public int R3Map { get; set; } = 9;
        public bool Enabled { get; set; } = false;
    }

    public sealed class MacroConfig
    {
        public bool TurboEnabled { get; set; } = false;
        public int TurboFrequency { get; set; } = 5;  // Press per sec (1-20)
        public string TurboButton { get; set; } = "";  // "Square", "Cross", etc
    }

    public sealed class PadProfile
    {
        public string Name { get; set; } = "Default";
        public List<string> ProcessNames { get; set; } = new();
        public int DeadzonePercent { get; set; } = 6;
        public int StickSensitivityL { get; set; } = 100;
        public int StickSensitivityR { get; set; } = 100;
        public int VibrationIntensity { get; set; } = 100;
        public int TriggerRangeL { get; set; } = 100; // 0-100%
        public int TriggerRangeR { get; set; } = 100;
        public bool XboxMode { get; set; } = true;
        public bool UniversalMode { get; set; }
        public TouchpadMode TouchpadMode { get; set; } = TouchpadMode.Button;
        public TouchpadClick TouchpadClick { get; set; } = TouchpadClick.Back;
        public TriggerPreset TriggerPresetL { get; set; } = TriggerPreset.Off;
        public TriggerPreset TriggerPresetR { get; set; } = TriggerPreset.Off;
        public int LightbarR { get; set; } = -1;
        public int LightbarG { get; set; } = -1;
        public int LightbarB { get; set; } = -1;
        public ButtonRemapping ButtonRemapping { get; set; } = new();
        public MacroConfig Macro { get; set; } = new();

        public PadProfile Clone()
        {
            return new PadProfile
            {
                Name = Name,
                ProcessNames = new List<string>(ProcessNames),
                DeadzonePercent = DeadzonePercent,
                StickSensitivityL = StickSensitivityL,
                StickSensitivityR = StickSensitivityR,
                VibrationIntensity = VibrationIntensity,
                TriggerRangeL = TriggerRangeL,
                TriggerRangeR = TriggerRangeR,
                XboxMode = XboxMode,
                UniversalMode = UniversalMode,
                TouchpadMode = TouchpadMode,
                TouchpadClick = TouchpadClick,
                TriggerPresetL = TriggerPresetL,
                TriggerPresetR = TriggerPresetR,
                LightbarR = LightbarR,
                LightbarG = LightbarG,
                LightbarB = LightbarB,
                ButtonRemapping = new ButtonRemapping 
                { 
                    SquareMap = ButtonRemapping.SquareMap,
                    CrossMap = ButtonRemapping.CrossMap,
                    CircleMap = ButtonRemapping.CircleMap,
                    TriangleMap = ButtonRemapping.TriangleMap,
                    L1Map = ButtonRemapping.L1Map,
                    R1Map = ButtonRemapping.R1Map,
                    L3Map = ButtonRemapping.L3Map,
                    R3Map = ButtonRemapping.R3Map,
                    Enabled = ButtonRemapping.Enabled
                },
                Macro = new MacroConfig
                {
                    TurboEnabled = Macro.TurboEnabled,
                    TurboFrequency = Macro.TurboFrequency,
                    TurboButton = Macro.TurboButton
                }
            };
        }
    }

    public sealed class AppConfig
    {
        public int ThemeIndex { get; set; }
        public bool AutoHidHide { get; set; }
        public int? DeadzonePercent { get; set; }
        public bool StartWithWindows { get; set; }
        public bool MinimizeToTray { get; set; } = true;
        public string ActiveProfileName { get; set; } = "Default";
        public HotkeyConfig Hotkeys { get; set; } = new();
        public List<PadProfile> Profiles { get; set; } = new();

        [JsonIgnore]
        public PadProfile ActiveProfile
        {
            get
            {
                EnsureProfiles();
                return Profiles.FirstOrDefault(p =>
                    string.Equals(p.Name, ActiveProfileName, StringComparison.OrdinalIgnoreCase))
                    ?? Profiles[0];
            }
        }

        public void EnsureProfiles()
        {
            if (Profiles.Count == 0)
            {
                Profiles.Add(new PadProfile { Name = "Default" });
                Profiles.Add(new PadProfile
                {
                    Name = "FPS Aim",
                    StickSensitivityL = 120,
                    StickSensitivityR = 100,
                    TriggerPresetL = TriggerPreset.Resist,
                    TriggerPresetR = TriggerPreset.Pulse,
                    TouchpadMode = TouchpadMode.Button
                });
                Profiles.Add(new PadProfile
                {
                    Name = "Desktop",
                    XboxMode = false,
                    TouchpadMode = TouchpadMode.Mouse
                });
            }

            if (string.IsNullOrWhiteSpace(ActiveProfileName))
                ActiveProfileName = Profiles[0].Name;
        }

        public PadProfile NextProfile()
        {
            EnsureProfiles();
            int idx = Profiles.FindIndex(p =>
                string.Equals(p.Name, ActiveProfileName, StringComparison.OrdinalIgnoreCase));
            idx = (idx + 1) % Profiles.Count;
            ActiveProfileName = Profiles[idx].Name;
            return Profiles[idx];
        }

        public Color ResolveLightbar(Color themeColor)
        {
            var p = ActiveProfile;
            if (p.LightbarR < 0 || p.LightbarG < 0 || p.LightbarB < 0)
                return themeColor;
            return Color.FromArgb(
                Math.Clamp(p.LightbarR, 0, 255),
                Math.Clamp(p.LightbarG, 0, 255),
                Math.Clamp(p.LightbarB, 0, 255));
        }
    }
}
