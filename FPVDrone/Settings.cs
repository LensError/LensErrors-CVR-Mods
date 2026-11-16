using MelonLoader;
using UnityEngine;

namespace FPVDrone
{
    public enum DronePreset
    {
        Freestyle5Inch = 0,
        TinyWhoop = 1,
        Cinewhoop35Inch = 2,
        Cinematic7Inch = 3
    }

    public enum FlightMode
    {
        Acro = 0,
        Angle = 1,
        PositionHold = 2
    }

    public enum ControllerDevice
    {
        Auto = 0,
        RadioTransmitter = 1,
        Gamepad = 2,
        KeyboardOnly = 3,
        VRControllers = 4
    }

    public enum StickMode
    {
        Mode2 = 0, // Mode 2: Left = Throttle/Yaw, Right = Pitch/Roll (Default)
        Mode1 = 1, // Mode 1: Left = Pitch/Yaw, Right = Throttle/Roll
        Mode3 = 2, // Mode 3: Left = Throttle/Roll, Right = Pitch/Yaw
        Mode4 = 3  // Mode 4: Left = Pitch/Roll, Right = Throttle/Yaw
    }

    public enum RadioAxis
    {
        X = 0,
        Y = 1,
        Z = 2,
        R = 3,
        U = 4,
        V = 5
    }

    public enum RateType
    {
        Actual = 0,
        Betaflight = 1
    }

    public enum ThrottleLimitType
    {
        Off = 0,
        Scale = 1,
        Clip = 2
    }

    public enum VtxSystem
    {
        Analog = 0,
        Digital = 1
    }

    public enum VrViewMode
    {
        Goggles2D = 0,
        Stereo3D = 1,
        CockpitVR3D = 2
    }

    public enum DroneAction
    {
        Arm = 0,
        Menu = 1,
        Reset = 2,
        TurtleFlip = 3,
        FlightMode = 4
    }

    public enum SpoutGimbalMode
    {
        Off = 0,
        OneAxisRoll = 1,
        TwoAxisHorizon = 2,
        ThreeAxisSmooth = 3,
        Custom = 4
    }

    public enum GimbalAxisMode
    {
        Raw = 0,
        Smooth = 1,
        Lock = 2
    }

    public enum GimbalYawMode
    {
        Raw = 0,
        Smooth = 1
    }

    public enum VideoResolution
    {
        MatchScreen = 0,
        Res720p = 1,
        Res1080p = 2,
        Res1440p = 3,
        Res4K = 4
    }

    public enum GamepadButtonBinding
    {
        SelectView = 0,
        Start = 1,
        ButtonSouth = 2,     // A (Xbox) / Cross (PS)
        ButtonEast = 3,      // B (Xbox) / Circle (PS)
        ButtonWest = 4,      // X (Xbox) / Square (PS)
        ButtonNorth = 5,     // Y (Xbox) / Triangle (PS)
        LeftShoulder = 6,    // LB / L1
        RightShoulder = 7,   // RB / R1
        LeftStickPress = 8,  // L3 / Left Stick Click
        RightStickPress = 9, // R3 / Right Stick Click
        DpadUp = 10,
        DpadDown = 11,
        DpadLeft = 12,
        DpadRight = 13,
        None = 14
    }

    public static class Settings
    {
        private const string CategoryName = "FPVDrone";

        // Hardware & Controller Selection
        public static MelonPreferences_Entry<bool> ModEnabled;
        public static MelonPreferences_Entry<ControllerDevice> InputDevice;
        public static MelonPreferences_Entry<StickMode> ControlStickMode;
        public static MelonPreferences_Entry<bool> GamepadTriggerThrottle;
        public static MelonPreferences_Entry<bool> EnableOnboardAudio;

        // Button Bindings (Gamepad)
        public static MelonPreferences_Entry<GamepadButtonBinding> GamepadArmBind;
        public static MelonPreferences_Entry<GamepadButtonBinding> GamepadMenuBind;
        public static MelonPreferences_Entry<GamepadButtonBinding> GamepadResetBind;
        public static MelonPreferences_Entry<GamepadButtonBinding> GamepadTurtleBind;
        public static MelonPreferences_Entry<GamepadButtonBinding> GamepadFlightModeBind;

        // Button Bindings (RC Radio: DirectInput Button Indices 0..15, -1 = None)
        public static MelonPreferences_Entry<int> RadioArmButton;
        public static MelonPreferences_Entry<int> RadioMenuButton;
        public static MelonPreferences_Entry<int> RadioResetButton;
        public static MelonPreferences_Entry<int> RadioTurtleButton;
        public static MelonPreferences_Entry<int> RadioFlightModeButton;

        // Axis Mapping (RC Radio: X, Y, Z, R, U, V)
        public static MelonPreferences_Entry<RadioAxis> RadioRollAxis;
        public static MelonPreferences_Entry<RadioAxis> RadioPitchAxis;
        public static MelonPreferences_Entry<RadioAxis> RadioThrottleAxis;
        public static MelonPreferences_Entry<RadioAxis> RadioYawAxis;

