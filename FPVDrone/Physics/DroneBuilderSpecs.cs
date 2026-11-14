using UnityEngine;

namespace FPVDrone
{
    public struct MotorSizePreset
    {
        public string Name;
        public float StatorDiameterMm;
        public float StatorHeightMm;
        public float[] PopularKvs;
    }

    public struct LipoPreset
    {
        public string Label;
        public int Cells;
        public float CapacityMah;
    }

    public struct DroneHardwareSpec
    {
        // Geometry (1 Unity unit = 1 meter)
        public float WheelbaseMm;
        public float FrameRadiusM;
        public float ArmHalfXY;
        public float PropDiameterM;
        public float PropPitchM;
        public int PropBlades;
        public bool IsDucted;
        public float HalfSpanM;
        public float BellyHeightM;
        public float TopHeightM;
        public float CollisionRestitution;

        // Mass & Inertia
        public float MassKg;
        public Vector3 InertiaTensor; // Diagonal (Ixx, Iyy, Izz) in kg*m^2

        // Aerodynamics
        public float DragNormal;
        public float DragTangential;
        public float DragLateral;
        public float LinearRotorDrag;
        public float GroundEffectMaxAltM;
        public float PropCt;
        public float PropCp;

        // Propulsion & Electrical
        public float MotorKv;
        public float StatorDiameterMm;
        public float StatorHeightMm;
        public int BatteryCells;
        public float BatteryCapacityMah;
        public float EscAmperage;
        public float FullVoltage;
        public float NominalVoltage;
        public float EmptyVoltage;
        public float InternalResistanceOhms;

        // Derived Performance at Full Battery
        public float MaxLoadedRpm;
        public float MaxThrustNewtons;
        public float ThrustToWeightRatio;
        public float HoverThrottle01;
        public float PeakCurrentAmps;
        public float PitchSpeedMaxMps;
        public float MotorSpoolTau;
        public float RollPitchWn;
        public float YawWn;
        public float RollPitchZeta;
        public bool IsEscLimited;
        public bool IsUnderTorqued;

        // Audio
        public float MaxBladePassF0;
        public float AudioMinDistance;
        public float AudioMaxDistance;
    }

    public static class DroneBuilder
    {
        public static readonly float[] PopularFrameWheelbasesMm =
        {
            65f, 75f, 85f, 120f, 150f, 200f, 225f, 260f, 300f, 400f
        };

        public static readonly float[] PopularPropInches =
        {
            1.2f, 1.6f, 2.0f, 2.5f, 3.0f, 3.5f, 4.0f, 5.0f, 5.1f, 6.0f, 7.0f, 8.0f, 10.0f
        };

        public static readonly float[] PopularEscAmps =
        {
            5f, 12f, 20f, 35f, 45f, 50f, 60f, 80f
        };

        public static readonly MotorSizePreset[] PopularMotors =
        {
            new MotorSizePreset { Name = "0802 (Tiny Whoop)", StatorDiameterMm = 8f, StatorHeightMm = 2f, PopularKvs = new[] { 19000f, 23000f, 27000f } },
            new MotorSizePreset { Name = "1103 (75-85mm)", StatorDiameterMm = 11f, StatorHeightMm = 3f, PopularKvs = new[] { 8000f, 11000f, 13500f } },
            new MotorSizePreset { Name = "1404 (2.5-3\" Micro)", StatorDiameterMm = 14f, StatorHeightMm = 4f, PopularKvs = new[] { 2800f, 3800f, 4600f } },
            new MotorSizePreset { Name = "2004 (3.5\" Cinewhoop)", StatorDiameterMm = 20f, StatorHeightMm = 4f, PopularKvs = new[] { 1700f, 2400f, 3000f } },
            new MotorSizePreset { Name = "2207 (5\" Freestyle)", StatorDiameterMm = 22f, StatorHeightMm = 7f, PopularKvs = new[] { 1750f, 1850f, 1950f, 2450f, 2750f } },
            new MotorSizePreset { Name = "2306 (5\" Smooth)", StatorDiameterMm = 23f, StatorHeightMm = 6f, PopularKvs = new[] { 1750f, 1950f, 2450f } },
            new MotorSizePreset { Name = "2806.5 (7\" Long Range)", StatorDiameterMm = 28f, StatorHeightMm = 6.5f, PopularKvs = new[] { 1300f, 1500f, 1700f } },
            new MotorSizePreset { Name = "3115 (8-10\" Cinelifter)", StatorDiameterMm = 31f, StatorHeightMm = 15f, PopularKvs = new[] { 900f, 1050f } }
        };

