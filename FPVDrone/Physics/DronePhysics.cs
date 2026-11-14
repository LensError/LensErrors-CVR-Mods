using System;
using UnityEngine;

namespace FPVDrone
{
    public class DronePhysics
    {
        // State variables
        public Vector3 Position { get; set; }
        public Quaternion Rotation { get; set; } = Quaternion.identity;
        public Vector3 LinearVelocity { get; set; }
        public Vector3 AngularVelocity { get; set; } // Radians per second, world space

        // Telemetry
        public float CurrentThrottle01 { get; private set; }
        public float CurrentRpmPercent { get; private set; }
        public float CurrentThrustNewtons { get; private set; }
        public float BatteryVoltage { get; private set; } = 25.2f;
        public float BatteryCurrentAmps { get; private set; }
        public float ConsumedMah { get; private set; }
        public float GForceLoad { get; private set; } = 1.0f;
        public bool IsLimpMode { get; private set; }
        public bool IsInPropWash { get; private set; }
        public bool IsInGroundEffect { get; private set; }
        public bool IsOnGround { get; private set; } = true;
        public bool IsCrashed { get; private set; }
        public bool IsTurtleFlipping { get; private set; }

        public event Action<float> OnCollisionImpact;

        // Individual motor outputs [0: Rear-Right, 1: Front-Right, 2: Rear-Left, 3: Front-Left]
        public float[] MotorThrottles { get; } = new float[4];
        public float[] MotorRpms { get; } = new float[4];
        public Vector3 LocalAngularVelocityDeg => _currentLocalAngularVelDeg;

        public float BellyHeightM => DroneBuilder.EvaluateCurrent().BellyHeightM;

        // Flight controller state
        private Vector3 _currentLocalAngularVelDeg = Vector3.zero;
        private Vector3 _angularRateDerivDeg = Vector3.zero;
        private float _physicsAccumulator = 0.0f;
        private float _physicsTime = 0.0f;
        private float _rpmSmoothed = 0.0f;
        private float _crashTimer = 0.0f;
        private float _airborneStableTimer = 0.0f;
        private float _turtleFlipTimer = 0.0f;

        // Position & Altitude Hold State (DJI N-Mode)
        private float _targetHoldAltitude = 0.0f;
        private bool _isAltitudeHolding = false;
        private Vector2 _targetHoldPos2D = Vector2.zero;
        private bool _isPosHolding2D = false;

        // Physical constants
        public const float AirDensity = 1.225f; // kg/m^3
        public const float AirModeIdleThrottle = 0.045f; // 4.5% idle motor spin
        public const float ThrustExponent = 1.85f;
        public const float FixedSubstepDt = 0.004f; // 250 Hz Betaflight physics loop

        // Reusable corner buffer for multi-point floor contact
        private readonly Vector3[] _contactPointsLocal = new Vector3[8];

        // Layer mask for world collisions (Excludes UI, PlayerClone, UIInternal)
        public static readonly int WorldCollisionMask = ~((1 << 5) | (1 << 9) | (1 << 15));

        public static float CalculateHoverThrottle(float twr)
        {
            twr = Mathf.Max(1.0f, twr);
            return Mathf.Pow(1.0f / twr, 1.0f / ThrustExponent);
        }

        public void Reset(Vector3 spawnPosition, Quaternion spawnRotation)
        {
            DroneHardwareSpec hw = DroneBuilder.EvaluateCurrent();

            Position = spawnPosition;
            Rotation = spawnRotation;
            LinearVelocity = Vector3.zero;
            AngularVelocity = Vector3.zero;
            _currentLocalAngularVelDeg = Vector3.zero;
            _angularRateDerivDeg = Vector3.zero;
            _physicsAccumulator = 0.0f;
            _rpmSmoothed = 0.0f;
            for (int i = 0; i < 4; i++)
            {
                MotorThrottles[i] = 0.0f;
                MotorRpms[i] = 0.0f;
            }
            _crashTimer = 0.0f;
            _airborneStableTimer = 0.0f;
            _turtleFlipTimer = 0.0f;
            _isAltitudeHolding = false;
            _isPosHolding2D = false;
            IsCrashed = false;
            IsTurtleFlipping = false;
            IsOnGround = true;
            IsLimpMode = false;
            ConsumedMah = 0.0f;
            GForceLoad = 1.0f;
            BatteryVoltage = hw.FullVoltage;

            SnapToFloor();
            _isAltitudeHolding = false;
            _isPosHolding2D = false;
        }

        public void SnapToFloor()
        {
            DroneHardwareSpec hw = DroneBuilder.EvaluateCurrent();

            if (Physics.Raycast(Position + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 5.0f, WorldCollisionMask, QueryTriggerInteraction.Ignore))
            {
                Position = new Vector3(Position.x, hit.point.y + hw.BellyHeightM, Position.z);
                Vector3 forwardFlat = Vector3.ProjectOnPlane(Rotation * Vector3.forward, hit.normal).normalized;
                if (forwardFlat.sqrMagnitude > 0.001f)
                {
                    Rotation = Quaternion.LookRotation(forwardFlat, hit.normal);
                }
                IsOnGround = true;
                LinearVelocity = Vector3.zero;
                AngularVelocity = Vector3.zero;
                _currentLocalAngularVelDeg = Vector3.zero;
                _angularRateDerivDeg = Vector3.zero;
            }
        }