        // Flight Modes & Rates
        public static MelonPreferences_Entry<FlightMode> Mode;
        public static MelonPreferences_Entry<DronePreset> ActivePreset;
        public static MelonPreferences_Entry<RateType> RateSystem;
        public static MelonPreferences_Entry<float> MaxAngleDegrees;
        public static MelonPreferences_Entry<float> RcRate;
        public static MelonPreferences_Entry<float> SuperRate;
        public static MelonPreferences_Entry<float> Expo;
        public static MelonPreferences_Entry<float> PitchRcRate;
        public static MelonPreferences_Entry<float> PitchSuperRate;
        public static MelonPreferences_Entry<float> PitchExpo;
        public static MelonPreferences_Entry<float> YawRcRate;
        public static MelonPreferences_Entry<float> YawSuperRate;
        public static MelonPreferences_Entry<float> YawExpo;

        // Actual Rates (Betaflight 4.2+)
        public static MelonPreferences_Entry<float> RollCenterSens;
        public static MelonPreferences_Entry<float> RollMaxRate;
        public static MelonPreferences_Entry<float> RollActualExpo;
        public static MelonPreferences_Entry<float> PitchCenterSens;
        public static MelonPreferences_Entry<float> PitchMaxRate;
        public static MelonPreferences_Entry<float> PitchActualExpo;
        public static MelonPreferences_Entry<float> YawCenterSens;
        public static MelonPreferences_Entry<float> YawMaxRate;
        public static MelonPreferences_Entry<float> YawActualExpo;

        public static MelonPreferences_Entry<float> CameraTiltAngle;
        public static MelonPreferences_Entry<float> CameraHeightOffsetMm;
        public static MelonPreferences_Entry<float> CameraForwardOffsetMm;
        public static MelonPreferences_Entry<bool> EnableCameraAngleMix;

        // Drone Hardware Builder
        public static MelonPreferences_Entry<float> FrameWheelbaseMm;
        public static MelonPreferences_Entry<bool> IsDuctedFrame;
        public static MelonPreferences_Entry<float> PropDiameterInches;
        public static MelonPreferences_Entry<float> PropPitchInches;
        public static MelonPreferences_Entry<int> PropBlades;
        public static MelonPreferences_Entry<float> MotorStatorDiameterMm;
        public static MelonPreferences_Entry<float> MotorStatorHeightMm;
        public static MelonPreferences_Entry<float> MotorKv;
        public static MelonPreferences_Entry<int> BatteryCells;
        public static MelonPreferences_Entry<float> EscAmperage;
        public static MelonPreferences_Entry<bool> AutoEstimateMass;

        // PID Tuning (Simple + Advanced)
        public static MelonPreferences_Entry<float> PidSnappiness;
        public static MelonPreferences_Entry<bool> AdvancedPidTuning;
        public static MelonPreferences_Entry<float> PidRollPitchWn;
        public static MelonPreferences_Entry<float> PidYawWn;
        public static MelonPreferences_Entry<float> PidDamping;

        // Physics Dynamics & Realism
        public static MelonPreferences_Entry<float> QuadMassKg;
        public static MelonPreferences_Entry<float> PropWashStrength;
        public static MelonPreferences_Entry<float> GroundEffectStrength;
        public static MelonPreferences_Entry<float> SnapWobbleStrength;
        public static MelonPreferences_Entry<bool> EnableTranslationalLift;
        public static MelonPreferences_Entry<bool> EnableMotorSaturation;

        // Battery Simulation
        public static MelonPreferences_Entry<bool> SimulateBatteryDepletion;
        public static MelonPreferences_Entry<float> BatteryCapacityMah;

        // Controls
        public static MelonPreferences_Entry<float> StickDeadzone;
        public static MelonPreferences_Entry<float> ThrottleMid;
        public static MelonPreferences_Entry<float> ThrottleExpo;
        public static MelonPreferences_Entry<ThrottleLimitType> ThrottleLimitMode;
        public static MelonPreferences_Entry<float> ThrottleLimitPercent;
        public static MelonPreferences_Entry<bool> InvertPitch;
        public static MelonPreferences_Entry<bool> SpringCenteredThrottle;
        public static MelonPreferences_Entry<float> AutoFlipDelay;
        public static MelonPreferences_Entry<float> KeyboardSmoothing;

        // OSD & Telemetry
        public static MelonPreferences_Entry<bool> ShowDebugOsd;
        public static MelonPreferences_Entry<bool> ShowStickOverlay;
        public static MelonPreferences_Entry<bool> OsdShowHeader;
        public static MelonPreferences_Entry<bool> OsdShowHorizon;
        public static MelonPreferences_Entry<bool> OsdShowCrosshair;
        public static MelonPreferences_Entry<bool> OsdShowTimers;
        public static MelonPreferences_Entry<bool> ShowDroneMeshInFpv;
        public static MelonPreferences_Entry<bool> AutoBindDroneProp;
        public static MelonPreferences_Entry<bool> AutoSpawnDroneProp;
        public static MelonPreferences_Entry<string> DronePropGuid;
        public static MelonPreferences_Entry<bool> SuppressIndicatorWhenPropBound;
        public static MelonPreferences_Entry<bool> DebugHearPropAudio;
        public static MelonPreferences_Entry<bool> DisableCollisions;

