using HidSharp;
using System.IO;

namespace DualSenseApp.Hid
{
    public sealed class SonyPad : IDisposable
    {
        public const int SonyVid = 0x054C;
        public static readonly int[] SonyPids = { 0x0CE6, 0x0DF2, 0x05C4, 0x09CC, 0x0BA0 };

        private HidDevice? _device;
        private HidStream? _stream;

        public HidDevice? Device => _device;
        public bool IsOpen => _stream != null;
        public string? DevicePath => _device?.DevicePath;

        public static SonyPad? Find()
        {
            var match = DeviceList.Local.GetHidDevices()
                .Where(d => d.VendorID == SonyVid && SonyPids.Contains(d.ProductID))
                .OrderBy(d => d.GetMaxInputReportLength() > 64 ? 1 : 0)
                .FirstOrDefault();
            return match == null ? null : new SonyPad { _device = match };
        }

        public bool TryOpen()
        {
            if (_device == null)
                return false;
            if (_stream != null)
                return true;
            if (!_device.TryOpen(out HidStream stream))
                return false;
            stream.ReadTimeout = 1000;
            stream.WriteTimeout = 500;
            _stream = stream;
            return true;
        }

        public bool TryRead(byte[] buffer, out int bytesRead)
        {
            bytesRead = 0;
            if (_stream == null)
                return false;
            try
            {
                bytesRead = _stream.Read(buffer, 0, buffer.Length);
                return bytesRead > 0;
            }
            catch (TimeoutException)
            {
                return true;
            }
            catch (IOException ex) when (ex.InnerException is TimeoutException)
            {
                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool TryWrite(byte[] report)
        {
            if (_stream == null || _device == null)
                return false;
            try
            {
                int max = _device.GetMaxOutputReportLength();
                byte[] payload = report;
                if (max > 0 && report.Length != max)
                {
                    payload = new byte[max];
                    Buffer.BlockCopy(report, 0, payload, 0, Math.Min(report.Length, max));
                }
                _stream.Write(payload, 0, payload.Length);
                return true;
            }
            catch
            {
                try
                {
                    _stream.SetFeature(report);
                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }

        public int InputBufferSize =>
            Math.Max(_device?.GetMaxInputReportLength() ?? 128, 128);

        public void Close()
        {
            try { _stream?.Dispose(); } catch { }
            _stream = null;
        }

        public void Dispose() => Close();
    }
}