        private void SettleDisarmedOnGround(DroneHardwareSpec hw, float dt)
        {
            if (Physics.Raycast(Position + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 5.0f, WorldCollisionMask, QueryTriggerInteraction.Ignore))
            {
                Vector3 groundNormal = hit.normal;
                Vector3 quadUp = Rotation * Vector3.up;
                float dotUp = Vector3.Dot(quadUp, groundNormal);

                Quaternion targetRot;
                float targetFloorY;

                if (dotUp > 0.35f)
                {
                    targetRot = Quaternion.FromToRotation(quadUp, groundNormal) * Rotation;
                    targetFloorY = hit.point.y + hw.BellyHeightM;
                }
                else
                {
                    targetRot = Quaternion.FromToRotation(quadUp, -groundNormal) * Rotation;
                    targetFloorY = hit.point.y + hw.TopHeightM;
                    IsCrashed = true;
                }

                Rotation = Quaternion.Slerp(Rotation, targetRot, dt * 10.0f);
                Position = new Vector3(Position.x, Mathf.MoveTowards(Position.y, targetFloorY, dt * 6.0f), Position.z);
                IsOnGround = true;
            }
        }

        public void StepPhysics(
            float dt,
            bool isArmed,
            float rawThrottle,
            float pitchStick,
            float rollStick,
            float yawStick)
        {
            if (dt <= 0.0001f || dt > 0.1f)
                dt = 0.016f;

            DroneHardwareSpec hw = DroneBuilder.EvaluateCurrent();

            if (IsTurtleFlipping)
            {
                UpdateTurtleFlip(hw, dt);
                return;
            }

            if (!isArmed)
            {
                LinearVelocity = Vector3.zero;
                AngularVelocity = Vector3.zero;
                _currentLocalAngularVelDeg = Vector3.zero;
                _angularRateDerivDeg = Vector3.zero;
                _physicsAccumulator = 0.0f;
                _rpmSmoothed = Mathf.Lerp(_rpmSmoothed, 0.0f, dt * 12.0f);
                for (int i = 0; i < 4; i++)
                {
                    MotorThrottles[i] = Mathf.Lerp(MotorThrottles[i], 0.0f, dt * 14.0f);
                    MotorRpms[i] = Mathf.Lerp(MotorRpms[i], 0.0f, dt * 14.0f);
                }
                CurrentThrottle01 = 0.0f;
                CurrentRpmPercent = 0.0f;
                CurrentThrustNewtons = 0.0f;
                BatteryCurrentAmps = 0.2f;
                BatteryVoltage = Mathf.Lerp(BatteryVoltage, hw.FullVoltage, dt * 2.0f);
                _isAltitudeHolding = false;
                _isPosHolding2D = false;

                SettleDisarmedOnGround(hw, dt);
                return;
            }

            _physicsAccumulator += Mathf.Min(dt, 0.050f);
            while (_physicsAccumulator >= FixedSubstepDt)
            {
                _physicsTime += FixedSubstepDt;
                SubstepPhysics(hw, FixedSubstepDt, rawThrottle, pitchStick, rollStick, yawStick);
                _physicsAccumulator -= FixedSubstepDt;
            }

            CheckCrashStatus(dt);
        }

        private void SubstepPhysics(
            DroneHardwareSpec hw,
            float subDt,
            float rawThrottle,
            float pitchStick,
            float rollStick,
            float yawStick)
        {
            float mass = hw.MassKg;
            float gravityForceMag = mass * 9.81f;

            float effectiveThrottle = Mathf.Clamp(rawThrottle, AirModeIdleThrottle, 1.0f);
            CurrentThrottle01 = effectiveThrottle;

            UpdateBattery(hw, subDt, effectiveThrottle);

            Vector3 localLinearVel = Quaternion.Inverse(Rotation) * LinearVelocity;
            float forwardSpeed = Mathf.Max(0.0f, localLinearVel.z);

            Vector3 targetRatesDeg = ComputeRateTargets(hw, pitchStick, rollStick, yawStick, forwardSpeed);

            ComputeMotorDemands(ref targetRatesDeg, effectiveThrottle, subDt, hw, out float motorSaturationThrustFactor);

            ComputeAeroCoefficients(hw, forwardSpeed, out float groundEffectMultiplier, out float propWashThrustLoss, out Vector3 propWashBuffetAcc, out float axialInflowFactor, out float etlLiftMultiplier);

            ComputeThrust(hw, rawThrottle, subDt, mass, gravityForceMag, motorSaturationThrustFactor, etlLiftMultiplier, axialInflowFactor, groundEffectMultiplier, propWashThrustLoss);

            if (IsOnGround)
            {
                ApplyGroundReactionAndSettling(hw, subDt, mass, gravityForceMag);
            }

            IntegrateAttitude(hw, targetRatesDeg, propWashBuffetAcc, subDt);

            IntegrateVelocityAndPosition(subDt, hw, mass, gravityForceMag);
        }

