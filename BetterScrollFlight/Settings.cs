using MelonLoader;
using System;
using System.ComponentModel;
using UIExpansionKit.API;
using UnityEngine;

namespace BetterScrollFlight
{
    static class Settings
    {
        public enum ModifierKeyOption
        {
            [Description("Left Ctrl")]  LeftCtrl,
            [Description("Left Shift")] LeftShift,
            [Description("Left Alt")]   LeftAlt,
            [Description("F")]          F,
            [Description("G")]          G,
        }

        public enum SpeedStepOption
        {
            [Description("5%")]  P5,
            [Description("10%")] P10,
            [Description("15%")] P15,
            [Description("20%")] P20,
            [Description("25%")] P25,
            [Description("30%")] P30,
            [Description("50%")] P50,
        }

        public enum MaxMultiplierOption
        {
            [Description("2x")]  X2,
            [Description("5x")]  X5,
            [Description("10x")] X10,
            [Description("20x")] X20,
            [Description("50x")] X50,
        }

        static readonly KeyCode[] s_modKeyCodes = {
            KeyCode.LeftControl, KeyCode.LeftShift, KeyCode.LeftAlt, KeyCode.F, KeyCode.G,
        };

        static readonly float[] s_speedStepValues = { 0.05f, 0.10f, 0.15f, 0.20f, 0.25f, 0.30f, 0.50f };
        static readonly float[] s_maxMultiplierValues = { 2f, 5f, 10f, 20f, 50f };

        public static bool Enabled { get; private set; } = true;
        public static bool ShowHud { get; private set; } = true;
        public static bool RequireModifier { get; private set; } = false;
        public static bool ResetOnExitFlight { get; private set; } = false;
        public static KeyCode ModifierKey => s_modKeyCodes[(int)ms_modKeyEntry.Value];
        public static float SpeedStep => s_speedStepValues[(int)ms_speedStepEntry.Value];
        public static float MaxMultiplier => s_maxMultiplierValues[(int)ms_maxMultiplierEntry.Value];
        public static float SpeedScale { get; private set; } = 1f;

        static MelonPreferences_Entry<bool> ms_enabledEntry;
        static MelonPreferences_Entry<bool> ms_showHudEntry;
        static MelonPreferences_Entry<bool> ms_requireModifierEntry;
        static MelonPreferences_Entry<bool> ms_resetOnExitFlightEntry;
        static MelonPreferences_Entry<ModifierKeyOption> ms_modKeyEntry;
        static MelonPreferences_Entry<SpeedStepOption> ms_speedStepEntry;
        static MelonPreferences_Entry<MaxMultiplierOption> ms_maxMultiplierEntry;
        static MelonPreferences_Entry<float> ms_speedScaleEntry;

        internal static void Init()
        {
            var cat = MelonPreferences.CreateCategory("BSF", "BetterScrollFlight");
            ms_enabledEntry           = cat.CreateEntry("Enabled",           true,                         "Enabled");
            ms_showHudEntry           = cat.CreateEntry("ShowHud",           true,                         "Show HUD");
            ms_requireModifierEntry   = cat.CreateEntry("RequireModifier",   false,                        "Require Modifier Key");
            ms_resetOnExitFlightEntry = cat.CreateEntry("ResetOnExitFlight", false,                        "Reset Speed on Exit Flight");
            ms_modKeyEntry            = cat.CreateEntry("ModifierKey",       ModifierKeyOption.LeftCtrl,   "Modifier Key");
            ms_speedStepEntry         = cat.CreateEntry("SpeedStep",         SpeedStepOption.P15,          "Scroll Step");
            ms_maxMultiplierEntry     = cat.CreateEntry("MaxMultiplier",     MaxMultiplierOption.X10,      "Max Speed Multiplier");
            ms_speedScaleEntry        = cat.CreateEntry("SpeedScale",        1f);
            ms_speedScaleEntry.IsHidden = true;

            Enabled           = ms_enabledEntry.Value;
            ShowHud           = ms_showHudEntry.Value;
            RequireModifier   = ms_requireModifierEntry.Value;
            ResetOnExitFlight = ms_resetOnExitFlightEntry.Value;
            SpeedScale        = Math.Clamp(ms_speedScaleEntry.Value, 0.1f, MaxMultiplier);

            var updateModKeyVisibility = ExpansionKitApi.RegisterSettingsVisibilityCallback(
                ms_modKeyEntry, () => RequireModifier);

            ms_enabledEntry.OnEntryValueChanged.Subscribe((_, val) => Enabled = val);
            ms_showHudEntry.OnEntryValueChanged.Subscribe((_, val) => ShowHud = val);
            ms_requireModifierEntry.OnEntryValueChanged.Subscribe((_, val) =>
            {
                RequireModifier = val;
                updateModKeyVisibility();
            });
            ms_resetOnExitFlightEntry.OnEntryValueChanged.Subscribe((_, val) => ResetOnExitFlight = val);
        }

        internal static void AdjustSpeedScale(float multiplier)
        {
            SpeedScale = Math.Clamp(SpeedScale * multiplier, 0.1f, MaxMultiplier);
            ms_speedScaleEntry.Value = SpeedScale;
        }

        internal static void ResetSpeedScale()
        {
            SpeedScale = 1f;
            ms_speedScaleEntry.Value = 1f;
        }
    }
}
