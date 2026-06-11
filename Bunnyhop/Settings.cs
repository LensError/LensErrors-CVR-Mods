using System;
using System.Collections.Generic;
using MelonLoader;
using UnityEngine;

namespace Bunnyhop
{
    static class Settings
    {
        public static bool Enabled { get; private set; } = true;
        public static float JumpMultiplier { get; private set; } = 1.5f;
        public static float MaxSpeedMultiplier { get; private set; } = 3f;
        public static float GroundResetDelay { get; private set; } = 0.5f;
        public static string CurrentAvatarId { get; private set; } = string.Empty;
        public static bool DisabledForCurrentAvatar =>
            !string.IsNullOrEmpty(CurrentAvatarId) &&
            s_disabledAvatarIds.Contains(CurrentAvatarId);
        public static bool ActiveForCurrentAvatar => Enabled && !DisabledForCurrentAvatar;

        static MelonPreferences_Entry<bool> s_enabledEntry;
        static MelonPreferences_Entry<float> s_jumpMultiplierEntry;
        static MelonPreferences_Entry<float> s_maxSpeedMultiplierEntry;
        static MelonPreferences_Entry<float> s_groundResetDelayEntry;
        static MelonPreferences_Entry<string> s_disabledAvatarIdsEntry;
        static readonly HashSet<string> s_disabledAvatarIds =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        internal static void Init()
        {
            var category = MelonPreferences.CreateCategory("BHOP", "Bunnyhop");
            s_enabledEntry = category.CreateEntry("Enabled", true, "Enabled");
            s_jumpMultiplierEntry = category.CreateEntry("JumpMultiplier", 1.5f, "Speed Multiplier Per Jump");
            s_maxSpeedMultiplierEntry = category.CreateEntry("MaxSpeedMultiplier", 3f, "Maximum Speed Multiplier");
            s_groundResetDelayEntry = category.CreateEntry("GroundResetDelay", 0.5f, "Ground Reset Delay");
            s_disabledAvatarIdsEntry = category.CreateEntry("DisabledAvatarIds", string.Empty);

            s_enabledEntry.IsHidden = true;
            s_jumpMultiplierEntry.IsHidden = true;
            s_maxSpeedMultiplierEntry.IsHidden = true;
            s_groundResetDelayEntry.IsHidden = true;
            s_disabledAvatarIdsEntry.IsHidden = true;

            Enabled = s_enabledEntry.Value;
            JumpMultiplier = Mathf.Clamp(s_jumpMultiplierEntry.Value, 1f, 2f);
            MaxSpeedMultiplier = Mathf.Clamp(s_maxSpeedMultiplierEntry.Value, 1f, 10f);
            GroundResetDelay = Mathf.Clamp(s_groundResetDelayEntry.Value, 0.1f, 3f);
            LoadDisabledAvatarIds();
        }

        internal static void SetEnabled(bool value)
        {
            Enabled = value;
            s_enabledEntry.Value = value;
            MelonPreferences.Save();
        }

        internal static void SetJumpMultiplier(float value)
        {
            JumpMultiplier = Mathf.Clamp(value, 1f, 2f);
            s_jumpMultiplierEntry.Value = JumpMultiplier;
            MelonPreferences.Save();
        }

        internal static void SetMaxSpeedMultiplier(float value)
        {
            MaxSpeedMultiplier = Mathf.Clamp(value, 1f, 10f);
            s_maxSpeedMultiplierEntry.Value = MaxSpeedMultiplier;
            MelonPreferences.Save();
        }

        internal static void SetGroundResetDelay(float value)
        {
            GroundResetDelay = Mathf.Clamp(value, 0.1f, 3f);
            s_groundResetDelayEntry.Value = GroundResetDelay;
            MelonPreferences.Save();
        }

        internal static void SetCurrentAvatarId(string avatarId)
        {
            CurrentAvatarId = avatarId ?? string.Empty;
        }

        internal static void SetDisabledForCurrentAvatar(bool disabled)
        {
            if (string.IsNullOrEmpty(CurrentAvatarId))
                return;

            if (disabled)
                s_disabledAvatarIds.Add(CurrentAvatarId);
            else
                s_disabledAvatarIds.Remove(CurrentAvatarId);

            s_disabledAvatarIdsEntry.Value = string.Join(";", s_disabledAvatarIds);
            MelonPreferences.Save();
        }

        static void LoadDisabledAvatarIds()
        {
            s_disabledAvatarIds.Clear();

            foreach (string avatarId in s_disabledAvatarIdsEntry.Value.Split(';'))
            {
                if (!string.IsNullOrWhiteSpace(avatarId))
                    s_disabledAvatarIds.Add(avatarId.Trim());
            }
        }
    }
}