        private void ApplyGroundReactionAndSettling(DroneHardwareSpec hw, float subDt, float mass, float gravityForceMag)
        {
            if (Physics.Raycast(Position + Vector3.up * 0.20f, Vector3.down, out RaycastHit floorHit, hw.HalfSpanM + 0.40f, WorldCollisionMask, QueryTriggerInteraction.Ignore))
            {
                Vector3 groundNormal = floorHit.normal;
                Vector3 quadUp = Rotation * Vector3.up;
                float dotUp = Vector3.Dot(quadUp, groundNormal);

                if (CurrentThrustNewtons < gravityForceMag * 0.98f)
                {
                    float vN = Vector3.Dot(LinearVelocity, groundNormal);
                    if (vN < 0.0f)
                    {
                        LinearVelocity -= groundNormal * vN;
                    }

                    float groundFriction = Mathf.Exp(-6.0f * subDt);
                    LinearVelocity = new Vector3(LinearVelocity.x * groundFriction, LinearVelocity.y, LinearVelocity.z * groundFriction);

                    if (dotUp > 0.42f)
                    {
                        Vector3 tiltAxis = Vector3.Cross(quadUp, groundNormal);
                        Vector3 settleTorqueWorld = tiltAxis * (18.0f * gravityForceMag * hw.FrameRadiusM) - AngularVelocity * (8.0f * hw.InertiaTensor.x);
                        Vector3 settleTorqueLocal = Quaternion.Inverse(Rotation) * settleTorqueWorld;

                        _currentLocalAngularVelDeg.x += (settleTorqueLocal.x / Mathf.Max(1e-5f, hw.InertiaTensor.x)) * subDt * Mathf.Rad2Deg;
                        _currentLocalAngularVelDeg.z += (settleTorqueLocal.z / Mathf.Max(1e-5f, hw.InertiaTensor.z)) * subDt * Mathf.Rad2Deg;

                        _currentLocalAngularVelDeg.x *= Mathf.Exp(-10.0f * subDt);
                        _currentLocalAngularVelDeg.z *= Mathf.Exp(-10.0f * subDt);
                        _angularRateDerivDeg.x *= Mathf.Exp(-12.0f * subDt);
                        _angularRateDerivDeg.z *= Mathf.Exp(-12.0f * subDt);
                    }
                    else
                    {
                        Vector3 tipAxis = Vector3.Cross(quadUp, -groundNormal);
                        Vector3 tipTorqueWorld = tipAxis * (12.0f * gravityForceMag * hw.FrameRadiusM);
                        Vector3 tipTorqueLocal = Quaternion.Inverse(Rotation) * tipTorqueWorld;
                        _currentLocalAngularVelDeg.x += (tipTorqueLocal.x / Mathf.Max(1e-5f, hw.InertiaTensor.x)) * subDt * Mathf.Rad2Deg;
                        _currentLocalAngularVelDeg.z += (tipTorqueLocal.z / Mathf.Max(1e-5f, hw.InertiaTensor.z)) * subDt * Mathf.Rad2Deg;
                    }
                }
                else
                {
                    if (Vector3.Dot(LinearVelocity, groundNormal) > 0.05f)
                    {
                        IsOnGround = false;
                    }
                }
            }
        }

        private void UpdateBattery(DroneHardwareSpec hw, float subDt, float effectiveThrottle)
        {
            float motorSpoolAlpha = 1.0f - Mathf.Exp(-subDt / hw.MotorSpoolTau);
            _rpmSmoothed = Mathf.Lerp(_rpmSmoothed, effectiveThrottle, motorSpoolAlpha);
            CurrentRpmPercent = _rpmSmoothed * 100.0f;

            // Electrical current scales roughly with mechanical power (~RPM^2.6 from idle to peak)
            float idleCurrent = 0.5f + hw.BatteryCells * 0.18f;
            BatteryCurrentAmps = Mathf.Lerp(idleCurrent, hw.PeakCurrentAmps, Mathf.Pow(_rpmSmoothed, 2.2f));

            float voltageSag = Settings.EnableVoltageSag.Value ? (BatteryCurrentAmps * hw.InternalResistanceOhms) : 0.0f;

            float dtHours = subDt / 3600.0f;
            ConsumedMah += BatteryCurrentAmps * dtHours * 1000.0f;

            if (Settings.SimulateBatteryDepletion.Value)
            {
                float totalCap = Mathf.Max(200.0f, hw.BatteryCapacityMah);
                float usedRatio = ConsumedMah / totalCap;
                float cells = hw.BatteryCells;
                float restingPerCell;

                if (usedRatio <= 0.80f)
                    restingPerCell = Mathf.Lerp(4.20f, 3.70f, usedRatio / 0.80f);
                else if (usedRatio <= 0.95f)
                    restingPerCell = Mathf.Lerp(3.70f, 3.50f, (usedRatio - 0.80f) / 0.15f);
                else if (usedRatio <= 1.0f)
                    restingPerCell = Mathf.Lerp(3.50f, 3.30f, (usedRatio - 0.95f) / 0.05f);
                else
                    restingPerCell = Mathf.Lerp(3.30f, 3.00f, Mathf.Clamp01((usedRatio - 1.0f) / 0.15f));

                float restingVoltage = restingPerCell * cells;
                BatteryVoltage = Mathf.Max(2.75f * cells, restingVoltage - voltageSag);
                IsLimpMode = usedRatio >= 1.0f;
            }
            else
            {
                BatteryVoltage = Mathf.Max(3.0f * hw.BatteryCells, hw.FullVoltage - voltageSag);
                IsLimpMode = false;
            }
        }

        private void GetPitchRollAngles(out float currentPitch, out float currentRoll)
        {
            Vector3 forward = Rotation * Vector3.forward;
            Vector3 right = Rotation * Vector3.right;

            currentPitch = -Mathf.Asin(Mathf.Clamp(forward.y, -0.999f, 0.999f)) * Mathf.Rad2Deg;
            currentRoll = -Mathf.Asin(Mathf.Clamp(right.y, -0.999f, 0.999f)) * Mathf.Rad2Deg;
        }

