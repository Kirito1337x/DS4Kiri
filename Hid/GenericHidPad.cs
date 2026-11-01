using HidSharp;
using HidSharp.Reports;
using System.Diagnostics;

namespace DualSenseApp.Hid
{
    public sealed class GenericHidPad : IDisposable
    {
        private readonly HidDevice _device;
        private readonly ReportDescriptor _descriptor;
        private readonly Dictionary<byte, Report[]> _inputReports;
        private HidStream? _stream;

        private GenericHidPad(HidDevice device, ReportDescriptor descriptor, IEnumerable<Report> reports)
        {
            _device = device;
            _descriptor = descriptor;
            _inputReports = reports
                .GroupBy(report => report.ReportID)
                .ToDictionary(group => group.Key, group => group.ToArray());
        }

        public int VendorId => _device.VendorID;
        public int ProductId => _device.ProductID;
        public string Name => _device.GetProductName() ?? _device.GetFriendlyName();
        public int InputBufferSize => Math.Max(_device.GetMaxInputReportLength(), _descriptor.MaxInputReportLength);

        public static GenericHidPad? Find()
        {
            foreach (HidDevice device in DeviceList.Local.GetHidDevices())
            {
                if (device.VendorID == SonyPad.SonyVid ||
                    device.GetMaxInputReportLength() < 5 ||
                    IsViGEmVirtualController(device))
                    continue;

                try
                {
                    ReportDescriptor descriptor = device.GetReportDescriptor();
                    var gamepadCollections = descriptor.DeviceItems.Where(item =>
                        item.Usages.GetAllValues().Any(usage =>
                            usage is (uint)Usage.GenericDesktopGamepad
                                or (uint)Usage.GenericDesktopJoystick
                                or (uint)Usage.GenericDesktopMultiaxisController));

                    Report[] inputReports = gamepadCollections
                        .SelectMany(item => item.InputReports)
                        .Where(HasMappableUsage)
                        .ToArray();

                    if (inputReports.Length > 0)
                        return new GenericHidPad(device, descriptor, inputReports);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Skipping HID device {device.VendorID:X4}:{device.ProductID:X4}: {ex.Message}");
                }
            }

            return null;
        }

        public bool TryOpen()
        {
            if (_stream != null)
                return true;
            if (!_device.TryOpen(out HidStream stream))
                return false;

            stream.ReadTimeout = 1000;
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
            catch (IOException ex)
            {
                Debug.WriteLine($"Generic HID read failed for {VendorId:X4}:{ProductId:X4}: {ex.Message}");
                return false;
            }
        }

        public bool TryParse(byte[] buffer, int bytesRead, out PadState state)
        {
            state = new PadState
            {
                Kind = SonyPadKind.Generic,
                VendorId = VendorId,
                ProductId = ProductId,
                Bluetooth = _device.GetMaxInputReportLength() > 64
            };

            if (bytesRead <= 0)
                return false;

            byte reportId = _descriptor.ReportsUseID && buffer[0] != 0
                ? buffer[0]
                : (byte)0;
            if (!_inputReports.TryGetValue(reportId, out Report[]? reports))
                return false;

            PadState parsedState = state;
            bool mappedValueFound = false;
            foreach (Report report in reports)
            {
                if (bytesRead < report.Length)
                    continue;

                try
                {
                    report.Read(buffer, 0, (readBuffer, bitOffset, dataItem, _) =>
                    {
                        if (dataItem.IsConstant)
                            return;

                        for (int elementIndex = 0; elementIndex < dataItem.ElementCount; elementIndex++)
                        {
                            if (!dataItem.TryReadValue(readBuffer, bitOffset, elementIndex, out DataValue value))
                                continue;

                            foreach (uint usage in value.Usages)
                                mappedValueFound |= ApplyUsage(parsedState, usage, value);
                        }
                    });
                }
                catch (ArgumentException)
                {
                    return false;
                }
            }

            parsedState.Valid = mappedValueFound;
            state = parsedState;
            return mappedValueFound;
        }

