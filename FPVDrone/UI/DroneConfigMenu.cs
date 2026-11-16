using System;
using System.Collections.Generic;
using MelonLoader;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FPVDrone
{
    /// <summary>
    /// IMGUI configurator. Tabs are lists of declarative ConfigRow objects (see ConfigModel.cs);
    /// this class only renders them and routes mouse / keyboard / gamepad input.
    /// </summary>
    public class DroneConfigMenu
    {
        public bool IsOpen { get; private set; } = false;

        public event Action OnResetDroneRequested;
        public event Action OnTurtleFlipRequested;

        private class ConfigTab
        {
            public string Name;
            public List<ConfigRow> Rows = new List<ConfigRow>();
        }

        // Layout (virtual units, scaled to the screen)
        public const float TabletWidth = PanelW;
        public const float TabletHeight = PanelH;
        private const float PanelW = 1060f;
        private const float PanelH = 680f;
        private const float HeaderH = 46f;
        private const float SidebarW = 160f;
        private const float DetailH = 50f;
        private const float FooterH = 28f;
        private const float RowH = 32f;
        private const float RatesGraphPaneW = 360f;
        private const float SaveDelay = 0.6f;

        private DroneInput _input;
        private readonly List<ConfigTab> _tabs = new List<ConfigTab>();
        private readonly List<ConfigRow> _vis = new List<ConfigRow>();
        private readonly List<float> _ys = new List<float>();
        private int _tab;
        private ConfigRow _sel;
        private ConfigRow _dragRow;
        private float _scrollY;
        private bool _ensureVisible;
        private bool _mouseInContent;

        // Debounced preference saving
        private bool _dirty;
        private float _lastEdit;

        // Navigation repeat
        private int _lastNavFrame = -1;
        private int _heldX, _heldY;
        private float _nextRepX, _nextRepY, _heldXSince;
        private string _lastError;

        // Styling
        private GUIStyle _tx;
        private GUIStyle _textFieldStyle;
        private Font _uiFont;
        private Font _monoFont;
        private Texture2D _circleTex;
        private Texture2D _ringTex;

        // ------------------------------------------------------------------ lifecycle

        public void Initialize(DroneInput input = null)
        {
            _input = input;
            BuildTabs();
            SwitchTab(0);
        }

        public void ToggleMenu()
        {
            SetOpen(!IsOpen);
        }

        public void SetOpen(bool open)
        {
            if (open == IsOpen)
                return;

            IsOpen = open;
            if (open)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                _heldX = _heldY = 0;
                if (_sel == null)
                    SwitchTab(_tab);
            }
            else
            {
                _dragRow = null;
                _input?.CancelListeningForBind();
                FlushSave();
            }
        }

        public void Destroy()
        {
            FlushSave();
            _tx = null;
            _textFieldStyle = null;
            if (_circleTex != null) { UnityEngine.Object.Destroy(_circleTex); _circleTex = null; }
            if (_ringTex != null) { UnityEngine.Object.Destroy(_ringTex); _ringTex = null; }
        }

        private void Touch()
        {
            _dirty = true;
            _lastEdit = Time.unscaledTime;
        }

        private void FlushSave()
        {
            if (!_dirty)
                return;
            _dirty = false;
            MelonPreferences.Save();
        }

        // ------------------------------------------------------------------ tab definitions

        private static string F0(float v) { return v.ToString("F0"); }
        private static string F1(float v) { return v.ToString("F1"); }
        private static string F2(float v) { return v.ToString("F2"); }
        private static string Pct(float v) { return (v * 100f).ToString("F0") + "%"; }
        private static string Deg(float v) { return v.ToString("F0") + " deg"; }

        private bool PadActive()
        {
            return Settings.InputDevice.Value == ControllerDevice.Auto || Settings.InputDevice.Value == ControllerDevice.Gamepad;
        }

        private bool RadioActive()
        {
            return Settings.InputDevice.Value == ControllerDevice.Auto || Settings.InputDevice.Value == ControllerDevice.RadioTransmitter;
        }

        private static void CyclePreset(int dir)
        {
            int count = Enum.GetValues(typeof(DronePreset)).Length;
            int next = ((int)Settings.ActivePreset.Value + dir + count) % count;
            Settings.ApplyDronePreset((DronePreset)next);
        }

        private static int FindNearestIndex(float[] arr, float val)
        {
            int best = 0;
            float bestDiff = float.MaxValue;
            for (int i = 0; i < arr.Length; i++)
            {
                float d = Mathf.Abs(arr[i] - val);
                if (d < bestDiff) { bestDiff = d; best = i; }
            }
            return best;
        }

        private static void CycleFrameSize(int dir)
        {
            float[] frames = DroneBuilder.PopularFrameWheelbasesMm;
            int idx = FindNearestIndex(frames, Settings.FrameWheelbaseMm.Value);
            Settings.FrameWheelbaseMm.Value = frames[(idx + dir + frames.Length) % frames.Length];
            DroneBuilder.EnforceGeometryConstraints();
        }

        private static void CyclePropSize(int dir)
        {
            float[] props = DroneBuilder.PopularPropInches;
            float maxFit = DroneBuilder.MaxPropInchesForFrame(Settings.FrameWheelbaseMm.Value, Settings.IsDuctedFrame.Value);

            int maxAllowedIdx = 0;
            for (int i = 0; i < props.Length; i++)
            {
                if (props[i] <= maxFit + 0.001f)
                    maxAllowedIdx = i;
            }

            int count = maxAllowedIdx + 1;
            int cur = Mathf.Clamp(FindNearestIndex(props, Settings.PropDiameterInches.Value), 0, maxAllowedIdx);
            int next = (cur + dir + count) % count;
            Settings.PropDiameterInches.Value = props[next];
            DroneBuilder.EnforceGeometryConstraints();
        }

        private static void CyclePropBlades(int dir)
        {
            int cur = Mathf.Clamp(Settings.PropBlades.Value, 2, 4);
            int next = ((cur - 2 + dir + 3) % 3) + 2;
            Settings.PropBlades.Value = next;
        }

        private static int FindCurrentMotorIndex()
        {
            MotorSizePreset[] motors = DroneBuilder.PopularMotors;
            int best = 0;
            float bestDiff = float.MaxValue;
            for (int i = 0; i < motors.Length; i++)
            {
                float d = Mathf.Abs(motors[i].StatorDiameterMm - Settings.MotorStatorDiameterMm.Value)
                        + Mathf.Abs(motors[i].StatorHeightMm - Settings.MotorStatorHeightMm.Value);
                if (d < bestDiff) { bestDiff = d; best = i; }
            }
            return best;
        }

        private static void CycleMotorSize(int dir)
        {
            MotorSizePreset[] motors = DroneBuilder.PopularMotors;
            int next = (FindCurrentMotorIndex() + dir + motors.Length) % motors.Length;
            MotorSizePreset m = motors[next];
            Settings.MotorStatorDiameterMm.Value = m.StatorDiameterMm;
            Settings.MotorStatorHeightMm.Value = m.StatorHeightMm;
            Settings.MotorKv.Value = m.PopularKvs[m.PopularKvs.Length / 2];
        }

        private static void CycleMotorKv(int dir)
        {
            MotorSizePreset m = DroneBuilder.PopularMotors[FindCurrentMotorIndex()];
            float[] kvs = m.PopularKvs;
            int idx = FindNearestIndex(kvs, Settings.MotorKv.Value);
            Settings.MotorKv.Value = kvs[(idx + dir + kvs.Length) % kvs.Length];
        }

        private static void CycleLipoPack(int dir)
        {
            LipoPreset[] packs = DroneBuilder.PopularLipos;
            int best = 0;
            float bestDiff = float.MaxValue;
            for (int i = 0; i < packs.Length; i++)
            {
                float d = Mathf.Abs(packs[i].Cells - Settings.BatteryCells.Value) * 1000f
                        + Mathf.Abs(packs[i].CapacityMah - Settings.BatteryCapacityMah.Value);
                if (d < bestDiff) { bestDiff = d; best = i; }
            }
            LipoPreset next = packs[(best + dir + packs.Length) % packs.Length];
            Settings.BatteryCells.Value = next.Cells;
            Settings.BatteryCapacityMah.Value = next.CapacityMah;
        }

        private static void CycleEscAmps(int dir)
        {
            float[] amps = DroneBuilder.PopularEscAmps;
            int idx = FindNearestIndex(amps, Settings.EscAmperage.Value);
            Settings.EscAmperage.Value = amps[(idx + dir + amps.Length) % amps.Length];
        }

        private static void CycleVtxPower(int dir)
        {
            float[] powers = { 25f, 200f, 800f, 1000f };
            int cur = 0;
            for (int i = 0; i < powers.Length; i++)
            {
                if (Mathf.Abs(Settings.VtxPowerMw.Value - powers[i]) < 10f) { cur = i; break; }
            }
            Settings.VtxPowerMw.Value = powers[(cur + dir + powers.Length) % powers.Length];
        }

        private static string VtxModeName(VtxSystem v) => v == VtxSystem.Digital ? "Digital (HD)" : "Analog (5.8G)";

        private static string FlightModeName(FlightMode m)
        {
            switch (m)
            {
                case FlightMode.Angle: return "ANGLE (Auto-Level)";
                case FlightMode.PositionHold: return "POS HOLD (DJI)";
                default: return "ACRO (Rate)";
            }
        }

        private static string DeviceName(ControllerDevice d)
        {
            switch (d)
            {
                case ControllerDevice.RadioTransmitter: return "RC TRANSMITTER";
                case ControllerDevice.Gamepad: return "GAMEPAD";
                case ControllerDevice.KeyboardOnly: return "KEYBOARD ONLY";
                case ControllerDevice.VRControllers: return "VR CONTROLLERS";
                default: return "AUTO DETECT";
            }
        }

        private static string StickName(StickMode m)
        {
            switch (m)
            {
                case StickMode.Mode1: return "MODE 1";
                case StickMode.Mode3: return "MODE 3";
                case StickMode.Mode4: return "MODE 4";
                default: return "MODE 2 (Default)";
            }
        }

        private static string AxisName(RadioAxis a)
        {
            switch (a)
            {
                case RadioAxis.X: return "AXIS X";
                case RadioAxis.Y: return "AXIS Y";
                case RadioAxis.Z: return "AXIS Z";
                case RadioAxis.R: return "AXIS R (Rz)";
                case RadioAxis.U: return "AXIS U (Rx)";
                case RadioAxis.V: return "AXIS V (Ry)";
                default: return a.ToString();
            }
        }

        private static string RateTypeName(RateType t)
        {
            return t == RateType.Actual ? "ACTUAL (BF 4.2+)" : "BETAFLIGHT CLASSIC";
        }

        private static string ThrottleLimitName(ThrottleLimitType t)
        {
            switch (t)
            {
                case ThrottleLimitType.Scale: return "SCALE";
                case ThrottleLimitType.Clip: return "CLIP";
                default: return "OFF";
            }
        }

        private static string VrViewName(VrViewMode m)
        {
            switch (m)
            {
                case VrViewMode.Stereo3D: return "3D GOGGLES";
                case VrViewMode.CockpitVR3D: return "FULL 3D VR";
                default: return "2D GOGGLES";
            }
        }

        private static string GimbalModeName(SpoutGimbalMode m)
        {
            switch (m)
            {
                case SpoutGimbalMode.OneAxisRoll: return "1-AXIS (Roll Lock)";
                case SpoutGimbalMode.TwoAxisHorizon: return "2-AXIS (Horizon Lock)";
                case SpoutGimbalMode.ThreeAxisSmooth: return "3-AXIS (Gyroflow Smooth)";
                case SpoutGimbalMode.Custom: return "CUSTOM (Per-Axis)";
                default: return "OFF (Frame-Locked)";
            }
        }

        private static string GimbalAxisName(GimbalAxisMode m)
        {
            switch (m)
            {
                case GimbalAxisMode.Smooth: return "SMOOTH";
                case GimbalAxisMode.Lock: return "HORIZON LOCK";
                default: return "RAW (Frame)";
            }
        }

        private static string GimbalYawName(GimbalYawMode m)
        {
            return m == GimbalYawMode.Smooth ? "SMOOTH" : "RAW (Frame)";
        }

        private static string ResolutionName(VideoResolution r)
        {
            switch (r)
            {
                case VideoResolution.Res720p: return "720p (1280x720)";
                case VideoResolution.Res1080p: return "1080p (1920x1080)";
                case VideoResolution.Res1440p: return "1440p (2560x1440)";
                case VideoResolution.Res4K: return "4K (3840x2160)";
                default: return "MATCH SCREEN";
            }
        }

        private ConfigRow GamepadBind(string label, DroneAction a, MelonPreferences_Entry<GamepadButtonBinding> e)
        {
            return Rows.Bind(label, a, false, () => Settings.GetGamepadButtonName(e.Value), "Click, then press the gamepad button to assign.", PadActive);
        }

        private ConfigRow RadioBind(string label, DroneAction a, MelonPreferences_Entry<int> e)
        {
            return Rows.Bind(label, a, true, () => Settings.GetRadioButtonName(e.Value), "Click, then flip the switch or press the button to assign.", RadioActive);
        }

        private void BuildTabs()
        {
            _tabs.Clear();

            bool IsActual() => Settings.RateSystem.Value == RateType.Actual;
            bool IsClassic() => Settings.RateSystem.Value == RateType.Betaflight;

            void AddAxisRateRows(
                ConfigTab tab,
                string title,
                int axisIdx,
                MelonPreferences_Entry<float> cSens,
                MelonPreferences_Entry<float> maxR,
                MelonPreferences_Entry<float> aExpo,
                MelonPreferences_Entry<float> rc,
                MelonPreferences_Entry<float> super,
                MelonPreferences_Entry<float> expo)
            {
                tab.Rows.Add(Rows.Section(title));
                tab.Rows.Add(Rows.Float("Center Sensitivity", cSens, 10f, 20f, 600f, v => F0(v) + " deg/s", "Exact rotation speed near stick center (Actual rates).", IsActual));
                tab.Rows.Add(Rows.Float("Max Rate", maxR, 10f, 100f, 1998f, v => F0(v) + " deg/s", "Exact rotation speed at full stick deflection (Actual rates).", IsActual));
                tab.Rows.Add(Rows.Float("Expo", aExpo, 0.02f, 0.0f, 1.00f, F2, "Softens the transition from center sensitivity to max rate.", IsActual));
                tab.Rows.Add(Rows.Float("RC Rate", rc, 0.05f, 0.20f, 2.50f, F2, "Center sensitivity and overall rate scale.", IsClassic));
                tab.Rows.Add(Rows.Float("Super Rate", super, 0.01f, 0.0f, 0.95f, F2, "Boosts rate toward full stick deflection.", IsClassic));
                tab.Rows.Add(Rows.Float("Expo", expo, 0.01f, 0.0f, 1.00f, F2, "Softens the center of the stick for fine control.", IsClassic));
                tab.Rows.Add(Rows.Info("Max rate", () => F0(BetaflightRates.GetAxisMaxRate(axisIdx)) + " deg/s"));
            }

            var rates = new ConfigTab { Name = "Rates" };
            rates.Rows.Add(Rows.Choice("Rate Type", Settings.RateSystem, RateTypeName, "Actual (BF 4.2+): direct center/max deg/s. Betaflight Classic: RC/Super/Expo."));
            AddAxisRateRows(rates, "Roll", 0, Settings.RollCenterSens, Settings.RollMaxRate, Settings.RollActualExpo, Settings.RcRate, Settings.SuperRate, Settings.Expo);
            AddAxisRateRows(rates, "Pitch", 1, Settings.PitchCenterSens, Settings.PitchMaxRate, Settings.PitchActualExpo, Settings.PitchRcRate, Settings.PitchSuperRate, Settings.PitchExpo);
            AddAxisRateRows(rates, "Yaw", 2, Settings.YawCenterSens, Settings.YawMaxRate, Settings.YawActualExpo, Settings.YawRcRate, Settings.YawSuperRate, Settings.YawExpo);
            rates.Rows.Add(Rows.Section("Throttle"));
            rates.Rows.Add(Rows.Float("Throttle Mid", Settings.ThrottleMid, 0.02f, 0.05f, 0.95f, F2, "Stick position where the throttle expo curve flattens (hover point)."));
            rates.Rows.Add(Rows.Float("Throttle Expo", Settings.ThrottleExpo, 0.05f, 0.0f, 1.00f, Pct, "Flattens throttle sensitivity around Throttle Mid."));
            rates.Rows.Add(Rows.Choice("Throttle Limit", Settings.ThrottleLimitMode, ThrottleLimitName, "Off: 100% power. Scale: scales full range. Clip: caps max output."));
            rates.Rows.Add(Rows.Float("Throttle Limit %", Settings.ThrottleLimitPercent, 5f, 25.0f, 100.0f, v => F0(v) + "%", "Maximum throttle output percentage.", () => Settings.ThrottleLimitMode.Value != ThrottleLimitType.Off));
            _tabs.Add(rates);

            var drone = new ConfigTab { Name = "Drone" };
            drone.Rows.Add(Rows.Section("Presets & Saved Builds"));
            drone.Rows.Add(Rows.Choice("Default Preset", () => DronePresets.Current.DisplayName, CyclePreset, "Applies a built-in preset to the builder sliders (read-only, never overwritten)."));
            drone.Rows.Add(Rows.Choice("Saved Builds", () => DroneProfileStore.SelectedLabel, d => DroneProfileStore.CycleSelected(d), "Select a custom drone build saved in UserData/FPVDrone/Builds."));
            drone.Rows.Add(Rows.TextInput("Build Name", () => DroneProfileStore.CustomNameDraft, s => DroneProfileStore.CustomNameDraft = s, "Optional custom name...", "Optional name for saving or renaming a custom build (defaults to frame/prop/KV)."));
            drone.Rows.Add(Rows.Action("Save as New Build", () => DroneProfileStore.SaveCurrentBuild(), "Saves current hardware and rates as a new JSON file in UserData/FPVDrone/Builds."));
            drone.Rows.Add(Rows.Action("Overwrite Selected Build", () => DroneProfileStore.UpdateSelectedBuild(), "Updates the selected saved build with current hardware, rates, and name.", false, () => DroneProfileStore.HasSelection));
            drone.Rows.Add(Rows.Action("Load Selected Build", () => DroneProfileStore.LoadSelectedBuild(), "Loads the selected saved build (hardware + rates).", false, () => DroneProfileStore.HasSelection));
            drone.Rows.Add(Rows.Action("Delete Selected Build", () => DroneProfileStore.DeleteSelectedBuild(), "Deletes the selected build JSON from UserData.", true, () => DroneProfileStore.HasSelection));
            drone.Rows.Add(Rows.Info("Build Status", () => DroneProfileStore.LastStatusMessage, () => !string.IsNullOrEmpty(DroneProfileStore.LastStatusMessage)));

            drone.Rows.Add(Rows.Section("Frame & Propellers"));
            drone.Rows.Add(Rows.Choice("Frame Size", () => $"{Settings.FrameWheelbaseMm.Value:F0} mm", CycleFrameSize, "Wheelbase (motor-to-motor diagonal). Automatically caps max prop size."));
            drone.Rows.Add(Rows.Toggle("Ducted Guards", () => Settings.IsDuctedFrame.Value, () => { Settings.IsDuctedFrame.Value = !Settings.IsDuctedFrame.Value; DroneBuilder.EnforceGeometryConstraints(); }, "Whoop ducts / prop guards: higher bounce restitution and bump tolerance.", "DUCTED", "OPEN PROP"));
            drone.Rows.Add(Rows.Choice("Prop Diameter", () => $"{Settings.PropDiameterInches.Value:F1}\" (max {DroneBuilder.MaxPropInchesForFrame(Settings.FrameWheelbaseMm.Value, Settings.IsDuctedFrame.Value):F1}\")", CyclePropSize, "Propeller diameter, clamped to physically fit the frame wheelbase."));
            var propPitchRow = Rows.Float("Prop Pitch", Settings.PropPitchInches, 0.1f, 0.8f, 6.0f, v => F1(v) + "\"", "Propeller pitch (higher = more top speed and amp draw).");
            var origSetPitch = propPitchRow.SetFloat;
            propPitchRow.SetFloat = v => { origSetPitch(v); DroneBuilder.EnforceGeometryConstraints(); };
            drone.Rows.Add(propPitchRow);
            drone.Rows.Add(Rows.Choice("Prop Blades", () => $"{Settings.PropBlades.Value}-Blade", CyclePropBlades, "Number of blades per propeller (2, 3, or 4). Affects thrust and audio BPF."));

            drone.Rows.Add(Rows.Section("Motors, ESC & Battery"));
            drone.Rows.Add(Rows.Choice("Motor Size", () => DroneBuilder.PopularMotors[FindCurrentMotorIndex()].Name, CycleMotorSize, "Popular stator size. Sets stator torque and a matching KV."));
            drone.Rows.Add(Rows.Choice("Motor KV", () => $"{Settings.MotorKv.Value:F0} KV", CycleMotorKv, "Popular KV for this motor size. Drives RPM, thrust, and audio pitch."));
            drone.Rows.Add(Rows.Choice("LiPo Pack", () => $"{Settings.BatteryCells.Value}S {Settings.BatteryCapacityMah.Value:F0}mAh", CycleLipoPack, "Popular LiPo cell count and capacity presets."));
            drone.Rows.Add(Rows.Int("Battery Cells", Settings.BatteryCells, 1, 6, v => $"{v:F0}S ({v * 4.2f:F1}V)", "LiPo cell count (1S to 6S)."));
            drone.Rows.Add(Rows.Float("Pack Capacity", Settings.BatteryCapacityMah, 50f, 250f, 6000f, v => $"{v:F0} mAh", "Battery capacity in mAh."));
            drone.Rows.Add(Rows.Choice("ESC Rating", () => $"{Settings.EscAmperage.Value:F0} A", CycleEscAmps, "Per-motor ESC current limit. Caps power if props/KV draw too many amps."));
            drone.Rows.Add(Rows.Bool("Weight Mode", Settings.AutoEstimateMass, "Automatically estimate All-Up Weight from frame, motors, props, and LiPo, or override manually.", "AUTO (Parts)", "MANUAL"));
            drone.Rows.Add(Rows.Float("All-Up Weight", Settings.QuadMassKg, 0.01f, 0.02f, 4.0f, v => v < 0.2f ? $"{v * 1000f:F0} g" : $"{v:F2} kg", "Manual total take-off weight override.", () => !Settings.AutoEstimateMass.Value));
            drone.Rows.Add(Rows.Section("Camera Mount"));
            drone.Rows.Add(Rows.Float("Cam Mount Height", Settings.CameraHeightOffsetMm, 1f, -5.0f, 50.0f, v => $"{v:F0} mm", "Vertical camera lens offset above frame center (raise above ducts on whoops)."));
            drone.Rows.Add(Rows.Float("Cam Mount Fwd", Settings.CameraForwardOffsetMm, 2f, 0.0f, 80.0f, v => $"{v:F0} mm", "Forward camera lens offset toward the nose (reduces prop/duct occlusion)."));
            _tabs.Add(drone);

            var flight = new ConfigTab { Name = "Flight" };
            flight.Rows.Add(Rows.Section("Mode"));
            flight.Rows.Add(Rows.Choice("Flight Mode", Settings.Mode, FlightModeName, "Acro uses rates, Angle auto-levels, Pos Hold brakes to a hover."));
            flight.Rows.Add(Rows.Float("Max Angle", Settings.MaxAngleDegrees, 1f, 15.0f, 85.0f, Deg, "Tilt limit in Angle mode.", () => Settings.Mode.Value != FlightMode.Acro));
            flight.Rows.Add(Rows.Float("Auto Turtle Delay", Settings.AutoFlipDelay, 0.5f, 0.5f, 10.0f, v => F1(v) + " s", "Time upside down before the drone flips itself upright."));
            flight.Rows.Add(Rows.Section("Camera & Mixing"));
            flight.Rows.Add(Rows.Float("Camera Uptilt", Settings.CameraTiltAngle, 1f, 0.0f, 75.0f, Deg, "FPV camera pitch angle above the frame plane."));
            flight.Rows.Add(Rows.Bool("Tilt Yaw Correction", Settings.EnableCameraAngleMix, "Mixes yaw/roll to follow the camera tilt (FPV angle mix).", "FLAT (Mixed)", "RAW BODY"));
            flight.Rows.Add(Rows.Section("PID Tune"));
            flight.Rows.Add(Rows.Bool("Tuning Mode", Settings.AdvancedPidTuning, "Simple snappiness multiplier or full manual PID loop tuning.", "ADVANCED", "SIMPLE"));
            flight.Rows.Add(Rows.Float("PID Snappiness", Settings.PidSnappiness, 0.05f, 0.5f, 1.6f, v => $"{v:F2}x", "Overall flight controller responsiveness (0.6x Cinema to 1.4x Race).", () => !Settings.AdvancedPidTuning.Value));
            flight.Rows.Add(Rows.Float("Roll/Pitch Bandwidth", Settings.PidRollPitchWn, 1f, 15f, 85f, v => $"{v:F0} rad/s", "Roll/Pitch PID loop natural frequency.", () => Settings.AdvancedPidTuning.Value));
            flight.Rows.Add(Rows.Float("Yaw Bandwidth", Settings.PidYawWn, 1f, 10f, 65f, v => $"{v:F0} rad/s", "Yaw PID loop natural frequency.", () => Settings.AdvancedPidTuning.Value));
            flight.Rows.Add(Rows.Float("D-Term Damping", Settings.PidDamping, 0.02f, 0.50f, 1.25f, F2, "Damping ratio (< 1.0 adds snap bounce-back, 1.0+ is critically damped).", () => Settings.AdvancedPidTuning.Value));
            flight.Rows.Add(Rows.Section("Throttle"));
            flight.Rows.Add(Rows.Bool("Gamepad Throttle", Settings.SpringCenteredThrottle, "Gamepad only: Spring centered (center = 0) vs Full range (bottom = 0). RC Radio always uses full range.", "SPRING CENTERED", "FULL RANGE"));
            flight.Rows.Add(Rows.Section("Sticks"));
            flight.Rows.Add(Rows.Float("Stick Deadzone", Settings.StickDeadzone, 0.01f, 0.01f, 0.35f, Pct, "Ignore stick movement below this threshold."));
            flight.Rows.Add(Rows.Bool("Invert Pitch", Settings.InvertPitch, "Standard drone convention: stick forward pitches the nose down.", "INVERTED", "NORMAL"));
            flight.Rows.Add(Rows.Float("Keyboard Smoothing", Settings.KeyboardSmoothing, 0.5f, 1.0f, 20.0f, F1, "Slew speed of keyboard stick emulation. Higher is snappier.", () => Settings.InputDevice.Value == ControllerDevice.Auto || Settings.InputDevice.Value == ControllerDevice.KeyboardOnly));
            _tabs.Add(flight);

            var physics = new ConfigTab { Name = "Physics" };
            physics.Rows.Add(Rows.Section("Aerodynamics"));
            physics.Rows.Add(Rows.Float("Prop Wash", Settings.PropWashStrength, 0.05f, 0.0f, 3.0f, Pct, "Turbulence strength when descending through your own wash."));
            physics.Rows.Add(Rows.Float("Ground Effect", Settings.GroundEffectStrength, 0.05f, 0.0f, 3.0f, Pct, "Extra lift cushion close to the ground."));
            physics.Rows.Add(Rows.Float("Snap Wobble", Settings.SnapWobbleStrength, 0.05f, 0.0f, 2.0f, Pct, "Motor-inertia wobble after hard stick stops."));
            physics.Rows.Add(Rows.Bool("Translational Lift", Settings.EnableTranslationalLift, "Effective translational lift and inflow pitch-up in forward flight."));
            physics.Rows.Add(Rows.Bool("Motor Saturation", Settings.EnableMotorSaturation, "Mixer RPM saturation and yaw washout at full throttle."));
            physics.Rows.Add(Rows.Bool("World Collisions", Settings.DisableCollisions, "Disable all collider hits so the drone flies through walls and floors.", "DISABLED (NOCLIP)", "ENABLED"));
            physics.Rows.Add(Rows.Section("Battery Simulation"));
            physics.Rows.Add(Rows.Bool("LiPo Depletion", Settings.SimulateBatteryDepletion, "Drain the pack over time and enter limp mode when empty."));
            physics.Rows.Add(Rows.Bool("Voltage Sag", Settings.EnableVoltageSag, "Voltage drops under high throttle, reducing power and OSD voltage."));
            _tabs.Add(physics);

            var input = new ConfigTab { Name = "Input" };
            input.Rows.Add(Rows.Section("Device"));
            input.Rows.Add(Rows.Choice("Input Device", Settings.InputDevice, DeviceName, "Which controller drives the drone."));
            input.Rows.Add(Rows.Info("Active Device", () => _input != null ? _input.ActiveDeviceName : "None"));
            input.Rows.Add(Rows.Info("Radio Axes", () => _input != null ? _input.GetRadioAxesSummary() : "-", RadioActive));
            input.Rows.Add(Rows.Info("Radio Buttons", () => _input != null ? _input.GetRadioButtonsSummary() : "-", RadioActive));
            input.Rows.Add(Rows.Choice("RC Stick Mode", Settings.ControlStickMode, StickName, "Radio stick layout. Mode 2: throttle/yaw left, pitch/roll right.", RadioActive));
            input.Rows.Add(Rows.Bool("Xbox RT Throttle", Settings.GamepadTriggerThrottle, "Use the right trigger for throttle instead of the left stick.", "RIGHT TRIGGER", "LEFT STICK", PadActive));
            input.Rows.Add(Rows.Section("Gamepad Bindings", PadActive));
            input.Rows.Add(GamepadBind("Arm / Disarm", DroneAction.Arm, Settings.GamepadArmBind));
            input.Rows.Add(GamepadBind("Menu Toggle", DroneAction.Menu, Settings.GamepadMenuBind));
            input.Rows.Add(GamepadBind("Reset / Teleport", DroneAction.Reset, Settings.GamepadResetBind));
            input.Rows.Add(GamepadBind("Turtle Flip", DroneAction.TurtleFlip, Settings.GamepadTurtleBind));
            input.Rows.Add(GamepadBind("Flight Mode", DroneAction.FlightMode, Settings.GamepadFlightModeBind));
            input.Rows.Add(Rows.Section("RC Radio Axes", RadioActive));
            input.Rows.Add(Rows.Choice("Roll Axis", Settings.RadioRollAxis, AxisName, "Windows joystick axis mapped to Roll.", RadioActive));
            input.Rows.Add(Rows.Choice("Pitch Axis", Settings.RadioPitchAxis, AxisName, "Windows joystick axis mapped to Pitch.", RadioActive));
            input.Rows.Add(Rows.Choice("Throttle Axis", Settings.RadioThrottleAxis, AxisName, "Windows joystick axis mapped to Throttle.", RadioActive));
            input.Rows.Add(Rows.Choice("Yaw Axis", Settings.RadioYawAxis, AxisName, "Windows joystick axis mapped to Yaw.", RadioActive));
            input.Rows.Add(Rows.Section("RC Radio Bindings", RadioActive));
            input.Rows.Add(RadioBind("Arm Switch", DroneAction.Arm, Settings.RadioArmButton));
            input.Rows.Add(RadioBind("Flight Mode", DroneAction.FlightMode, Settings.RadioFlightModeButton));
            input.Rows.Add(RadioBind("Reset Drone", DroneAction.Reset, Settings.RadioResetButton));
            input.Rows.Add(RadioBind("Turtle Flip", DroneAction.TurtleFlip, Settings.RadioTurtleButton));
            input.Rows.Add(RadioBind("Menu Toggle", DroneAction.Menu, Settings.RadioMenuButton));
            input.Rows.Add(Rows.Section("Maintenance"));
            input.Rows.Add(Rows.Action("Reset All Binds to Default", () => Settings.ResetAllBindingsToDefault(), "Restores gamepad and radio bindings to their defaults.", true));
            _tabs.Add(input);

            var video = new ConfigTab { Name = "Video + Audio" };
            video.Rows.Add(Rows.Section("VTX"));
            video.Rows.Add(Rows.Choice("VTX System", Settings.VtxType, VtxModeName, "Analog (5.8G snow/tear) or Digital (HD macroblocking/bitrate drop)."));
            video.Rows.Add(Rows.Choice("VTX Power", () => F0(Settings.VtxPowerMw.Value) + " mW", CycleVtxPower, "Output power steps: 25 / 200 / 800 / 1000 mW. Higher keeps the link clean at range."));
            video.Rows.Add(Rows.Bool("RF Static", Settings.EnableVtxStatic, "Simulate VTX signal degradation based on range and obstacles."));
            video.Rows.Add(Rows.Float("Noise Amount", Settings.VtxNoiseStrength, 0.05f, 0.0f, 2.0f, Pct, "VTX artifact intensity multiplier.", () => Settings.EnableVtxStatic.Value));
            video.Rows.Add(Rows.Section("Camera"));
            video.Rows.Add(Rows.Bool("LOS Fly Mode", Settings.LosFlyMode, "Line-of-Sight flight from your player camera (hides FPV feed and OSD on Desktop & VR).", "LOS (Player View)", "FPV (Goggles)"));
            video.Rows.Add(Rows.Bool("Desktop LOS Auto-Track", Settings.LosAutoTrackCamera, "Smoothly pan/tilt Desktop camera toward the drone during LOS flight.", "AUTO-TRACK", "FIXED LOOK", () => Settings.LosFlyMode.Value));
            video.Rows.Add(Rows.Float("Camera FOV", Settings.FpvCameraFov, 1f, 75f, 155f, Deg, "FPV lens field of view (wider shows more of the arms and props)."));
            video.Rows.Add(Rows.Bool("Frame in FPV View", Settings.ShowDroneMeshInFpv, "Render the 3D frame arms, ducts, and spinning props in FPV view.", "VISIBLE", "HIDDEN"));
            video.Rows.Add(Rows.Choice("VR View Mode", Settings.VrView, VrViewName, "2D/3D Goggles: virtual screen with black mask. Full 3D VR: true full-HMD stereoscopic cockpit on drone."));
            video.Rows.Add(Rows.Bool("Full 3D VR HUD", Settings.CockpitVrShowOsd, "Show translucent head-tracked Betaflight OSD overlay in Full 3D VR view.", "HUD ON", "HUD OFF", () => Settings.VrView.Value == VrViewMode.CockpitVR3D));
            video.Rows.Add(Rows.Float("VR Goggle FOV", Settings.VrGoggleScreenFov, 2f, 35f, 110f, Deg, "Angular size of the virtual FPV goggle screen inside the VR headset.", () => Settings.VrView.Value != VrViewMode.CockpitVR3D));
            video.Rows.Add(Rows.Float("VR Stereo IPD", Settings.VrStereoIpdMm, 1f, 40f, 85f, v => F0(v) + " mm", "Left/right drone camera separation in VR 3D Goggles mode.", () => Settings.VrView.Value == VrViewMode.Stereo3D));
            video.Rows.Add(Rows.Choice("FPV Resolution", Settings.FpvCameraResolution, ResolutionName, "FPV camera render resolution (applies to goggles, desktop view, and Raw FPV Spout output)."));
            video.Rows.Add(Rows.Bool("Fisheye Lens", Settings.EnableLensDistortion, "Ultra-wide barrel distortion."));
            video.Rows.Add(Rows.Float("Distortion Factor", Settings.LensDistortionK, 0.01f, 0.0f, 0.50f, F2, "Barrel distortion strength (k).", () => Settings.EnableLensDistortion.Value));
            video.Rows.Add(Rows.Section("Audio"));
            video.Rows.Add(Rows.Float("Motor Volume", Settings.MotorVolume, 0.05f, 0.0f, 1.0f, Pct, "3D motor audio volume."));
            video.Rows.Add(Rows.Bool("Prop Blade Slap", Settings.EnableBladeSlapAudio, "Aerodynamic blade slap and tearing sounds."));
            video.Rows.Add(Rows.Bool("Desktop FPV Audio", Settings.EnableOnboardAudio, "Where the desktop audio listener sits while flying.", "ONBOARD (Camera)", "PILOT (Head)"));
            video.Rows.Add(Rows.Section("Spout2 Output"));
            video.Rows.Add(Rows.Toggle("Video Stream", () => DroneController.Instance?.Spout?.IsSpoutActive ?? false, () => DroneController.Instance?.ToggleSpout(), "Broadcast the FPV feed to OBS via Spout2.", "BROADCASTING", "OFF"));
            video.Rows.Add(Rows.Bool("Stream Type", Settings.SpoutIncludeOSD, "Raw FPV feed with OSD telemetry, or Clean camera feed (supports gimbal).", "RAW FPV (OSD)", "CLEAN CAM"));
            video.Rows.Add(Rows.Bool("Show Drone in Spout", Settings.SpoutShowDroneMesh, "Show the 3D drone frame, arms, and props in Clean Cam Spout2 output (independent of FPV goggles).", "SHOWN", "HIDDEN", () => !Settings.SpoutIncludeOSD.Value));
            bool IsGimbalOn() => !Settings.SpoutIncludeOSD.Value && Settings.SpoutGimbal.Value != SpoutGimbalMode.Off;
            bool IsGimbalCustom() => IsGimbalOn() && Settings.SpoutGimbal.Value == SpoutGimbalMode.Custom;
            bool UsesSmoothing() => IsGimbalOn() && (Settings.SpoutGimbal.Value == SpoutGimbalMode.ThreeAxisSmooth ||
                (Settings.SpoutGimbal.Value == SpoutGimbalMode.Custom &&
                (Settings.SpoutGimbalRollMode.Value == GimbalAxisMode.Smooth || Settings.SpoutGimbalPitchMode.Value == GimbalAxisMode.Smooth || Settings.SpoutGimbalYawMode.Value == GimbalYawMode.Smooth)));
            bool UsesPitchLock() => IsGimbalOn() && (Settings.SpoutGimbal.Value == SpoutGimbalMode.TwoAxisHorizon ||
                (Settings.SpoutGimbal.Value == SpoutGimbalMode.Custom && Settings.SpoutGimbalPitchMode.Value == GimbalAxisMode.Lock));

            video.Rows.Add(Rows.Choice("Gimbal Mode", Settings.SpoutGimbal, GimbalModeName, "Stabilize clean Spout output: Roll Lock, Horizon Lock, 3-Axis Smooth, or Custom per-axis.", () => !Settings.SpoutIncludeOSD.Value));
            video.Rows.Add(Rows.Choice("Gimbal Roll", Settings.SpoutGimbalRollMode, GimbalAxisName, "Roll axis stabilization: Raw, Smooth, or Horizon Lock.", IsGimbalCustom));
            video.Rows.Add(Rows.Choice("Gimbal Pitch", Settings.SpoutGimbalPitchMode, GimbalAxisName, "Pitch axis stabilization: Raw, Smooth, or Horizon Lock.", IsGimbalCustom));
            video.Rows.Add(Rows.Choice("Gimbal Yaw", Settings.SpoutGimbalYawMode, GimbalYawName, "Yaw axis stabilization: Raw or Smooth.", IsGimbalCustom));
            video.Rows.Add(Rows.Float("Gimbal Smoothing", Settings.SpoutGimbalSmoothSpeed, 0.5f, 1.0f, 20.0f, F1, "Stabilization follow speed (lower = smoother cinema, higher = snappier).", UsesSmoothing));
            video.Rows.Add(Rows.Float("Horizon Pitch Offset", Settings.SpoutGimbalPitchOffset, 1f, -30f, 45f, Deg, "Camera pitch angle when Pitch is Horizon Locked.", UsesPitchLock));
            video.Rows.Add(Rows.Float("Gimbal FOV", Settings.SpoutGimbalFov, 1f, 75f, 155f, Deg, "Field of view for the stabilized Spout gimbal camera.", IsGimbalOn));
            video.Rows.Add(Rows.Choice("Gimbal Resolution", Settings.SpoutGimbalResolution, ResolutionName, "Render resolution for the stabilized Spout gimbal camera.", IsGimbalOn));
            video.Rows.Add(Rows.Info("Sender name", () => Settings.SpoutSenderName.Value));
            _tabs.Add(video);

            var system = new ConfigTab { Name = "System" };
            system.Rows.Add(Rows.Section("Overlays"));
            system.Rows.Add(Rows.Bool("OSD Header & VTX", Settings.OsdShowHeader, "Show top-left VTX, build name, RSSI, and RC link info."));
            system.Rows.Add(Rows.Bool("OSD Timers & Batt", Settings.OsdShowTimers, "Show flight timer, voltage, current, speed, and altitude."));
            system.Rows.Add(Rows.Bool("OSD Horizon", Settings.OsdShowHorizon, "Show artificial horizon pitch/roll line."));
            system.Rows.Add(Rows.Bool("OSD Crosshair", Settings.OsdShowCrosshair, "Show center aiming crosshair."));
            system.Rows.Add(Rows.Bool("Virtual Sticks", Settings.ShowStickOverlay, "Show the stick gimbal overlay in the FPV view."));
            system.Rows.Add(Rows.Bool("Debug Telemetry", Settings.ShowDebugOsd, "Developer aerodynamics banners on the OSD."));
            system.Rows.Add(Rows.Section("Commands"));
            system.Rows.Add(Rows.Action("Reset / Teleport Drone to Pilot", () => OnResetDroneRequested?.Invoke(), "Respawns the drone at your position."));
            system.Rows.Add(Rows.Action("Execute Turtle Mode (Flip Upright)", () => OnTurtleFlipRequested?.Invoke(), "Flips an upside-down drone upright."));
            system.Rows.Add(Rows.Action("Close Configurator", () => SetOpen(false), "Close this window."));
            _tabs.Add(system);
        }

        // ------------------------------------------------------------------ state helpers

        private void BuildVisible()
        {
            _vis.Clear();
            List<ConfigRow> rows = _tabs[_tab].Rows;
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].IsShown())
                    _vis.Add(rows[i]);
            }

            if (_sel == null || !_vis.Contains(_sel))
            {
                _sel = null;
                for (int i = 0; i < _vis.Count; i++)
                {
                    if (_vis[i].IsSelectable) { _sel = _vis[i]; break; }
                }
            }
        }

        private void SwitchTab(int index)
        {
            _tab = (index % _tabs.Count + _tabs.Count) % _tabs.Count;
            _scrollY = 0f;
            _sel = null;
            _dragRow = null;
            _input?.CancelListeningForBind();
            BuildVisible();
        }

        private void MoveSelection(int delta)
        {
            int count = 0;
            int cur = -1;
            for (int i = 0; i < _vis.Count; i++)
            {
                if (!_vis[i].IsSelectable) continue;
                if (_vis[i] == _sel) cur = count;
                count++;
            }
            if (count == 0) return;

            int next = cur < 0 ? 0 : ((cur + delta) % count + count) % count;
            int n = 0;
            for (int i = 0; i < _vis.Count; i++)
            {
                if (!_vis[i].IsSelectable) continue;
                if (n == next) { _sel = _vis[i]; break; }
                n++;
            }
            _ensureVisible = true;
        }

        private void StepRow(ConfigRow row, int dir, int mult)
        {
            if (row == null) return;
            switch (row.Kind)
            {
                case RowKind.Float:
                    float v = row.GetFloat() + dir * row.Step * mult;
                    SetFloatValue(row, v);
                    break;
                case RowKind.Bool:
                    row.ToggleBool?.Invoke();
                    Touch();
                    break;
                case RowKind.Choice:
                    row.Cycle?.Invoke(dir);
                    Touch();
                    break;
            }
        }

        private void SetFloatValue(ConfigRow row, float v)
        {
            v = Mathf.Clamp(Mathf.Round(v * 10000f) / 10000f, row.Min, row.Max);
            if (Mathf.Abs(v - row.GetFloat()) < 0.00001f) return;
            row.SetFloat(v);
            Touch();
        }

        private void ActivateRow(ConfigRow row)
        {
            if (row == null) return;
            switch (row.Kind)
            {
                case RowKind.Bool:
                case RowKind.Choice:
                    StepRow(row, 1, 1);
                    break;
                case RowKind.Bind:
                    _input?.StartListeningForBind(row.BindAction, row.BindRadio);
                    break;
                case RowKind.Action:
                    row.Invoke?.Invoke();
                    break;
            }
        }

        private void ResetRow(ConfigRow row)
        {
            if (row == null || row.Reset == null) return;
            row.Reset();
            Touch();
        }

        private void ResetCurrentTab()
        {
            foreach (ConfigRow row in _tabs[_tab].Rows)
                if (row.Reset != null) row.Reset();
            Touch();
        }

        private bool IsListeningFor(ConfigRow row)
        {
            return _input != null && _input.IsListeningForBind
                && _input.ListeningAction == row.BindAction && _input.ListeningForRadio == row.BindRadio;
        }

        // ------------------------------------------------------------------ navigation

        private void HandleNavigation()
        {
            // OnGUI runs several times per frame; process input once per frame.
            if (_lastNavFrame == Time.frameCount) return;
            _lastNavFrame = Time.frameCount;

            if (_input != null && _input.IsListeningForBind)
            {
                if (Input.GetKeyDown(KeyCode.Escape)) _input.CancelListeningForBind();
                _heldX = _heldY = 0;
                return;
            }

            if (GUI.GetNameOfFocusedControl() == "FPVBuildName")
            {
                if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                {
                    GUI.FocusControl(null);
                }
                _heldX = _heldY = 0;
                return;
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                SetOpen(false);
                return;
            }

            Gamepad gp = Gamepad.current;
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

            int tabDelta = 0;
            if (Input.GetKeyDown(KeyCode.Tab)) tabDelta = shift ? -1 : 1;
            if (Input.GetKeyDown(KeyCode.E)) tabDelta = 1;
            if (Input.GetKeyDown(KeyCode.Q)) tabDelta = -1;
            if (gp != null)
            {
                if (gp.rightShoulder.wasPressedThisFrame) tabDelta = 1;
                if (gp.leftShoulder.wasPressedThisFrame) tabDelta = -1;
            }
            if (tabDelta != 0)
            {
                SwitchTab(_tab + tabDelta);
                return;
            }

            int dx = 0, dy = 0;
            if (Input.GetKey(KeyCode.UpArrow)) dy = 1;
            else if (Input.GetKey(KeyCode.DownArrow)) dy = -1;
            if (Input.GetKey(KeyCode.LeftArrow)) dx = -1;
            else if (Input.GetKey(KeyCode.RightArrow)) dx = 1;
            if (gp != null)
            {
                Vector2 d = gp.dpad.ReadValue();
                if (d.y > 0.5f) dy = 1; else if (d.y < -0.5f) dy = -1;
                if (d.x > 0.5f) dx = 1; else if (d.x < -0.5f) dx = -1;
            }

            float now = Time.unscaledTime;

            if (dy != _heldY)
            {
                _heldY = dy;
                if (dy != 0) { MoveSelection(-dy); _nextRepY = now + 0.38f; }
            }
            else if (dy != 0 && now >= _nextRepY)
            {
                MoveSelection(-dy);
                _nextRepY = now + 0.09f;
            }

            if (dx != _heldX)
            {
                _heldX = dx;
                if (dx != 0)
                {
                    _heldXSince = now;
                    StepRow(_sel, dx, 1);
                    _nextRepX = now + 0.38f;
                }
            }
            else if (dx != 0 && now >= _nextRepX)
            {
                int mult = (now - _heldXSince > 1.2f) ? 5 : 1;
                StepRow(_sel, dx, mult);
                _nextRepX = now + 0.06f;
            }

            bool activate = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)
                || (gp != null && gp.buttonSouth.wasPressedThisFrame);
            if (activate) ActivateRow(_sel);

            bool reset = Input.GetKeyDown(KeyCode.Backspace) || Input.GetKeyDown(KeyCode.Delete)
                || (gp != null && gp.buttonWest.wasPressedThisFrame);
            if (reset) ResetRow(_sel);
        }

        // ------------------------------------------------------------------ drawing primitives

        private void EnsureStyles()
        {
            if (_tx != null) return;

            _uiFont = Font.CreateDynamicFontFromOSFont(new[] { "Segoe UI", "Arial" }, 14);
            _monoFont = Font.CreateDynamicFontFromOSFont(new[] { "Consolas", "Courier New" }, 13);

            _tx = new GUIStyle(GUI.skin.label)
            {
                wordWrap = false,
                clipping = TextClipping.Clip,
                richText = false,
                padding = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0)
            };

            _textFieldStyle = new GUIStyle(GUI.skin.textField)
            {
                font = _monoFont,
                fontSize = 12,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(8, 8, 4, 4)
            };
            _textFieldStyle.normal.textColor = ConfigTheme.White;
            _textFieldStyle.focused.textColor = ConfigTheme.White;
            _textFieldStyle.normal.background = Texture2D.whiteTexture;
            _textFieldStyle.focused.background = Texture2D.whiteTexture;

            CreateCircleTextures();
        }

        private void CreateCircleTextures()
        {
            const int sz = 128;
            _circleTex = new Texture2D(sz, sz, TextureFormat.ARGB32, false);
            _ringTex = new Texture2D(sz, sz, TextureFormat.ARGB32, false);
            float c = (sz - 1) * 0.5f;
            float rOuter = sz * 0.5f - 1.5f;

            for (int y = 0; y < sz; y++)
            {
                for (int x = 0; x < sz; x++)
                {
                    float dx = x - c;
                    float dy = y - c;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float discAlpha = Mathf.Clamp01(rOuter - d + 0.5f);
                    _circleTex.SetPixel(x, y, new Color(1f, 1f, 1f, discAlpha));

                    float ringDist = Mathf.Abs(d - (rOuter - 2.5f));
                    float ringAlpha = Mathf.Clamp01(2.2f - ringDist);
                    _ringTex.SetPixel(x, y, new Color(1f, 1f, 1f, ringAlpha));
                }
            }
            _circleTex.Apply();
            _ringTex.Apply();
        }

        private static void DrawTex(Rect r, Texture2D tex, Color c)
        {
            if (tex == null) return;
            Color old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, tex, ScaleMode.StretchToFill, true, 0f);
            GUI.color = old;
        }

        private static void DrawLine(Vector2 a, Vector2 b, float width, Color c)
        {
            Vector2 d = b - a;
            float len = d.magnitude;
            if (len < 0.5f) return;
            float ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            Matrix4x4 prev = GUI.matrix;
            GUI.matrix = prev * Matrix4x4.TRS(new Vector3(a.x, a.y, 0f), Quaternion.Euler(0f, 0f, ang), Vector3.one);
            Fill(new Rect(0f, -width * 0.5f, len, width), c);
            GUI.matrix = prev;
        }

        private static void Fill(Rect r, Color c)
        {
            Color old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f);
            GUI.color = old;
        }

        private void Text(Rect r, string s, int size, Color c, TextAnchor anchor, bool bold = false, bool mono = false)
        {
            _tx.font = mono ? _monoFont : _uiFont;
            _tx.fontSize = size;
            _tx.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            _tx.alignment = anchor;
            _tx.normal.textColor = c;
            GUI.Label(r, s, _tx);
        }

        private float Measure(string s, int size, bool bold = false, bool mono = false)
        {
            _tx.font = mono ? _monoFont : _uiFont;
            _tx.fontSize = size;
            _tx.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            return _tx.CalcSize(new GUIContent(s)).x;
        }

        private bool Clicked(Rect r, bool gate = true, int button = 0)
        {
            Event e = Event.current;
            if (gate && e.type == EventType.MouseDown && e.button == button && r.Contains(e.mousePosition))
            {
                e.Use();
                return true;
            }
            return false;
        }

        private bool Hover(Rect r, bool gate = true)
        {
            return gate && r.Contains(Event.current.mousePosition);
        }

        /// <summary>Plain text arrow, no box.</summary>
        private bool Arrow(Rect r, string label, bool gate)
        {
            bool hover = Hover(r, gate);
            Text(r, label, 14, hover ? ConfigTheme.White : ConfigTheme.Dim, TextAnchor.MiddleCenter, false, true);
            return Clicked(r, gate);
        }

        // ------------------------------------------------------------------ main draw

        public void DrawMenu()
        {
            if (!IsOpen) return;
            if (_tabs.Count == 0) return;

            EnsureStyles();

            if (Cursor.lockState != CursorLockMode.None) Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (_dirty && Time.unscaledTime - _lastEdit > SaveDelay) FlushSave();

            float s = Mathf.Clamp(Screen.height / 1080f, 0.6f, 2f);
            s = Mathf.Min(s, Screen.width / (PanelW + 24f), Screen.height / (PanelH + 24f));

            Matrix4x4 prev = GUI.matrix;
            GUI.matrix = prev * Matrix4x4.Scale(new Vector3(s, s, 1f));
            try
            {
                DrawPanel(Screen.width / s, Screen.height / s);
            }
            catch (Exception ex)
            {
                if (ex.Message != _lastError)
                {
                    _lastError = ex.Message;
                    MelonLogger.Error("[Config] draw failed: " + ex);
                }
            }
            finally
            {
                GUI.matrix = prev;
            }
        }

        private readonly Event _vrSynthEvent = new Event();

        public void DrawMenuToRenderTexture(
            RenderTexture targetRt,
            Vector2 pointerPos,
            bool isHovering,
            bool pointerDownThisFrame,
            bool pointerUpThisFrame,
            bool pointerHeld,
            float scrollDelta)
        {
            if (!IsOpen || _tabs.Count == 0 || targetRt == null)
                return;

            EnsureStyles();

            if (_dirty && Time.unscaledTime - _lastEdit > SaveDelay)
                FlushSave();

            RenderTexture prevActive = RenderTexture.active;
            Matrix4x4 prevMatrix = GUI.matrix;
            bool prevSrgb = GL.sRGBWrite;
            Event prevEvent = Event.current;

            try
            {
                RenderTexture.active = targetRt;
                GL.Clear(true, true, ConfigTheme.Panel);
                GL.sRGBWrite = QualitySettings.activeColorSpace == ColorSpace.Linear;
                GUI.matrix = Matrix4x4.identity;

                Vector2 activePointer = isHovering ? pointerPos : new Vector2(-1000f, -1000f);

                if (isHovering && pointerDownThisFrame)
                    DispatchTabletPass(EventType.MouseDown, 0, activePointer, Vector2.zero);

                if (pointerHeld && !pointerDownThisFrame)
                    DispatchTabletPass(EventType.MouseDrag, 0, activePointer, Vector2.zero);

                if (pointerUpThisFrame)
                    DispatchTabletPass(EventType.MouseUp, 0, activePointer, Vector2.zero);

                if (isHovering && Mathf.Abs(scrollDelta) > 0.01f)
                    DispatchTabletPass(EventType.ScrollWheel, 0, activePointer, new Vector2(0f, scrollDelta));

                DispatchTabletPass(EventType.Repaint, 0, activePointer, Vector2.zero);

                if (isHovering && IsOpen)
                {
                    DrawTex(new Rect(pointerPos.x - 6f, pointerPos.y - 6f, 12f, 12f), _circleTex,
                        pointerHeld ? ConfigTheme.White : ConfigTheme.Accent);
                }
            }
            catch (Exception ex)
            {
                if (ex.Message != _lastError)
                {
                    _lastError = ex.Message;
                    MelonLogger.Error("[Config] VR tablet draw failed: " + ex);
                }
            }
            finally
            {
                Event.current = prevEvent;
                GUI.matrix = prevMatrix;
                GL.sRGBWrite = prevSrgb;
                RenderTexture.active = prevActive;
            }
        }

        private void DispatchTabletPass(EventType type, int button, Vector2 mousePos, Vector2 delta)
        {
            if (!IsOpen) return;
            _vrSynthEvent.type = type;
            _vrSynthEvent.button = button;
            _vrSynthEvent.mousePosition = mousePos;
            _vrSynthEvent.delta = delta;
            Event.current = _vrSynthEvent;
            DrawPanel(PanelW, PanelH, type == EventType.Repaint);
        }

        private void DrawPanel(float vw, float vh, bool runNav = true)
        {
            Event ev = Event.current;
            float left = Mathf.Round((vw - PanelW) * 0.5f);
            float top = Mathf.Round((vh - PanelH) * 0.5f);

            Rect panel = new Rect(left, top, PanelW, PanelH);
            Rect header = new Rect(left, top, PanelW, HeaderH);
            float bodyH = PanelH - HeaderH - DetailH - FooterH;
            Rect sidebar = new Rect(left, top + HeaderH, SidebarW, bodyH);
            Rect fullContent = new Rect(left + SidebarW, top + HeaderH, PanelW - SidebarW, bodyH);
            bool isRatesTab = _tabs[_tab].Name == "Rates";
            bool isDroneTab = _tabs[_tab].Name == "Drone";
            bool hasRightPane = isRatesTab || isDroneTab;
            Rect content = hasRightPane
                ? new Rect(fullContent.x, fullContent.y, fullContent.width - RatesGraphPaneW, fullContent.height)
                : fullContent;
            Rect detail = new Rect(left + SidebarW, fullContent.yMax, PanelW - SidebarW, DetailH);
            Rect footer = new Rect(left, detail.yMax, PanelW, FooterH);

            _mouseInContent = content.Contains(ev.mousePosition);

            if (runNav) HandleNavigation();
            if (!IsOpen) return;

            // Backgrounds: flat tonal blocks, no outlines
            Fill(panel, ConfigTheme.Panel);
            Fill(header, ConfigTheme.Chrome);
            Fill(sidebar, ConfigTheme.Chrome);
            Fill(footer, ConfigTheme.Chrome);
            Fill(new Rect(detail.x, detail.y, detail.width, 1f), ConfigTheme.Line);

            DrawHeader(header);
            if (!IsOpen) return;

            DrawSidebar(sidebar);

            BuildVisible();
            DrawContent(content);
            if (hasRightPane)
            {
                Rect rightPane = new Rect(content.xMax, fullContent.y, RatesGraphPaneW, fullContent.height);
                Fill(new Rect(rightPane.x, rightPane.y, 1f, rightPane.height), ConfigTheme.Line);
                if (isRatesTab)
                    DrawPinnedRatesPane(rightPane);
                else
                    DrawPinnedBuilderPane(rightPane);
            }
            DrawDetail(detail);
            DrawFooter(footer);

            // Swallow clicks and wheel inside the panel so the game does not react.
            if (panel.Contains(ev.mousePosition) &&
                (ev.type == EventType.MouseDown || ev.type == EventType.MouseUp ||
                 ev.type == EventType.MouseDrag || ev.type == EventType.ScrollWheel))
            {
                ev.Use();
            }
        }

        private void DrawHeader(Rect h)
        {
            Text(new Rect(h.x + 20f, h.y, 180f, h.height), "FPV Configurator", 14, ConfigTheme.White, TextAnchor.MiddleLeft, true);

            string status = DronePresets.Current.DisplayName + "  /  " + FlightModeName(Settings.Mode.Value);
            Text(new Rect(h.x + 200f, h.y, 420f, h.height), status, 12, ConfigTheme.Dim, TextAnchor.MiddleLeft);

            Rect close = new Rect(h.xMax - 44f, h.y, 44f, h.height);
            Text(close, "x", 16, Hover(close) ? ConfigTheme.White : ConfigTheme.Dim, TextAnchor.MiddleCenter, false, true);
            if (Clicked(close)) SetOpen(false);

            Fill(new Rect(h.x, h.yMax - 1f, h.width, 1f), ConfigTheme.Line);
        }

        private void DrawSidebar(Rect sb)
        {
            for (int i = 0; i < _tabs.Count; i++)
            {
                Rect r = new Rect(sb.x + 6f, sb.y + 10f + i * 34f, sb.width - 12f, 30f);
                bool active = i == _tab;
                if (active) Fill(r, ConfigTheme.Selected);
                else if (Hover(r)) Fill(r, ConfigTheme.Hover);

                Text(new Rect(r.x + 12f, r.y, r.width - 12f, r.height), _tabs[i].Name, 13,
                    active ? ConfigTheme.White : ConfigTheme.Dim, TextAnchor.MiddleLeft);
                if (Clicked(r)) SwitchTab(i);
            }

            Rect reset = new Rect(sb.x + 6f, sb.yMax - 38f, sb.width - 12f, 28f);
            bool rh = Hover(reset);
            Text(new Rect(reset.x + 12f, reset.y, reset.width - 12f, reset.height), "Reset tab to defaults", 11,
                rh ? ConfigTheme.Red : ConfigTheme.Dim, TextAnchor.MiddleLeft);
            if (Clicked(reset)) ResetCurrentTab();

            Fill(new Rect(sb.xMax - 1f, sb.y, 1f, sb.height), ConfigTheme.Line);
        }

        private static float RowHeight(ConfigRow row)
        {
            switch (row.Kind)
            {
                case RowKind.Section: return 38f;
                case RowKind.Graph: return 200f;
                case RowKind.Info: return 24f;
                case RowKind.Action: return 40f;
                default: return RowH;
            }
        }

        private void DrawContent(Rect c)
        {
            Event ev = Event.current;

            _ys.Clear();
            float y = 4f;
            for (int i = 0; i < _vis.Count; i++)
            {
                _ys.Add(y);
                y += RowHeight(_vis[i]);
            }
            float totalH = y + 8f;
            float maxScroll = Mathf.Max(0f, totalH - c.height);

            if (ev.type == EventType.ScrollWheel && _mouseInContent)
            {
                _scrollY += ev.delta.y * 36f;
                ev.Use();
            }

            if (_ensureVisible && _sel != null)
            {
                int idx = _vis.IndexOf(_sel);
                if (idx >= 0)
                {
                    float rt = _ys[idx];
                    float rb = rt + RowHeight(_sel);
                    if (rt < _scrollY + 4f) _scrollY = rt - 40f;
                    else if (rb > _scrollY + c.height - 4f) _scrollY = rb - c.height + 8f;
                }
                _ensureVisible = false;
            }
            _scrollY = Mathf.Clamp(_scrollY, 0f, maxScroll);

            float rowW = c.width - 14f;
            GUI.BeginGroup(c);
            for (int i = 0; i < _vis.Count; i++)
            {
                Rect r = new Rect(0f, _ys[i] - _scrollY, rowW, RowHeight(_vis[i]));
                if (r.yMax < -4f || r.y > c.height + 4f)
                {
                    // Off-screen rows only need to release an active slider drag.
                    if (_dragRow == _vis[i] && ev.rawType == EventType.MouseUp) _dragRow = null;
                    continue;
                }
                DrawRow(_vis[i], r);
            }
            GUI.EndGroup();

            if (maxScroll > 0f)
            {
                float trackH = c.height - 8f;
                float thumbH = Mathf.Max(24f, trackH * c.height / totalH);
                float thumbY = c.y + 4f + (trackH - thumbH) * (_scrollY / maxScroll);
                Fill(new Rect(c.xMax - 6f, thumbY, 3f, thumbH), new Color(1f, 1f, 1f, 0.18f));
            }
        }

        private void DrawRow(ConfigRow row, Rect r)
        {
            bool compact = r.width < 650f;
            float labelW = compact ? 175f : 260f;
            float cx = r.x + (compact ? 190f : 290f);

            switch (row.Kind)
            {
                case RowKind.Section:
                    Text(new Rect(r.x + 20f, r.y + 14f, r.width - 40f, 18f), row.Label.ToUpper(), 11, ConfigTheme.Dim, TextAnchor.MiddleLeft, true);
                    return;
                case RowKind.Graph:
                    DrawGraph(r);
                    return;
                case RowKind.Info:
                    Text(new Rect(r.x + 20f, r.y, labelW, r.height), row.Label, 12, ConfigTheme.Dim, TextAnchor.MiddleLeft);
                    Text(new Rect(cx, r.y, r.xMax - cx - 12f, r.height), row.Text(), 12, ConfigTheme.Text, TextAnchor.MiddleLeft, false, true);
                    return;
            }

            bool g = _mouseInContent;
            bool sel = row == _sel;

            if (sel) Fill(r, ConfigTheme.Selected);
            else if (Hover(r, g)) Fill(r, ConfigTheme.Hover);

            // Right click resets a row to its default.
            if (Clicked(r, g, 1))
            {
                _sel = row;
                ResetRow(row);
            }

            if (row.Kind == RowKind.Action)
            {
                DrawActionRow(row, r, g);
                return;
            }

            Text(new Rect(r.x + 20f, r.y, labelW, r.height), row.Label, 13, sel ? ConfigTheme.White : ConfigTheme.Text, TextAnchor.MiddleLeft);

            bool modified = row.IsModified != null && row.IsModified();
            switch (row.Kind)
            {
                case RowKind.Float: DrawFloatControl(row, r, cx, g, modified); break;
                case RowKind.Bool: DrawBoolControl(row, r, cx, g, modified); break;
                case RowKind.Choice: DrawChoiceControl(row, r, cx, g, modified); break;
                case RowKind.Bind: DrawBindControl(row, r, cx, g); break;
                case RowKind.TextInput: DrawTextInputControl(row, r, cx, g); break;
            }

            // Fallback: clicking anywhere else on the row selects it.
            if (Clicked(r, g)) _sel = row;
        }

        private void DrawFloatControl(ConfigRow row, Rect r, float cx, bool g, bool modified)
        {
            Event ev = Event.current;
            float avail = r.xMax - cx - 12f;
            float trackW = Mathf.Clamp(avail - 135f, 120f, 300f);
            Rect dec = new Rect(cx, r.y + 4f, 22f, 24f);
            Rect track = new Rect(cx + 30f, r.y + r.height * 0.5f - 1f, trackW, 2f);
            Rect inc = new Rect(track.xMax + 8f, r.y + 4f, 22f, 24f);
            Rect val = new Rect(inc.xMax + 8f, r.y, r.xMax - inc.xMax - 12f, r.height);
            Rect hit = new Rect(track.x - 6f, r.y + 4f, track.width + 12f, r.height - 8f);

            float v = row.GetFloat();
            float t = Mathf.Clamp01((v - row.Min) / (row.Max - row.Min));

            Fill(track, new Color(1f, 1f, 1f, 0.16f));
            Fill(new Rect(track.x, track.y, track.width * t, track.height), ConfigTheme.Accent);
            bool active = _dragRow == row || Hover(hit, g);
            Fill(new Rect(track.x + track.width * t - 3f, r.y + r.height * 0.5f - 7f, 6f, 14f),
                active ? ConfigTheme.White : new Color(0.82f, 0.84f, 0.88f, 1f));

            if (Arrow(dec, "<", g)) { _sel = row; StepRow(row, -1, 1); }
            if (Arrow(inc, ">", g)) { _sel = row; StepRow(row, 1, 1); }

            Text(val, row.Format(v), 13, modified ? ConfigTheme.Modified : ConfigTheme.Text, TextAnchor.MiddleRight, false, true);

            // Slider drag
            if (_dragRow == row && ev.rawType == EventType.MouseUp)
            {
                _dragRow = null;
            }
            else if (_dragRow == row && ev.type == EventType.MouseDrag)
            {
                SetSliderFromMouse(row, track);
                ev.Use();
            }
            else if (Clicked(hit, g))
            {
                _sel = row;
                _dragRow = row;
                SetSliderFromMouse(row, track);
            }
        }

        private void SetSliderFromMouse(ConfigRow row, Rect track)
        {
            float t = Mathf.Clamp01((Event.current.mousePosition.x - track.x) / track.width);
            float v = row.Min + t * (row.Max - row.Min);
            v = row.Min + Mathf.Round((v - row.Min) / row.Step) * row.Step;
            SetFloatValue(row, v);
        }

        private void DrawBoolControl(ConfigRow row, Rect r, float cx, bool g, bool modified)
        {
            bool on = row.GetBool();
            Rect sw = new Rect(cx + 30f, r.y + 9f, 30f, 14f);
            Rect hit = new Rect(cx, r.y, 280f, r.height);

            Fill(sw, on ? ConfigTheme.Accent : new Color(1f, 1f, 1f, 0.16f));
            float kx = on ? sw.xMax - 12f : sw.x + 2f;
            Fill(new Rect(kx, sw.y + 2f, 10f, 10f), ConfigTheme.White);
            Text(new Rect(sw.xMax + 14f, r.y, 200f, r.height), on ? row.OnText : row.OffText, 12,
                modified ? ConfigTheme.Modified : (on ? ConfigTheme.Text : ConfigTheme.Dim), TextAnchor.MiddleLeft, false, true);

            if (Clicked(hit, g))
            {
                _sel = row;
                StepRow(row, 1, 1);
            }
        }

        private void DrawChoiceControl(ConfigRow row, Rect r, float cx, bool g, bool modified)
        {
            float boxW = Mathf.Clamp(r.xMax - cx - 72f, 160f, 360f);
            Rect dec = new Rect(cx, r.y + 4f, 22f, 24f);
            Rect box = new Rect(cx + 28f, r.y + 4f, boxW, 24f);
            Rect inc = new Rect(box.xMax + 6f, r.y + 4f, 22f, 24f);

            if (Hover(box, g)) Fill(box, ConfigTheme.Field);
            Text(box, row.Text(), 12, modified ? ConfigTheme.Modified : ConfigTheme.Text, TextAnchor.MiddleCenter, false, true);

            if (Arrow(dec, "<", g)) { _sel = row; StepRow(row, -1, 1); }
            if (Arrow(inc, ">", g)) { _sel = row; StepRow(row, 1, 1); }
            if (Clicked(box, g)) { _sel = row; StepRow(row, 1, 1); }
        }

        private void DrawTextInputControl(ConfigRow row, Rect r, float cx, bool g)
        {
            float boxW = Mathf.Clamp(r.xMax - cx - 44f, 180f, 388f);
            Rect box = new Rect(cx + 28f, r.y + 4f, boxW, 24f);

            Color bg = GUI.GetNameOfFocusedControl() == "FPVBuildName" ? ConfigTheme.FieldHover : ConfigTheme.Field;
            Fill(box, bg);

            string cur = row.GetText != null ? (row.GetText() ?? "") : "";
            Color oldBg = GUI.backgroundColor;
            GUI.backgroundColor = Color.clear;
            GUI.SetNextControlName("FPVBuildName");
            string updated = GUI.TextField(box, cur, 48, _textFieldStyle);
            GUI.backgroundColor = oldBg;

            if (string.IsNullOrEmpty(updated) && GUI.GetNameOfFocusedControl() != "FPVBuildName" && !string.IsNullOrEmpty(row.Placeholder))
            {
                Text(new Rect(box.x + 8f, box.y, box.width - 16f, box.height), row.Placeholder, 11, ConfigTheme.Dim, TextAnchor.MiddleLeft, false, true);
            }

            if (updated != cur)
            {
                _sel = row;
                row.SetText?.Invoke(updated);
            }
        }

        private void DrawBindControl(ConfigRow row, Rect r, float cx, bool g)
        {
            Rect box = new Rect(cx + 28f, r.y + 4f, 214f, 24f);
            bool listening = IsListeningFor(row);

            if (listening)
            {
                float pulse = 0.10f + 0.10f * Mathf.Sin(Time.unscaledTime * 6f);
                Fill(box, new Color(ConfigTheme.Modified.r, ConfigTheme.Modified.g, ConfigTheme.Modified.b, pulse + 0.1f));
                Text(box, row.BindRadio ? "Press a switch or button..." : "Press a button...", 12, ConfigTheme.Modified, TextAnchor.MiddleCenter, false, true);
            }
            else
            {
                Fill(box, Hover(box, g) ? ConfigTheme.FieldHover : ConfigTheme.Field);
                Text(box, row.Text(), 12, ConfigTheme.Text, TextAnchor.MiddleCenter, false, true);
            }

            if (Clicked(box, g))
            {
                _sel = row;
                _input?.StartListeningForBind(row.BindAction, row.BindRadio);
            }
        }

        private void DrawActionRow(ConfigRow row, Rect r, bool g)
        {
            Rect b = new Rect(r.x + 20f, r.y + 5f, Mathf.Min(400f, r.width - 40f), r.height - 10f);
            bool hover = Hover(b, g);

            Fill(b, hover ? ConfigTheme.FieldHover : ConfigTheme.Field);
            Text(b, row.Label, 12, row.Danger ? ConfigTheme.Red : ConfigTheme.White, TextAnchor.MiddleCenter);

            if (Clicked(b, g))
            {
                _sel = row;
                row.Invoke?.Invoke();
            }
            else if (Clicked(r, g))
            {
                _sel = row;
            }
        }

        // ------------------------------------------------------------------ rate curve preview

        private static float PlotY(Rect p, float yMax, float rate)
        {
            return p.yMax - Mathf.Clamp01(rate / yMax) * p.height;
        }

        private void DrawCurve(Rect p, float yMax, Func<float, float> evalY, Color col)
        {
            const int N = 160;
            float dx = p.width / N;
            for (int k = 0; k < N; k++)
            {
                float y0 = PlotY(p, yMax, evalY(k / (float)N));
                float y1 = PlotY(p, yMax, evalY((k + 1) / (float)N));
                float top = Mathf.Min(y0, y1) - 1f;
                Fill(new Rect(p.x + k * dx, top, dx + 1f, Mathf.Abs(y1 - y0) + 2f), col);
            }
        }

        private void DrawPinnedBuilderPane(Rect pane)
        {
            DroneHardwareSpec hw = DroneBuilder.EvaluateCurrent();

            Text(new Rect(pane.x + 16f, pane.y + 10f, pane.width - 32f, 18f), "AIRFRAME BLUEPRINT (TOP VIEW)", 11, ConfigTheme.Dim, TextAnchor.MiddleLeft, true);

            float boxSize = 260f;
            Rect vp = new Rect(pane.x + (pane.width - boxSize) * 0.5f, pane.y + 32f, boxSize, boxSize);
            Fill(vp, ConfigTheme.Inset);

            // Subtle crosshairs
            Fill(new Rect(vp.x, vp.center.y, vp.width, 1f), new Color(1f, 1f, 1f, 0.05f));
            Fill(new Rect(vp.center.x, vp.y, 1f, vp.height), new Color(1f, 1f, 1f, 0.05f));

            // Scale drone to fit viewport cleanly while showing relative size differences
            float totalSpanMm = hw.HalfSpanM * 2000f;
            float pxPerMm = Mathf.Min((boxSize - 36f) / Mathf.Max(90f, totalSpanMm), 1.45f);
            float armXY = hw.ArmHalfXY * 1000f * pxPerMm;
            float propRad = (hw.PropDiameterM * 0.5f) * 1000f * pxPerMm;
            float motorRad = Mathf.Clamp((hw.StatorDiameterMm * 0.5f) * pxPerMm * 1.1f, 4f, 16f);
            float ductRad = propRad + (hw.IsDucted ? 4.5f : 0f);

            Vector2 center = vp.center;
            Vector2[] motors =
            {
                new Vector2(center.x + armXY, center.y + armXY), // Rear-Right
                new Vector2(center.x + armXY, center.y - armXY), // Front-Right
                new Vector2(center.x - armXY, center.y + armXY), // Rear-Left
                new Vector2(center.x - armXY, center.y - armXY)  // Front-Left
            };

            // X-frame arms
            float armWidth = Mathf.Clamp(hw.WheelbaseMm * 0.025f, 3f, 8f);
            Color armCol = new Color(0.28f, 0.30f, 0.35f, 1f);
            for (int i = 0; i < 4; i++)
            {
                DrawLine(center, motors[i], armWidth, armCol);
            }

            // Center FC/ESC stack + LiPo battery silhouette
            float stackW = Mathf.Clamp(armXY * 0.58f, 18f, 44f);
            float stackH = Mathf.Clamp(armXY * 0.95f, 24f, 68f);
            Fill(new Rect(center.x - stackW * 0.5f, center.y - stackH * 0.5f, stackW, stackH), new Color(0.18f, 0.20f, 0.24f, 1f));
            Fill(new Rect(center.x - stackW * 0.36f, center.y - stackH * 0.38f, stackW * 0.72f, stackH * 0.76f), new Color(0.24f, 0.45f, 0.75f, 0.85f));

            // Forward arrow indicator
            Text(new Rect(vp.center.x - 30f, vp.y + 4f, 60f, 14f), "^ FWD", 9, ConfigTheme.Dim, TextAnchor.MiddleCenter, false, true);

            // Prop discs, blades, ducts, and motor bells
            float spinPhase = Time.unscaledTime * 2.2f;
            for (int i = 0; i < 4; i++)
            {
                Vector2 m = motors[i];
                // Prop wash disc
                DrawTex(new Rect(m.x - propRad, m.y - propRad, propRad * 2f, propRad * 2f), _circleTex, new Color(ConfigTheme.Accent.r, ConfigTheme.Accent.g, ConfigTheme.Accent.b, 0.14f));

                // Prop blades
                float dir = (i == 0 || i == 3) ? 1f : -1f;
                for (int b = 0; b < hw.PropBlades; b++)
                {
                    float ang = spinPhase * dir + (b * Mathf.PI * 2f / hw.PropBlades);
                    Vector2 tip = m + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * (propRad * 0.95f);
                    DrawLine(m, tip, 2f, new Color(0.75f, 0.85f, 0.95f, 0.65f));
                }

                // Outer prop ring or duct guard
                if (hw.IsDucted)
                    DrawTex(new Rect(m.x - ductRad, m.y - ductRad, ductRad * 2f, ductRad * 2f), _ringTex, new Color(0.95f, 0.72f, 0.25f, 0.85f));
                else
                    DrawTex(new Rect(m.x - propRad, m.y - propRad, propRad * 2f, propRad * 2f), _ringTex, new Color(ConfigTheme.Accent.r, ConfigTheme.Accent.g, ConfigTheme.Accent.b, 0.45f));

                // Motor stator bell
                DrawTex(new Rect(m.x - motorRad, m.y - motorRad, motorRad * 2f, motorRad * 2f), _circleTex, new Color(0.82f, 0.84f, 0.88f, 0.95f));
            }

            // Dimensions footer under viewport
            float maxPropFit = DroneBuilder.MaxPropInchesForFrame(hw.WheelbaseMm, hw.IsDucted);
            Text(new Rect(vp.x, vp.yMax + 4f, vp.width, 16f), $"Span {totalSpanMm:F0}mm  |  WB {hw.WheelbaseMm:F0}mm  |  Max {maxPropFit:F1}\"", 10, ConfigTheme.Dim, TextAnchor.MiddleCenter, false, true);

            // Live Calculated Specs below blueprint
            float sy = vp.yMax + 26f;
            Fill(new Rect(pane.x + 16f, sy, pane.width - 32f, 1f), ConfigTheme.Line);
            sy += 8f;
            Text(new Rect(pane.x + 16f, sy, pane.width - 32f, 18f), "CALCULATED PERFORMANCE", 11, ConfigTheme.Dim, TextAnchor.MiddleLeft, true);
            sy += 22f;

            void SpecRow(string k, string v, Color vc)
            {
                Text(new Rect(pane.x + 16f, sy, 130f, 20f), k, 12, ConfigTheme.Dim, TextAnchor.MiddleLeft);
                Text(new Rect(pane.x + 148f, sy, pane.width - 164f, 20f), v, 12, vc, TextAnchor.MiddleRight, false, true);
                sy += 22f;
            }

            string massStr = hw.MassKg < 0.2f ? $"{hw.MassKg * 1000f:F0} g" : $"{hw.MassKg:F2} kg";
            SpecRow("All-Up Weight", massStr, ConfigTheme.Text);
            SpecRow("Max Thrust / TWR", $"{hw.MaxThrustNewtons:F1} N  ({hw.ThrustToWeightRatio:F1}:1)", hw.ThrustToWeightRatio >= 2.0f ? ConfigTheme.Text : ConfigTheme.Red);
            SpecRow("Hover Throttle", $"{hw.HoverThrottle01 * 100f:F0}%", ConfigTheme.Text);
            SpecRow("Loaded Max RPM", $"{hw.MaxLoadedRpm:F0} RPM", ConfigTheme.Text);
            SpecRow("Est. Max Speed", $"{hw.EstMaxSpeedMps * 3.6f:F0} km/h", ConfigTheme.Text);
            SpecRow("Prop Pitch Speed", $"{hw.PitchSpeedMaxMps * 3.6f:F0} km/h", ConfigTheme.Text);
            SpecRow("Audio Blade-Pass", $"{hw.MaxBladePassF0:F0} Hz peak", ConfigTheme.Text);

            Color ampCol = hw.IsEscLimited ? ConfigTheme.Red : (hw.IsUnderTorqued ? ConfigTheme.Modified : new Color(0.35f, 0.85f, 0.55f, 1f));
            string statusTag = hw.IsEscLimited ? " [ESC CAP]" : (hw.IsUnderTorqued ? " [LOW TORQUE]" : " [OK]");
            SpecRow("Peak Current", $"{hw.PeakCurrentAmps:F0} A ({hw.PeakCurrentAmps * 0.25f:F0}A/m){statusTag}", ampCol);
        }

        private void DrawPinnedRatesPane(Rect pane)
        {
            Text(new Rect(pane.x + 16f, pane.y + 10f, pane.width - 32f, 18f), "RATE CURVES (DEG/S)", 11, ConfigTheme.Dim, TextAnchor.MiddleLeft, true);

            float topGraphH = Mathf.Round((pane.height - 46f) * 0.56f);
            Rect rateRect = new Rect(pane.x + 6f, pane.y + 30f, pane.width - 16f, topGraphH);
            DrawGraph(rateRect);

            float sepY = rateRect.yMax + 8f;
            Fill(new Rect(pane.x + 16f, sepY, pane.width - 32f, 1f), ConfigTheme.Line);
            Text(new Rect(pane.x + 16f, sepY + 8f, pane.width - 32f, 18f), "THROTTLE CURVE (%)", 11, ConfigTheme.Dim, TextAnchor.MiddleLeft, true);

            Rect thrRect = new Rect(pane.x + 6f, sepY + 28f, pane.width - 16f, pane.yMax - (sepY + 36f));
            DrawThrottleGraph(thrRect);
        }

        private void DrawThrottleGraph(Rect r)
        {
            Rect p = new Rect(r.x + 48f, r.y + 8f, r.width - 48f - 18f, r.height - 8f - 42f);
            if (p.height < 40f || p.width < 40f) return;
            Fill(p, ConfigTheme.Inset);

            for (int i = 0; i <= 4; i++)
            {
                float gy = p.y + p.height * i / 4f;
                Fill(new Rect(p.x, gy, p.width, 1f), new Color(1f, 1f, 1f, i == 4 ? 0.14f : 0.05f));
                Text(new Rect(r.x + 4f, gy - 8f, 40f, 16f), F0((1f - i / 4f) * 100f) + "%", 10, ConfigTheme.Dim, TextAnchor.MiddleRight, false, true);
            }
            for (int i = 1; i < 4; i++)
                Fill(new Rect(p.x + p.width * i / 4f, p.y, 1f, p.height), new Color(1f, 1f, 1f, 0.05f));

            // Hover throttle reference line
            float hover01 = DroneBuilder.EvaluateCurrent().HoverThrottle01;
            float hoverY = PlotY(p, 1.0f, hover01);
            Fill(new Rect(p.x, hoverY, p.width, 1f), new Color(0.95f, 0.72f, 0.25f, 0.45f));

            Color thrCol = new Color(0.35f, 0.85f, 0.55f, 1f);
            DrawCurve(p, 1.0f, x => BetaflightRates.CalculateThrottle(x, Settings.ThrottleMid.Value, Settings.ThrottleExpo.Value, Settings.ThrottleLimitPercent.Value, Settings.ThrottleLimitMode.Value), thrCol);

            Text(new Rect(p.x, p.yMax + 3f, 40f, 14f), "0%", 10, ConfigTheme.Dim, TextAnchor.MiddleLeft, false, true);
            Text(new Rect(p.x + p.width * 0.5f - 40f, p.yMax + 3f, 80f, 14f), "stick", 10, ConfigTheme.Dim, TextAnchor.MiddleCenter, false, true);
            Text(new Rect(p.xMax - 40f, p.yMax + 3f, 40f, 14f), "100%", 10, ConfigTheme.Dim, TextAnchor.MiddleRight, false, true);

            float ly = p.yMax + 20f;
            float thrMax = BetaflightRates.CalculateThrottle(1.0f, Settings.ThrottleMid.Value, Settings.ThrottleExpo.Value, Settings.ThrottleLimitPercent.Value, Settings.ThrottleLimitMode.Value) * 100f;
            Fill(new Rect(p.x, ly + 6f, 12f, 2f), thrCol);
            Text(new Rect(p.x + 16f, ly, 110f, 14f), "Max " + F0(thrMax) + "%", 11, ConfigTheme.Text, TextAnchor.MiddleLeft);
            Fill(new Rect(p.x + 130f, ly + 6f, 12f, 2f), new Color(0.95f, 0.72f, 0.25f, 0.85f));
            Text(new Rect(p.x + 146f, ly, 120f, 14f), "Hover " + F0(hover01 * 100f) + "%", 11, ConfigTheme.Text, TextAnchor.MiddleLeft);
        }

        private void DrawGraph(Rect r)
        {
            float maxR = BetaflightRates.GetAxisMaxRate(0);
            float maxP = BetaflightRates.GetAxisMaxRate(1);
            float maxY = BetaflightRates.GetAxisMaxRate(2);
            float yMax = Mathf.Max(200f, Mathf.Max(maxR, Mathf.Max(maxP, maxY)) * 1.05f);

            Rect p = new Rect(r.x + 62f, r.y + 14f, r.width - 62f - 24f, r.height - 14f - 52f);
            Fill(p, ConfigTheme.Inset);

            for (int i = 0; i <= 4; i++)
            {
                float gy = p.y + p.height * i / 4f;
                Fill(new Rect(p.x, gy, p.width, 1f), new Color(1f, 1f, 1f, i == 4 ? 0.14f : 0.05f));
                Text(new Rect(r.x + 6f, gy - 8f, 50f, 16f), F0(yMax * (1f - i / 4f)), 10, ConfigTheme.Dim, TextAnchor.MiddleRight, false, true);
            }
            for (int i = 1; i < 4; i++)
                Fill(new Rect(p.x + p.width * i / 4f, p.y, 1f, p.height), new Color(1f, 1f, 1f, 0.05f));

            Color pitchCol = new Color(0.95f, 0.72f, 0.25f, 1f);
            Color yawCol = new Color(0.78f, 0.80f, 0.84f, 1f);

            DrawCurve(p, yMax, x => BetaflightRates.EvaluateAxisRate(x, 2), yawCol);
            DrawCurve(p, yMax, x => BetaflightRates.EvaluateAxisRate(x, 1), pitchCol);
            DrawCurve(p, yMax, x => BetaflightRates.EvaluateAxisRate(x, 0), ConfigTheme.Accent);

            Text(new Rect(p.x, p.yMax + 3f, 40f, 14f), "0%", 10, ConfigTheme.Dim, TextAnchor.MiddleLeft, false, true);
            Text(new Rect(p.x + p.width * 0.5f - 60f, p.yMax + 3f, 120f, 14f), "stick deflection", 10, ConfigTheme.Dim, TextAnchor.MiddleCenter, false, true);
            Text(new Rect(p.xMax - 40f, p.yMax + 3f, 40f, 14f), "100%", 10, ConfigTheme.Dim, TextAnchor.MiddleRight, false, true);

            float ly = p.yMax + 24f;
            Fill(new Rect(p.x, ly + 6f, 10f, 2f), ConfigTheme.Accent);
            Text(new Rect(p.x + 14f, ly, 82f, 14f), "Roll " + F0(maxR), 11, ConfigTheme.Text, TextAnchor.MiddleLeft);
            Fill(new Rect(p.x + 98f, ly + 6f, 10f, 2f), pitchCol);
            Text(new Rect(p.x + 112f, ly, 84f, 14f), "Pitch " + F0(maxP), 11, ConfigTheme.Text, TextAnchor.MiddleLeft);
            Fill(new Rect(p.x + 198f, ly + 6f, 10f, 2f), yawCol);
            Text(new Rect(p.x + 212f, ly, 82f, 14f), "Yaw " + F0(maxY), 11, ConfigTheme.Text, TextAnchor.MiddleLeft);
        }

        // ------------------------------------------------------------------ detail + footer

        private void DrawDetail(Rect d)
        {
            if (_sel == null) return;

            Text(new Rect(d.x + 20f, d.y + 7f, 300f, 18f), _sel.Label, 13, ConfigTheme.White, TextAnchor.MiddleLeft);
            Text(new Rect(d.x + 20f, d.y + 26f, d.width - 40f, 18f), _sel.Desc, 11, ConfigTheme.Dim, TextAnchor.MiddleLeft);

            string meta = null;
            if (_sel.Kind == RowKind.Float)
                meta = "range " + _sel.Format(_sel.Min) + " to " + _sel.Format(_sel.Max) + "   default " + _sel.Format(_sel.DefaultFloat);
            else if (_sel.Kind == RowKind.Bool && _sel.IsModified != null)
                meta = "default " + (_sel.DefaultBool ? _sel.OnText : _sel.OffText);

            if (_sel.IsModified != null && _sel.IsModified())
                meta = (meta == null ? "" : meta + "   ") + "changed";

            if (meta != null)
                Text(new Rect(d.xMax - 420f, d.y + 7f, 400f, 18f), meta, 11, ConfigTheme.Dim, TextAnchor.MiddleRight, false, true);
        }

        private void DrawFooter(Rect f)
        {
            Text(new Rect(f.x + 20f, f.y, 600f, f.height),
                "Up/Down select    Left/Right adjust    Enter activate    Q/E tabs    Backspace reset    Esc close",
                11, ConfigTheme.Dim, TextAnchor.MiddleLeft);

            Text(new Rect(f.xMax - 140f, f.y, 120f, f.height), _dirty ? "Unsaved" : "Saved", 11,
                _dirty ? ConfigTheme.Modified : ConfigTheme.Dim, TextAnchor.MiddleRight);
        }
    }
}
