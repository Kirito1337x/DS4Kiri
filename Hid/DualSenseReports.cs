using DualSenseApp.Config;
using DualSenseApp.Mapping;

namespace DualSenseApp.Hid
{
    public static class DualSenseReports
    {
        public static bool TryParse(byte[] buf, int len, int productId, out PadState state)
        {
            state = new PadState
            {
                ProductId = productId,
                Valid = false
            };

            if (len < 10)
                return false;

            bool isDs4 = productId is 0x05C4 or 0x09CC or 0x0BA0;
            state.Kind = isDs4 ? SonyPadKind.DualShock4 : SonyPadKind.DualSense;

            bool ok = isDs4 ? ParseDs4(buf, len, state) : ParseDualSense(buf, len, state);
            state.Valid = ok;
            return ok;
        }

        private static bool ParseDualSense(byte[] buf, int len, PadState s)
        {
            int o;
            if (buf[0] == 0x31 && len >= 40)
            {
                o = 2;
                s.Bluetooth = true;
            }
            else if (buf[0] == 0x01 && len >= 32)
            {
                o = 1;
                s.Bluetooth = len > 64;
            }
            else
            {
                return false;
            }

            s.LX = buf[o];
            s.LY = buf[o + 1];
            s.RX = buf[o + 2];
            s.RY = buf[o + 3];
            s.L2 = buf[o + 4];
            s.R2 = buf[o + 5];

            byte b0 = buf[o + 7];
            byte b1 = buf[o + 8];
            byte b2 = buf[o + 9];

            DecodeFaceAndDpad(s, b0);
            s.L1 = (b1 & 0x01) != 0;
            s.R1 = (b1 & 0x02) != 0;
            s.L2Click = (b1 & 0x04) != 0;
            s.R2Click = (b1 & 0x08) != 0;
            s.Create = (b1 & 0x10) != 0;
            s.Options = (b1 & 0x20) != 0;
            s.L3 = (b1 & 0x40) != 0;
            s.R3 = (b1 & 0x80) != 0;
            s.Ps = (b2 & 0x01) != 0;
            s.TouchpadClick = (b2 & 0x02) != 0;
            s.Mute = (b2 & 0x04) != 0;

            if (len > o + 26)
            {
                s.GyroX = ReadI16(buf, o + 15);
                s.GyroY = ReadI16(buf, o + 17);
                s.GyroZ = ReadI16(buf, o + 19);
                s.AccelX = ReadI16(buf, o + 21);
                s.AccelY = ReadI16(buf, o + 23);
                s.AccelZ = ReadI16(buf, o + 25);
            }

            int touch = o + 32;
            if (len > touch + 8)
            {
                DecodeTouch(buf[touch], buf[touch + 1], buf[touch + 2], buf[touch + 3],
                    out s.Touch1Active, out s.Touch1X, out s.Touch1Y);
                DecodeTouch(buf[touch + 4], buf[touch + 5], buf[touch + 6], buf[touch + 7],
                    out s.Touch2Active, out s.Touch2X, out s.Touch2Y);
            }

            int statusIndex = o + 52;
            if (len > statusIndex)
            {
                byte status = buf[statusIndex];
                int batteryLevel = status & 0x0F;
                int chargingStatus = (status >> 4) & 0x0F;
                s.Charging = chargingStatus == 1;
                s.BatteryPercent = chargingStatus == 2
                    ? 100
                    : Math.Clamp(batteryLevel * 10 + 5, 0, 100);
            }

            return true;
        }

        private static bool ParseDs4(byte[] buf, int len, PadState s)
        {
            byte reportId = buf[0];
            int offset = reportId == 0x11 ? 2 : 0;
            if (len < 10 + offset)
                return false;

            s.Bluetooth = reportId == 0x11 || len > 64;

            s.LX = buf[1 + offset];
            s.LY = buf[2 + offset];
            s.RX = buf[3 + offset];
            s.RY = buf[4 + offset];

            byte b0 = buf[5 + offset];
            byte b1 = buf[6 + offset];
            byte b2 = buf[7 + offset];
            s.L2 = buf[8 + offset];
            s.R2 = buf[9 + offset];

            DecodeFaceAndDpad(s, b0);
            s.L1 = (b1 & 0x01) != 0;
            s.R1 = (b1 & 0x02) != 0;
            s.L2Click = (b1 & 0x04) != 0;
            s.R2Click = (b1 & 0x08) != 0;
            s.Create = (b1 & 0x10) != 0;
            s.Options = (b1 & 0x20) != 0;
            s.L3 = (b1 & 0x40) != 0;
            s.R3 = (b1 & 0x80) != 0;
            s.Ps = (b2 & 0x01) != 0;
            s.TouchpadClick = (b2 & 0x02) != 0;

            int gyroBase = 13 + offset;
            if (len > gyroBase + 12)
            {
                s.GyroX = ReadI16(buf, gyroBase);
                s.GyroY = ReadI16(buf, gyroBase + 2);
                s.GyroZ = ReadI16(buf, gyroBase + 4);
                s.AccelX = ReadI16(buf, gyroBase + 6);
                s.AccelY = ReadI16(buf, gyroBase + 8);
                s.AccelZ = ReadI16(buf, gyroBase + 10);
            }

            int batteryIndex = 30 + offset;
            if (len > batteryIndex)
            {
                byte bat = buf[batteryIndex];
                s.Charging = (bat & 0x10) != 0;
                int level = bat & 0x0F;
                s.BatteryPercent = level > 10
                    ? 100
                    : Math.Clamp(level * 10 + 5, 0, 100);
            }

            int touch = 35 + offset;
            if (len > touch + 8)
            {
                DecodeTouch(buf[touch], buf[touch + 1], buf[touch + 2], buf[touch + 3],
                    out s.Touch1Active, out s.Touch1X, out s.Touch1Y);
                DecodeTouch(buf[touch + 4], buf[touch + 5], buf[touch + 6], buf[touch + 7],
                    out s.Touch2Active, out s.Touch2X, out s.Touch2Y);
            }

            return true;
        }