        public void Close()
        {
            _stream?.Dispose();
            _stream = null;
        }

        public void Dispose() => Close();

        private static bool HasMappableUsage(Report report)
        {
            return report.DataItems
                .Where(item => !item.IsConstant)
                .SelectMany(item => item.Usages.GetAllValues())
                .Any(IsMappableUsage);
        }

        private static bool IsMappableUsage(uint usage)
        {
            uint page = usage >> 16;
            uint id = usage & 0xFFFF;
            return (page == 0x01 && id is >= 0x30 and <= 0x39 or >= 0x3D and <= 0x3E or >= 0x90 and <= 0x93)
                || (page == 0x09 && id is >= 1 and <= 16);
        }

        private static bool ApplyUsage(PadState state, uint usage, DataValue value)
        {
            if (!IsMappableUsage(usage) || value.IsNull)
                return false;

            uint page = usage >> 16;
            uint id = usage & 0xFFFF;
            int logicalValue = value.GetLogicalValue();
            if (page == 0x09)
            {
                bool pressed = logicalValue != 0;
                switch (id)
                {
                    case 1: state.Cross = pressed; break;
                    case 2: state.Circle = pressed; break;
                    case 3: state.Square = pressed; break;
                    case 4: state.Triangle = pressed; break;
                    case 5: state.L1 = pressed; break;
                    case 6: state.R1 = pressed; break;
                    case 7:
                        state.L2Click = pressed;
                        if (pressed) state.L2 = byte.MaxValue;
                        break;
                    case 8:
                        state.R2Click = pressed;
                        if (pressed) state.R2 = byte.MaxValue;
                        break;
                    case 9: state.Create = pressed; break;
                    case 10: state.Options = pressed; break;
                    case 11: state.L3 = pressed; break;
                    case 12: state.R3 = pressed; break;
                    case 13: state.Ps = pressed; break;
                    case 14: state.Mute = pressed; break;
                    default: return false;
                }
                return true;
            }

            if (page != 0x01)
                return false;

            switch (id)
            {
                case 0x30: state.LX = ToAxisByte(value); break;
                case 0x31: state.LY = ToAxisByte(value); break;
                case 0x32: state.L2 = ToAxisByte(value); break;
                case 0x33: state.RX = ToAxisByte(value); break;
                case 0x34: state.RY = ToAxisByte(value); break;
                case 0x35: state.R2 = ToAxisByte(value); break;
                case 0x36: state.L2 = ToAxisByte(value); break;
                case 0x37: state.R2 = ToAxisByte(value); break;
                case 0x39: ApplyHat(state, value); break;
                case 0x3D: state.Options = logicalValue != 0; break;
                case 0x3E: state.Create = logicalValue != 0; break;
                case 0x90: state.DpadUp = logicalValue != 0; break;
                case 0x91: state.DpadDown = logicalValue != 0; break;
                case 0x92: state.DpadLeft = logicalValue != 0; break;
                case 0x93: state.DpadRight = logicalValue != 0; break;
                default: return false;
            }
            return true;
        }

        private static int ToAxisByte(DataValue value)
        {
            return Math.Clamp((int)Math.Round(value.GetScaledValue(0, 255)), 0, 255);
        }

        private static void ApplyHat(PadState state, DataValue value)
        {
            int direction = value.GetLogicalValue() - value.DataItem.LogicalMinimum;
            if (direction is < 0 or > 7)
                return;

            state.DpadUp = direction is 0 or 1 or 7;
            state.DpadRight = direction is 1 or 2 or 3;
            state.DpadDown = direction is 3 or 4 or 5;
            state.DpadLeft = direction is 5 or 6 or 7;
        }

        private static bool IsViGEmVirtualController(HidDevice device)
        {
            return device.VendorID == 0x045E && device.ProductID == 0x028E;
        }
    }
}
