namespace DualSenseApp.Hid
{
    internal static class SonyCrc32
    {
        private static readonly uint[] Table = CreateTable();

        private static uint[] CreateTable()
        {
            var table = new uint[256];
            for (uint i = 0; i < 256; i++)
            {
                uint c = i;
                for (int j = 0; j < 8; j++)
                    c = (c & 1) != 0 ? (0xEDB88320u ^ (c >> 1)) : (c >> 1);
                table[i] = c;
            }
            return table;
        }

        public static uint Compute(byte prefix, byte[] data, int offset, int length)
        {
            uint crc = 0xFFFFFFFFu;
            crc = (crc >> 8) ^ Table[(crc ^ prefix) & 0xFF];
            for (int i = 0; i < length; i++)
                crc = (crc >> 8) ^ Table[(crc ^ data[offset + i]) & 0xFF];
            return crc ^ 0xFFFFFFFFu;
        }

        public static void WriteAtEnd(byte[] report, byte hidBtPrefix = 0xA2)
        {
            uint crc = Compute(hidBtPrefix, report, 0, report.Length - 4);
            int o = report.Length - 4;
            report[o] = (byte)crc;
            report[o + 1] = (byte)(crc >> 8);
            report[o + 2] = (byte)(crc >> 16);
            report[o + 3] = (byte)(crc >> 24);
        }
    }
}
