using System;
using System.Collections.Generic;
using System.IO;
using MelonLoader;
using MelonLoader.Utils;
using UnityEngine;

namespace FPVDrone
{
    [Serializable]
    public class DroneBuildProfile
    {
        public string Name;

        // Hardware
        public float FrameWheelbaseMm;
        public bool IsDuctedFrame;
        public float PropDiameterInches;
        public float PropPitchInches;
        public int PropBlades;
        public float MotorStatorDiameterMm;
        public float MotorStatorHeightMm;
        public float MotorKv;
        public int BatteryCells;
        public float BatteryCapacityMah;
        public float EscAmperage;
        public float QuadMassKg;

        // Camera & Rates
        public float CameraTiltAngle;
        public float CameraHeightOffsetMm = 18.0f;
        public float CameraForwardOffsetMm = 45.0f;
        public bool EnableCameraAngleMix;
        public int RateSystem;
        public float RcRate, SuperRate, Expo;
        public float PitchRcRate, PitchSuperRate, PitchExpo;
        public float YawRcRate, YawSuperRate, YawExpo;
        public float RollCenterSens, RollMaxRate, RollActualExpo;
        public float PitchCenterSens, PitchMaxRate, PitchActualExpo;
        public float YawCenterSens, YawMaxRate, YawActualExpo;
        public float ThrottleMid, ThrottleExpo;
        public int ThrottleLimitMode;
        public float ThrottleLimitPercent;
    }

    public static class DroneProfileStore
    {
        private static readonly List<DroneBuildProfile> _profiles = new List<DroneBuildProfile>();
        private static readonly List<string> _filePaths = new List<string>();
        private static bool _loaded;
        private static int _selectedIndex;

        public static string CustomNameDraft = "";
        public static string LastStatusMessage = "";

        public static string ProfilesDir => Path.Combine(MelonEnvironment.UserDataDirectory, "FPVDrone", "Builds");

        public static int Count
        {
            get
            {
                EnsureLoaded();
                return _profiles.Count;
            }
        }

        public static bool HasSelection
        {
            get
            {
                EnsureLoaded();
                return _profiles.Count > 0;
            }
        }

        public static string SelectedLabel
        {
            get
            {
                EnsureLoaded();
                if (_profiles.Count == 0)
                    return "(No saved builds)";
                _selectedIndex = Mathf.Clamp(_selectedIndex, 0, _profiles.Count - 1);
                return $"{_profiles[_selectedIndex].Name} ({_selectedIndex + 1}/{_profiles.Count})";
            }
        }

        public static void EnsureLoaded()
        {
            if (_loaded)
                return;
            _loaded = true;
            Reload();
        }