        private Vector3 ComputeRateTargets(DroneHardwareSpec hw, float pitchStick, float rollStick, float yawStick, float forwardSpeed)
        {
            float cmdPitchRate;
            float cmdRollRate;
            float cmdYawRate = BetaflightRates.EvaluateAxisRate(yawStick, 2);

            bool isPosHold = Settings.Mode.Value == FlightMode.PositionHold;
            bool isAngle = Settings.Mode.Value == FlightMode.Angle;

            if (isPosHold)
            {
                GetPitchRollAngles(out float currentPitch, out float currentRoll);

                bool sticksNeutral = Mathf.Abs(pitchStick) < 0.06f && Mathf.Abs(rollStick) < 0.06f;
                float targetPitchAngle;
                float targetRollAngle;

                if (sticksNeutral)
                {
                    if (!_isPosHolding2D)
                    {
                        _targetHoldPos2D = new Vector2(Position.x, Position.z);
                        _isPosHolding2D = true;
                    }

                    Vector2 currentPos2D = new Vector2(Position.x, Position.z);
                    Vector2 posError = _targetHoldPos2D - currentPos2D;
                    Vector2 targetVel2D = Vector2.ClampMagnitude(posError * 2.2f, 4.5f);
                    Vector2 velError = targetVel2D - new Vector2(LinearVelocity.x, LinearVelocity.z);

                    Vector3 forwardFlat = Vector3.ProjectOnPlane(Rotation * Vector3.forward, Vector3.up).normalized;
                    Vector3 rightFlat = Vector3.ProjectOnPlane(Rotation * Vector3.right, Vector3.up).normalized;

                    float fwdVelErr = Vector2.Dot(velError, new Vector2(forwardFlat.x, forwardFlat.z));
                    float rgtVelErr = Vector2.Dot(velError, new Vector2(rightFlat.x, rightFlat.z));

                    targetPitchAngle = Mathf.Clamp(fwdVelErr * 4.0f, -22.0f, 22.0f);
                    targetRollAngle = Mathf.Clamp(rgtVelErr * 4.0f, -22.0f, 22.0f);
                }
                else
                {
                    _isPosHolding2D = false;
                    targetPitchAngle = pitchStick * 25.0f;
                    targetRollAngle = rollStick * 25.0f;
                }

                float kLevel = 8.5f;
                cmdPitchRate = Mathf.Clamp((targetPitchAngle - currentPitch) * kLevel, -350.0f, 350.0f);
                cmdRollRate = Mathf.Clamp((targetRollAngle - currentRoll) * kLevel, -350.0f, 350.0f);
            }
            else if (isAngle)
            {
                GetPitchRollAngles(out float currentPitch, out float currentRoll);

                float maxAngle = Mathf.Clamp(Settings.MaxAngleDegrees.Value, 15.0f, 85.0f);
                float targetPitchAngle = pitchStick * maxAngle;
                float targetRollAngle = rollStick * maxAngle;

                float kLevel = 8.5f;
                cmdPitchRate = Mathf.Clamp((targetPitchAngle - currentPitch) * kLevel, -450.0f, 450.0f);
                cmdRollRate = Mathf.Clamp((targetRollAngle - currentRoll) * kLevel, -450.0f, 450.0f);
            }
            else
            {
                cmdPitchRate = BetaflightRates.EvaluateAxisRate(pitchStick, 1);
                cmdRollRate = BetaflightRates.EvaluateAxisRate(rollStick, 0);
            }

            float bodyPitchRate = cmdPitchRate;
            float bodyYawRate = cmdYawRate;
            float bodyRollRate = cmdRollRate;

            if (!isPosHold && !isAngle && Settings.EnableCameraAngleMix.Value && Mathf.Abs(Settings.CameraTiltAngle.Value) > 0.1f)
            {
                float camTiltRad = Settings.CameraTiltAngle.Value * Mathf.Deg2Rad;
                float cosAngle = Mathf.Cos(camTiltRad);
                float sinAngle = Mathf.Sin(camTiltRad);

                bodyYawRate = cmdYawRate * cosAngle - cmdRollRate * sinAngle;
                bodyRollRate = cmdRollRate * cosAngle + cmdYawRate * sinAngle;
            }

            Vector3 targetRatesDeg = new Vector3(
                bodyPitchRate,
                bodyYawRate,
                -bodyRollRate
            );

            // Subtle high-speed inflow pitch-up (PID I-term rejects most of it in Acro)
            if (Settings.EnableTranslationalLift.Value && !isAngle && !isPosHold)
            {
                float pitchUpMomentDeg = Mathf.Clamp(0.5f * AirDensity * forwardSpeed * forwardSpeed * hw.DragTangential * 0.25f, 0.0f, 8.0f);
                targetRatesDeg.x -= pitchUpMomentDeg;
            }

            return targetRatesDeg;
        }