        // Keybinds
        public static MelonPreferences_Entry<KeyCode> ArmKey;
        public static MelonPreferences_Entry<KeyCode> MenuKey;
        public static MelonPreferences_Entry<KeyCode> ResetKey;
        public static MelonPreferences_Entry<KeyCode> TurtleFlipKey;
        public static MelonPreferences_Entry<KeyCode> FlightModeKey;

        public static MelonPreferences_Entry<KeyCode> ThrottleUpKey;
        public static MelonPreferences_Entry<KeyCode> ThrottleDownKey;
        public static MelonPreferences_Entry<KeyCode> YawLeftKey;
        public static MelonPreferences_Entry<KeyCode> YawRightKey;
        public static MelonPreferences_Entry<KeyCode> PitchDownKey;
        public static MelonPreferences_Entry<KeyCode> PitchUpKey;
        public static MelonPreferences_Entry<KeyCode> RollLeftKey;
        public static MelonPreferences_Entry<KeyCode> RollRightKey;

        // Optics & VTX
        public static MelonPreferences_Entry<float> FpvCameraFov;
        public static MelonPreferences_Entry<bool> EnableLensDistortion;
        public static MelonPreferences_Entry<float> LensDistortionK;
        public static MelonPreferences_Entry<VideoResolution> FpvCameraResolution;
        public static MelonPreferences_Entry<VrViewMode> VrView;
        public static MelonPreferences_Entry<float> VrGoggleScreenFov;
        public static MelonPreferences_Entry<float> VrStereoIpdMm;
        public static MelonPreferences_Entry<bool> CockpitVrShowOsd;
        public static MelonPreferences_Entry<bool> LosFlyMode;
        public static MelonPreferences_Entry<bool> LosAutoTrackCamera;
        public static MelonPreferences_Entry<bool> EnableVtxStatic;
        public static MelonPreferences_Entry<VtxSystem> VtxType;
        public static MelonPreferences_Entry<float> VtxPowerMw;
        public static MelonPreferences_Entry<float> VtxNoiseStrength;
        public static MelonPreferences_Entry<bool> EnableVoltageSag;

        // Audio
        public static MelonPreferences_Entry<float> MotorVolume;
        public static MelonPreferences_Entry<bool> EnableBladeSlapAudio;

        // Spout2 Video Streaming (Action-Cam / Raw FPV Output)
        public static MelonPreferences_Entry<bool> EnableSpout;
        public static MelonPreferences_Entry<string> SpoutSenderName;
        public static MelonPreferences_Entry<bool> SpoutIncludeOSD;
        public static MelonPreferences_Entry<bool> SpoutShowDroneMesh;
        public static MelonPreferences_Entry<SpoutGimbalMode> SpoutGimbal;
        public static MelonPreferences_Entry<GimbalAxisMode> SpoutGimbalRollMode;
        public static MelonPreferences_Entry<GimbalAxisMode> SpoutGimbalPitchMode;
        public static MelonPreferences_Entry<GimbalYawMode> SpoutGimbalYawMode;
        public static MelonPreferences_Entry<float> SpoutGimbalSmoothSpeed;
        public static MelonPreferences_Entry<float> SpoutGimbalPitchOffset;
        public static MelonPreferences_Entry<float> SpoutGimbalFov;
        public static MelonPreferences_Entry<VideoResolution> SpoutGimbalResolution;