        public static readonly LipoPreset[] PopularLipos =
        {
            new LipoPreset { Label = "1S 300mAh", Cells = 1, CapacityMah = 300f },
            new LipoPreset { Label = "1S 450mAh", Cells = 1, CapacityMah = 450f },
            new LipoPreset { Label = "2S 450mAh", Cells = 2, CapacityMah = 450f },
            new LipoPreset { Label = "3S 850mAh", Cells = 3, CapacityMah = 850f },
            new LipoPreset { Label = "4S 850mAh", Cells = 4, CapacityMah = 850f },
            new LipoPreset { Label = "4S 1300mAh", Cells = 4, CapacityMah = 1300f },
            new LipoPreset { Label = "4S 1550mAh", Cells = 4, CapacityMah = 1550f },
            new LipoPreset { Label = "6S 1100mAh", Cells = 6, CapacityMah = 1100f },
            new LipoPreset { Label = "6S 1300mAh", Cells = 6, CapacityMah = 1300f },
            new LipoPreset { Label = "6S 1550mAh", Cells = 6, CapacityMah = 1550f },
            new LipoPreset { Label = "6S 3000mAh", Cells = 6, CapacityMah = 3000f },
            new LipoPreset { Label = "6S 4000mAh", Cells = 6, CapacityMah = 4000f }
        };

        /// <summary>
        /// Maximum propeller diameter (inches) that physically fits an X-frame of given wheelbase without blade collision.
        /// Adjacent motor spacing on an X-frame is wheelbase / sqrt(2).
        /// </summary>
        public static float MaxPropInchesForFrame(float wheelbaseMm, bool isDucted)
        {
            float adjMotorDistMm = wheelbaseMm * 0.70710678f;
            float clearanceMm = (isDucted ? 9.5f : 8.0f) + wheelbaseMm * 0.04f;
            float maxPropMm = Mathf.Max(25.4f, adjMotorDistMm - clearanceMm);
            return maxPropMm / 25.4f;
        }

        public static float ClampPropToFrame(float propInches, float wheelbaseMm, bool isDucted)
        {
            float maxInches = MaxPropInchesForFrame(wheelbaseMm, isDucted);
            return Mathf.Clamp(propInches, 1.0f, maxInches);
        }

        public static void EnforceGeometryConstraints()
        {
            float clampedProp = ClampPropToFrame(Settings.PropDiameterInches.Value, Settings.FrameWheelbaseMm.Value, Settings.IsDuctedFrame.Value);
            // Snap down to largest popular prop if very close or just clamp cleanly
            if (Settings.PropDiameterInches.Value > clampedProp + 0.001f)
            {
                float bestPopular = 1.0f;
                for (int i = 0; i < PopularPropInches.Length; i++)
                {
                    if (PopularPropInches[i] <= clampedProp + 0.001f)
                        bestPopular = PopularPropInches[i];
                }
                Settings.PropDiameterInches.Value = Mathf.Min(clampedProp, bestPopular);
            }
            if (Settings.PropPitchInches.Value > Settings.PropDiameterInches.Value * 1.15f)
            {
                Settings.PropPitchInches.Value = Mathf.Round(Settings.PropDiameterInches.Value * 1.15f * 10f) * 0.1f;
            }
        }

