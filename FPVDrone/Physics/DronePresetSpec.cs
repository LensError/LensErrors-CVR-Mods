using System.Collections.Generic;

namespace FPVDrone
{
    public class PresetSpec
    {
        public DronePreset Preset { get; set; }
        public string DisplayName { get; set; }

        // Drone Builder Hardware Values
        public float WheelbaseMm { get; set; }
        public bool IsDucted { get; set; }
        public float PropDiameterInches { get; set; }
        public float PropPitchInches { get; set; }
        public int PropBlades { get; set; }
        public float MotorStatorDiameterMm { get; set; }
        public float MotorStatorHeightMm { get; set; }
        public float MotorKv { get; set; }
        public int BatteryCells { get; set; }
        public float BatteryCapacityMah { get; set; }
        public float EscAmperage { get; set; }
        public float MassKg { get; set; }

        // Default Rates & Camera
        public float CameraTiltAngle { get; set; }
        public float CameraHeightOffsetMm { get; set; }
        public float CameraForwardOffsetMm { get; set; }
        public float RcRate { get; set; }
        public float SuperRate { get; set; }
        public float Expo { get; set; }
        public float YawRcRate { get; set; }
        public float YawSuperRate { get; set; }
        public float YawExpo { get; set; }
    }

    public static class DronePresets
    {
        private static readonly Dictionary<DronePreset, PresetSpec> Specs = new Dictionary<DronePreset, PresetSpec>()
        {
            [DronePreset.Freestyle5Inch] = new PresetSpec
            {
                Preset = DronePreset.Freestyle5Inch,
                DisplayName = "5\" Freestyle (6S)",
                WheelbaseMm = 225f,
                IsDucted = false,
                PropDiameterInches = 5.1f,
                PropPitchInches = 4.3f,
                PropBlades = 3,
                MotorStatorDiameterMm = 22f,
                MotorStatorHeightMm = 7f,
                MotorKv = 1850f,
                BatteryCells = 6,
                BatteryCapacityMah = 1300f,
                EscAmperage = 50f,
                MassKg = 0.680f,
                CameraTiltAngle = 30.0f,
                CameraHeightOffsetMm = 18.0f,
                CameraForwardOffsetMm = 52.0f,
                RcRate = 1.0f,
                SuperRate = 0.70f,
                Expo = 0.18f,
                YawRcRate = 1.0f,
                YawSuperRate = 0.65f,
                YawExpo = 0.15f
            },
            [DronePreset.TinyWhoop] = new PresetSpec
            {
                Preset = DronePreset.TinyWhoop,
                DisplayName = "Tiny Whoop (65mm 1S)",
                WheelbaseMm = 65f,
                IsDucted = true,
                PropDiameterInches = 1.2f,
                PropPitchInches = 1.0f,
                PropBlades = 3,
                MotorStatorDiameterMm = 8f,
                MotorStatorHeightMm = 2f,
                MotorKv = 23000f,
                BatteryCells = 1,
                BatteryCapacityMah = 300f,
                EscAmperage = 5f,
                MassKg = 0.032f,
                CameraTiltAngle = 20.0f,
                CameraHeightOffsetMm = 18.0f,
                CameraForwardOffsetMm = 20.0f,
                RcRate = 0.90f,
                SuperRate = 0.55f,
                Expo = 0.22f,
                YawRcRate = 0.90f,
                YawSuperRate = 0.55f,
                YawExpo = 0.20f
            },
            [DronePreset.Cinewhoop35Inch] = new PresetSpec
            {
                Preset = DronePreset.Cinewhoop35Inch,
                DisplayName = "3.5\" Cinewhoop (4S)",
                WheelbaseMm = 150f,
                IsDucted = true,
                PropDiameterInches = 3.5f,
                PropPitchInches = 2.5f,
                PropBlades = 3,
                MotorStatorDiameterMm = 20f,
                MotorStatorHeightMm = 4f,
                MotorKv = 3000f,
                BatteryCells = 4,
                BatteryCapacityMah = 850f,
                EscAmperage = 35f,
                MassKg = 0.280f,
                CameraTiltAngle = 15.0f,
                CameraHeightOffsetMm = 22.0f,
                CameraForwardOffsetMm = 40.0f,
                RcRate = 0.75f,
                SuperRate = 0.45f,
                Expo = 0.25f,
                YawRcRate = 0.75f,
                YawSuperRate = 0.45f,
                YawExpo = 0.20f
            },
            [DronePreset.Cinematic7Inch] = new PresetSpec
            {
                Preset = DronePreset.Cinematic7Inch,
                DisplayName = "7\" Long Range (6S)",
                WheelbaseMm = 300f,
                IsDucted = false,
                PropDiameterInches = 7.0f,
                PropPitchInches = 4.0f,
                PropBlades = 3,
                MotorStatorDiameterMm = 28f,
                MotorStatorHeightMm = 6.5f,
                MotorKv = 1300f,
                BatteryCells = 6,
                BatteryCapacityMah = 3000f,
                EscAmperage = 55f,
                MassKg = 1.150f,
                CameraTiltAngle = 20.0f,
                CameraHeightOffsetMm = 24.0f,
                CameraForwardOffsetMm = 68.0f,
                RcRate = 0.75f,
                SuperRate = 0.45f,
                Expo = 0.25f,
                YawRcRate = 0.75f,
                YawSuperRate = 0.45f,
                YawExpo = 0.20f
            }
        };

        public static PresetSpec Get(DronePreset preset)
        {
            if (Specs.TryGetValue(preset, out var spec))
                return spec;
            return Specs[DronePreset.Freestyle5Inch];
        }

        public static PresetSpec Current => Get(Settings.ActivePreset.Value);
    }
}