        public static void Initialize()
        {
            var category = MelonPreferences.CreateCategory(CategoryName, "FPV Drone");

            ModEnabled = category.CreateEntry("ModEnabled", false, "Enable FPV Drone Mod");
            Mode = category.CreateEntry("FlightMode", FlightMode.Acro, "Flight Mode (Acro, Angle, PositionHold)");
            ActivePreset = category.CreateEntry("ActivePreset", DronePreset.Freestyle5Inch, "Active Drone Hardware Preset");
            InputDevice = category.CreateEntry("InputDevice", ControllerDevice.Auto, "Preferred Input Controller Device (Auto, Radio, Gamepad, Keyboard)");
            ControlStickMode = category.CreateEntry("ControlStickMode", StickMode.Mode2, "RC Stick Mode (Mode 2 Default: Throttle/Yaw Left, Pitch/Roll Right)");
            GamepadTriggerThrottle = category.CreateEntry("GamepadTriggerThrottle", false, "Use Gamepad Right Trigger (RT) for Throttle (0% to 100%)");
            EnableOnboardAudio = category.CreateEntry("EnableOnboardAudio", false, "FPV Onboard Audio Listener (Camera Audio in Desktop Mode)");

            GamepadArmBind = category.CreateEntry("GamepadArmBind", GamepadButtonBinding.SelectView, "Gamepad Arm / Disarm Button");
            GamepadMenuBind = category.CreateEntry("GamepadMenuBind", GamepadButtonBinding.Start, "Gamepad Configurator Menu Button");
            GamepadResetBind = category.CreateEntry("GamepadResetBind", GamepadButtonBinding.ButtonNorth, "Gamepad Reset Drone Button (Y)");
            GamepadTurtleBind = category.CreateEntry("GamepadTurtleBind", GamepadButtonBinding.ButtonWest, "Gamepad Turtle Mode Flip Button (X)");
            GamepadFlightModeBind = category.CreateEntry("GamepadFlightModeBind", GamepadButtonBinding.ButtonEast, "Gamepad Cycle Flight Mode Button (B)");

            RadioArmButton = category.CreateEntry("RadioArmButton", 0, "RC Radio Arm Switch (DirectInput Button 0)");
            RadioMenuButton = category.CreateEntry("RadioMenuButton", -1, "RC Radio Menu Button (-1 = Disabled)");
            RadioResetButton = category.CreateEntry("RadioResetButton", 2, "RC Radio Reset Button (DirectInput Button 2)");
            RadioTurtleButton = category.CreateEntry("RadioTurtleButton", 3, "RC Radio Turtle Flip Button (DirectInput Button 3)");
            RadioFlightModeButton = category.CreateEntry("RadioFlightModeButton", 1, "RC Radio Flight Mode Switch (DirectInput Button 1)");

            RadioRollAxis = category.CreateEntry("RadioRollAxis", RadioAxis.X, "RC Radio Roll Axis");
            RadioPitchAxis = category.CreateEntry("RadioPitchAxis", RadioAxis.Y, "RC Radio Pitch Axis");
            RadioThrottleAxis = category.CreateEntry("RadioThrottleAxis", RadioAxis.Z, "RC Radio Throttle Axis");
            RadioYawAxis = category.CreateEntry("RadioYawAxis", RadioAxis.R, "RC Radio Yaw Axis");

            MelonPreferences_Entry<bool> legacyAngle = category.CreateEntry("IsAngleMode", false, "Legacy Auto-Level Angle Mode");
            if (legacyAngle.Value && Mode.Value == FlightMode.Acro)
            {
                Mode.Value = FlightMode.Angle;
                legacyAngle.Value = false;
            }

            MaxAngleDegrees = category.CreateEntry("MaxAngleDegrees", 50.0f, "Maximum Tilt Angle in Angle Mode (deg)");
            RateSystem = category.CreateEntry("RateSystem", RateType.Actual, "Rate System (Actual or Betaflight Classic)");

            RcRate = category.CreateEntry("RcRate", 1.0f, "Roll RC Rate (Betaflight)");
            SuperRate = category.CreateEntry("SuperRate", 0.70f, "Roll SuperRate (Betaflight)");
            Expo = category.CreateEntry("Expo", 0.18f, "Roll Expo (Betaflight)");

            PitchRcRate = category.CreateEntry("PitchRcRate", RcRate.Value, "Pitch RC Rate (Betaflight)");
            PitchSuperRate = category.CreateEntry("PitchSuperRate", SuperRate.Value, "Pitch SuperRate (Betaflight)");
            PitchExpo = category.CreateEntry("PitchExpo", Expo.Value, "Pitch Expo (Betaflight)");

            YawRcRate = category.CreateEntry("YawRcRate", 1.0f, "Yaw RC Rate (Betaflight)");
            YawSuperRate = category.CreateEntry("YawSuperRate", 0.65f, "Yaw SuperRate (Betaflight)");
            YawExpo = category.CreateEntry("YawExpo", 0.15f, "Yaw Expo (Betaflight)");

            RollCenterSens = category.CreateEntry("RollCenterSens", 180.0f, "Roll Center Sensitivity (Actual deg/s)");
            RollMaxRate = category.CreateEntry("RollMaxRate", 670.0f, "Roll Max Rate (Actual deg/s)");
            RollActualExpo = category.CreateEntry("RollActualExpo", 0.50f, "Roll Expo (Actual)");

            PitchCenterSens = category.CreateEntry("PitchCenterSens", 180.0f, "Pitch Center Sensitivity (Actual deg/s)");
            PitchMaxRate = category.CreateEntry("PitchMaxRate", 670.0f, "Pitch Max Rate (Actual deg/s)");
            PitchActualExpo = category.CreateEntry("PitchActualExpo", 0.50f, "Pitch Expo (Actual)");

            YawCenterSens = category.CreateEntry("YawCenterSens", 160.0f, "Yaw Center Sensitivity (Actual deg/s)");
            YawMaxRate = category.CreateEntry("YawMaxRate", 550.0f, "Yaw Max Rate (Actual deg/s)");
            YawActualExpo = category.CreateEntry("YawActualExpo", 0.40f, "Yaw Expo (Actual)");

            CameraTiltAngle = category.CreateEntry("CameraTiltAngle", 30.0f, "Camera Uptilt Angle (deg)");
            CameraHeightOffsetMm = category.CreateEntry("CameraHeightOffsetMm", 18.0f, "FPV Camera Mount Height Offset (mm)");
            CameraForwardOffsetMm = category.CreateEntry("CameraForwardOffsetMm", 52.0f, "FPV Camera Mount Forward Offset (mm)");
            EnableCameraAngleMix = category.CreateEntry("EnableCameraAngleMix", true, "FPV Camera Angle Yaw/Roll Mixing (Betaflight FPV Mix)");

            FrameWheelbaseMm = category.CreateEntry("FrameWheelbaseMm", 225.0f, "Frame Wheelbase (mm)");
            IsDuctedFrame = category.CreateEntry("IsDuctedFrame", false, "Ducted Frame / Prop Guards");
            PropDiameterInches = category.CreateEntry("PropDiameterInches", 5.1f, "Propeller Diameter (inches)");
            PropPitchInches = category.CreateEntry("PropPitchInches", 4.3f, "Propeller Pitch (inches)");
            PropBlades = category.CreateEntry("PropBlades", 3, "Propeller Blade Count (2, 3, or 4)");
            MotorStatorDiameterMm = category.CreateEntry("MotorStatorDiameterMm", 22.0f, "Motor Stator Diameter (mm)");
            MotorStatorHeightMm = category.CreateEntry("MotorStatorHeightMm", 7.0f, "Motor Stator Height (mm)");
            MotorKv = category.CreateEntry("MotorKv", 1850.0f, "Motor KV Rating");
            BatteryCells = category.CreateEntry("BatteryCells", 6, "LiPo Cell Count (1S - 6S)");
            EscAmperage = category.CreateEntry("EscAmperage", 50.0f, "ESC Current Rating per Motor (A)");
            AutoEstimateMass = category.CreateEntry("AutoEstimateMass", true, "Auto-Estimate All-Up Weight from Selected Hardware");

            PidSnappiness = category.CreateEntry("PidSnappiness", 1.0f, "PID Tune Snappiness Multiplier (0.6 - 1.5)");
            AdvancedPidTuning = category.CreateEntry("AdvancedPidTuning", false, "Show Advanced PID Bandwidth and Damping Sliders");
            PidRollPitchWn = category.CreateEntry("PidRollPitchWn", 42.0f, "Advanced Roll/Pitch Loop Bandwidth (rad/s)");
            PidYawWn = category.CreateEntry("PidYawWn", 26.0f, "Advanced Yaw Loop Bandwidth (rad/s)");
            PidDamping = category.CreateEntry("PidDamping", 0.85f, "Advanced D-Term Damping Ratio (0.5 - 1.2)");

            QuadMassKg = category.CreateEntry("QuadMassKg", 0.68f, "Quad All-Up Weight (kg)");
            PropWashStrength = category.CreateEntry("PropWashStrength", 1.0f, "Prop Wash Turbulence Strength");
            GroundEffectStrength = category.CreateEntry("GroundEffectStrength", 1.0f, "Ground Effect Cushion Strength");
            SnapWobbleStrength = category.CreateEntry("SnapWobbleStrength", 1.0f, "Snap-Stop Motor Inertia Wobble Strength");
            EnableTranslationalLift = category.CreateEntry("EnableTranslationalLift", true, "Effective Translational Lift (ETL) and Inflow Pitch-Up");
            EnableMotorSaturation = category.CreateEntry("EnableMotorSaturation", true, "Motor Mixer RPM Saturation and Yaw Washout");

            SimulateBatteryDepletion = category.CreateEntry("SimulateBatteryDepletion", false, "Simulate True LiPo Capacity Drain and Limp Mode");
            BatteryCapacityMah = category.CreateEntry("BatteryCapacityMah", 1300.0f, "LiPo Pack Capacity (mAh)");

            DroneBuilder.EnforceGeometryConstraints();

            StickDeadzone = category.CreateEntry("StickDeadzone", 0.05f, "Stick Deadzone");
            ThrottleMid = category.CreateEntry("ThrottleMid", 0.50f, "Betaflight Throttle Mid (Inflection Point 0..1)");
            ThrottleExpo = category.CreateEntry("ThrottleExpo", 0.35f, "Betaflight Throttle Expo around Throttle Mid");
            ThrottleLimitMode = category.CreateEntry("ThrottleLimitMode", ThrottleLimitType.Off, "Throttle Limit Type (Off, Scale, Clip)");
            ThrottleLimitPercent = category.CreateEntry("ThrottleLimitPercent", 100.0f, "Throttle Limit Percentage (25% - 100%)");
            InvertPitch = category.CreateEntry("InvertPitch", true, "Invert Pitch Axis (Stick Forward = Nose Down)");
            SpringCenteredThrottle = category.CreateEntry("SpringCenteredThrottle", true, "Spring Centered Throttle (Center = 0, Push Up = Climb)");
            AutoFlipDelay = category.CreateEntry("AutoFlipDelay", 2.0f, "Auto Turtle Mode Flip Delay (sec)");
            KeyboardSmoothing = category.CreateEntry("KeyboardSmoothing", 6.5f, "Keyboard Slew Rate Smoothing Factor");

            ShowDebugOsd = category.CreateEntry("ShowDebugOsd", false, "Display Developer Aerodynamics Debug Banners on OSD");
            ShowStickOverlay = category.CreateEntry("ShowStickOverlay", true, "Display FPV Virtual Stick Position Gimbal Overlay");
            OsdShowHeader = category.CreateEntry("OsdShowHeader", true, "Show Top VTX / Build Header on OSD");
            OsdShowHorizon = category.CreateEntry("OsdShowHorizon", true, "Show Artificial Horizon on OSD");
            OsdShowCrosshair = category.CreateEntry("OsdShowCrosshair", true, "Show Center Crosshair on OSD");
            OsdShowTimers = category.CreateEntry("OsdShowTimers", true, "Show Flight Timers, Battery, and Speed/Alt on OSD");
            ShowDroneMeshInFpv = category.CreateEntry("ShowDroneMeshInFpv", true, "Show 3D Frame Arms & Spinning Props in FPV View");
            AutoBindDroneProp = category.CreateEntry("AutoBindDroneProp", true, "Auto-Bind Spawned Drone CVRSpawnable Prop");
            AutoSpawnDroneProp = category.CreateEntry("AutoSpawnDroneProp", false, "Auto-Spawn Drone Prop on Arm (if permitted)");
            DronePropGuid = category.CreateEntry("DronePropGuid", "5eeb9775-3a6d-41e4-a0e8-8e0c70ff7d17", "Drone Prop CVR Content GUID");
            SuppressIndicatorWhenPropBound = category.CreateEntry("SuppressIndicatorWhenPropBound", true, "Hide CVR Camera Indicator When Prop Is Bound");
            DebugHearPropAudio = category.CreateEntry("DebugHearPropAudio", false, "Debug: Hear Prop Audio Instead of Mod Audio");
            DisableCollisions = category.CreateEntry("DisableCollisions", false, "Disable World Collisions (Noclip)");

            ArmKey = category.CreateEntry("ArmKey", KeyCode.F6, "Arm / Disarm Toggle Key");
            MenuKey = category.CreateEntry("MenuKey", KeyCode.F7, "Configurator Menu Key");
            ResetKey = category.CreateEntry("ResetKey", KeyCode.F8, "Reset / Teleport Drone Key");
            TurtleFlipKey = category.CreateEntry("TurtleFlipKey", KeyCode.Space, "Turtle Mode Flip Key");
            FlightModeKey = category.CreateEntry("FlightModeKey", KeyCode.N, "Toggle Flight Mode (Acro / Angle)");

            ThrottleUpKey = category.CreateEntry("ThrottleUpKey", KeyCode.W, "Keyboard Throttle Up Key");
            ThrottleDownKey = category.CreateEntry("ThrottleDownKey", KeyCode.S, "Keyboard Throttle Down Key");
            YawLeftKey = category.CreateEntry("YawLeftKey", KeyCode.A, "Keyboard Yaw Left Key");
            YawRightKey = category.CreateEntry("YawRightKey", KeyCode.D, "Keyboard Yaw Right Key");
            PitchDownKey = category.CreateEntry("PitchDownKey", KeyCode.I, "Keyboard Pitch Down (Nose Down) Key");
            PitchUpKey = category.CreateEntry("PitchUpKey", KeyCode.K, "Keyboard Pitch Up (Nose Up) Key");
            RollLeftKey = category.CreateEntry("RollLeftKey", KeyCode.J, "Keyboard Roll Left Key");
            RollRightKey = category.CreateEntry("RollRightKey", KeyCode.L, "Keyboard Roll Right Key");

            FpvCameraFov = category.CreateEntry("FpvCameraFov", 105.0f, "FPV Camera Field of View (deg)");
            EnableLensDistortion = category.CreateEntry("EnableLensDistortion", true, "FPV Ultra-Wide Fisheye Lens Barrel Distortion");
            LensDistortionK = category.CreateEntry("LensDistortionK", 0.16f, "Optical Barrel Distortion Factor");
            FpvCameraResolution = category.CreateEntry("FpvCameraResolution", VideoResolution.MatchScreen, "FPV Camera Render Resolution (MatchScreen, 720p, 1080p, 1440p, 4K)");
            VrView = category.CreateEntry("VrView", VrViewMode.Goggles2D, "VR View Mode (Goggles2D, Stereo3D Goggles, or CockpitVR3D)");
            VrGoggleScreenFov = category.CreateEntry("VrGoggleScreenFov", 68.0f, "VR Goggle Screen Angular FOV (deg)");
            VrStereoIpdMm = category.CreateEntry("VrStereoIpdMm", 64.0f, "VR Stereo 3D Camera Eye Separation / IPD (mm)");
            CockpitVrShowOsd = category.CreateEntry("CockpitVrShowOsd", true, "Show Translucent Betaflight HUD in Full 3D VR View");
            LosFlyMode = category.CreateEntry("LosFlyMode", false, "Line-of-Sight (LOS) Fly Mode (Desktop & VR)");
            LosAutoTrackCamera = category.CreateEntry("LosAutoTrackCamera", true, "Desktop LOS Auto-Track Camera");

            EnableVtxStatic = category.CreateEntry("EnableVtxStatic", true, "Simulate VTX RF Static and Snow");
            VtxType = category.CreateEntry("VtxType", VtxSystem.Analog, "VTX Video System (Analog or Digital)");
            VtxPowerMw = category.CreateEntry("VtxPowerMw", 25.0f, "VTX Output Power (mW)");
            VtxNoiseStrength = category.CreateEntry("VtxNoiseStrength", 1.0f, "VTX Static Noise Intensity Multiplier");
            EnableVoltageSag = category.CreateEntry("EnableVoltageSag", true, "Simulate LiPo Voltage Sag Under Throttle");

            MotorVolume = category.CreateEntry("MotorVolume", 0.85f, "3D Motor Audio Volume");
            EnableBladeSlapAudio = category.CreateEntry("EnableBladeSlapAudio", true, "Simulate Aerodynamic Prop Blade Slap and Tearing Audio");

            EnableSpout = category.CreateEntry("EnableSpout", false, "Enable Spout2 Video Streaming Output to OBS");
            SpoutSenderName = category.CreateEntry("SpoutSenderName", "CVR_FPVDrone", "Spout2 Sender Name (Broadcast to OBS)");
            SpoutIncludeOSD = category.CreateEntry("SpoutIncludeOSD", false, "Spout2 Stream Type (True = Raw FPV with OSD, False = Clean Camera)");
            SpoutShowDroneMesh = category.CreateEntry("SpoutShowDroneMesh", false, "Show 3D Drone Frame & Props in Spout2 Output");
            SpoutGimbal = category.CreateEntry("SpoutGimbal", SpoutGimbalMode.Off, "Spout2 Clean Camera Gimbal Mode (Off, 1-Axis Roll, 2-Axis Horizon, 3-Axis Smooth, Custom)");
            SpoutGimbalRollMode = category.CreateEntry("SpoutGimbalRollMode", GimbalAxisMode.Lock, "Custom Gimbal Roll Axis Mode (Raw, Smooth, Lock)");
            SpoutGimbalPitchMode = category.CreateEntry("SpoutGimbalPitchMode", GimbalAxisMode.Smooth, "Custom Gimbal Pitch Axis Mode (Raw, Smooth, Lock)");
            SpoutGimbalYawMode = category.CreateEntry("SpoutGimbalYawMode", GimbalYawMode.Smooth, "Custom Gimbal Yaw Axis Mode (Raw, Smooth)");
            SpoutGimbalSmoothSpeed = category.CreateEntry("SpoutGimbalSmoothSpeed", 6.0f, "Gimbal Smoothing Speed (Lower = Smoother)");
            SpoutGimbalPitchOffset = category.CreateEntry("SpoutGimbalPitchOffset", 0.0f, "Horizon-Locked Pitch Offset (deg)");
            SpoutGimbalFov = category.CreateEntry("SpoutGimbalFov", 110.0f, "Spout2 Gimbal Camera Field of View (deg)");
            SpoutGimbalResolution = category.CreateEntry("SpoutGimbalResolution", VideoResolution.Res1080p, "Spout2 Gimbal Camera Render Resolution");

            // Always start disabled on game launch so normal CVR sessions are unaffected until toggled on
            ModEnabled.Value = false;
        }

