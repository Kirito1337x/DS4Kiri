using System.Diagnostics;
using DualSenseApp.Config;
using DualSenseApp.Native;

namespace DualSenseApp.Services
{
    public sealed class ProfileWatcher : IDisposable
    {
        private readonly AppConfig _config;
        private readonly System.Windows.Forms.Timer _timer = new();
        private string _lastProcess = "";
        private string? _autoProfile;

        public event Action<PadProfile>? ProfileApplied;

        public ProfileWatcher(AppConfig config)
        {
            _config = config;
            _timer.Interval = 1500;
            _timer.Tick += (_, _) => Scan();
        }

        public void Start() => _timer.Start();
        public void Stop() => _timer.Stop();

        public void Scan()
        {
            _config.EnsureProfiles();
            string process = GetForegroundProcessName();
            PadProfile? matched = null;
            if (!string.IsNullOrEmpty(process))
            {
                matched = _config.Profiles.FirstOrDefault(p =>
                    p.ProcessNames.Any(n =>
                        string.Equals(System.IO.Path.GetFileName(n), process, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(n, process, StringComparison.OrdinalIgnoreCase)));
            }

            if (matched != null)
            {
                _lastProcess = process;
                if (!string.Equals(_config.ActiveProfileName, matched.Name, StringComparison.OrdinalIgnoreCase))
                {
                    _config.ActiveProfileName = matched.Name;
                    _autoProfile = matched.Name;
                    ProfileApplied?.Invoke(matched);
                }
                return;
            }

            if (_autoProfile != null)
            {
                _autoProfile = null;
                _lastProcess = "";
                var fallback = _config.Profiles.FirstOrDefault(p =>
                                   string.Equals(p.Name, "Default", StringComparison.OrdinalIgnoreCase))
                               ?? _config.Profiles[0];
                _config.ActiveProfileName = fallback.Name;
                ProfileApplied?.Invoke(fallback);
            }
        }

        private static string GetForegroundProcessName()
        {
            try
            {
                IntPtr hwnd = NativeMethods.GetForegroundWindow();
                if (hwnd == IntPtr.Zero)
                    return "";
                NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);
                if (pid == 0)
                    return "";
                using var proc = Process.GetProcessById((int)pid);
                return proc.ProcessName + ".exe";
            }
            catch
            {
                return "";
            }
        }

        public void Dispose()
        {
            _timer.Stop();
            _timer.Dispose();
        }
    }
}
