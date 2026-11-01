using DualSenseApp.Config;
using DualSenseApp.Hid;
using DualSenseApp.Native;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;

namespace DualSenseApp.Mapping
{
    public sealed class InputMapper
    {
        private bool _touchWasActive;
        private int _prevTouchX, _prevTouchY;
        private bool _mouseDown;

        public void ResetTransient()
        {
            _touchWasActive = false;
            if (_mouseDown)
            {
                NativeMethods.SendMouseButton(false);
                _mouseDown = false;
            }
        }

        public static void ApplyRadialDeadzone(int rawX, int rawY, int deadzonePercent, bool invertY, out short outX, out short outY)
        {
            float nx = (rawX - 128) / 128f;
            float ny = (rawY - 128) / 128f;
            float mag = MathF.Sqrt(nx * nx + ny * ny);
            float dz = Math.Clamp(deadzonePercent, 0, 90) / 100f;

            if (mag <= dz || mag < 0.0001f)
            {
                outX = 0;
                outY = 0;
                return;
            }

            float scale = (mag - dz) / ((1f - dz) * mag);
            nx = Math.Clamp(nx * scale, -1f, 1f);
            ny = Math.Clamp(ny * scale, -1f, 1f);
            if (invertY)
                ny = -ny;

            outX = (short)Math.Clamp((int)MathF.Round(nx * 32767f), -32768, 32767);
            outY = (short)Math.Clamp((int)MathF.Round(ny * 32767f), -32768, 32767);
        }

        public void Map(
            PadState pad,
            PadProfile profile,
            IXbox360Controller? xbox,
            bool xboxMode)
        {
            ApplyRadialDeadzone(pad.LX, pad.LY, profile.DeadzonePercent, invertY: true, out short lx, out short ly);
            ApplyRadialDeadzone(pad.RX, pad.RY, profile.DeadzonePercent, invertY: true, out short rx, out short ry);

            // Applica sensibilità stick
            float sensL = profile.StickSensitivityL / 100f;
            float sensR = profile.StickSensitivityR / 100f;
            lx = (short)Math.Clamp((int)(lx * sensL), -32768, 32767);
            ly = (short)Math.Clamp((int)(ly * sensL), -32768, 32767);
            rx = (short)Math.Clamp((int)(rx * sensR), -32768, 32767);
            ry = (short)Math.Clamp((int)(ry * sensR), -32768, 32767);

            HandleTouchpad(pad, profile, out bool tpBack, out bool tpGuide);

            if (!xboxMode || xbox == null)
                return;

            xbox.SetAxisValue(Xbox360Axis.LeftThumbX, lx);
            xbox.SetAxisValue(Xbox360Axis.LeftThumbY, ly);
            xbox.SetAxisValue(Xbox360Axis.RightThumbX, rx);
            xbox.SetAxisValue(Xbox360Axis.RightThumbY, ry);
            xbox.SetSliderValue(Xbox360Slider.LeftTrigger, (byte)pad.L2);
            xbox.SetSliderValue(Xbox360Slider.RightTrigger, (byte)pad.R2);

            xbox.SetButtonState(Xbox360Button.X, pad.Square);
            xbox.SetButtonState(Xbox360Button.A, pad.Cross);
            xbox.SetButtonState(Xbox360Button.B, pad.Circle);
            xbox.SetButtonState(Xbox360Button.Y, pad.Triangle);
            xbox.SetButtonState(Xbox360Button.Up, pad.DpadUp);
            xbox.SetButtonState(Xbox360Button.Right, pad.DpadRight);
            xbox.SetButtonState(Xbox360Button.Down, pad.DpadDown);
            xbox.SetButtonState(Xbox360Button.Left, pad.DpadLeft);
            xbox.SetButtonState(Xbox360Button.LeftShoulder, pad.L1);
            xbox.SetButtonState(Xbox360Button.RightShoulder, pad.R1);
            xbox.SetButtonState(Xbox360Button.LeftThumb, pad.L3);
            xbox.SetButtonState(Xbox360Button.RightThumb, pad.R3);
            xbox.SetButtonState(Xbox360Button.Start, pad.Options);

            bool back = pad.Create;
            bool guide = pad.Ps;
            if (tpBack) back = true;
            if (tpGuide) guide = true;
            xbox.SetButtonState(Xbox360Button.Back, back);
            xbox.SetButtonState(Xbox360Button.Guide, guide);
            xbox.SubmitReport();
        }

        private void HandleTouchpad(PadState pad, PadProfile profile, out bool tpBack, out bool tpGuide)
        {
            tpBack = false;
            tpGuide = false;

            if (profile.TouchpadMode == TouchpadMode.Button)
            {
                if (pad.TouchpadClick)
                {
                    if (profile.TouchpadClick == TouchpadClick.Guide)
                        tpGuide = true;
                    else
                        tpBack = true;
                }
                _touchWasActive = false;
                if (_mouseDown)
                {
                    NativeMethods.SendMouseButton(false);
                    _mouseDown = false;
                }
                return;
            }

            if (profile.TouchpadMode != TouchpadMode.Mouse)
            {
                _touchWasActive = false;
                return;
            }

            if (pad.Touch1Active)
            {
                if (_touchWasActive)
                {
                    float dx = (pad.Touch1X - _prevTouchX) * 0.35f;
                    float dy = (pad.Touch1Y - _prevTouchY) * 0.35f;
                    NativeMethods.SendMouseMove((int)MathF.Round(dx), (int)MathF.Round(dy));
                }
                _prevTouchX = pad.Touch1X;
                _prevTouchY = pad.Touch1Y;
                _touchWasActive = true;
            }
            else
            {
                _touchWasActive = false;
            }

            if (pad.TouchpadClick != _mouseDown)
            {
                NativeMethods.SendMouseButton(pad.TouchpadClick);
                _mouseDown = pad.TouchpadClick;
            }
        }
    }
}