        public static void GetResolutionDimensions(VideoResolution res, out int width, out int height)
        {
            switch (res)
            {
                case VideoResolution.Res720p:
                    width = 1280;
                    height = 720;
                    return;
                case VideoResolution.Res1080p:
                    width = 1920;
                    height = 1080;
                    return;
                case VideoResolution.Res1440p:
                    width = 2560;
                    height = 1440;
                    return;
                case VideoResolution.Res4K:
                    width = 3840;
                    height = 2160;
                    return;
                case VideoResolution.MatchScreen:
                default:
                    width = Mathf.Max(1920, Screen.width);
                    height = Mathf.Max(1080, Screen.height);
                    return;
            }
        }

        public static void ApplyDronePreset(DronePreset preset)
        {
            ActivePreset.Value = preset;
            PresetSpec spec = DronePresets.Get(preset);

            FrameWheelbaseMm.Value = spec.WheelbaseMm;
            IsDuctedFrame.Value = spec.IsDucted;
            PropDiameterInches.Value = spec.PropDiameterInches;
            PropPitchInches.Value = spec.PropPitchInches;
            PropBlades.Value = spec.PropBlades;
            MotorStatorDiameterMm.Value = spec.MotorStatorDiameterMm;
            MotorStatorHeightMm.Value = spec.MotorStatorHeightMm;
            MotorKv.Value = spec.MotorKv;
            BatteryCells.Value = spec.BatteryCells;
            BatteryCapacityMah.Value = spec.BatteryCapacityMah;
            EscAmperage.Value = spec.EscAmperage;
            QuadMassKg.Value = spec.MassKg;

            DroneBuilder.EnforceGeometryConstraints();

            CameraTiltAngle.Value = spec.CameraTiltAngle;
            CameraHeightOffsetMm.Value = spec.CameraHeightOffsetMm;
            CameraForwardOffsetMm.Value = spec.CameraForwardOffsetMm;
            RcRate.Value = spec.RcRate;
            SuperRate.Value = spec.SuperRate;
            Expo.Value = spec.Expo;
            PitchRcRate.Value = spec.RcRate;
            PitchSuperRate.Value = spec.SuperRate;
            PitchExpo.Value = spec.Expo;
            YawRcRate.Value = spec.YawRcRate;
            YawSuperRate.Value = spec.YawSuperRate;
            YawExpo.Value = spec.YawExpo;

            float rpMax = BetaflightRates.GetMaxRate(spec.RcRate, spec.SuperRate, spec.Expo);
            float yMax = BetaflightRates.GetMaxRate(spec.YawRcRate, spec.YawSuperRate, spec.YawExpo);
            RollCenterSens.Value = Mathf.Round(spec.RcRate * 180.0f);
            RollMaxRate.Value = Mathf.Round(rpMax);
            RollActualExpo.Value = Mathf.Clamp01(spec.Expo + 0.30f);
            PitchCenterSens.Value = RollCenterSens.Value;
            PitchMaxRate.Value = RollMaxRate.Value;
            PitchActualExpo.Value = RollActualExpo.Value;
            YawCenterSens.Value = Mathf.Round(spec.YawRcRate * 160.0f);
            YawMaxRate.Value = Mathf.Round(yMax);
            YawActualExpo.Value = Mathf.Clamp01(spec.YawExpo + 0.25f);

            MelonPreferences.Save();
        }