        private void ComputeMotorDemands(
            ref Vector3 targetRatesDeg,
            float effectiveThrottle,
            float subDt,
            DroneHardwareSpec hw,
            out float motorSaturationThrustFactor)
        {
            float pNorm = targetRatesDeg.x / 1200.0f;
            float rNorm = (-targetRatesDeg.z) / 1200.0f;
            float yNorm = targetRatesDeg.y / 1200.0f;
            bool isPosHold = Settings.Mode.Value == FlightMode.PositionHold;
            float tNorm = isPosHold ? _rpmSmoothed : effectiveThrottle;

            float m1Demand = tNorm + pNorm - rNorm - yNorm;
            float m2Demand = tNorm - pNorm - rNorm + yNorm;
            float m3Demand = tNorm + pNorm + rNorm + yNorm;
            float m4Demand = tNorm - pNorm + rNorm - yNorm;

            motorSaturationThrustFactor = 1.0f;
            if (Settings.EnableMotorSaturation.Value)
            {
                float maxDemand = Mathf.Max(Mathf.Max(m1Demand, m2Demand), Mathf.Max(m3Demand, m4Demand));
                if (maxDemand > 1.0f)
                {
                    float saturationExcess = maxDemand - 1.0f;
                    motorSaturationThrustFactor = Mathf.Max(0.82f, 1.0f - saturationExcess * 0.32f);
                    targetRatesDeg.y *= Mathf.Max(0.40f, 1.0f - saturationExcess * 0.60f);
                }
            }

            float motorSpoolAlphaIndiv = 1.0f - Mathf.Exp(-subDt / hw.MotorSpoolTau);
            float voltageRatio = Mathf.Clamp(BatteryVoltage / Mathf.Max(1.0f, hw.FullVoltage), 0.65f, 1.05f);
            float maxLiveRpm = hw.MaxLoadedRpm * voltageRatio;

            float[] demands = { m1Demand, m2Demand, m3Demand, m4Demand };
            for (int i = 0; i < 4; i++)
            {
                MotorThrottles[i] = Mathf.Lerp(MotorThrottles[i], Mathf.Clamp01(demands[i]), motorSpoolAlphaIndiv);
                // Motor RPM scales roughly as throttle^0.925 (consistent with Thrust ~ throttle^1.85 ~ RPM^2)
                float rpmNorm = Mathf.Pow(Mathf.Clamp01(MotorThrottles[i]), 0.925f);
                MotorRpms[i] = rpmNorm * maxLiveRpm;
            }
        }

        private void ComputeAeroCoefficients(
            DroneHardwareSpec hw,
            float forwardSpeed,
            out float groundEffectMultiplier,
            out float propWashThrustLoss,
            out Vector3 propWashBuffetAcc,
            out float axialInflowFactor,
            out float etlLiftMultiplier)
        {
            float groundAltitude = CheckGroundAltitude(hw);
            IsInGroundEffect = groundAltitude > 0.0f && groundAltitude < hw.GroundEffectMaxAltM;
            groundEffectMultiplier = 1.0f;
            if (IsInGroundEffect)
            {
                float groundCushion = Mathf.Clamp01((hw.GroundEffectMaxAltM - groundAltitude) / hw.GroundEffectMaxAltM);
                groundEffectMultiplier += groundCushion * 0.28f * Settings.GroundEffectStrength.Value;
            }

            float bodyAxialSpeed = Vector3.Dot(LinearVelocity, Rotation * Vector3.up);
            float bodyEdgewiseSpeed = Vector3.ProjectOnPlane(LinearVelocity, Rotation * Vector3.up).magnitude;
            IsInPropWash = !IsOnGround && bodyAxialSpeed < -1.8f && bodyEdgewiseSpeed < 3.5f && _rpmSmoothed > 0.10f;

            propWashThrustLoss = 0.0f;
            propWashBuffetAcc = Vector3.zero;
            if (IsInPropWash)
            {
                float propWashSeverity = Mathf.Clamp01((-bodyAxialSpeed - 1.8f) / 3.5f) * Settings.PropWashStrength.Value;
                propWashThrustLoss = propWashSeverity * 0.22f;

                float timeOsc = _physicsTime * 22.0f;
                float buffetX = (Mathf.Sin(timeOsc) + Mathf.Sin(timeOsc * 1.73f)) * 48.0f * propWashSeverity;
                float buffetZ = (Mathf.Cos(timeOsc * 1.31f) + Mathf.Cos(timeOsc * 2.11f)) * 48.0f * propWashSeverity;
                propWashBuffetAcc = new Vector3(buffetX, 0f, buffetZ);
            }

            axialInflowFactor = 1.0f;
            if (bodyAxialSpeed > 0.0f && hw.PitchSpeedMaxMps > 1.0f)
            {
                axialInflowFactor = Mathf.Clamp01(1.0f - (bodyAxialSpeed / hw.PitchSpeedMaxMps));
            }

            etlLiftMultiplier = 1.0f;
            if (Settings.EnableTranslationalLift.Value)
            {
                float etlFactor = Mathf.Clamp01(forwardSpeed / 22.0f);
                etlLiftMultiplier = 1.0f + etlFactor * 0.18f;
            }
        }

