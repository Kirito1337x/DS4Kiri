using System.Text.Json;
using System.Text.Json.Serialization;

namespace DualSenseApp.Config
{
    public static class ConfigStore
    {
        public static readonly string Path = System.IO.Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "config.json");

        private static readonly JsonSerializerOptions Options = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter() }
        };

        public static AppConfig Load()
        {
            try
            {
                if (File.Exists(Path))
                {
                    var cfg = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(Path), Options)
                              ?? new AppConfig();
                    cfg.EnsureProfiles();
                    cfg.ThemeIndex = Math.Clamp(cfg.ThemeIndex, 0, 2);
                    if (cfg.DeadzonePercent is >= 0)
                    {
                        var def = cfg.Profiles.First(p => p.Name == "Default");
                        def.DeadzonePercent = Math.Clamp(cfg.DeadzonePercent.Value, 0, 25);
                        cfg.DeadzonePercent = null;
                    }
                    foreach (var p in cfg.Profiles)
                        p.DeadzonePercent = Math.Clamp(p.DeadzonePercent, 0, 25);
                    return cfg;
                }
            }
            catch { }

            var fresh = new AppConfig();
            fresh.EnsureProfiles();
            return fresh;
        }

        public static void Save(AppConfig config)
        {
            try
            {
                File.WriteAllText(Path, JsonSerializer.Serialize(config, Options));
            }
            catch { }
        }
    }
}