        public static void CycleFlightMode()
        {
            int next = ((int)Mode.Value + 1) % 3;
            Mode.Value = (FlightMode)next;
            MelonPreferences.Save();
        }

        public static void CycleDronePreset()
        {
            int count = System.Enum.GetValues(typeof(DronePreset)).Length;
            int next = ((int)ActivePreset.Value + 1) % count;
            ApplyDronePreset((DronePreset)next);
        }

        public static void CycleControllerDevice()
        {
            int next = ((int)InputDevice.Value + 1) % 5;
            InputDevice.Value = (ControllerDevice)next;
            MelonPreferences.Save();
        }

        public static void CycleStickMode()
        {
            int next = ((int)ControlStickMode.Value + 1) % 4;
            ControlStickMode.Value = (StickMode)next;
            MelonPreferences.Save();
        }

        public static string GetGamepadButtonName(GamepadButtonBinding btn)
        {
            switch (btn)
            {
                case GamepadButtonBinding.SelectView: return "View / Select";
                case GamepadButtonBinding.Start: return "Start / Menu";
                case GamepadButtonBinding.ButtonSouth: return "A Button";
                case GamepadButtonBinding.ButtonEast: return "B Button";
                case GamepadButtonBinding.ButtonWest: return "X Button";
                case GamepadButtonBinding.ButtonNorth: return "Y Button";
                case GamepadButtonBinding.LeftShoulder: return "Left Bumper (LB)";
                case GamepadButtonBinding.RightShoulder: return "Right Bumper (RB)";
                case GamepadButtonBinding.LeftStickPress: return "L-Stick Click (L3)";
                case GamepadButtonBinding.RightStickPress: return "R-Stick Click (R3)";
                case GamepadButtonBinding.DpadUp: return "D-Pad Up";
                case GamepadButtonBinding.DpadDown: return "D-Pad Down";
                case GamepadButtonBinding.DpadLeft: return "D-Pad Left";
                case GamepadButtonBinding.DpadRight: return "D-Pad Right";
                case GamepadButtonBinding.None: return "Unbound (None)";
                default: return btn.ToString();
            }
        }

        public static string GetRadioButtonName(int btnIndex)
        {
            if (btnIndex < 0) return "Unbound (None)";
            return $"Button {btnIndex}";
        }

        public static void ResetAllBindingsToDefault()
        {
            GamepadArmBind.Value = GamepadButtonBinding.SelectView;
            GamepadMenuBind.Value = GamepadButtonBinding.Start;
            GamepadResetBind.Value = GamepadButtonBinding.ButtonNorth;
            GamepadTurtleBind.Value = GamepadButtonBinding.ButtonWest;
            GamepadFlightModeBind.Value = GamepadButtonBinding.ButtonEast;

            RadioArmButton.Value = 0;
            RadioMenuButton.Value = -1;
            RadioResetButton.Value = 2;
            RadioTurtleButton.Value = 3;
            RadioFlightModeButton.Value = 1;

            MelonPreferences.Save();
        }
    }
}