        public static float EstimateMassKg(
            float wheelbaseMm,
            bool isDucted,
            float propInches,
            float statorDiamMm,
            float statorHeightMm,
            int batteryCells,
            float batteryCapMah)
        {
            // Frame carbon/plastic + ducts
            float frameGrams = Mathf.Pow(wheelbaseMm / 225f, 2.0f) * (isDucted ? 155f : 115f);
            frameGrams = Mathf.Max(5.5f, frameGrams);

            // 4x brushless motors (stator volume + bell/shaft)
            float statorVol = statorDiamMm * statorDiamMm * statorHeightMm;
            float fourMotorsGrams = 4f * (1.1f + (statorVol / 3388f) * 32.0f);

            // 4x props + FC/ESC/VTX/RX stack + camera/hardware
            float propsGrams = 4f * Mathf.Pow(propInches / 5.1f, 2.3f) * 4.0f;
            float electronicsGrams = Mathf.Clamp(wheelbaseMm * 0.28f, 7.5f, 90f);
            float payloadAllowance = wheelbaseMm >= 180f ? Mathf.Clamp((wheelbaseMm - 160f) * 0.9f, 20f, 160f) : 0f;

            // LiPo pack (~165 Wh/kg energy density + wrapper/leads)
            float wattHours = batteryCells * 3.7f * (batteryCapMah * 0.001f);
            float batteryGrams = (wattHours / 0.165f) + 2.5f + batteryCells * 1.5f;

            float totalGrams = frameGrams + fourMotorsGrams + propsGrams + electronicsGrams + payloadAllowance + batteryGrams;
            return Mathf.Clamp(Mathf.Round(totalGrams) * 0.001f, 0.020f, 4.500f);
        }

        public static DroneHardwareSpec EvaluateCurrent()
        {
            float massKg = Settings.QuadMassKg.Value;
            if (Settings.AutoEstimateMass.Value)
            {
                massKg = EstimateMassKg(
                    Settings.FrameWheelbaseMm.Value,
                    Settings.IsDuctedFrame.Value,
                    Settings.PropDiameterInches.Value,
                    Settings.MotorStatorDiameterMm.Value,
                    Settings.MotorStatorHeightMm.Value,
                    Settings.BatteryCells.Value,
                    Settings.BatteryCapacityMah.Value
                );
                Settings.QuadMassKg.Value = massKg;
            }

            return Evaluate(
                Settings.FrameWheelbaseMm.Value,
                Settings.IsDuctedFrame.Value,
                Settings.PropDiameterInches.Value,
                Settings.PropPitchInches.Value,
                Settings.PropBlades.Value,
                Settings.MotorStatorDiameterMm.Value,
                Settings.MotorStatorHeightMm.Value,
                Settings.MotorKv.Value,
                Settings.BatteryCells.Value,
                Settings.BatteryCapacityMah.Value,
                Settings.EscAmperage.Value,
                massKg
            );
        }

