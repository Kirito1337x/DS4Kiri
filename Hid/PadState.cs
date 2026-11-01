namespace DualSenseApp.Hid
{
    public enum SonyPadKind
    {
        DualSense,
        DualShock4,
        Generic
    }

    public sealed class PadState
    {
        public bool Valid;
        public SonyPadKind Kind;
        public bool Bluetooth;
        public int VendorId;
        public int ProductId;

        public int LX = 128, LY = 128, RX = 128, RY = 128;
        public int L2, R2;

        public bool Square, Cross, Circle, Triangle;
        public bool DpadUp, DpadRight, DpadDown, DpadLeft;
        public bool L1, R1, L2Click, R2Click, L3, R3;
        public bool Create, Options, Ps, Mute, TouchpadClick;

        public short GyroX, GyroY, GyroZ;
        public short AccelX, AccelY, AccelZ;

        public bool Touch1Active;
        public int Touch1X, Touch1Y;
        public bool Touch2Active;
        public int Touch2X, Touch2Y;

        public int BatteryPercent;
        public bool Charging;

        public PadState Clone()
        {
            return (PadState)MemberwiseClone();
        }
    }
}
