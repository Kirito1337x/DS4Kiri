using Microsoft.Win32;

namespace DualSenseApp.Services
{
    public static class StartupService
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "DualSenseApp";

        public static void Apply(bool enabled)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true)
                                ?? Registry.CurrentUser.CreateSubKey(RunKey);
                if (key == null)
                    return;
                if (enabled)
                {
                    string exe = Environment.ProcessPath ?? AppContext.BaseDirectory;
                    key.SetValue(ValueName, $"\"{exe}\"");
                }
                else if (key.GetValue(ValueName) != null)
                {
                    key.DeleteValue(ValueName);
                }
            }
            catch { }
        }
    }
}