        public static DroneHardwareSpec Evaluate(
            float wheelbaseMm,
            bool isDucted,
            float propInches,
            float propPitchInches,
            int propBlades,
            float statorDiamMm,
            float statorHeightMm,
            float motorKv,
            int batteryCells,
            float batteryCapMah,
            float escAmps,
            float massKg)
        {
            wheelbaseMm = Mathf.Clamp(wheelbaseMm, 55f, 500f);
            propInches = ClampPropToFrame(propInches, wheelbaseMm, isDucted);
            propPitchInches = Mathf.Clamp(propPitchInches, 0.7f, propInches * 1.25f);
            propBlades = Mathf.Clamp(propBlades, 2, 4);
            statorDiamMm = Mathf.Clamp(statorDiamMm, 6f, 40f);
            statorHeightMm = Mathf.Clamp(statorHeightMm, 2f, 20f);
            motorKv = Mathf.Clamp(motorKv, 500f, 32000f);
            batteryCells = Mathf.Clamp(batteryCells, 1, 6);
            batteryCapMah = Mathf.Clamp(batteryCapMah, 200f, 8000f);
            escAmps = Mathf.Clamp(escAmps, 3f, 120f);
            massKg = Mathf.Clamp(massKg, 0.018f, 5.0f);

            float frameRadiusM = (wheelbaseMm * 0.5f) * 0.001f;
            float armHalfXY = frameRadiusM * 0.70710678f;
            float propDiamM = propInches * 0.0254f;
            float propPitchM = propPitchInches * 0.0254f;
            float halfSpanM = armHalfXY + propDiamM * 0.5f + (isDucted ? 0.005f : 0f);
            float bellyHeightM = Mathf.Clamp(halfSpanM * 0.16f, 0.012f, 0.045f);
            float topHeightM = Mathf.Clamp(halfSpanM * 0.24f, 0.018f, 0.065f);

            // Diagonal inertia tensor for X-frame quadcopter
            float ixx = massKg * (0.55f * frameRadiusM * frameRadiusM + 0.00012f);
            float iyy = massKg * (0.95f * frameRadiusM * frameRadiusM + 0.00020f);
            Vector3 inertia = new Vector3(ixx, iyy, ixx);

            // Aerodynamic drag areas (Cd * A in m^2)
            float dragTangential = 0.0020f + (halfSpanM * halfSpanM) * 0.65f;
            float dragLateral = dragTangential * 1.85f;
            float dragNormal = dragTangential * 3.80f;

            // Propeller thrust & power coefficients
            float pitchRatio = Mathf.Clamp(propPitchInches / propInches, 0.45f, 1.25f);
            float bladeFactorCt = Mathf.Pow(propBlades / 3.0f, 0.35f);
            float bladeFactorCp = Mathf.Pow(propBlades / 3.0f, 0.55f);
            float ductEff = isDucted ? 1.08f : 1.0f;

            float ct = (0.088f + 0.042f * pitchRatio) * bladeFactorCt * ductEff;
            float cp = (0.032f + 0.036f * pitchRatio) * bladeFactorCp;

            // Battery electrical parameters
            float fullVolt = batteryCells * 4.2f;
            float nomVolt = batteryCells * 3.7f;
            float emptyVolt = batteryCells * 3.3f;
            // Internal resistance per cell scales inversely with capacity (e.g. 1300mAh ~4 mOhm/cell, 300mAh ~18 mOhm/cell)
            float rPerCell = 0.0052f * (1300f / Mathf.Max(250f, batteryCapMah));
            float internalResistance = rPerCell * batteryCells;

            // Stator volume (mm^3) -> motor torque capacity & prop load matching
            float statorVol = statorDiamMm * statorDiamMm * statorHeightMm;
            float motorEfficiency = 0.78f;

            // Reference loaded RPM at nominal voltage before ESC limiting
            // Larger prop (D^5) vs stator volume causes realistic RPM droop under load
            float propLoadIndex = Mathf.Pow(propDiamM / 0.12954f, 4.2f) * (pitchRatio / 0.84f);
            float motorTorqueIndex = Mathf.Max(0.02f, statorVol / 3388f); // normalized to 2207
            float torqueRatio = motorTorqueIndex / Mathf.Max(0.002f, propLoadIndex);
            bool isUnderTorqued = torqueRatio < 0.45f;

            // Loaded RPM factor: ~0.82 for well-matched, drops if prop is too heavy for stator
            float loadedFactor = Mathf.Clamp(0.84f - 0.18f / Mathf.Max(0.25f, torqueRatio), 0.42f, 0.85f);
            float maxRpmUnclamped = motorKv * fullVolt * loadedFactor;

            // Electrical current draw at maxRpmUnclamped
            float nUnclamped = maxRpmUnclamped / 60f;
            float d4 = propDiamM * propDiamM * propDiamM * propDiamM;
            float d5 = d4 * propDiamM;
            float shaftPowerPerMotor = cp * DronePhysics.AirDensity * nUnclamped * nUnclamped * nUnclamped * d5;
            float elecPowerPerMotor = shaftPowerPerMotor / motorEfficiency;
            float reqCurrentPerMotor = elecPowerPerMotor / Mathf.Max(2.5f, fullVolt);

            bool isEscLimited = reqCurrentPerMotor > escAmps;
            float maxLoadedRpm = maxRpmUnclamped;
            if (isEscLimited)
            {
                // P ~ n^3, so capping current/power scales n by (I_esc / I_req)^(1/3)
                float escScale = Mathf.Pow(escAmps / Mathf.Max(0.1f, reqCurrentPerMotor), 1.0f / 3.0f);
                maxLoadedRpm *= escScale;
            }

            float nMax = maxLoadedRpm / 60f;
            float maxThrustPerMotor = ct * DronePhysics.AirDensity * nMax * nMax * d4;
            float maxThrustTotal = maxThrustPerMotor * 4f;
            float weightN = massKg * 9.81f;
            float twr = maxThrustTotal / Mathf.Max(0.05f, weightN);
            float hoverThrottle = DronePhysics.CalculateHoverThrottle(twr);

            float actualPerMotorCurrent = Mathf.Min(reqCurrentPerMotor, escAmps);
            float peakCurrentTotal = actualPerMotorCurrent * 4f;

            // Pitch speed: V_pitch = n * pitch_m
            float pitchSpeedMax = nMax * propPitchM;

            // Motor spool time constant (seconds): prop inertia / motor torque
            float propInertiaIndex = Mathf.Pow(propInches / 5.1f, 3.2f);
            float motorSpoolTau = Mathf.Clamp(0.026f * (propInertiaIndex / Mathf.Max(0.05f, motorTorqueIndex)), 0.012f, 0.095f);

            // Rotational control bandwidth (natural frequency rad/s)
            float sizeScale = Mathf.Clamp(0.1125f / Mathf.Max(0.025f, frameRadiusM), 0.65f, 1.55f);
            float baseRpWn = 42.0f * sizeScale * Mathf.Clamp(twr / 8.0f, 0.70f, 1.25f);
            float baseYawWn = 26.0f * sizeScale;

            float snappiness = Mathf.Clamp(Settings.PidSnappiness != null ? Settings.PidSnappiness.Value : 1.0f, 0.5f, 1.6f);
            float rollPitchWn = (Settings.AdvancedPidTuning != null && Settings.AdvancedPidTuning.Value)
                ? Settings.PidRollPitchWn.Value
                : baseRpWn * snappiness;
            float yawWn = (Settings.AdvancedPidTuning != null && Settings.AdvancedPidTuning.Value)
                ? Settings.PidYawWn.Value
                : baseYawWn * snappiness;
            float rollPitchZeta = (Settings.AdvancedPidTuning != null && Settings.AdvancedPidTuning.Value)
                ? Settings.PidDamping.Value
                : Mathf.Lerp(1.0f, 0.70f, Mathf.Clamp01(Settings.SnapWobbleStrength != null ? Settings.SnapWobbleStrength.Value : 1.0f));

            float maxBpf = (maxLoadedRpm / 60f) * propBlades;

            return new DroneHardwareSpec
            {
                WheelbaseMm = wheelbaseMm,
                FrameRadiusM = frameRadiusM,
                ArmHalfXY = armHalfXY,
                PropDiameterM = propDiamM,
                PropPitchM = propPitchM,
                PropBlades = propBlades,
                IsDucted = isDucted,
                HalfSpanM = halfSpanM,
                BellyHeightM = bellyHeightM,
                TopHeightM = topHeightM,
                CollisionRestitution = isDucted ? 0.70f : 0.20f,
                MassKg = massKg,
                InertiaTensor = inertia,
                DragNormal = dragNormal,
                DragTangential = dragTangential,
                DragLateral = dragLateral,
                LinearRotorDrag = Mathf.Clamp(0.030f * (propDiamM / 0.12954f), 0.006f, 0.060f),
                GroundEffectMaxAltM = propDiamM * 2.5f,
                PropCt = ct,
                PropCp = cp,
                MotorKv = motorKv,
                StatorDiameterMm = statorDiamMm,
                StatorHeightMm = statorHeightMm,
                BatteryCells = batteryCells,
                BatteryCapacityMah = batteryCapMah,
                EscAmperage = escAmps,
                FullVoltage = fullVolt,
                NominalVoltage = nomVolt,
                EmptyVoltage = emptyVolt,
                InternalResistanceOhms = internalResistance,
                MaxLoadedRpm = maxLoadedRpm,
                MaxThrustNewtons = maxThrustTotal,
                ThrustToWeightRatio = twr,
                HoverThrottle01 = hoverThrottle,
                PeakCurrentAmps = peakCurrentTotal,
                PitchSpeedMaxMps = pitchSpeedMax,
                MotorSpoolTau = motorSpoolTau,
                RollPitchWn = rollPitchWn,
                YawWn = yawWn,
                RollPitchZeta = rollPitchZeta,
                IsEscLimited = isEscLimited,
                IsUnderTorqued = isUnderTorqued,
                MaxBladePassF0 = maxBpf,
                AudioMinDistance = Mathf.Clamp(halfSpanM * 6.0f, 0.40f, 2.0f),
                AudioMaxDistance = Mathf.Clamp(halfSpanM * 480.0f, 18.0f, 120.0f)
            };
        }
    }
}

