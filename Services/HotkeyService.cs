using DualSenseApp.Config;
using DualSenseApp.Native;

namespace DualSenseApp.Services
{
    public sealed class HotkeyService : IDisposable
    {
        public const int IdToggleXbox = 1;
        public const int IdCycleProfile = 2;

        private readonly IntPtr _hwnd;
        private bool _registered;

        public HotkeyService(IntPtr hwnd) => _hwnd = hwnd;

        public void Register(HotkeyConfig cfg)
        {
            Unregister();
            uint mods = NativeMethods.ModNorepeat;
            if (cfg.Ctrl) mods |= NativeMethods.ModControl;
            if (cfg.Shift) mods |= NativeMethods.ModShift;
            if (cfg.Alt) mods |= NativeMethods.ModAlt;
            NativeMethods.RegisterHotKey(_hwnd, IdToggleXbox, mods, (uint)cfg.ToggleXbox);
            NativeMethods.RegisterHotKey(_hwnd, IdCycleProfile, mods, (uint)cfg.CycleProfile);
            _registered = true;
        }

        public void Unregister()
        {
            if (!_registered)
                return;
            NativeMethods.UnregisterHotKey(_hwnd, IdToggleXbox);
            NativeMethods.UnregisterHotKey(_hwnd, IdCycleProfile);
            _registered = false;
        }

        public void Dispose() => Unregister();
    }
}
