using System.Drawing;
using DualSenseApp.Config;
using DualSenseApp.Hid;
using DualSenseApp.Mapping;
using DualSenseApp.Services;
using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;

namespace DualSenseApp.Engine
{
    public sealed class ControllerEngine : IDisposable
    {
        private readonly AppConfig _config;
        private readonly object _stateLock = new();
        private readonly InputMapper _mapper = new();

        private ViGEmClient? _vigem;
        private IXbox360Controller? _xbox;
        private Thread? _worker;
        private volatile bool _running;
        private volatile bool _installing;
        private volatile bool _disconnectRequested;
        private volatile bool _connectRequested;
        private volatile bool _connectionPaused;
        private volatile byte _rumbleLeft, _rumbleRight;
        private byte _outSeq;
        private int _lightbarArgb;
        private PadState _hud = new();
        private string? _lastHidPath;

        public volatile bool XboxModeActive = true;
        public volatile bool UniversalModeActive;
        public volatile bool VigemInstalled;
        public volatile bool IsConnected;
        public volatile string StatusText = "SEARCHING UNIT...";
        public volatile string ConnText = "LINK: OFFLINE";
        public Color LightbarFallback = Color.FromArgb(255, 42, 133);

        public bool IsInstalling => _installing;
        public bool IsRunning => _running;

        public event Action? Changed;

        public ControllerEngine(AppConfig config)
        {
            _config = config;
            XboxModeActive = config.ActiveProfile.XboxMode;
            UniversalModeActive = config.ActiveProfile.UniversalMode;
            _lightbarArgb = config.ResolveLightbar(LightbarFallback).ToArgb();
        }

        public PadState Snapshot()
        {
            lock (_stateLock)
                return _hud.Clone();
        }

        public void Start()
        {
            try
            {
                _vigem = new ViGEmClient();
                _xbox = _vigem.CreateXbox360Controller();
                _xbox.AutoSubmitReport = false;
                _xbox.FeedbackReceived += (_, e) =>
                {
                    _rumbleLeft = e.LargeMotor;
                    _rumbleRight = e.SmallMotor;
                };
                _xbox.Connect();
                VigemInstalled = true;
            }
            catch
            {
                VigemInstalled = false;
                StatusText = "MISSING VIGEMBUS DRIVER!";
                ConnText = "CLICK '1-CLICK ALL DRIVERS' BELOW TO INSTALL";
                Changed?.Invoke();
                return;
            }

            if (_running)
                return;
            _running = true;
            _worker = new Thread(PollingLoop)
            {
                IsBackground = true,
                Name = "SonyPadPoll",
                Priority = ThreadPriority.Highest
            };
            _worker.Start();
        }

        public void Stop()
        {
            _running = false;
            try { _xbox?.Disconnect(); } catch { }
            try { _vigem?.Dispose(); } catch { }
            _xbox = null;
            _vigem = null;
        }

        public void ApplyHidHide(bool forceAdmin = false)
        {
            bool cloak = (XboxModeActive || UniversalModeActive) && _config.AutoHidHide;
            HidHideService.Apply(cloak, _lastHidPath, forceAdmin);
        }

        public async Task InstallDriversAsync(bool vigem, bool hidHide)
        {
            if (_installing)
                return;

            _installing = true;
            StatusText = "DOWNLOADING OFFICIAL DRIVER SETUP...";
            ConnText = "POWERSHELL // APPROVE THE WINDOWS ADMIN PROMPT";
            Changed?.Invoke();

            bool completed = false;
            bool rebootRequired = false;
            try
            {
                rebootRequired = await Task.Run(() => DriverInstallerService.Install(vigem, hidHide));
                completed = true;
            }
            catch (Exception ex)
            {
                StatusText = "DRIVER INSTALLATION FAILED";
                ConnText = ex.GetBaseException().Message;
                throw;
            }
            finally
            {
                _installing = false;
                if (completed)
                {
                    if (vigem && !rebootRequired && !VigemInstalled)
                        Start();

                    if (rebootRequired)
                    {
                        StatusText = "RESTART WINDOWS TO FINISH DRIVER SETUP";
                        ConnText = "DRIVER INSTALLER REQUESTED A REBOOT";
                    }
                    else if (vigem && !VigemInstalled)
                    {
                        StatusText = "VIGEMBUS INSTALLED";
                        ConnText = "RESTART WINDOWS IF THE DRIVER IS NOT READY";
                    }
                    else
                    {
                        StatusText = "DRIVER SETUP COMPLETED";
                        ConnText = "OFFICIAL NEFARIUS PACKAGES INSTALLED";
                    }
                }
                Changed?.Invoke();
            }
        }

