using System;
using UnityEngine;

namespace FPVDrone
{
    public class DroneOSD
    {
        private GUIStyle _osdStyle;
        private GUIStyle _centerStyle;
        private GUIStyle _warningStyle;
        private GUIStyle _horizonStyle;
        private GUIStyle _stickLabelStyle;
        private Texture2D _staticNoiseTexture;
        private Texture2D _pixelTexture;
        private Color32[] _noiseColorBuffer;
        private int _lastNoiseFrame = -1;

        private float _flightTimer = 0.0f;
        private float _consumedMah = 0.0f;
        private float _peakSpeedKmh = 0.0f;
        private float _peakAmps = 0.0f;
        private float _maxAltM = 0.0f;
        private float _maxG = 1.0f;
        private float _summaryTimer = 0.0f;
        private string _summaryText = "";

        public void Initialize()
        {
            _pixelTexture = new Texture2D(1, 1);
            _pixelTexture.SetPixel(0, 0, Color.white);
            _pixelTexture.Apply();

            // Procedural analog static snow texture
            int size = 128;
            _staticNoiseTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            _noiseColorBuffer = new Color32[size * size];
            UpdateStaticTexture(1.0f);
        }

        public void ResetTimer()
        {
            _flightTimer = 0.0f;
            _consumedMah = 0.0f;
            _peakSpeedKmh = 0.0f;
            _peakAmps = 0.0f;
            _maxAltM = 0.0f;
            _maxG = 1.0f;
            _summaryTimer = 0.0f;
        }

        public void UpdateTimer(float dt, float consumedMah)
        {
            _flightTimer += dt;
            _consumedMah = consumedMah;
        }

        public void UpdateFlightStats(float dt, DronePhysics physics)
        {
            _flightTimer += dt;
            _consumedMah = physics.ConsumedMah;
            _peakSpeedKmh = Mathf.Max(_peakSpeedKmh, physics.LinearVelocity.magnitude * 3.6f);
            _peakAmps = Mathf.Max(_peakAmps, physics.BatteryCurrentAmps);
            _maxAltM = Mathf.Max(_maxAltM, Mathf.Max(0.0f, physics.Position.y));
            _maxG = Mathf.Max(_maxG, physics.GForceLoad);
        }

        public void NotifyDisarmSummary(float consumedMah)
        {
            _consumedMah = consumedMah;
            if (_flightTimer < 1.5f)
                return;

            TimeSpan ts = TimeSpan.FromSeconds(_flightTimer);
            _summaryText = $"FLIGHT SUMMARY  |  TIME {ts.Minutes:D2}:{ts.Seconds:D2}  |  TOP {_peakSpeedKmh:F0} KM/H  |  ALT {_maxAltM:F1}M  |  PEAK {_peakAmps:F0}A  |  {_maxG:F1}G  |  USED {_consumedMah:F0}mAh";
            _summaryTimer = 5.5f;
            MelonLoader.MelonLogger.Msg("[FPVDrone] " + _summaryText);
        }

        public void TickPostFlightSummary(float dt)
        {
            if (_summaryTimer > 0.0f)
                _summaryTimer = Mathf.Max(0.0f, _summaryTimer - dt);
        }

        public void DrawPostFlightSummary()
        {
            if (_summaryTimer <= 0.0f || string.IsNullOrEmpty(_summaryText))
                return;

            EnsureStyles();
            float alpha = Mathf.Clamp01(_summaryTimer);
            GUI.color = new Color(0.92f, 0.96f, 0.92f, alpha);
            float w = 780f;
            GUI.Label(new Rect((Screen.width - w) * 0.5f, 36f, w, 32f), _summaryText, _centerStyle);
            GUI.color = Color.white;
        }