        private void ComputeThrust(
            DroneHardwareSpec hw,
            float rawThrottle,
            float subDt,
            float mass,
            float gravityForceMag,
            float motorSaturationThrustFactor,
            float etlLiftMultiplier,
            float axialInflowFactor,
            float groundEffectMultiplier,
            float propWashThrustLoss)
        {
            float voltageRatio = Mathf.Clamp(BatteryVoltage / Mathf.Max(1.0f, hw.FullVoltage), 0.65f, 1.05f);
            float maxThrustNewtons = hw.MaxThrustNewtons * (voltageRatio * voltageRatio);

            bool isPosHold = Settings.Mode.Value == FlightMode.PositionHold;
            float rawThrust;

            if (isPosHold)
            {
                bool throttleNeutral = Mathf.Abs(rawThrottle - 0.5f) < 0.05f;
                float targetClimbSpeed;

                if (throttleNeutral)
                {
                    if (!_isAltitudeHolding)
                    {
                        _targetHoldAltitude = Position.y;
                        _isAltitudeHolding = true;
                    }
                    float altError = _targetHoldAltitude - Position.y;
                    targetClimbSpeed = Mathf.Clamp(altError * 2.5f, -3.5f, 3.5f);
                }
                else
                {
                    _isAltitudeHolding = false;
                    if (rawThrottle > 0.55f)
                        targetClimbSpeed = ((rawThrottle - 0.55f) / 0.45f) * 5.0f;
                    else
                        targetClimbSpeed = ((rawThrottle - 0.45f) / 0.45f) * 4.0f;
                }

                float vyError = targetClimbSpeed - LinearVelocity.y;
                float targetAccY = vyError * 5.5f + 9.81f;

                Vector3 quadUp = Rotation * Vector3.up;
                float cosTilt = Mathf.Max(0.35f, quadUp.y);
                float neededThrust = (mass * Mathf.Max(0.0f, targetAccY)) / cosTilt;

                rawThrust = Mathf.Clamp(neededThrust, 0.0f, maxThrustNewtons * 0.95f);

                if (IsOnGround && rawThrottle < 0.25f)
                {
                    rawThrust = 0.0f;
                    _isAltitudeHolding = false;
                }

                float equivalentThrottle = Mathf.Clamp01(Mathf.Pow(rawThrust / Mathf.Max(0.01f, maxThrustNewtons), 1.0f / ThrustExponent));
                _rpmSmoothed = Mathf.Lerp(_rpmSmoothed, equivalentThrottle, subDt * 16.0f);
                CurrentRpmPercent = _rpmSmoothed * 100.0f;
            }
            else
            {
                rawThrust = Mathf.Pow(_rpmSmoothed, ThrustExponent) * maxThrustNewtons;
                rawThrust *= axialInflowFactor;
                rawThrust *= groundEffectMultiplier;
                rawThrust *= (1.0f - propWashThrustLoss);
                rawThrust *= etlLiftMultiplier;
                rawThrust *= motorSaturationThrustFactor;
            }

            if (IsLimpMode)
            {
                rawThrust = Mathf.Min(rawThrust, maxThrustNewtons * 0.38f);
            }

            CurrentThrustNewtons = rawThrust;
        }

        private void IntegrateAttitude(DroneHardwareSpec hw, Vector3 targetRatesDeg, Vector3 propWashBuffetAcc, float subDt)
        {
            float wnRP = hw.RollPitchWn;
            float zetaRP = hw.RollPitchZeta;
            float errPitch = targetRatesDeg.x - _currentLocalAngularVelDeg.x;
            float errRoll = targetRatesDeg.z - _currentLocalAngularVelDeg.z;

            float accPitch = wnRP * wnRP * errPitch - 2.0f * zetaRP * wnRP * _angularRateDerivDeg.x + propWashBuffetAcc.x;
            float accRoll = wnRP * wnRP * errRoll - 2.0f * zetaRP * wnRP * _angularRateDerivDeg.z + propWashBuffetAcc.z;

            float wnYaw = hw.YawWn;
            float zetaYaw = 1.0f;
            float errYaw = targetRatesDeg.y - _currentLocalAngularVelDeg.y;
            float accYaw = wnYaw * wnYaw * errYaw - 2.0f * zetaYaw * wnYaw * _angularRateDerivDeg.y;

            _angularRateDerivDeg.x += accPitch * subDt;
            _angularRateDerivDeg.y += accYaw * subDt;
            _angularRateDerivDeg.z += accRoll * subDt;

            _currentLocalAngularVelDeg += _angularRateDerivDeg * subDt;

            Vector3 localAngularVelRad = _currentLocalAngularVelDeg * Mathf.Deg2Rad;
            AngularVelocity = Rotation * localAngularVelRad;

            float angleRad = localAngularVelRad.magnitude;
            if (angleRad > 0.0001f)
            {
                Vector3 axis = localAngularVelRad / angleRad;
                Rotation = Rotation * Quaternion.AngleAxis(angleRad * Mathf.Rad2Deg * subDt, axis);
            }
        }

        private void IntegrateVelocityAndPosition(float subDt, DroneHardwareSpec hw, float mass, float gravityForceMag)
        {
            Vector3 thrustForce = (Rotation * Vector3.up) * CurrentThrustNewtons;
            Vector3 gravityForce = Vector3.down * gravityForceMag;

            Vector3 discNormal = Rotation * Vector3.up;
            Vector3 edgewiseVel = Vector3.ProjectOnPlane(LinearVelocity, discNormal);
            Vector3 hForce = -edgewiseVel * (_rpmSmoothed * hw.LinearRotorDrag * mass * 9.81f);

            Vector3 localLinearVel = Quaternion.Inverse(Rotation) * LinearVelocity;
            float normalDragForce = -0.5f * AirDensity * hw.DragNormal * localLinearVel.y * Mathf.Abs(localLinearVel.y);
            float tanDragX = -0.5f * AirDensity * hw.DragLateral * localLinearVel.x * Mathf.Abs(localLinearVel.x);
            float tanDragZ = -0.5f * AirDensity * hw.DragTangential * localLinearVel.z * Mathf.Abs(localLinearVel.z);
            Vector3 worldParasiticDrag = Rotation * new Vector3(tanDragX, normalDragForce, tanDragZ);

            Vector3 totalForce = thrustForce + gravityForce + hForce + worldParasiticDrag;
            Vector3 linearAcc = totalForce / mass;

            GForceLoad = (thrustForce + hForce + worldParasiticDrag).magnitude / gravityForceMag;

            LinearVelocity += linearAcc * subDt;

            ApplyMovementWithCollision(hw, subDt);
        }