        private void PollingLoop()
        {
            while (_running)
            {
                if (_connectRequested)
                {
                    _connectRequested = false;
                    _connectionPaused = false;
                    _disconnectRequested = false;
                }

                if (_connectionPaused)
                {
                    IsConnected = false;
                    StatusText = "DISCONNECTED BY USER";
                    ConnText = "PRESS CONNECT TO RESUME";
                    lock (_stateLock)
                        _hud = new PadState();
                    Changed?.Invoke();
                    Thread.Sleep(500);
                    continue;
                }

                if (UniversalModeActive)
                {
                    GenericHidPad? genericPad = GenericHidPad.Find();
                    if (genericPad != null)
                    {
                        PollGenericController(genericPad);
                        continue;
                    }
                }

                using var pad = SonyPad.Find();
                if (pad == null)
                {
                    if (UniversalModeActive)
                    {
                        IsConnected = false;
                        _mapper.ResetTransient();
                        if (!_installing)
                        {
                            StatusText = "WAITING FOR UNIVERSAL HID GAMEPAD...";
                            ConnText = "CONNECT A USB OR BLUETOOTH GAMEPAD";
                        }
                        lock (_stateLock)
                            _hud = new PadState();
                        Changed?.Invoke();
                        Thread.Sleep(1000);
                        continue;
                    }

                    IsConnected = false;
                    _mapper.ResetTransient();
                    if (!_installing)
                    {
                        StatusText = "WAITING FOR DUALSENSE UNIT...";
                        ConnText = "LINK: OFFLINE // PRESS PS BUTTON OR RESET HIDHIDE";
                    }
                    lock (_stateLock)
                        _hud = new PadState();
                    Changed?.Invoke();
                    Thread.Sleep(1000);
                    continue;
                }

                if (!pad.TryOpen())
                {
                    IsConnected = false;
                    if (!_installing)
                    {
                        StatusText = $"UNIT [{pad.Device!.VendorID:X4}:{pad.Device.ProductID:X4}] LOCKED!";
                        ConnText = "CLOSE DS4WINDOWS OR STEAM IN BACKGROUND";
                    }
                    lock (_stateLock)
                        _hud = new PadState();
                    Changed?.Invoke();
                    Thread.Sleep(1000);
                    continue;
                }

                _lastHidPath = pad.DevicePath;
                bool isDs4 = pad.Device!.ProductID is 0x05C4 or 0x09CC or 0x0BA0;
                string padName = isDs4 ? "DUALSHOCK 4" : "DUALSENSE";
                int maxIn = pad.Device.GetMaxInputReportLength();
                bool bluetooth = maxIn > 64;
                IsConnected = false;
                StatusText = $"{padName} DETECTED [{pad.Device.VendorID:X4}:{pad.Device.ProductID:X4}]";
                ConnText = "WAITING FOR VALID INPUT...";
                Changed?.Invoke();

                byte[] buffer = new byte[pad.InputBufferSize];
                int idleWrites = 0;

                while (_running && !_disconnectRequested && !_connectRequested && !_connectionPaused)
                {
                    if (!pad.TryRead(buffer, out int n))
                        break;
                    if (n <= 0)
                        continue;

                    if (!DualSenseReports.TryParse(buffer, n, pad.Device.ProductID, out var state))
                        continue;

                    state.VendorId = pad.Device.VendorID;
                    state.Bluetooth = bluetooth || state.Bluetooth;
                    if (!IsConnected)
                    {
                        IsConnected = true;
                        StatusText = $"{padName} LINKED [{pad.Device.VendorID:X4}:{pad.Device.ProductID:X4}]";
                        ConnText = $"CHANNEL: {(state.Bluetooth ? "BLUETOOTH WIRELESS" : "USB DIRECT")} // PACKET {maxIn}B";
                        ApplyHidHide();
                        Changed?.Invoke();
                    }
                    lock (_stateLock)
                        _hud = state;

                    var profile = _config.ActiveProfile;
                    bool xboxMode = XboxModeActive || UniversalModeActive;
                    _mapper.Map(state, profile, xboxMode ? _xbox : null, xboxMode);

                    idleWrites++;
                    if (idleWrites >= 2)
                    {
                        idleWrites = 0;
                        WriteOutput(pad, state, profile);
                    }
                }

                bool manuallyDisconnected = _disconnectRequested || _connectionPaused;
                _disconnectRequested = false;
                IsConnected = false;
                StatusText = manuallyDisconnected ? "DISCONNECTED BY USER" : "WAITING FOR DUALSENSE UNIT...";
                ConnText = manuallyDisconnected
                    ? "PRESS CONNECT TO RESUME"
                    : "LINK: OFFLINE // PRESS PS BUTTON OR RESET HIDHIDE";
                lock (_stateLock)
                    _hud = new PadState();
                pad.Close();
                _mapper.ResetTransient();
                Changed?.Invoke();
            }
        }

