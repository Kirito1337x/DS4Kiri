using System.Drawing;

namespace DualSenseApp.Ui
{
    public struct AnimeTheme
    {
        public string Name;
        public Color BgDark;
        public Color BgPanel;
        public Color PrimaryNeon;
        public Color SecondaryNeon;
        public Color TextMain;
        public Color TextDim;

        /// <summary>Optional background image path (relative to app root). Null = solid color only.</summary>
        public string? BackgroundImage;

        public static readonly AnimeTheme[] All =
        {
            // ─── THEMES WITH BACKGROUND IMAGES ───────────────────────────────
            new()
            {
                Name = "CRIMSON GAZE",
                BgDark = Color.FromArgb(10, 8, 14),
                BgPanel = Color.FromArgb(255, 20, 14, 26),
                PrimaryNeon = Color.FromArgb(255, 30, 80),
                SecondaryNeon = Color.FromArgb(0, 220, 255),
                TextMain = Color.FromArgb(255, 245, 250),
                TextDim = Color.FromArgb(160, 140, 170),
                BackgroundImage = "Themes/crimson_gaze.jpg"
            },
            new()
            {
                Name = "VOID BLOOM",
                BgDark = Color.FromArgb(8, 5, 12),
                BgPanel = Color.FromArgb(255, 18, 10, 30),
                PrimaryNeon = Color.FromArgb(180, 80, 255),
                SecondaryNeon = Color.FromArgb(255, 160, 40),
                TextMain = Color.FromArgb(250, 245, 255),
                TextDim = Color.FromArgb(150, 130, 180),
                BackgroundImage = "Themes/void_bloom.jpg"
            },
            new()
            {
                Name = "GHOST VEIL",
                BgDark = Color.FromArgb(6, 10, 14),
                BgPanel = Color.FromArgb(255, 12, 18, 24),
                PrimaryNeon = Color.FromArgb(0, 200, 190),
                SecondaryNeon = Color.FromArgb(255, 60, 80),
                TextMain = Color.FromArgb(235, 245, 250),
                TextDim = Color.FromArgb(120, 150, 160),
                BackgroundImage = "Themes/ghost_veil.jpg"
            },
            new()
            {
                Name = "SUNBURN",
                BgDark = Color.FromArgb(14, 10, 6),
                BgPanel = Color.FromArgb(255, 28, 20, 12),
                PrimaryNeon = Color.FromArgb(255, 170, 30),
                SecondaryNeon = Color.FromArgb(255, 90, 40),
                TextMain = Color.FromArgb(255, 250, 240),
                TextDim = Color.FromArgb(180, 160, 130),
                BackgroundImage = "Themes/sunburn.jpg"
            },
            new()
            {
                Name = "VIOLET PULSE",
                BgDark = Color.FromArgb(12, 6, 16),
                BgPanel = Color.FromArgb(255, 24, 12, 32),
                PrimaryNeon = Color.FromArgb(255, 50, 180),
                SecondaryNeon = Color.FromArgb(140, 60, 255),
                TextMain = Color.FromArgb(255, 240, 255),
                TextDim = Color.FromArgb(170, 130, 190),
                BackgroundImage = "Themes/violet_pulse.jpg"
            },

            // ─── THEMES WITHOUT IMAGES (SOLID COLOR) ─────────────────────────
            new()
            {
                Name = "CYBER SAKURA",
                BgDark = Color.FromArgb(13, 11, 24),
                BgPanel = Color.FromArgb(22, 18, 40),
                PrimaryNeon = Color.FromArgb(255, 42, 133),
                SecondaryNeon = Color.FromArgb(0, 240, 255),
                TextMain = Color.FromArgb(245, 245, 255),
                TextDim = Color.FromArgb(140, 135, 175)
            },
            new()
            {
                Name = "CRIMSON ONI",
                BgDark = Color.FromArgb(14, 10, 14),
                BgPanel = Color.FromArgb(26, 16, 22),
                PrimaryNeon = Color.FromArgb(255, 30, 66),
                SecondaryNeon = Color.FromArgb(255, 184, 0),
                TextMain = Color.FromArgb(255, 245, 240),
                TextDim = Color.FromArgb(170, 135, 140)
            },
            new()
            {
                Name = "ASTRAL LOTUS",
                BgDark = Color.FromArgb(10, 14, 28),
                BgPanel = Color.FromArgb(18, 25, 48),
                PrimaryNeon = Color.FromArgb(165, 94, 234),
                SecondaryNeon = Color.FromArgb(76, 201, 240),
                TextMain = Color.FromArgb(240, 248, 255),
                TextDim = Color.FromArgb(130, 155, 195)
            },
            new()
            {
                Name = "EMERALD DRIFT",
                BgDark = Color.FromArgb(6, 14, 12),
                BgPanel = Color.FromArgb(12, 28, 22),
                PrimaryNeon = Color.FromArgb(0, 230, 150),
                SecondaryNeon = Color.FromArgb(180, 255, 60),
                TextMain = Color.FromArgb(240, 255, 248),
                TextDim = Color.FromArgb(120, 170, 150)
            },
            new()
            {
                Name = "FROST BYTE",
                BgDark = Color.FromArgb(8, 12, 20),
                BgPanel = Color.FromArgb(14, 22, 36),
                PrimaryNeon = Color.FromArgb(80, 180, 255),
                SecondaryNeon = Color.FromArgb(200, 230, 255),
                TextMain = Color.FromArgb(240, 248, 255),
                TextDim = Color.FromArgb(130, 160, 200)
            }
        };
    }
}