        private void ApplyRigidBodyImpulse(DroneHardwareSpec hw, Vector3 contactOffsetWorld, Vector3 normal, float restitution, float frictionMu)
        {
            Vector3 vPoint = LinearVelocity + Vector3.Cross(AngularVelocity, contactOffsetWorld);
            float vN = Vector3.Dot(vPoint, normal);
            if (vN >= 0.0f)
                return;

            // World-space inverse inertia effect for this contact arm
            Vector3 rCrossNLocal = Quaternion.Inverse(Rotation) * Vector3.Cross(contactOffsetWorld, normal);
            Vector3 invIRCrossNLocal = new Vector3(
                rCrossNLocal.x / Mathf.Max(1e-6f, hw.InertiaTensor.x),
                rCrossNLocal.y / Mathf.Max(1e-6f, hw.InertiaTensor.y),
                rCrossNLocal.z / Mathf.Max(1e-6f, hw.InertiaTensor.z)
            );
            Vector3 rotTerm = Vector3.Cross(Rotation * invIRCrossNLocal, contactOffsetWorld);
            float denom = (1.0f / hw.MassKg) + Vector3.Dot(normal, rotTerm);

            float jN = -(1.0f + restitution) * vN / Mathf.Max(1e-5f, denom);
            Vector3 impulseN = normal * jN;

            LinearVelocity += impulseN / hw.MassKg;
            Vector3 dOmegaLocal = Quaternion.Inverse(Rotation) * Vector3.Cross(contactOffsetWorld, impulseN);
            dOmegaLocal = new Vector3(
                dOmegaLocal.x / Mathf.Max(1e-6f, hw.InertiaTensor.x),
                dOmegaLocal.y / Mathf.Max(1e-6f, hw.InertiaTensor.y),
                dOmegaLocal.z / Mathf.Max(1e-6f, hw.InertiaTensor.z)
            );
            // Damp rotational kick slightly to account for prop/frame flex
            _currentLocalAngularVelDeg += dOmegaLocal * (Mathf.Rad2Deg * 0.55f);
            AngularVelocity = Rotation * (_currentLocalAngularVelDeg * Mathf.Deg2Rad);

            // Tangential Coulomb friction
            Vector3 vTang = vPoint - normal * vN;
            float vTangMag = vTang.magnitude;
            if (vTangMag > 0.01f)
            {
                float maxFriction = frictionMu * jN / hw.MassKg;
                float dvTang = Mathf.Min(vTangMag, maxFriction);
                LinearVelocity -= (vTang / vTangMag) * dvTang;
            }
        }

        private void ApplyMovementWithCollision(DroneHardwareSpec hw, float dt)
        {
            Vector3 deltaMove = LinearVelocity * dt;

            // Step 1: Swept oriented BoxCast for obstacles & walls (flat quadcopter profile, not a giant sphere)
            float moveDist = deltaMove.magnitude;
            if (moveDist > 0.0001f)
            {
                Vector3 direction = deltaMove / moveDist;
                Vector3 boxHalfExtents = new Vector3(
                    hw.HalfSpanM * 0.90f,
                    (hw.BellyHeightM + hw.TopHeightM) * 0.45f,
                    hw.HalfSpanM * 0.90f
                );

                if (Physics.BoxCast(Position, boxHalfExtents, direction, out RaycastHit hit, Rotation, moveDist, WorldCollisionMask, QueryTriggerInteraction.Ignore))
                {
                    if (hit.distance > 0.0001f)
                    {
                        Position += direction * hit.distance + hit.normal * 0.003f;
                    }
                    else
                    {
                        Position += hit.normal * 0.004f;
                    }

                    Vector3 normal = hit.normal;
                    float vNorm = Vector3.Dot(LinearVelocity, normal);
                    if (vNorm < 0.0f)
                    {
                        if (-vNorm > 1.5f)
                        {
                            OnCollisionImpact?.Invoke(-vNorm);
                        }

                        Vector3 contactOffset = hit.point != Vector3.zero
                            ? (hit.point - Position)
                            : (-direction * hw.HalfSpanM);

                        ApplyRigidBodyImpulse(hw, contactOffset, normal, hw.CollisionRestitution, 0.25f);

                        if (-vNorm > 4.5f && !hw.IsDucted)
                        {
                            IsCrashed = true;
                        }
                    }
                }
                else
                {
                    Position += deltaMove;
                }
            }

            // Step 2: Multi-point flat-frame floor & surface contact (4 skid/top pads + 4 arm tips when tilted)
            Vector3 quadUp = Rotation * Vector3.up;
            float dotUp = Vector3.Dot(quadUp, Vector3.up);
            float padY = dotUp >= 0.0f ? -hw.BellyHeightM : hw.TopHeightM;

            _contactPointsLocal[0] = new Vector3(-hw.ArmHalfXY, padY, -hw.ArmHalfXY);
            _contactPointsLocal[1] = new Vector3( hw.ArmHalfXY, padY, -hw.ArmHalfXY);
            _contactPointsLocal[2] = new Vector3(-hw.ArmHalfXY, padY,  hw.ArmHalfXY);
            _contactPointsLocal[3] = new Vector3( hw.ArmHalfXY, padY,  hw.ArmHalfXY);

            int pointCount = 4;
            if (Mathf.Abs(dotUp) < 0.85f)
            {
                float tipXY = hw.HalfSpanM * 0.70710678f;
                _contactPointsLocal[4] = new Vector3(-tipXY, 0.0f, -tipXY);
                _contactPointsLocal[5] = new Vector3( tipXY, 0.0f, -tipXY);
                _contactPointsLocal[6] = new Vector3(-tipXY, 0.0f,  tipXY);
                _contactPointsLocal[7] = new Vector3( tipXY, 0.0f,  tipXY);
                pointCount = 8;
            }

            float maxPenetration = 0.0f;
            Vector3 deepestOffsetWorld = Vector3.zero;
            Vector3 deepestNormal = Vector3.up;
            bool anyFloorContact = false;

            for (int i = 0; i < pointCount; i++)
            {
                Vector3 offsetWorld = Rotation * _contactPointsLocal[i];
                Vector3 worldPt = Position + offsetWorld;

                if (Physics.Raycast(worldPt + Vector3.up * 0.14f, Vector3.down, out RaycastHit floorHit, 0.22f, WorldCollisionMask, QueryTriggerInteraction.Ignore))
                {
                    float pen = floorHit.point.y - worldPt.y;
                    if (pen >= -0.008f)
                    {
                        anyFloorContact = true;
                        if (pen > maxPenetration)
                        {
                            maxPenetration = pen;
                            deepestOffsetWorld = offsetWorld;
                            deepestNormal = floorHit.normal;
                        }
                    }
                }
            }

            if (maxPenetration > 0.0f)
            {
                Position += Vector3.up * maxPenetration;

                float vN = Vector3.Dot(LinearVelocity + Vector3.Cross(AngularVelocity, deepestOffsetWorld), deepestNormal);
                if (vN < 0.0f)
                {
                    if (-vN > 1.8f)
                    {
                        OnCollisionImpact?.Invoke(-vN);
                    }
                    if (-vN > 6.0f && !hw.IsDucted)
                    {
                        IsCrashed = true;
                    }

                    float bounceThreshold = 0.28f;
                    float effRestitution = (-vN * hw.CollisionRestitution < bounceThreshold) ? 0.0f : hw.CollisionRestitution;
                    ApplyRigidBodyImpulse(hw, deepestOffsetWorld, deepestNormal, effRestitution, 0.45f);
                    IsOnGround = effRestitution == 0.0f;
                }
                else
                {
                    IsOnGround = anyFloorContact && CurrentThrustNewtons < hw.MassKg * 9.81f * 1.02f;
                }
            }
            else if (!anyFloorContact)
            {
                IsOnGround = false;
            }
        }

