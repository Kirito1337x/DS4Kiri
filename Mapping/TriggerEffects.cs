using DualSenseApp.Config;

namespace DualSenseApp.Mapping
{
    public static class TriggerEffects
    {
        public static byte[] Build(TriggerPreset preset)
        {
            var e = new byte[11];
            switch (preset)
            {
                case TriggerPreset.Resist:
                    e[0] = 0x01;
                    e[1] = 0x00;
                    e[2] = 0x90;
                    break;
                case TriggerPreset.Pulse:
                    e[0] = 0x02;
                    e[1] = 0x00;
                    e[2] = 0x40;
                    break;
                case TriggerPreset.Rigid:
                    e[0] = 0x21;
                    e[1] = 0xFF;
                    e[2] = 0xFF;
                    e[3] = 0xFF;
                    e[4] = 0xFF;
                    break;
                case TriggerPreset.Machine:
                    e[0] = 0x26;
                    e[1] = 0x0A;
                    e[2] = 0xFF;
                    e[3] = 0x00;
                    e[4] = 0x00;
                    e[5] = 0x00;
                    e[6] = 0x00;
                    e[7] = 0x3F;
                    break;
            }
            return e;
        }
    }
}