        public void DrawOSD(
            DronePhysics physics,
            DroneVTXSimulation vtx,
            DroneInput input,
            bool isArmed,
            float customWidth = -1f,
            float customHeight = -1f,
            bool skipStatic = false)
        {
            EnsureStyles();

            float w = customWidth > 0f ? customWidth : Screen.width;
            float h = customHeight > 0f ? customHeight : Screen.height;

            // Apply VTX voltage sag flicker to GUI color
            Color baseColor = new Color(0.92f, 0.95f, 0.92f, 0.95f) * (skipStatic ? 1.0f : vtx.VoltageFlickerFactor);
            GUI.color = baseColor;

            // 1. TOP LEFT: VTX & Signal Telemetry
            if (Settings.OsdShowHeader.Value)
            {
                string buildLabel = !string.IsNullOrWhiteSpace(DroneProfileStore.CustomNameDraft)
                    ? DroneProfileStore.CustomNameDraft.Trim().ToUpper()
                    : $"{Settings.FrameWheelbaseMm.Value:F0}MM {Settings.PropDiameterInches.Value:F1}\" {Settings.BatteryCells.Value}S";
                string signalBars = GetSignalBars(vtx.LinkQualityPercent);
                string vtxHeader = $"VTX: R8 5880MHz  PWR: {Settings.VtxPowerMw.Value:F0}mW  [{buildLabel}]\n" +
                                   $"RSSI: {vtx.RssiDbm:F0}dBm  LQ: {vtx.LinkQualityPercent}%  {signalBars}  |  RC: {input.ActiveDeviceName} [{Settings.ControlStickMode.Value}]";
                GUI.Label(new Rect(35f, 30f, 700f, 50f), vtxHeader, _osdStyle);
            }

            // 2. TOP RIGHT: Flight Timers
            float cellVolt = physics.BatteryVoltage / Mathf.Max(1, Settings.BatteryCells.Value);
            if (Settings.OsdShowTimers.Value)
            {
                TimeSpan timeSpan = TimeSpan.FromSeconds(_flightTimer);
                string timerText = $"FLY: {timeSpan.Minutes:D2}:{timeSpan.Seconds:D2}.{timeSpan.Milliseconds / 100:D1}\n" +
                                   $"VOLT: {physics.BatteryVoltage:F1}V ({cellVolt:F2}V/c)";
                GUI.Label(new Rect(w - 270f, 30f, 235f, 50f), timerText, _osdStyle);
            }

            // 3. CENTER: Artificial Horizon & Crosshairs
            if (Settings.OsdShowHorizon.Value || Settings.OsdShowCrosshair.Value)
            {
                DrawArtificialHorizon(w, h, physics.Rotation);
            }

            // Center Flight Mode / Status Flag
            string activeModeName = Settings.Mode.Value == FlightMode.PositionHold ? "DJI / POS HOLD" :
                                    (Settings.Mode.Value == FlightMode.Angle) ? "ANGLE / LEVEL" : "ACRO / AIRMODE";
            string modeText = !isArmed ? "DISARMED" :
                              physics.IsTurtleFlipping ? "TURTLE FLIP" :
                              physics.IsCrashed ? "CRASHED" :
                              physics.IsOnGround ? "ARMED / GROUND" : activeModeName;
            GUI.Label(new Rect((w - 200f) * 0.5f, h * 0.62f, 200f, 30f), modeText, _centerStyle);

            // Dynamic warnings (Limp Mode / Prop wash / Ground cushion)
            if (physics.IsLimpMode)
            {
                GUI.Label(new Rect((w - 300f) * 0.5f, h * 0.67f, 300f, 30f), "[ LOW BATT - LIMP MODE ]", _warningStyle);
            }
            else if (Settings.ShowDebugOsd.Value)
            {
                if (physics.IsInPropWash)
                {
                    GUI.Label(new Rect((w - 300f) * 0.5f, h * 0.67f, 300f, 30f), "[ PROP WASH VORTEX ]", _warningStyle);
                }
                else if (physics.IsInGroundEffect)
                {
                    GUI.Label(new Rect((w - 300f) * 0.5f, h * 0.67f, 300f, 30f), "[ GROUND CUSHION ]", _osdStyle);
                }
            }

            // 4. BOTTOM LEFT: Battery & Power Draw
            bool isVoltageSagWarning = cellVolt < 3.55f && (Time.time % 0.4f < 0.2f);
            if (Settings.OsdShowTimers.Value)
            {
                GUIStyle battStyle = isVoltageSagWarning ? _warningStyle : _osdStyle;
                string battText = $"BATT: {physics.BatteryVoltage:F1}V ({cellVolt:F2}V)  CURR: {physics.BatteryCurrentAmps:F1}A\n" +
                                  $"USED: {_consumedMah:F0}mAh  G-LOAD: {physics.GForceLoad:F1}G";
                GUI.Label(new Rect(35f, h - 80f, 400f, 50f), battText, battStyle);

                // 5. BOTTOM RIGHT: Speed, Altitude, Throttle
                float speedKmh = physics.LinearVelocity.magnitude * 3.6f;
                float alt = Mathf.Max(0.0f, physics.Position.y);
                int throttleBars = Mathf.RoundToInt(physics.CurrentThrottle01 * 10.0f);
                string thrVisual = "[" + new string('=', throttleBars) + new string(' ', 10 - throttleBars) + "]";
                string flightData = $"SPEED: {speedKmh:F0} KM/H  ALT: {alt:F1}M\n" +
                                    $"THR: {physics.CurrentThrottle01 * 100f:F0}% {thrVisual}";
                GUI.Label(new Rect(w - 340f, h - 80f, 300f, 50f), flightData, _osdStyle);
            }

            // 6. MIDDLE RIGHT: Virtual Stick Gimbal Overlay
            DrawVirtualStickOverlay(w, h, input);

            // 7. ANALOG VTX STATIC OVERLAY
            if (!skipStatic && vtx.StaticIntensity01 > 0.01f)
            {
                DrawVtxStatic(w, h, vtx.StaticIntensity01, vtx.IsSignalLost);
            }

            GUI.color = Color.white;
        }

