using System.Runtime.InteropServices;

namespace DualSenseApp.Native
{
    internal static class NativeMethods
    {
        public const int WmHotkey = 0x0312;
        public const int InputMouse = 0;
        public const int MouseeventfMove = 0x0001;
        public const int MouseeventfLeftdown = 0x0002;
        public const int MouseeventfLeftup = 0x0004;
        public const uint ModAlt = 0x0001;
        public const uint ModControl = 0x0002;
        public const uint ModShift = 0x0004;
        public const uint ModNorepeat = 0x4000;

        [DllImport("user32.dll")] public static extern bool ReleaseCapture();
        [DllImport("user32.dll")] public static extern int SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);
        [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [StructLayout(LayoutKind.Sequential)]
        public struct INPUT
        {
            public uint Type;
            public InputUnion U;
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct InputUnion
        {
            [FieldOffset(0)] public MOUSEINPUT Mi;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MOUSEINPUT
        {
            public int Dx;
            public int Dy;
            public uint MouseData;
            public uint DwFlags;
            public uint Time;
            public IntPtr DwExtraInfo;
        }

        public static void SendMouseMove(int dx, int dy)
        {
            if (dx == 0 && dy == 0)
                return;
            var input = new INPUT
            {
                Type = InputMouse,
                U = new InputUnion
                {
                    Mi = new MOUSEINPUT { Dx = dx, Dy = dy, DwFlags = MouseeventfMove }
                }
            };
            SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>());
        }

        public static void SendMouseButton(bool down)
        {
            var input = new INPUT
            {
                Type = InputMouse,
                U = new InputUnion
                {
                    Mi = new MOUSEINPUT { DwFlags = (uint)(down ? MouseeventfLeftdown : MouseeventfLeftup) }
                }
            };
            SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>());
        }
    }
}