        private float CheckGroundAltitude(DroneHardwareSpec hw)
        {
            if (Physics.Raycast(Position + Vector3.up * 0.1f, Vector3.down, out RaycastHit hit, 3.0f, WorldCollisionMask, QueryTriggerInteraction.Ignore))
            {
                return Mathf.Max(0.0f, hit.distance - 0.1f - hw.BellyHeightM);
            }
            return -1.0f;
        }

        private void CheckCrashStatus(float dt)
        {
            bool isUpsideDown = Vector3.Dot(Rotation * Vector3.up, Vector3.up) < 0.15f;
            bool isStationary = LinearVelocity.sqrMagnitude < 0.3f && IsOnGround;

            if (!isUpsideDown)
            {
                if (!IsOnGround && CurrentThrottle01 > 0.08f)
                {
                    _airborneStableTimer += dt;
                    if (_airborneStableTimer > 0.6f)
                    {
                        IsCrashed = false;
                    }
                }
                else if (isStationary)
                {
                    IsCrashed = false;
                    _airborneStableTimer = 0.0f;
                }
            }
            else
            {
                _airborneStableTimer = 0.0f;
            }

            if (isUpsideDown && isStationary)
            {
                _crashTimer += dt;
                if (_crashTimer >= Settings.AutoFlipDelay.Value)
                {
                    TriggerTurtleFlip();
                }
            }
            else
            {
                _crashTimer = Mathf.Max(0.0f, _crashTimer - dt * 2.0f);
            }
        }

        public void TriggerTurtleFlip()
        {
            if (!IsOnGround && !IsCrashed)
                return;

            IsTurtleFlipping = true;
            _turtleFlipTimer = 0.0f;
            _crashTimer = 0.0f;
            _airborneStableTimer = 0.0f;
            _angularRateDerivDeg = Vector3.zero;
            IsCrashed = false;
            IsOnGround = false;
        }

        private void UpdateTurtleFlip(DroneHardwareSpec hw, float dt)
        {
            _turtleFlipTimer += dt;

            if (_turtleFlipTimer < 0.14f)
            {
                LinearVelocity = Vector3.up * 2.0f;
                MotorThrottles[0] = 0.75f;
                MotorThrottles[1] = 0.75f;
                MotorThrottles[2] = 0.0f;
                MotorThrottles[3] = 0.0f;
            }
            else if (_turtleFlipTimer < 0.45f)
            {
                Vector3 forwardFlat = Vector3.ProjectOnPlane(Rotation * Vector3.forward, Vector3.up).normalized;
                if (forwardFlat.sqrMagnitude < 0.01f) forwardFlat = Vector3.forward;
                Quaternion targetUpright = Quaternion.LookRotation(forwardFlat, Vector3.up);
                Rotation = Quaternion.Slerp(Rotation, targetUpright, dt * 14.0f);
                LinearVelocity += Vector3.down * (9.81f * dt);
                MotorThrottles[0] = 0.50f;
                MotorThrottles[1] = 0.50f;
                MotorThrottles[2] = 0.0f;
                MotorThrottles[3] = 0.0f;
            }
            else
            {
                IsTurtleFlipping = false;
                AngularVelocity = Vector3.zero;
                _currentLocalAngularVelDeg = Vector3.zero;
                LinearVelocity = Vector3.zero;
                for (int i = 0; i < 4; i++)
                {
                    MotorThrottles[i] = 0.0f;
                    MotorRpms[i] = 0.0f;
                }
                SnapToFloor();
            }

            ApplyMovementWithCollision(hw, dt);
        }
    }
}