        private void DrawVirtualStickOverlay(float screenW, float screenH, DroneInput input)
        {
            if (input == null || !Settings.ShowStickOverlay.Value)
                return;

            float boxSize = 64f;
            float spacing = 10f;
            float totalW = boxSize * 2f + spacing;
            float totalH = boxSize + 22f;

            // Defaulted to middle right edge
            float startX = screenW - totalW - 25f;
            float startY = (screenH - totalH) * 0.5f;

            string leftLabel = "THR / YAW";
            string rightLabel = "PITCH / ROLL";

            switch (Settings.ControlStickMode.Value)
            {
                case StickMode.Mode1:
                    leftLabel = "PITCH / YAW";
                    rightLabel = "THR / ROLL";
                    break;
                case StickMode.Mode3:
                    leftLabel = "THR / ROLL";
                    rightLabel = "PITCH / YAW";
                    break;
                case StickMode.Mode4:
                    leftLabel = "PITCH / ROLL";
                    rightLabel = "THR / YAW";
                    break;
                case StickMode.Mode2:
                default:
                    leftLabel = (Settings.GamepadTriggerThrottle.Value && input.ActiveDeviceName.IndexOf("Gamepad", StringComparison.OrdinalIgnoreCase) >= 0)
                        ? "RT / YAW"
                        : "THR / YAW";
                    rightLabel = "PITCH / ROLL";
                    break;
            }

            // Draw header showing active RC device and mode
            string deviceLabel = $"{input.ActiveDeviceName} [{Settings.ControlStickMode.Value}]";
            GUI.Label(new Rect(startX, startY - 18f, totalW + 20f, 18f), deviceLabel, _stickLabelStyle);

            // Draw Left Gimbal
            Rect leftBox = new Rect(startX, startY, boxSize, boxSize);
            DrawGimbalBox(leftBox, leftLabel);

            float yawX = leftBox.x + (boxSize * 0.5f) + input.Yaw * (boxSize * 0.44f);
            float thrY = leftBox.y + boxSize - 4f - input.Throttle * (boxSize - 8f);
            DrawStickDot(yawX, thrY);

            // Draw Right Gimbal
            Rect rightBox = new Rect(startX + boxSize + spacing, startY, boxSize, boxSize);
            DrawGimbalBox(rightBox, rightLabel);

            float rollX = rightBox.x + (boxSize * 0.5f) + input.Roll * (boxSize * 0.44f);
            float pitchY = rightBox.y + (boxSize * 0.5f) - input.Pitch * (boxSize * 0.44f);
            DrawStickDot(rollX, pitchY);
        }

