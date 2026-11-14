using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace FPVDrone
{
    /// <summary>
    /// Reads Windows calibrated joysticks via WinMM (joyGetPosEx), bypassing Unity InputSystem's
    /// HID parser when USB RC transmitters (e.g. DeviationTX) exhibit signed/unsigned wraparound.
    /// </summary>
    internal sealed class WinMMJoystick
    {
        private const uint JoyErrorNoError = 0;
        private const uint JoyReturnAll = 0x000000FFu;
        private const int MaxJoySlots = 16;
        private const float RescanIntervalSec = 1.0f;

        [StructLayout(LayoutKind.Sequential)]
        private struct JOYINFOEX
        {
            public uint dwSize;
            public uint dwFlags;
            public uint dwXpos;
            public uint dwYpos;
            public uint dwZpos;
            public uint dwRpos;
            public uint dwUpos;
            public uint dwVpos;
            public uint dwButtons;
            public uint dwButtonNumber;
            public uint dwPOV;
            public uint dwReserved1;
            public uint dwReserved2;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode, Pack = 1)]
        private struct JOYCAPSW
        {
            public ushort wMid;
            public ushort wPid;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string szPname;
            public uint wXmin;
            public uint wXmax;
            public uint wYmin;
            public uint wYmax;
            public uint wZmin;
            public uint wZmax;
            public uint wNumButtons;
            public uint wPeriodMin;
            public uint wPeriodMax;
            public uint wRmin;
            public uint wRmax;
            public uint wUmin;
            public uint wUmax;
            public uint wVmin;
            public uint wVmax;
            public uint wCaps;
            public uint wMaxAxes;
            public uint wNumAxes;
            public uint wMaxButtons;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string szRegKey;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szOEMVxD;
        }

        [DllImport("winmm.dll", CallingConvention = CallingConvention.Winapi)]
        private static extern uint joyGetNumDevs();

        [DllImport("winmm.dll", CallingConvention = CallingConvention.Winapi)]
        private static extern uint joyGetPosEx(uint uJoyID, ref JOYINFOEX pji);

        [DllImport("winmm.dll", CallingConvention = CallingConvention.Winapi, CharSet = CharSet.Unicode)]
        private static extern uint joyGetDevCapsW(UIntPtr uJoyID, ref JOYCAPSW pjc, uint cbjc);

        private static readonly string[] AxisLabels = { "X", "Y", "Z", "R", "U", "V" };

        public bool IsConnected { get; private set; }
        public uint ActiveJoyId { get; private set; }
        public string DeviceName { get; private set; } = "RC Radio";
        public int NumAxes { get; private set; }
        public int NumButtons { get; private set; }
        public uint CurrentButtons => _currButtons;

        public readonly float[] Axes = new float[6];

        private readonly uint[] _axisMin = new uint[6];
        private readonly uint[] _axisMax = new uint[6];
        private uint _prevButtons;
        private uint _currButtons;
        private float _lastScanTime = -10.0f;
        private bool _winmmUnavailable;

        private JOYINFOEX _info = new JOYINFOEX
        {
            dwSize = (uint)Marshal.SizeOf<JOYINFOEX>(),
            dwFlags = JoyReturnAll
        };

        public bool Poll()
        {
            if (_winmmUnavailable)
                return false;

            try
            {
                if (IsConnected)
                {
                    if (joyGetPosEx(ActiveJoyId, ref _info) == JoyErrorNoError)
                    {
                        UpdateState();
                        return true;
                    }

                    IsConnected = false;
                    _prevButtons = 0;
                    _currButtons = 0;
                }

                float now = Time.realtimeSinceStartup;
                if (now - _lastScanTime < RescanIntervalSec)
                    return false;
                _lastScanTime = now;

                uint numDevs = Math.Min(joyGetNumDevs(), (uint)MaxJoySlots);
                for (uint id = 0; id < numDevs; id++)
                {
                    if (joyGetPosEx(id, ref _info) == JoyErrorNoError)
                    {
                        Connect(id);
                        UpdateState();
                        return true;
                    }
                }
            }
            catch (DllNotFoundException)
            {
                _winmmUnavailable = true;
            }
            catch (EntryPointNotFoundException)
            {
                _winmmUnavailable = true;
            }
            catch (Exception ex)
            {
                MelonLoader.MelonLogger.Warning($"[FPVDrone] WinMM joystick error: {ex.Message}");
                IsConnected = false;
            }

            return false;
        }

        private void Connect(uint id)
        {
            ActiveJoyId = id;
            IsConnected = true;
            _prevButtons = 0;
            _currButtons = 0;

            var caps = new JOYCAPSW();
            uint capsSize = (uint)Marshal.SizeOf<JOYCAPSW>();
            if (joyGetDevCapsW((UIntPtr)id, ref caps, capsSize) == JoyErrorNoError)
            {
                DeviceName = string.IsNullOrEmpty(caps.szPname) ? $"Joystick {id}" : caps.szPname;
                NumAxes = (int)Math.Min(caps.wNumAxes, 6u);
                NumButtons = (int)Math.Min(caps.wNumButtons, 32u);

                _axisMin[0] = caps.wXmin; _axisMax[0] = caps.wXmax;
                _axisMin[1] = caps.wYmin; _axisMax[1] = caps.wYmax;
                _axisMin[2] = caps.wZmin; _axisMax[2] = caps.wZmax;
                _axisMin[3] = caps.wRmin; _axisMax[3] = caps.wRmax;
                _axisMin[4] = caps.wUmin; _axisMax[4] = caps.wUmax;
                _axisMin[5] = caps.wVmin; _axisMax[5] = caps.wVmax;
            }
            else
            {
                DeviceName = $"Joystick {id}";
                NumAxes = 6;
                NumButtons = 16;
                for (int i = 0; i < 6; i++)
                {
                    _axisMin[i] = 0;
                    _axisMax[i] = 65535;
                }
            }

            MelonLoader.MelonLogger.Msg(
                $"[FPVDrone] WinMM radio connected: id={id} name='{DeviceName}' axes={NumAxes} buttons={NumButtons}");
        }

        private void UpdateState()
        {
            Axes[0] = Normalize(_info.dwXpos, _axisMin[0], _axisMax[0]);
            Axes[1] = Normalize(_info.dwYpos, _axisMin[1], _axisMax[1]);
            Axes[2] = Normalize(_info.dwZpos, _axisMin[2], _axisMax[2]);
            Axes[3] = Normalize(_info.dwRpos, _axisMin[3], _axisMax[3]);
            Axes[4] = Normalize(_info.dwUpos, _axisMin[4], _axisMax[4]);
            Axes[5] = Normalize(_info.dwVpos, _axisMin[5], _axisMax[5]);

            _prevButtons = _currButtons;
            _currButtons = _info.dwButtons;
        }

        private static float Normalize(uint raw, uint min, uint max)
        {
            if (max <= min) return 0.0f;
            float t = (float)(raw - min) / (float)(max - min);
            return Mathf.Clamp(t * 2.0f - 1.0f, -1.0f, 1.0f);
        }

        public bool WasButtonPressed(int index)
        {
            if (!IsConnected || index < 0 || index >= 32)
                return false;
            uint mask = 1u << index;
            return (_currButtons & mask) != 0 && (_prevButtons & mask) == 0;
        }

        public int DetectPressedButton()
        {
            if (!IsConnected) return -1;
            uint rising = _currButtons & ~_prevButtons;
            if (rising == 0) return -1;

            int limit = Math.Max(NumButtons, 32);
            for (int i = 0; i < limit; i++)
            {
                if ((rising & (1u << i)) != 0)
                    return i;
            }
            return -1;
        }

        public string GetAxesSummary()
        {
            if (!IsConnected) return "Disconnected";
            int count = Mathf.Clamp(NumAxes, 1, 6);
            string s = "";
            for (int i = 0; i < count; i++)
            {
                if (i > 0) s += "  ";
                s += $"{AxisLabels[i]}:{Axes[i]:F2}";
            }
            return s;
        }

        public string GetButtonsSummary()
        {
            if (!IsConnected) return "-";
            string pressed = "";
            int limit = Math.Max(NumButtons, 16);
            for (int i = 0; i < limit; i++)
            {
                if ((_currButtons & (1u << i)) != 0)
                {
                    if (pressed.Length > 0) pressed += ",";
                    pressed += i.ToString();
                }
            }
            return pressed.Length > 0
                ? $"{NumButtons} total (Pressed: {pressed})"
                : $"{NumButtons} total (None pressed)";
        }
    }
}