        public static void Reload()
        {
            _profiles.Clear();
            _filePaths.Clear();
            try
            {
                if (!Directory.Exists(ProfilesDir))
                    return;

                string[] files = Directory.GetFiles(ProfilesDir, "*.json");
                Array.Sort(files, StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < files.Length; i++)
                {
                    try
                    {
                        string json = File.ReadAllText(files[i]);
                        DroneBuildProfile p = JsonUtility.FromJson<DroneBuildProfile>(json);
                        if (p != null)
                        {
                            if (string.IsNullOrEmpty(p.Name))
                                p.Name = Path.GetFileNameWithoutExtension(files[i]);
                            _profiles.Add(p);
                            _filePaths.Add(files[i]);
                        }
                    }
                    catch (Exception ex)
                    {
                        MelonLogger.Warning($"[FPVDrone] Failed to load build profile {files[i]}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[FPVDrone] Failed to scan build profiles: {ex.Message}");
            }

            if (_profiles.Count > 0)
                _selectedIndex = Mathf.Clamp(_selectedIndex, 0, _profiles.Count - 1);
            else
                _selectedIndex = 0;
        }

        public static void CycleSelected(int dir)
        {
            EnsureLoaded();
            if (_profiles.Count == 0)
                return;
            _selectedIndex = (_selectedIndex + dir + _profiles.Count) % _profiles.Count;
            CustomNameDraft = _profiles[_selectedIndex].Name;
        }

        private static bool IsReservedDefaultName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;
            string trimmed = name.Trim();
            if (string.Equals(trimmed, "Default", StringComparison.OrdinalIgnoreCase))
                return true;

            foreach (DronePreset preset in Enum.GetValues(typeof(DronePreset)))
            {
                PresetSpec spec = DronePresets.Get(preset);
                if (spec != null && string.Equals(trimmed, spec.DisplayName, StringComparison.OrdinalIgnoreCase))
                    return true;
                if (string.Equals(trimmed, preset.ToString(), StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static string SanitizeFileName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "";
            char[] invalid = Path.GetInvalidFileNameChars();
            char[] chars = name.Trim().ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                for (int j = 0; j < invalid.Length; j++)
                {
                    if (chars[i] == invalid[j])
                    {
                        chars[i] = '_';
                        break;
                    }
                }
            }
            return new string(chars).Trim();
        }

        private static string ResolveDesiredName(string optionalName)
        {
            string trimmed = string.IsNullOrWhiteSpace(optionalName) ? "" : optionalName.Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                return $"{Settings.FrameWheelbaseMm.Value:F0}mm {Settings.PropDiameterInches.Value:F1}in {Settings.BatteryCells.Value}S {Settings.MotorKv.Value:F0}KV";
            }
            if (IsReservedDefaultName(trimmed))
            {
                LastStatusMessage = "Default preset names cannot be overwritten; saved as custom copy.";
                return trimmed + " (Custom)";
            }
            return trimmed;
        }

        public static void SaveCurrentBuild()
        {
            EnsureLoaded();
            try
            {
                Directory.CreateDirectory(ProfilesDir);
                string displayName = ResolveDesiredName(CustomNameDraft);
                string fileBase = SanitizeFileName(displayName);
                if (string.IsNullOrEmpty(fileBase))
                    fileBase = "CustomBuild";

                string uniqueBase = fileBase;
                int suffix = 1;
                while (File.Exists(Path.Combine(ProfilesDir, uniqueBase + ".json")))
                {
                    suffix++;
                    uniqueBase = $"{fileBase}_{suffix}";
                }
                if (suffix > 1)
                    displayName = $"{displayName} ({suffix})";

                DroneBuildProfile p = CaptureCurrent(displayName);
                string path = Path.Combine(ProfilesDir, uniqueBase + ".json");
                File.WriteAllText(path, JsonUtility.ToJson(p, true));
                MelonPreferences.Save();
                Reload();

                for (int i = 0; i < _filePaths.Count; i++)
                {
                    if (string.Equals(_filePaths[i], path, StringComparison.OrdinalIgnoreCase))
                    {
                        _selectedIndex = i;
                        break;
                    }
                }
                CustomNameDraft = displayName;
                LastStatusMessage = $"Saved '{displayName}'";
            }
            catch (Exception ex)
            {
                LastStatusMessage = "Save failed: " + ex.Message;
                MelonLogger.Error($"[FPVDrone] Failed to save build profile: {ex.Message}");
            }
        }

        public static void UpdateSelectedBuild()
        {
            EnsureLoaded();
            if (_profiles.Count == 0)
            {
                SaveCurrentBuild();
                return;
            }

            _selectedIndex = Mathf.Clamp(_selectedIndex, 0, _profiles.Count - 1);
            string oldPath = _filePaths[_selectedIndex];
            string oldName = _profiles[_selectedIndex].Name;

            try
            {
                Directory.CreateDirectory(ProfilesDir);
                string targetName = string.IsNullOrWhiteSpace(CustomNameDraft) ? oldName : CustomNameDraft.Trim();
                if (IsReservedDefaultName(targetName))
                {
                    LastStatusMessage = "Cannot overwrite a default preset name.";
                    targetName = oldName;
                }

                string newFileBase = SanitizeFileName(targetName);
                if (string.IsNullOrEmpty(newFileBase))
                    newFileBase = Path.GetFileNameWithoutExtension(oldPath);

                string targetPath = Path.Combine(ProfilesDir, newFileBase + ".json");
                if (!string.Equals(targetPath, oldPath, StringComparison.OrdinalIgnoreCase) && File.Exists(targetPath))
                {
                    // Keep old file path if target name collides with another existing build
                    targetPath = oldPath;
                }

                DroneBuildProfile p = CaptureCurrent(targetName);
                File.WriteAllText(targetPath, JsonUtility.ToJson(p, true));
                if (!string.Equals(targetPath, oldPath, StringComparison.OrdinalIgnoreCase) && File.Exists(oldPath))
                {
                    File.Delete(oldPath);
                }

                MelonPreferences.Save();
                Reload();
                for (int i = 0; i < _filePaths.Count; i++)
                {
                    if (string.Equals(_filePaths[i], targetPath, StringComparison.OrdinalIgnoreCase))
                    {
                        _selectedIndex = i;
                        break;
                    }
                }
                CustomNameDraft = targetName;
                LastStatusMessage = $"Updated '{targetName}'";
            }
            catch (Exception ex)
            {
                LastStatusMessage = "Update failed: " + ex.Message;
                MelonLogger.Error($"[FPVDrone] Failed to update build profile: {ex.Message}");
            }
        }

        public static void LoadSelectedBuild()
        {
            EnsureLoaded();
            if (_profiles.Count == 0)
                return;
            _selectedIndex = Mathf.Clamp(_selectedIndex, 0, _profiles.Count - 1);
            DroneBuildProfile p = _profiles[_selectedIndex];
            Apply(p);
            CustomNameDraft = p.Name;
            LastStatusMessage = $"Loaded '{p.Name}'";
        }

        public static void DeleteSelectedBuild()
        {
            EnsureLoaded();
            if (_profiles.Count == 0)
                return;
            _selectedIndex = Mathf.Clamp(_selectedIndex, 0, _profiles.Count - 1);
            string name = _profiles[_selectedIndex].Name;
            string path = _filePaths[_selectedIndex];
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
                Reload();
                CustomNameDraft = _profiles.Count > 0 ? _profiles[_selectedIndex].Name : "";
                LastStatusMessage = $"Deleted '{name}'";
            }
            catch (Exception ex)
            {
                LastStatusMessage = "Delete failed: " + ex.Message;
                MelonLogger.Warning($"[FPVDrone] Failed to delete build profile {name}: {ex.Message}");
            }
        }

        private static DroneBuildProfile CaptureCurrent(string name)
        {
            return new DroneBuildProfile
            {
                Name = name,
                FrameWheelbaseMm = Settings.FrameWheelbaseMm.Value,
                IsDuctedFrame = Settings.IsDuctedFrame.Value,
                PropDiameterInches = Settings.PropDiameterInches.Value,
                PropPitchInches = Settings.PropPitchInches.Value,
                PropBlades = Settings.PropBlades.Value,
                MotorStatorDiameterMm = Settings.MotorStatorDiameterMm.Value,
                MotorStatorHeightMm = Settings.MotorStatorHeightMm.Value,
                MotorKv = Settings.MotorKv.Value,
                BatteryCells = Settings.BatteryCells.Value,
                BatteryCapacityMah = Settings.BatteryCapacityMah.Value,
                EscAmperage = Settings.EscAmperage.Value,
                QuadMassKg = Settings.QuadMassKg.Value,
                CameraTiltAngle = Settings.CameraTiltAngle.Value,
                CameraHeightOffsetMm = Settings.CameraHeightOffsetMm.Value,
                CameraForwardOffsetMm = Settings.CameraForwardOffsetMm.Value,
                EnableCameraAngleMix = Settings.EnableCameraAngleMix.Value,
                RateSystem = (int)Settings.RateSystem.Value,
                RcRate = Settings.RcRate.Value,
                SuperRate = Settings.SuperRate.Value,
                Expo = Settings.Expo.Value,
                PitchRcRate = Settings.PitchRcRate.Value,
                PitchSuperRate = Settings.PitchSuperRate.Value,
                PitchExpo = Settings.PitchExpo.Value,
                YawRcRate = Settings.YawRcRate.Value,
                YawSuperRate = Settings.YawSuperRate.Value,
                YawExpo = Settings.YawExpo.Value,
                RollCenterSens = Settings.RollCenterSens.Value,
                RollMaxRate = Settings.RollMaxRate.Value,
                RollActualExpo = Settings.RollActualExpo.Value,
                PitchCenterSens = Settings.PitchCenterSens.Value,
                PitchMaxRate = Settings.PitchMaxRate.Value,
                PitchActualExpo = Settings.PitchActualExpo.Value,
                YawCenterSens = Settings.YawCenterSens.Value,
                YawMaxRate = Settings.YawMaxRate.Value,
                YawActualExpo = Settings.YawActualExpo.Value,
                ThrottleMid = Settings.ThrottleMid.Value,
                ThrottleExpo = Settings.ThrottleExpo.Value,
                ThrottleLimitMode = (int)Settings.ThrottleLimitMode.Value,
                ThrottleLimitPercent = Settings.ThrottleLimitPercent.Value
            };
        }

        private static void Apply(DroneBuildProfile p)
        {
            Settings.FrameWheelbaseMm.Value = p.FrameWheelbaseMm;
            Settings.IsDuctedFrame.Value = p.IsDuctedFrame;
            Settings.PropDiameterInches.Value = p.PropDiameterInches;
            Settings.PropPitchInches.Value = p.PropPitchInches;
            Settings.PropBlades.Value = p.PropBlades;
            Settings.MotorStatorDiameterMm.Value = p.MotorStatorDiameterMm;
            Settings.MotorStatorHeightMm.Value = p.MotorStatorHeightMm;
            Settings.MotorKv.Value = p.MotorKv;
            Settings.BatteryCells.Value = p.BatteryCells;
            Settings.BatteryCapacityMah.Value = p.BatteryCapacityMah;
            Settings.EscAmperage.Value = p.EscAmperage;
            Settings.QuadMassKg.Value = p.QuadMassKg;

            DroneBuilder.EnforceGeometryConstraints();

            Settings.CameraTiltAngle.Value = p.CameraTiltAngle;
            Settings.CameraHeightOffsetMm.Value = Mathf.Clamp(p.CameraHeightOffsetMm, -5.0f, 50.0f);
            Settings.CameraForwardOffsetMm.Value = Mathf.Clamp(p.CameraForwardOffsetMm, 0.0f, 80.0f);
            Settings.EnableCameraAngleMix.Value = p.EnableCameraAngleMix;
            Settings.RateSystem.Value = (RateType)Mathf.Clamp(p.RateSystem, 0, 1);
            Settings.RcRate.Value = p.RcRate;
            Settings.SuperRate.Value = p.SuperRate;
            Settings.Expo.Value = p.Expo;
            Settings.PitchRcRate.Value = p.PitchRcRate;
            Settings.PitchSuperRate.Value = p.PitchSuperRate;
            Settings.PitchExpo.Value = p.PitchExpo;
            Settings.YawRcRate.Value = p.YawRcRate;
            Settings.YawSuperRate.Value = p.YawSuperRate;
            Settings.YawExpo.Value = p.YawExpo;
            Settings.RollCenterSens.Value = p.RollCenterSens;
            Settings.RollMaxRate.Value = p.RollMaxRate;
            Settings.RollActualExpo.Value = p.RollActualExpo;
            Settings.PitchCenterSens.Value = p.PitchCenterSens;
            Settings.PitchMaxRate.Value = p.PitchMaxRate;
            Settings.PitchActualExpo.Value = p.PitchActualExpo;
            Settings.YawCenterSens.Value = p.YawCenterSens;
            Settings.YawMaxRate.Value = p.YawMaxRate;
            Settings.YawActualExpo.Value = p.YawActualExpo;
            Settings.ThrottleMid.Value = p.ThrottleMid;
            Settings.ThrottleExpo.Value = p.ThrottleExpo;
            Settings.ThrottleLimitMode.Value = (ThrottleLimitType)Mathf.Clamp(p.ThrottleLimitMode, 0, 2);
            Settings.ThrottleLimitPercent.Value = p.ThrottleLimitPercent;

            MelonPreferences.Save();
        }
    }
}
