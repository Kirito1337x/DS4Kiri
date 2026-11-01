using System.Diagnostics;
using DualSenseApp.Hid;

namespace DualSenseApp.Services
{
    public static class HidHideService
    {
        public const string CliPath = @"C:\Program Files\Nefarius Software Solutions\HidHide\x64\HidHideCLI.exe";

        public static bool Installed => File.Exists(CliPath);

        public static void Apply(bool enableCloak, string? devicePath, bool forceAdminPrompt = false)
        {
            if (!Installed)
                return;

            try
            {
                string currentExe = Process.GetCurrentProcess().MainModule?.FileName ?? "";
                string builtExe = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DualSenseApp.exe");

                var parts = new List<string>
                {
                    $"\"{CliPath}\" --app-reg \"{currentExe}\""
                };
                if (File.Exists(builtExe) && !string.Equals(builtExe, currentExe, StringComparison.OrdinalIgnoreCase))
                    parts.Add($"\"{CliPath}\" --app-reg \"{builtExe}\"");

                string? instanceId = ToInstanceId(devicePath);
                if (!string.IsNullOrWhiteSpace(instanceId))
                {
                    parts.Add(enableCloak
                        ? $"\"{CliPath}\" --dev-hide \"{instanceId}\""
                        : $"\"{CliPath}\" --dev-unhide \"{instanceId}\"");
                }

                foreach (int pid in SonyPad.SonyPids)
                {
                    parts.Add(enableCloak
                        ? $"\"{CliPath}\" --dev-hide \"HID\\VID_054C&PID_{pid:X4}\""
                        : $"\"{CliPath}\" --dev-unhide \"HID\\VID_054C&PID_{pid:X4}\"");
                }

                parts.Add(enableCloak
                    ? $"\"{CliPath}\" --cloak-on"
                    : $"\"{CliPath}\" --cloak-off");

                string cmd = "/c \"" + string.Join(" & ", parts) + "\"";
                var psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = cmd,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true,
                    UseShellExecute = forceAdminPrompt,
                    Verb = forceAdminPrompt ? "runas" : ""
                };
                using var p = Process.Start(psi);
                p?.WaitForExit(2500);
            }
            catch { }
        }

        private static string? ToInstanceId(string? devicePath)
        {
            if (string.IsNullOrWhiteSpace(devicePath))
                return null;
            string id = devicePath;
            if (id.StartsWith(@"\\?\", StringComparison.Ordinal))
                id = id[4..];
            id = id.Replace('#', '\\');
            int guid = id.IndexOf('{');
            if (guid > 0)
                id = id[..guid].TrimEnd('\\');
            return id;
        }
    }
}
