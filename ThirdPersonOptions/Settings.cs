using MelonLoader;
using System.ComponentModel;
using UnityEngine;

namespace ThirdPersonOptions
{
    static class Settings
    {
        public enum ModifierKeyOption
        {
            [Description("Left Ctrl")] LeftCtrl,
            [Description("Right Ctrl")] RightCtrl,
            [Description("Left Shift")] LeftShift,
            [Description("Right Shift")] RightShift,
            [Description("Left Alt")] LeftAlt,
            [Description("Right Alt")] RightAlt,
            [Description("F")] F,
            [Description("G")] G,
            [Description("T")] T,
            [Description("None")] None,
        }

        public enum CameraPositionOption
        {
            [Description("Back")] Back,
            [Description("Left")] Left,
            [Description("Right")] Right,
            [Description("Front")] Front,
        }

        static readonly KeyCode[] s_modifierKeyCodes =
        {
            KeyCode.LeftControl,
            KeyCode.RightControl,
            KeyCode.LeftShift,
            KeyCode.RightShift,
            KeyCode.LeftAlt,
            KeyCode.RightAlt,
            KeyCode.F,
            KeyCode.G,
            KeyCode.T,
            KeyCode.None,
        };

        public static bool Enabled { get; private set; } = true;
        public static bool CtrlTToggle { get; private set; } = true;
        public static bool MiddleClickZoom { get; private set; } = true;
        public static CameraPositionOption CameraPosition => ms_cameraPositionEntry.Value;
        public static ModifierKeyOption ScrollModifier => ms_modifierKeyEntry.Value;
        public static KeyCode ModifierKey => s_modifierKeyCodes[(int)ms_modifierKeyEntry.Value];

        static MelonPreferences_Entry<bool> ms_enabledEntry;
        static MelonPreferences_Entry<bool> ms_ctrlTToggleEntry;
        static MelonPreferences_Entry<bool> ms_middleClickZoomEntry;
        static MelonPreferences_Entry<bool> ms_legacyLeftCameraOffsetEntry;
        static MelonPreferences_Entry<CameraPositionOption> ms_cameraPositionEntry;
        static MelonPreferences_Entry<ModifierKeyOption> ms_modifierKeyEntry;

        internal static void Init()
        {
            var cat = MelonPreferences.CreateCategory("TPO", "Third Person Options");
            ms_enabledEntry = cat.CreateEntry("Enabled", true, "Enabled");
            ms_ctrlTToggleEntry = cat.CreateEntry("CtrlTToggle", true, "Ctrl+T Toggle");
            ms_middleClickZoomEntry = cat.CreateEntry("MiddleClickZoom", true, "3P Middle Click Zoom");
            ms_legacyLeftCameraOffsetEntry = cat.CreateEntry("LeftCameraOffset", false, "3P Left Camera Offset");
            ms_cameraPositionEntry = cat.CreateEntry(
                "CameraPosition",
                ms_legacyLeftCameraOffsetEntry.Value ? CameraPositionOption.Left : CameraPositionOption.Back,
                "3P Camera Position");
            ms_modifierKeyEntry = cat.CreateEntry("ModifierKey", ModifierKeyOption.LeftShift, "Scroll Modifier Key");
            ms_enabledEntry.IsHidden = true;
            ms_ctrlTToggleEntry.IsHidden = true;
            ms_middleClickZoomEntry.IsHidden = true;
            ms_legacyLeftCameraOffsetEntry.IsHidden = true;
            ms_cameraPositionEntry.IsHidden = true;
            ms_modifierKeyEntry.IsHidden = true;

            Enabled = ms_enabledEntry.Value;
            CtrlTToggle = ms_ctrlTToggleEntry.Value;
            MiddleClickZoom = ms_middleClickZoomEntry.Value;
            ms_enabledEntry.OnEntryValueChanged.Subscribe((_, val) => Enabled = val);
            ms_ctrlTToggleEntry.OnEntryValueChanged.Subscribe((_, val) => CtrlTToggle = val);
            ms_middleClickZoomEntry.OnEntryValueChanged.Subscribe((_, val) => MiddleClickZoom = val);
        }

        internal static string GetNativeSettingsJson()
        {
            return "{" +
                "\"enabled\":" + BoolJson(Enabled) + "," +
                "\"ctrlTToggle\":" + BoolJson(CtrlTToggle) + "," +
                "\"middleClickZoom\":" + BoolJson(MiddleClickZoom) + "," +
                "\"cameraPosition\":" + ((int)CameraPosition).ToString() + "," +
                "\"modifierKey\":" + ((int)ms_modifierKeyEntry.Value).ToString() +
                "}";
        }

        internal static void SetEnabled(bool value)
        {
            ms_enabledEntry.Value = value;
            Enabled = value;
            MelonPreferences.Save();
        }

        internal static void SetCtrlTToggle(bool value)
        {
            ms_ctrlTToggleEntry.Value = value;
            CtrlTToggle = value;
            MelonPreferences.Save();
        }

        internal static void SetMiddleClickZoom(bool value)
        {
            ms_middleClickZoomEntry.Value = value;
            MiddleClickZoom = value;
            MelonPreferences.Save();
        }

        internal static void SetCameraPosition(int value)
        {
            if (!System.Enum.IsDefined(typeof(CameraPositionOption), value))
                return;

            ms_cameraPositionEntry.Value = (CameraPositionOption)value;
            MelonPreferences.Save();
        }

        internal static void CycleCameraPosition()
        {
            int next = ((int)CameraPosition + 1) % System.Enum.GetValues(typeof(CameraPositionOption)).Length;
            SetCameraPosition(next);
        }

        internal static void SetModifierKey(int value)
        {
            if (!System.Enum.IsDefined(typeof(ModifierKeyOption), value))
                return;

            ms_modifierKeyEntry.Value = (ModifierKeyOption)value;
            MelonPreferences.Save();
        }

        static string BoolJson(bool value)
        {
            return value ? "true" : "false";
        }
    }
}