        private void PollGenericController(GenericHidPad pad)
        {
            using (pad)
            {
                PollGenericControllerCore(pad);
            }
        }

        private void PollGenericControllerCore(GenericHidPad pad)
        {
            if (!pad.TryOpen())
            {
                SetGenericDisconnectedStatus(
                    $"GAMEPAD [{pad.VendorId:X4}:{pad.ProductId:X4}] LOCKED!",
                    "CLOSE OTHER CONTROLLER TOOLS AND RETRY");
                Thread.Sleep(1000);
                return;
            }

            IsConnected = false;
            StatusText = $"{pad.Name.ToUpperInvariant()} DETECTED [{pad.VendorId:X4}:{pad.ProductId:X4}]";
            ConnText = "UNIVERSAL HID // WAITING FOR INPUT";
            Changed?.Invoke();

            var buffer = new byte[Math.Max(128, pad.InputBufferSize)];
            while (_running && UniversalModeActive && !_disconnectRequested && !_connectRequested && !_connectionPaused)
            {
                if (!pad.TryRead(buffer, out int bytesRead))
                    break;
                if (bytesRead <= 0 || !pad.TryParse(buffer, bytesRead, out var state))
                    continue;

                if (!IsConnected)
                {
                    IsConnected = true;
                    StatusText = $"{pad.Name.ToUpperInvariant()} LINKED [{pad.VendorId:X4}:{pad.ProductId:X4}]";
                    ConnText = $"UNIVERSAL HID // {(state.Bluetooth ? "BLUETOOTH" : "USB")} → XBOX VIRTUAL";
                    Changed?.Invoke();
                }

                lock (_stateLock)
                    _hud = state;
                var profile = _config.ActiveProfile;
                _mapper.Map(state, profile, _xbox, xboxMode: true);
            }

            bool manuallyDisconnected = _disconnectRequested || _connectionPaused;
            _disconnectRequested = false;
            IsConnected = false;
            StatusText = manuallyDisconnected ? "DISCONNECTED BY USER" : "WAITING FOR UNIVERSAL HID GAMEPAD...";
            ConnText = manuallyDisconnected
                ? "PRESS CONNECT TO RESUME"
                : "UNIVERSAL HID // LINK OFFLINE";
            lock (_stateLock)
                _hud = new PadState();
            pad.Close();
            _mapper.ResetTransient();
            Changed?.Invoke();
        }

        private void SetGenericDisconnectedStatus(string status, string connection)
        {
            IsConnected = false;
            StatusText = status;
            ConnText = connection;
            lock (_stateLock)
                _hud = new PadState();
            Changed?.Invoke();
        }

        private void WriteOutput(SonyPad pad, PadState state, PadProfile profile)
        {
            var color = Color.FromArgb(Volatile.Read(ref _lightbarArgb));
            try
            {
                if (state.Kind == SonyPadKind.DualShock4)
                {
                    pad.TryWrite(DualSenseReports.BuildDs4Output(
                        state.Bluetooth, _rumbleLeft, _rumbleRight, color.R, color.G, color.B));
                }
                else
                {
                    _outSeq++;
                    pad.TryWrite(DualSenseReports.BuildDualSenseOutput(
                        state.Bluetooth,
                        _rumbleLeft,
                        _rumbleRight,
                        color.R, color.G, color.B,
                        profile.TriggerPresetL,
                        profile.TriggerPresetR,
                        _outSeq));
                }
            }
            catch { }
        }

        public void SetLightbarColor(Color color)
        {
            Interlocked.Exchange(ref _lightbarArgb, color.ToArgb());
        }

        public void Disconnect()
        {
            _connectionPaused = true;
            _disconnectRequested = true;
        }

        public void Connect()
        {
            _connectionPaused = false;
            _connectRequested = true;
        }

        public void Dispose() => Stop();
    }
}