        private void DrawGimbalBox(Rect rect, string label)
        {
            // Background
            GUI.color = new Color(0.04f, 0.07f, 0.05f, 0.55f);
            GUI.DrawTexture(rect, _pixelTexture);

            // Border
            GUI.color = new Color(0.35f, 0.65f, 0.45f, 0.7f);
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 1.5f), _pixelTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.y + rect.height - 1.5f, rect.width, 1.5f), _pixelTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.y, 1.5f, rect.height), _pixelTexture);
            GUI.DrawTexture(new Rect(rect.x + rect.width - 1.5f, rect.y, 1.5f, rect.height), _pixelTexture);

            // Subtle center crosshairs
            GUI.color = new Color(0.4f, 0.5f, 0.45f, 0.28f);
            float cx = rect.x + rect.width * 0.5f;
            float cy = rect.y + rect.height * 0.5f;
            GUI.DrawTexture(new Rect(cx - 0.5f, rect.y + 4f, 1f, rect.height - 8f), _pixelTexture);
            GUI.DrawTexture(new Rect(rect.x + 4f, cy - 0.5f, rect.width - 8f, 1f), _pixelTexture);

            // Label below gimbal
            GUI.color = new Color(0.7f, 0.85f, 0.75f, 0.85f);
            GUI.Label(new Rect(rect.x - 10f, rect.y + rect.height + 2f, rect.width + 20f, 16f), label, _stickLabelStyle);
        }

        private void DrawStickDot(float cx, float cy)
        {
            float dotSize = 8f;
            Rect dotRect = new Rect(cx - dotSize * 0.5f, cy - dotSize * 0.5f, dotSize, dotSize);

            // Outer ring
            GUI.color = new Color(0.1f, 0.35f, 0.2f, 0.85f);
            GUI.DrawTexture(new Rect(dotRect.x - 1f, dotRect.y - 1f, dotSize + 2f, dotSize + 2f), _pixelTexture);

            // Bright green dot core
            GUI.color = new Color(0.35f, 1.0f, 0.55f, 0.95f);
            GUI.DrawTexture(dotRect, _pixelTexture);
        }

        private void DrawArtificialHorizon(float screenW, float screenH, Quaternion droneRotation)
        {
            // The FPV camera has mechanical uptilt relative to the drone body
            Quaternion camRot = droneRotation * Quaternion.Euler(-Settings.CameraTiltAngle.Value, 0.0f, 0.0f);
            Vector3 camRight = camRot * Vector3.right;
            Vector3 camForward = camRot * Vector3.forward;

            // Camera roll relative to world horizon:
            // When right wing dips down, camRight.y < 0.
            // In 2D GUI coordinates (positive is clockwise), rotating by horizonRollDeg (negative)
            // rotates counter-clockwise, keeping the artificial horizon parallel to the real world horizon!
            float horizonRollDeg = Mathf.Atan2(camRight.y, Mathf.Sqrt(camRight.x * camRight.x + camRight.z * camRight.z)) * Mathf.Rad2Deg;

            // Pitch angle of the camera line of sight (negative = looking up, positive = looking down)
            float camPitchDeg = -Mathf.Asin(Mathf.Clamp(camForward.y, -0.999f, 0.999f)) * Mathf.Rad2Deg;

            float cx = screenW * 0.5f;
            float cy = screenH * 0.5f;

            // Center crosshair
            if (Settings.OsdShowCrosshair.Value)
            {
                GUI.Label(new Rect(cx - 30f, cy - 15f, 60f, 30f), "-- + --", _centerStyle);
            }

            if (!Settings.OsdShowHorizon.Value)
                return;

            // In camera view: when looking down (camPitchDeg > 0), the horizon rises towards top of screen (smaller GUI Y)
            // When looking up (camPitchDeg < 0), the horizon drops towards bottom of screen (larger GUI Y)
            float pitchOffset = -camPitchDeg * 2.8f;
            float lineY = cy + pitchOffset;

            if (lineY > cy - 160f && lineY < cy + 160f)
            {
                GUIUtility.RotateAroundPivot(horizonRollDeg, new Vector2(cx, cy));
                GUI.Label(new Rect(cx - 50f, lineY - 10f, 100f, 20f), "---   ---", _horizonStyle);
                GUIUtility.RotateAroundPivot(-horizonRollDeg, new Vector2(cx, cy));
            }
        }

        private void DrawVtxStatic(float w, float h, float intensity, bool signalLost)
        {
            UpdateStaticTexture(intensity);

            GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01(intensity * 0.85f));
            GUI.DrawTexture(new Rect(0, 0, w, h), _staticNoiseTexture, ScaleMode.StretchToFill);

            // Procedural analog tear line
            float tearY = (Time.time * 650f) % h;
            float tearHeight = UnityEngine.Random.Range(10f, 40f) * intensity;
            GUI.color = new Color(0.8f, 0.8f, 0.8f, intensity * 0.9f);
            GUI.DrawTexture(new Rect(0, tearY, w, tearHeight), _pixelTexture);

            if (signalLost)
            {
                GUI.color = Color.red;
                GUI.Label(new Rect((w - 200f) * 0.5f, (h - 40f) * 0.5f, 200f, 40f), "NO SIGNAL", _warningStyle);
            }
        }

        private void UpdateStaticTexture(float intensity)
        {
            if (_staticNoiseTexture == null || _noiseColorBuffer == null)
                return;

            if (Time.frameCount == _lastNoiseFrame)
                return;
            _lastNoiseFrame = Time.frameCount;

            byte alpha = (byte)Mathf.Clamp(intensity * 255f, 0f, 255f);
            for (int i = 0; i < _noiseColorBuffer.Length; i++)
            {
                byte val = (byte)UnityEngine.Random.Range(0, 256);
                _noiseColorBuffer[i] = new Color32(val, val, val, (byte)((val * alpha) >> 8));
            }
            _staticNoiseTexture.SetPixels32(_noiseColorBuffer);
            _staticNoiseTexture.Apply(false);
        }

        private string GetSignalBars(int lq)
        {
            if (lq >= 85) return "[||||]";
            if (lq >= 60) return "[||| ]";
            if (lq >= 35) return "[||  ]";
            if (lq >= 15) return "[|   ]";
            return "[    ]";
        }

        private void EnsureStyles()
        {
            if (_osdStyle != null)
                return;

            Font monoFont = Font.CreateDynamicFontFromOSFont(new[] { "Consolas", "Courier New", "Lucida Console" }, 14);

            _osdStyle = new GUIStyle(GUI.skin.label)
            {
                font = monoFont,
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.88f, 0.98f, 0.88f, 0.95f) }
            };

            _centerStyle = new GUIStyle(_osdStyle)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 15,
                normal = { textColor = new Color(0.75f, 1.0f, 0.75f, 0.95f) }
            };

            _horizonStyle = new GUIStyle(_centerStyle)
            {
                fontSize = 16,
                normal = { textColor = new Color(0.9f, 0.9f, 0.9f, 0.6f) }
            };

            _warningStyle = new GUIStyle(_centerStyle)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1.0f, 0.35f, 0.25f, 0.95f) }
            };

            _stickLabelStyle = new GUIStyle(GUI.skin.label)
            {
                font = monoFont,
                fontSize = 9,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.65f, 0.85f, 0.72f, 0.85f) }
            };
        }

        public void Destroy()
        {
            if (_pixelTexture != null)
            {
                UnityEngine.Object.Destroy(_pixelTexture);
                _pixelTexture = null;
            }
            if (_staticNoiseTexture != null)
            {
                UnityEngine.Object.Destroy(_staticNoiseTexture);
                _staticNoiseTexture = null;
            }
        }
    }
}

