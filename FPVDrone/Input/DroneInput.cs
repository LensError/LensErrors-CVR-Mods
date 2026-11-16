using System;
using System.Collections.Generic;
using System.Reflection;
using ABI_RC.Core.InteractionSystem;
using ABI_RC.Core.Savior;
using ABI_RC.Systems.InputManagement;
using ABI_RC.Systems.InputManagement.InputModules;
using ABI_RC.Systems.InputManagement.XR;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace FPVDrone
{
    public class DroneInput
    {
        private const string InputLockId = "FPVDroneFlightLock";

        public float Throttle { get; private set; }
        public float Pitch { get; private set; }
        public float Roll { get; private set; }
        public float Yaw { get; private set; }

        public bool ArmTogglePressed { get; private set; }
        public bool MenuTogglePressed { get; private set; }
        public bool ResetPressed { get; private set; }
        public bool TurtleFlipPressed { get; private set; }
        public bool FlightModeTogglePressed { get; private set; }

        public string ActiveDeviceName { get; private set; } = "Keyboard";

        // Interactive Guided Binding state
        public bool IsListeningForBind { get; private set; } = false;
        public DroneAction ListeningAction { get; private set; } = DroneAction.Arm;
        public bool ListeningForRadio { get; private set; } = false;

        private bool _isInputLocked = false;

        // Cached Radio Transmitter hardware controls to eliminate per-frame GC allocations
        private readonly WinMMJoystick _winmmRadio = new WinMMJoystick();
        private Joystick _cachedRadio;
        private AxisControl _axisRoll;
        private AxisControl _axisPitch;
        private AxisControl _axisThrottle;
        private AxisControl _axisYaw;
        private readonly List<AxisControl> _cachedRadioAxes = new List<AxisControl>();
        private readonly List<ButtonControl> _cachedRadioButtons = new List<ButtonControl>();

        // Keyboard slew-rate smoothing state
        private float _smoothedKbThrottle = 0.0f;
        private float _smoothedKbPitch = 0.0f;
        private float _smoothedKbRoll = 0.0f;
        private float _smoothedKbYaw = 0.0f;

        // Cached reflection fields for ChilloutVR's unified SteamVR/OpenXR controller modules
        private static readonly FieldInfo LeftXrModuleField =
            typeof(CVRInputModule_XR).GetField("_leftModule", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo RightXrModuleField =
            typeof(CVRInputModule_XR).GetField("_rightModule", BindingFlags.Instance | BindingFlags.NonPublic);

        // VR thumbstick click / chord tracking
        private bool _prevVrLeftClick;
        private bool _prevVrRightClick;
        private bool _vrChordConsumed;

        public static void GetXrModules(out CVRXRModule leftModule, out CVRXRModule rightModule)
        {
            leftModule = null;
            rightModule = null;
            var xr = CVRInputManager._moduleXR;
            if (xr == null)
                return;

            if (LeftXrModuleField != null)
                leftModule = LeftXrModuleField.GetValue(xr) as CVRXRModule;
            if (RightXrModuleField != null)
                rightModule = RightXrModuleField.GetValue(xr) as CVRXRModule;
        }

        public void StartListeningForBind(DroneAction action, bool isRadio)
        {
            IsListeningForBind = true;
            ListeningAction = action;
            ListeningForRadio = isRadio;
        }

        public void CancelListeningForBind()
        {
            IsListeningForBind = false;
        }

        public void UpdateInputs(bool isFlightActive, bool isMenuOpen)
        {
            // Lock game input if in flight or if configurator menu is open
            SetGameInputLocked(isFlightActive || isMenuOpen);

            // Reset one-shot button states
            ArmTogglePressed = false;
            MenuTogglePressed = false;
            ResetPressed = false;
            TurtleFlipPressed = false;
            FlightModeTogglePressed = false;

            var gamepad = Gamepad.current;
            ControllerDevice prefDevice = Settings.InputDevice.Value;

            if (prefDevice == ControllerDevice.Auto || prefDevice == ControllerDevice.RadioTransmitter || (IsListeningForBind && ListeningForRadio))
            {
                _winmmRadio.Poll();
            }

            // Handle interactive guided button binding if active
            if (IsListeningForBind)
            {
                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    CancelListeningForBind();
                    return;
                }

                if (ListeningForRadio)
                {
                    int pressedBtn = -1;
                    if (_winmmRadio.IsConnected)
                    {
                        pressedBtn = _winmmRadio.DetectPressedButton();
                    }
                    else
                    {
                        Joystick radioJoy = FindRadioTransmitter();
                        if (radioJoy != null)
                        {
                            if (_cachedRadio != radioJoy)
                                CacheRadioControls(radioJoy);
                            pressedBtn = DetectPressedRadioButton();
                        }
                    }

                    if (pressedBtn >= 0)
                    {
                        switch (ListeningAction)
                        {
                            case DroneAction.Arm: Settings.RadioArmButton.Value = pressedBtn; break;
                            case DroneAction.Menu: Settings.RadioMenuButton.Value = pressedBtn; break;
                            case DroneAction.Reset: Settings.RadioResetButton.Value = pressedBtn; break;
                            case DroneAction.TurtleFlip: Settings.RadioTurtleButton.Value = pressedBtn; break;
                            case DroneAction.FlightMode: Settings.RadioFlightModeButton.Value = pressedBtn; break;
                        }
                        MelonLoader.MelonPreferences.Save();
                        CancelListeningForBind();
                        return;
                    }
                }
                else
                {
                    if (gamepad != null)
                    {
                        GamepadButtonBinding detected = DetectPressedGamepadButton(gamepad);
                        if (detected != GamepadButtonBinding.None)
                        {
                            switch (ListeningAction)
                            {
                                case DroneAction.Arm: Settings.GamepadArmBind.Value = detected; break;
                                case DroneAction.Menu: Settings.GamepadMenuBind.Value = detected; break;
                                case DroneAction.Reset: Settings.GamepadResetBind.Value = detected; break;
                                case DroneAction.TurtleFlip: Settings.GamepadTurtleBind.Value = detected; break;
                                case DroneAction.FlightMode: Settings.GamepadFlightModeBind.Value = detected; break;
                            }
                            MelonLoader.MelonPreferences.Save();
                            CancelListeningForBind();
                            return;
                        }
                    }
                }

                return;
            }

            // Menu toggle key is always processed
            if (Input.GetKeyDown(Settings.MenuKey.Value)) MenuTogglePressed = true;

            if (gamepad != null && IsGamepadButtonPressed(gamepad, Settings.GamepadMenuBind.Value))
                MenuTogglePressed = true;

            if (IsRadioButtonPressed(Settings.RadioMenuButton.Value))
                MenuTogglePressed = true;

            // Check ChilloutVR menus (QuickMenu + Main ViewManager Menu)
            bool isQuickMenuOpen = CVR_MenuManager.Instance != null && CVR_MenuManager.Instance.IsViewShown;
            bool isMainMenuOpen = ViewManager.Instance != null && ViewManager.Instance.IsAnyMenuOpen;
            bool isCvrMenuOpen = isQuickMenuOpen || isMainMenuOpen;

            // VR controller chords & buttons (L3+R3 arm/disarm, L3 menu when disarmed, R3 turtle flip, A reset, X mode)
            bool allowVrController = prefDevice == ControllerDevice.VRControllers ||
                                     (prefDevice == ControllerDevice.Auto && MetaPort.Instance != null && MetaPort.Instance.isUsingVr);
            if (allowVrController && !isCvrMenuOpen)
            {
                UpdateVrButtons(isFlightActive, isMenuOpen);
            }
            else
            {
                _prevVrLeftClick = false;
                _prevVrRightClick = false;
                _vrChordConsumed = false;
            }

            // If config menu or any ChilloutVR menu is open, block all flight controls
            if (isMenuOpen || isCvrMenuOpen)
            {
                Throttle = 0.0f;
                Pitch = 0.0f;
                Roll = 0.0f;
                Yaw = 0.0f;
                _smoothedKbThrottle = 0.0f;
                _smoothedKbPitch = 0.0f;
                _smoothedKbRoll = 0.0f;
                _smoothedKbYaw = 0.0f;
                return;
            }

            // Keyboard one-shots from user-configurable settings (only active when armed)
            if (isFlightActive)
            {
                if (Input.GetKeyDown(Settings.ArmKey.Value)) ArmTogglePressed = true;
                if (Input.GetKeyDown(Settings.ResetKey.Value)) ResetPressed = true;
                if (Input.GetKeyDown(Settings.TurtleFlipKey.Value) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.T)) TurtleFlipPressed = true;
                if (Input.GetKeyDown(Settings.FlightModeKey.Value)) FlightModeTogglePressed = true;
            }
            else
            {
                // While disarmed, only allow arm key
                if (Input.GetKeyDown(Settings.ArmKey.Value)) ArmTogglePressed = true;
            }

            float rawThrottle = 0.0f;
            float rawPitch = 0.0f;
            float rawRoll = 0.0f;
            float rawYaw = 0.0f;

            float twr = Mathf.Max(1.0f, DroneBuilder.EvaluateCurrent().ThrustToWeightRatio);
            float hoverThrottle = DronePhysics.CalculateHoverThrottle(twr);
            float deadzone = Mathf.Clamp(Settings.StickDeadzone.Value, 0.01f, 0.35f);

            bool deviceRead = false;

            if (prefDevice == ControllerDevice.KeyboardOnly)
            {
                ActiveDeviceName = "Keyboard";
            }
            else if (prefDevice == ControllerDevice.RadioTransmitter)
            {
                if (TryReadRadio(out rawThrottle, out rawPitch, out rawRoll, out rawYaw, deadzone, isFlightActive))
                {
                    deviceRead = true;
                    ActiveDeviceName = GetRadioDisplayName();
                }
                else
                {
                    ActiveDeviceName = "Radio (Disconnected)";
                }
            }
            else if (prefDevice == ControllerDevice.Gamepad)
            {
                if (gamepad != null && TryReadGamepad(gamepad, out rawThrottle, out rawPitch, out rawRoll, out rawYaw, deadzone, isFlightActive))
                {
                    deviceRead = true;
                    ActiveDeviceName = GetGamepadDisplayName(gamepad);
                }
                else
                {
                    ActiveDeviceName = "Gamepad (Disconnected)";
                }
            }
            else if (prefDevice == ControllerDevice.VRControllers)
            {
                if (TryReadVR(out rawThrottle, out rawPitch, out rawRoll, out rawYaw, deadzone))
                {
                    deviceRead = true;
                    ActiveDeviceName = Settings.GamepadTriggerThrottle.Value ? "VR Controllers (RT Throttle)" : "VR Controllers";
                }
                else
                {
                    ActiveDeviceName = "VR Controllers (Disconnected)";
                }
            }
            else // Auto detection: prioritize RC Radio, then Gamepad, then VR (when in VR), then Keyboard
            {
                if (TryReadRadio(out rawThrottle, out rawPitch, out rawRoll, out rawYaw, deadzone, isFlightActive))
                {
                    deviceRead = true;
                    ActiveDeviceName = GetRadioDisplayName();
                }
                else if (gamepad != null && TryReadGamepad(gamepad, out rawThrottle, out rawPitch, out rawRoll, out rawYaw, deadzone, isFlightActive))
                {
                    deviceRead = true;
                    ActiveDeviceName = GetGamepadDisplayName(gamepad);
                }
                else if (allowVrController && TryReadVR(out rawThrottle, out rawPitch, out rawRoll, out rawYaw, deadzone))
                {
                    deviceRead = true;
                    ActiveDeviceName = Settings.GamepadTriggerThrottle.Value ? "VR Controllers (RT Throttle)" : "VR Controllers";
                }
                else
                {
                    ActiveDeviceName = "Keyboard";
                }
            }

            // Keyboard testing controls with smooth slew-rate ramping
            float targetKbThrottle = 0.0f;
            float targetKbPitch = 0.0f;
            float targetKbRoll = 0.0f;
            float targetKbYaw = 0.0f;

            if (Input.GetKey(Settings.ThrottleUpKey.Value) || Input.GetKey(KeyCode.UpArrow))
                targetKbThrottle = 1.0f;
            else if (Input.GetKey(Settings.ThrottleDownKey.Value) || Input.GetKey(KeyCode.DownArrow))
                targetKbThrottle = -1.0f;

            if (Input.GetKey(Settings.YawLeftKey.Value)) targetKbYaw -= 1.0f;
            if (Input.GetKey(Settings.YawRightKey.Value)) targetKbYaw += 1.0f;

            if (Input.GetKey(Settings.PitchDownKey.Value)) targetKbPitch += 1.0f;
            if (Input.GetKey(Settings.PitchUpKey.Value)) targetKbPitch -= 1.0f;

            if (Input.GetKey(Settings.RollLeftKey.Value)) targetKbRoll -= 1.0f;
            if (Input.GetKey(Settings.RollRightKey.Value)) targetKbRoll += 1.0f;

            float rampRate = Mathf.Max(1.0f, Settings.KeyboardSmoothing.Value);
            float dt = Time.deltaTime;
            if (dt <= 0.0001f || dt > 0.1f) dt = 0.016f;

            _smoothedKbThrottle = Mathf.MoveTowards(_smoothedKbThrottle, targetKbThrottle, dt * rampRate * 1.4f);
            _smoothedKbPitch = Mathf.MoveTowards(_smoothedKbPitch, targetKbPitch, dt * rampRate);
            _smoothedKbRoll = Mathf.MoveTowards(_smoothedKbRoll, targetKbRoll, dt * rampRate);
            _smoothedKbYaw = Mathf.MoveTowards(_smoothedKbYaw, targetKbYaw, dt * rampRate);

            if (Settings.Mode.Value == FlightMode.PositionHold)
            {
                float baseThrottle = Mathf.Clamp01(rawThrottle + _smoothedKbThrottle * 0.5f);
                if (!deviceRead)
                {
                    baseThrottle = Mathf.Clamp01(0.5f + _smoothedKbThrottle * 0.5f);
                }
                Throttle = baseThrottle;
            }
            else
            {
                float baseThrottle = Mathf.Clamp01(rawThrottle + _smoothedKbThrottle);
                Throttle = BetaflightRates.CalculateThrottle(
                    baseThrottle,
                    Settings.ThrottleMid.Value,
                    Settings.ThrottleExpo.Value,
                    Settings.ThrottleLimitPercent.Value,
                    Settings.ThrottleLimitMode.Value);
            }

            Pitch = Mathf.Clamp(rawPitch + _smoothedKbPitch, -1.0f, 1.0f);
            Roll = Mathf.Clamp(rawRoll + _smoothedKbRoll, -1.0f, 1.0f);
            Yaw = Mathf.Clamp(rawYaw + _smoothedKbYaw, -1.0f, 1.0f);
        }

        private bool TryReadGamepad(
            Gamepad gamepad,
            out float rawThrottle,
            out float rawPitch,
            out float rawRoll,
            out float rawYaw,
            float deadzone,
            bool isFlightActive)
        {
            if (IsGamepadButtonPressed(gamepad, Settings.GamepadArmBind.Value))
                ArmTogglePressed = true;

            if (isFlightActive)
            {
                if (IsGamepadButtonPressed(gamepad, Settings.GamepadResetBind.Value))
                    ResetPressed = true;
                if (IsGamepadButtonPressed(gamepad, Settings.GamepadTurtleBind.Value))
                    TurtleFlipPressed = true;
                if (IsGamepadButtonPressed(gamepad, Settings.GamepadFlightModeBind.Value))
                    FlightModeTogglePressed = true;
            }

            Vector2 leftStick = gamepad.leftStick.ReadValue();
            Vector2 rightStick = gamepad.rightStick.ReadValue();
            float rightTrigger = gamepad.rightTrigger.ReadValue();

            ProcessSpringCenteredSticks(
                leftStick,
                rightStick,
                rightTrigger,
                deadzone,
                out rawThrottle,
                out rawPitch,
                out rawRoll,
                out rawYaw);

            return true;
        }

        private void UpdateVrButtons(bool isFlightActive, bool isMenuOpen)
        {
            GetXrModules(out CVRXRModule leftMod, out CVRXRModule rightMod);
            bool leftValid = leftMod != null && leftMod.IsValid;
            bool rightValid = rightMod != null && rightMod.IsValid;

            bool leftClickHeld = leftValid && leftMod.Primary2DAxisDown;
            bool rightClickHeld = rightValid && rightMod.Primary2DAxisDown;

            // L3 + R3 simultaneous click toggles Arm / Disarm cleanly without firing single-stick actions
            if (leftClickHeld && rightClickHeld)
            {
                if (!_vrChordConsumed && !isMenuOpen)
                    ArmTogglePressed = true;
                _vrChordConsumed = true;
            }
            else if (!leftClickHeld && !rightClickHeld)
            {
                // Fire single L3 / R3 actions on release only if they were not part of an L3+R3 chord
                if (_prevVrLeftClick && !_vrChordConsumed && !isFlightActive)
                    MenuTogglePressed = true;

                if (_prevVrRightClick && !_vrChordConsumed && isFlightActive && !isMenuOpen)
                    TurtleFlipPressed = true;

                _vrChordConsumed = false;
            }

            _prevVrLeftClick = leftClickHeld;
            _prevVrRightClick = rightClickHeld;

            if (isFlightActive && !isMenuOpen)
            {
                if (rightValid && rightMod.PrimaryButton)
                    ResetPressed = true;

                if (leftValid && leftMod.PrimaryButton)
                    FlightModeTogglePressed = true;
            }
        }

        private bool TryReadVR(
            out float rawThrottle,
            out float rawPitch,
            out float rawRoll,
            out float rawYaw,
            float deadzone)
        {
            rawThrottle = 0.0f;
            rawPitch = 0.0f;
            rawRoll = 0.0f;
            rawYaw = 0.0f;

            GetXrModules(out CVRXRModule leftMod, out CVRXRModule rightMod);
            bool leftValid = leftMod != null && leftMod.IsValid;
            bool rightValid = rightMod != null && rightMod.IsValid;
            if (!leftValid && !rightValid)
                return false;

            Vector2 leftStick = leftValid ? leftMod.Primary2DAxis : Vector2.zero;
            Vector2 rightStick = rightValid ? rightMod.Primary2DAxis : Vector2.zero;
            float rightTrigger = rightValid ? rightMod.Trigger : 0.0f;

            ProcessSpringCenteredSticks(
                leftStick,
                rightStick,
                rightTrigger,
                deadzone,
                out rawThrottle,
                out rawPitch,
                out rawRoll,
                out rawYaw);

            return true;
        }

        private void ProcessSpringCenteredSticks(
            Vector2 leftStick,
            Vector2 rightStick,
            float rightTrigger,
            float deadzone,
            out float rawThrottle,
            out float rawPitch,
            out float rawRoll,
            out float rawYaw)
        {
            MapSticksToAxes(
                leftStick,
                rightStick,
                Settings.ControlStickMode.Value,
                out float yawRaw,
                out float thrRaw,
                out float rollRaw,
                out float pitchRaw);

            // Throttle resolution: RT Trigger or Stick
            if (Settings.GamepadTriggerThrottle.Value)
            {
                rawThrottle = Mathf.Clamp01(rightTrigger);
            }
            else if (Settings.Mode.Value == FlightMode.PositionHold)
            {
                float stickY = ApplyDeadzone(thrRaw, deadzone);
                rawThrottle = 0.5f + stickY * 0.5f;
            }
            else if (Settings.SpringCenteredThrottle.Value)
            {
                float stickY = ApplyDeadzone(thrRaw, deadzone);
                // Center and backward stick rest strictly at 0% throttle
                rawThrottle = stickY > 0.001f ? Mathf.Clamp01(stickY) : 0.0f;
            }
            else
            {
                float normalizedY = (thrRaw + 1.0f) * 0.5f;
                rawThrottle = Mathf.Clamp01(normalizedY);
            }

            rawRoll = ApplyDeadzone(rollRaw, deadzone);
            rawYaw = ApplyDeadzone(yawRaw, deadzone);

            float pitchVal = ApplyDeadzone(pitchRaw, deadzone);
            rawPitch = Settings.InvertPitch.Value ? pitchVal : -pitchVal;
        }

        private bool TryReadRadio(
            out float rawThrottle,
            out float rawPitch,
            out float rawRoll,
            out float rawYaw,
            float deadzone,
            bool isFlightActive)
        {
            rawThrottle = 0.0f;
            rawPitch = 0.0f;
            rawRoll = 0.0f;
            rawYaw = 0.0f;

            float axisRollVal;
            float axisPitchVal;
            float axisThrVal;
            float axisYawVal;

            if (_winmmRadio.IsConnected)
            {
                axisRollVal = ReadWinMMAxis(Settings.RadioRollAxis.Value);
                axisPitchVal = ReadWinMMAxis(Settings.RadioPitchAxis.Value);
                axisThrVal = ReadWinMMAxis(Settings.RadioThrottleAxis.Value);
                axisYawVal = ReadWinMMAxis(Settings.RadioYawAxis.Value);
            }
            else
            {
                Joystick radioJoy = FindRadioTransmitter();
                if (radioJoy == null)
                    return false;

                if (_cachedRadio != radioJoy)
                {
                    CacheRadioControls(radioJoy);
                }

                axisRollVal = _axisRoll?.ReadValue() ?? 0.0f;
                axisPitchVal = _axisPitch?.ReadValue() ?? 0.0f;
                axisThrVal = _axisThrottle?.ReadValue() ?? -1.0f;
                axisYawVal = _axisYaw?.ReadValue() ?? 0.0f;
            }

            if (IsRadioButtonPressed(Settings.RadioArmButton.Value))
                ArmTogglePressed = true;

            if (isFlightActive)
            {
                if (IsRadioButtonPressed(Settings.RadioResetButton.Value))
                    ResetPressed = true;
                if (IsRadioButtonPressed(Settings.RadioTurtleButton.Value))
                    TurtleFlipPressed = true;
                if (IsRadioButtonPressed(Settings.RadioFlightModeButton.Value))
                    FlightModeTogglePressed = true;
            }

            // Mode 2 physical sticks: Left Stick = (Yaw, Throttle), Right Stick = (Roll, Pitch)
            Vector2 leftStick = new Vector2(axisYawVal, axisThrVal);
            Vector2 rightStick = new Vector2(axisRollVal, axisPitchVal);

            MapSticksToAxes(
                leftStick,
                rightStick,
                Settings.ControlStickMode.Value,
                out float yawRaw,
                out float thrRaw,
                out float rollRaw,
                out float pitchRaw);

            if (Settings.Mode.Value == FlightMode.PositionHold)
            {
                float stickY = ApplyDeadzone(thrRaw, deadzone);
                rawThrottle = 0.5f + stickY * 0.5f;
            }
            else
            {
                // Real RC throttle gimbal rests at bottom (-1.0f) = 0% throttle
                float normalizedY = (thrRaw + 1.0f) * 0.5f;
                rawThrottle = Mathf.Clamp01(normalizedY);
            }

            rawRoll = ApplyDeadzone(rollRaw, deadzone);
            rawYaw = ApplyDeadzone(yawRaw, deadzone);

            float pitchVal = ApplyDeadzone(pitchRaw, deadzone);
            rawPitch = Settings.InvertPitch.Value ? pitchVal : -pitchVal;

            return true;
        }

        private bool IsGamepadButtonPressed(Gamepad gp, GamepadButtonBinding bind)
        {
            if (gp == null) return false;
            switch (bind)
            {
                case GamepadButtonBinding.SelectView: return gp.selectButton.wasPressedThisFrame;
                case GamepadButtonBinding.Start: return gp.startButton.wasPressedThisFrame;
                case GamepadButtonBinding.ButtonSouth: return gp.buttonSouth.wasPressedThisFrame;
                case GamepadButtonBinding.ButtonEast: return gp.buttonEast.wasPressedThisFrame;
                case GamepadButtonBinding.ButtonWest: return gp.buttonWest.wasPressedThisFrame;
                case GamepadButtonBinding.ButtonNorth: return gp.buttonNorth.wasPressedThisFrame;
                case GamepadButtonBinding.LeftShoulder: return gp.leftShoulder.wasPressedThisFrame;
                case GamepadButtonBinding.RightShoulder: return gp.rightShoulder.wasPressedThisFrame;
                case GamepadButtonBinding.LeftStickPress: return gp.leftStickButton.wasPressedThisFrame;
                case GamepadButtonBinding.RightStickPress: return gp.rightStickButton.wasPressedThisFrame;
                case GamepadButtonBinding.DpadUp: return gp.dpad.up.wasPressedThisFrame;
                case GamepadButtonBinding.DpadDown: return gp.dpad.down.wasPressedThisFrame;
                case GamepadButtonBinding.DpadLeft: return gp.dpad.left.wasPressedThisFrame;
                case GamepadButtonBinding.DpadRight: return gp.dpad.right.wasPressedThisFrame;
                default: return false;
            }
        }

        private GamepadButtonBinding DetectPressedGamepadButton(Gamepad gp)
        {
            if (gp == null) return GamepadButtonBinding.None;
            if (gp.selectButton.wasPressedThisFrame) return GamepadButtonBinding.SelectView;
            if (gp.startButton.wasPressedThisFrame) return GamepadButtonBinding.Start;
            if (gp.buttonSouth.wasPressedThisFrame) return GamepadButtonBinding.ButtonSouth;
            if (gp.buttonEast.wasPressedThisFrame) return GamepadButtonBinding.ButtonEast;
            if (gp.buttonWest.wasPressedThisFrame) return GamepadButtonBinding.ButtonWest;
            if (gp.buttonNorth.wasPressedThisFrame) return GamepadButtonBinding.ButtonNorth;
            if (gp.leftShoulder.wasPressedThisFrame) return GamepadButtonBinding.LeftShoulder;
            if (gp.rightShoulder.wasPressedThisFrame) return GamepadButtonBinding.RightShoulder;
            if (gp.leftStickButton.wasPressedThisFrame) return GamepadButtonBinding.LeftStickPress;
            if (gp.rightStickButton.wasPressedThisFrame) return GamepadButtonBinding.RightStickPress;
            if (gp.dpad.up.wasPressedThisFrame) return GamepadButtonBinding.DpadUp;
            if (gp.dpad.down.wasPressedThisFrame) return GamepadButtonBinding.DpadDown;
            if (gp.dpad.left.wasPressedThisFrame) return GamepadButtonBinding.DpadLeft;
            if (gp.dpad.right.wasPressedThisFrame) return GamepadButtonBinding.DpadRight;
            return GamepadButtonBinding.None;
        }

        private float ReadWinMMAxis(RadioAxis axis)
        {
            int idx = Mathf.Clamp((int)axis, 0, _winmmRadio.Axes.Length - 1);
            return _winmmRadio.Axes[idx];
        }

        private bool IsRadioButtonPressed(int buttonIndex)
        {
            if (_winmmRadio.IsConnected)
                return _winmmRadio.WasButtonPressed(buttonIndex);

            if (buttonIndex < 0 || buttonIndex >= _cachedRadioButtons.Count)
                return false;
            return _cachedRadioButtons[buttonIndex].wasPressedThisFrame;
        }

        private int DetectPressedRadioButton()
        {
            for (int i = 0; i < _cachedRadioButtons.Count; i++)
            {
                if (_cachedRadioButtons[i].wasPressedThisFrame)
                    return i;
            }
            return -1;
        }

        private void MapSticksToAxes(
            Vector2 leftStick,
            Vector2 rightStick,
            StickMode stickMode,
            out float yawRaw,
            out float thrRaw,
            out float rollRaw,
            out float pitchRaw)
        {
            switch (stickMode)
            {
                case StickMode.Mode1:
                    yawRaw = leftStick.x;
                    pitchRaw = leftStick.y;
                    rollRaw = rightStick.x;
                    thrRaw = rightStick.y;
                    break;
                case StickMode.Mode3:
                    rollRaw = leftStick.x;
                    thrRaw = leftStick.y;
                    yawRaw = rightStick.x;
                    pitchRaw = rightStick.y;
                    break;
                case StickMode.Mode4:
                    rollRaw = leftStick.x;
                    pitchRaw = leftStick.y;
                    yawRaw = rightStick.x;
                    thrRaw = rightStick.y;
                    break;
                case StickMode.Mode2:
                default:
                    yawRaw = leftStick.x;
                    thrRaw = leftStick.y;
                    rollRaw = rightStick.x;
                    pitchRaw = rightStick.y;
                    break;
            }
        }

        private Joystick FindRadioTransmitter()
        {
            if (Joystick.all.Count > 0)
            {
                for (int i = 0; i < Joystick.all.Count; i++)
                {
                    var j = Joystick.all[i];
                    if (j != null && j.added && j.enabled)
                    {
                        return j;
                    }
                }
            }

            if (Joystick.current != null && Joystick.current.added && Joystick.current.enabled)
            {
                return Joystick.current;
            }

            // Search generic HID devices matching recognized RC transmitter brand strings
            foreach (var dev in InputSystem.devices)
            {
                if (dev is Gamepad || dev is Keyboard || dev is Mouse)
                    continue;

                string name = dev.displayName ?? dev.name ?? "";
                if (name.IndexOf("Radio", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("EdgeTX", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("OpenTX", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("FrSky", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("Taranis", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("TX16", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("Boxer", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("Pocket", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("Tango", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("Betaflight", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("Jumper", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    if (dev is Joystick joy)
                    {
                        return joy;
                    }
                }
            }

            return null;
        }

        private void CacheRadioControls(Joystick joy)
        {
            _cachedRadio = joy;
            _axisRoll = null;
            _axisPitch = null;
            _axisThrottle = null;
            _axisYaw = null;
            _cachedRadioAxes.Clear();
            _cachedRadioButtons.Clear();

            if (joy == null) return;

            foreach (var control in joy.allControls)
            {
                if (control is ButtonControl btn)
                {
                    // Skip synthetic direction buttons generated by StickControl / DpadControl
                    if (btn.parent is StickControl || btn.parent is DpadControl)
                        continue;

                    _cachedRadioButtons.Add(btn);
                }
                else if (control is AxisControl axis)
                {
                    if (axis.parent is DpadControl)
                        continue;

                    _cachedRadioAxes.Add(axis);
                }
            }

            // Match by standard HID axis names first
            _axisRoll = joy.stick?.x ?? FindAxisByName(_cachedRadioAxes, "x");
            _axisPitch = joy.stick?.y ?? FindAxisByName(_cachedRadioAxes, "y");
            _axisThrottle = FindAxisByName(_cachedRadioAxes, "z", "slider", "throttle") ?? joy.twist;
            _axisYaw = FindAxisByName(_cachedRadioAxes, "rz", "rotatez", "rx", "ry") ?? joy.twist;

            // Fallback to indexed axes if named controls were not present
            if (_axisRoll == null && _cachedRadioAxes.Count > 0) _axisRoll = _cachedRadioAxes[0];
            if (_axisPitch == null && _cachedRadioAxes.Count > 1) _axisPitch = _cachedRadioAxes[1];
            if (_axisThrottle == null && _cachedRadioAxes.Count > 2) _axisThrottle = _cachedRadioAxes[2];
            if (_axisYaw == null && _cachedRadioAxes.Count > 3) _axisYaw = _cachedRadioAxes[3];

            if (_axisRoll == null && _cachedRadioAxes.Count > 0) _axisRoll = _cachedRadioAxes[0];
            if (_axisPitch == null && _cachedRadioAxes.Count > 0) _axisPitch = _cachedRadioAxes[0];
            if (_axisThrottle == null && _cachedRadioAxes.Count > 0) _axisThrottle = _cachedRadioAxes[0];
            if (_axisYaw == null && _cachedRadioAxes.Count > 0) _axisYaw = _cachedRadioAxes[0];

            LogRadioDiagnostics(joy);
        }

        private static AxisControl FindAxisByName(List<AxisControl> axes, params string[] names)
        {
            for (int n = 0; n < names.Length; n++)
            {
                string target = names[n];
                for (int i = 0; i < axes.Count; i++)
                {
                    if (string.Equals(axes[i].name, target, StringComparison.OrdinalIgnoreCase))
                        return axes[i];
                }
            }
            return null;
        }

        private void LogRadioDiagnostics(Joystick joy)
        {
            try
            {
                string axesDesc = "";
                for (int i = 0; i < _cachedRadioAxes.Count; i++)
                {
                    var a = _cachedRadioAxes[i];
                    if (i > 0) axesDesc += ", ";
                    axesDesc += $"[{i}] {a.name} ({a.layout})={a.ReadValue():F2}";
                }

                MelonLoader.MelonLogger.Msg(
                    $"[FPVDrone] Radio detected: '{joy.displayName}' layout={joy.layout} " +
                    $"iface={joy.description.interfaceName} prod={joy.description.product} " +
                    $"axes={_cachedRadioAxes.Count} [{axesDesc}] buttons={_cachedRadioButtons.Count} " +
                    $"mapped=(R:{_axisRoll?.name ?? "null"}, P:{_axisPitch?.name ?? "null"}, T:{_axisThrottle?.name ?? "null"}, Y:{_axisYaw?.name ?? "null"})");
            }
            catch (Exception ex)
            {
                MelonLoader.MelonLogger.Warning($"[FPVDrone] Radio diagnostic log error: {ex.Message}");
            }
        }

        public string GetRadioAxesSummary()
        {
            if (_winmmRadio.IsConnected)
                return _winmmRadio.GetAxesSummary();

            if (_cachedRadio == null || !_cachedRadio.added)
                return "No RC joystick";

            if (_cachedRadioAxes.Count == 0)
                return "0 axes";

            string summary = "";
            for (int i = 0; i < _cachedRadioAxes.Count; i++)
            {
                if (i > 0) summary += "  ";
                var a = _cachedRadioAxes[i];
                summary += $"{a.name}:{a.ReadValue():F2}";
            }
            return summary;
        }

        public string GetRadioButtonsSummary()
        {
            if (_winmmRadio.IsConnected)
                return _winmmRadio.GetButtonsSummary();

            if (_cachedRadio == null || !_cachedRadio.added)
                return "-";

            string pressed = "";
            for (int i = 0; i < _cachedRadioButtons.Count; i++)
            {
                if (_cachedRadioButtons[i].isPressed)
                {
                    if (pressed.Length > 0) pressed += ",";
                    pressed += i.ToString();
                }
            }
            return pressed.Length > 0
                ? $"{_cachedRadioButtons.Count} total (Pressed: {pressed})"
                : $"{_cachedRadioButtons.Count} total (None pressed)";
        }

        private string GetRadioDisplayName()
        {
            if (_winmmRadio.IsConnected)
            {
                // Prefer friendly InputSystem product name (e.g. "Deviation GamePad") over generic WinMM driver name
                Joystick joy = FindRadioTransmitter();
                if (joy != null)
                {
                    string joyName = joy.displayName ?? joy.name;
                    if (!string.IsNullOrEmpty(joyName))
                        return joyName;
                }
                return _winmmRadio.DeviceName;
            }

            if (_cachedRadio == null) return "RC Radio";
            string name = _cachedRadio.displayName ?? _cachedRadio.name;
            return string.IsNullOrEmpty(name) ? "RC Radio" : name;
        }

        private string GetGamepadDisplayName(Gamepad gamepad)
        {
            if (gamepad == null) return "Gamepad";
            string name = gamepad.displayName ?? gamepad.name;
            if (string.IsNullOrEmpty(name)) name = "Gamepad";
            if (Settings.GamepadTriggerThrottle.Value)
            {
                name += " (RT Throttle)";
            }
            return name;
        }

        private float ApplyThrottleExpo(float inputThrottle, float hoverPoint, float expo)
        {
            if (expo <= 0.001f)
                return inputThrottle;

            expo = Mathf.Clamp(expo, 0.0f, 0.85f);
            hoverPoint = Mathf.Clamp(hoverPoint, 0.05f, 0.50f);

            if (inputThrottle >= hoverPoint)
            {
                float highRange = 1.0f - hoverPoint;
                if (highRange <= 0.001f) return inputThrottle;
                float normalized = (inputThrottle - hoverPoint) / highRange;
                float shaped = Mathf.Pow(normalized, 1.0f + expo * 1.6f);
                return hoverPoint + shaped * highRange;
            }
            else
            {
                if (hoverPoint <= 0.001f) return inputThrottle;
                float normalized = inputThrottle / hoverPoint;
                float shaped = Mathf.Pow(normalized, 1.0f + expo * 1.2f);
                return shaped * hoverPoint;
            }
        }

        private float ApplyDeadzone(float value, float deadzone)
        {
            if (Mathf.Abs(value) < deadzone)
                return 0.0f;

            return Mathf.Sign(value) * ((Mathf.Abs(value) - deadzone) / (1.0f - deadzone));
        }

        public void SetGameInputLocked(bool locked)
        {
            if (_isInputLocked == locked)
                return;

            _isInputLocked = locked;

            try
            {
                if (CVRInputManager._moduleKeyboard?.InputLock != null)
                    CVRInputManager._moduleKeyboard.InputLock.SetLock(InputLockId, locked);

                if (CVRInputManager._moduleMouse?.InputLock != null)
                    CVRInputManager._moduleMouse.InputLock.SetLock(InputLockId, locked);

                if (CVRInputManager._moduleGamepad?.InputLock != null)
                    CVRInputManager._moduleGamepad.InputLock.SetLock(InputLockId, locked);

                if (locked && CVRInputManager.Instance != null)
                {
                    var im = CVRInputManager.Instance;
                    im.movementVector = Vector3.zero;
                    im.lookVector = Vector2.zero;
                    im.rawLookVector = Vector2.zero;
                    im.sectionTurn = 0f;
                    im.floatDirection = 0f;
                    im.rollDirection = 0f;
                    im.jump = false;
                    im.sprint = false;
                }
            }
            catch (Exception ex)
            {
                MelonLoader.MelonLogger.Warning($"[FPVDrone] Error updating input lock: {ex.Message}");
            }
        }

        public void ReleaseAllLocks()
        {
            CancelListeningForBind();
            SetGameInputLocked(false);
            ResetSmoothing();
        }

        public void ResetSmoothing()
        {
            _smoothedKbThrottle = 0.0f;
            _smoothedKbPitch = 0.0f;
            _smoothedKbRoll = 0.0f;
            _smoothedKbYaw = 0.0f;
            Throttle = 0.0f;
            Pitch = 0.0f;
            Roll = 0.0f;
            Yaw = 0.0f;
        }
    }
}
