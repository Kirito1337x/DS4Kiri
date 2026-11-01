using System.Runtime.InteropServices;

namespace DualSenseApp.Native
{
    /// <summary>
    /// Sintesi di input tastiera/mouse tramite SendInput.
    /// I tasti vengono inviati come scan code perche' molti giochi
    /// (DirectInput / raw input) ignorano gli eventi solo virtual-key.
    /// </summary>
    internal static class InputSynth
    {
        private const uint InputMouse = 0;
        private const uint InputKeyboard = 1;

        private const uint KeyeventfExtendedkey = 0x0001;
        private const uint KeyeventfKeyup = 0x0002;
        private const uint KeyeventfScancode = 0x0008;

        private const uint MouseeventfMove = 0x0001;
        private const uint MouseeventfLeftdown = 0x0002;
        private const uint MouseeventfLeftup = 0x0004;
        private const uint MouseeventfRightdown = 0x0008;
        private const uint MouseeventfRightup = 0x0010;
        private const uint MouseeventfMiddledown = 0x0020;
        private const uint MouseeventfMiddleup = 0x0040;
        private const uint MouseeventfXdown = 0x0080;
        private const uint MouseeventfXup = 0x0100;
        private const uint MouseeventfWheel = 0x0800;
        private const uint MouseeventfHwheel = 0x1000;

        private const uint XbuttonOne = 0x0001;
        private const uint XbuttonTwo = 0x0002;

        private const uint MapvkVkToVsc = 0;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        private static extern uint MapVirtualKey(uint uCode, uint uMapType);

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public uint Type;
            public InputUnion U;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct InputUnion
        {
            [FieldOffset(0)] public MOUSEINPUT Mi;
            [FieldOffset(0)] public KEYBDINPUT Ki;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT
        {
            public int Dx;
            public int Dy;
            public uint MouseData;
            public uint DwFlags;
            public uint Time;
            public IntPtr DwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort WVk;
            public ushort WScan;
            public uint DwFlags;
            public uint Time;
            public IntPtr DwExtraInfo;
        }

        private static void Send(params INPUT[] inputs)
        {
            if (inputs.Length == 0)
                return;
            SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
        }
    }
}