        private static void DecodeFaceAndDpad(PadState s, byte b0)
        {
            int dpad = b0 & 0x0F;
            s.DpadUp = dpad is 0 or 1 or 7;
            s.DpadRight = dpad is 1 or 2 or 3;
            s.DpadDown = dpad is 3 or 4 or 5;
            s.DpadLeft = dpad is 5 or 6 or 7;
            s.Square = (b0 & 0x10) != 0;
            s.Cross = (b0 & 0x20) != 0;
            s.Circle = (b0 & 0x40) != 0;
            s.Triangle = (b0 & 0x80) != 0;
        }

        private static void DecodeTouch(byte c0, byte c1, byte c2, byte c3, out bool active, out int x, out int y)
        {
            active = (c0 & 0x80) == 0;
            x = c1 | ((c2 & 0x0F) << 8);
            y = (c2 >> 4) | (c3 << 4);
        }

        private static short ReadI16(byte[] buf, int index)
        {
            return (short)(buf[index] | (buf[index + 1] << 8));
        }

        public static byte[] BuildDualSenseOutput(
            bool bluetooth,
            byte rumbleLeft,
            byte rumbleRight,
            byte r, byte g, byte b,
            TriggerPreset leftTrigger,
            TriggerPreset rightTrigger,
            byte seq)
        {
            if (bluetooth)
            {
                var report = new byte[78];
                report[0] = 0x31;
                report[1] = (byte)((seq & 0x0F) << 4);
                report[2] = 0x10;
                FillDualSenseCommon(report, 3, rumbleLeft, rumbleRight, r, g, b, leftTrigger, rightTrigger);
                SonyCrc32.WriteAtEnd(report);
                return report;
            }

            var usb = new byte[48];
            usb[0] = 0x02;
            FillDualSenseCommon(usb, 1, rumbleLeft, rumbleRight, r, g, b, leftTrigger, rightTrigger);
            return usb;
        }

        private static void FillDualSenseCommon(
            byte[] report, int i,
            byte rumbleLeft, byte rumbleRight,
            byte r, byte g, byte b,
            TriggerPreset leftTrigger, TriggerPreset rightTrigger)
        {
            report[i + 0] = 0x0F;
            report[i + 1] = 0x15;
            report[i + 2] = rumbleRight;
            report[i + 3] = rumbleLeft;
            report[i + 8] = 0x00;
            report[i + 9] = 0x00;

            var right = TriggerEffects.Build(rightTrigger);
            var left = TriggerEffects.Build(leftTrigger);
            Buffer.BlockCopy(right, 0, report, i + 10, 11);
            Buffer.BlockCopy(left, 0, report, i + 21, 11);

            report[i + 38] = 0x02;
            report[i + 41] = 0x02;
            report[i + 42] = 0x02;
            report[i + 44] = r;
            report[i + 45] = g;
            report[i + 46] = b;
        }

        public static byte[] BuildDs4Output(bool bluetooth, byte rumbleLeft, byte rumbleRight, byte r, byte g, byte b)
        {
            if (bluetooth)
            {
                var report = new byte[78];
                report[0] = 0x11;
                report[1] = 0xC0;
                report[2] = 0x00;
                report[3] = 0x07;
                report[6] = rumbleRight;
                report[7] = rumbleLeft;
                report[8] = r;
                report[9] = g;
                report[10] = b;
                SonyCrc32.WriteAtEnd(report);
                return report;
            }

            return new byte[]
            {
                0x05, 0xFF, 0x00, 0x00, rumbleRight, rumbleLeft, r, g, b,
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0
            };
        }
    }
}
