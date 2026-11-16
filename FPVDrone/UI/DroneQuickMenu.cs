using System.Reflection;
using ABI_RC.Systems.UI.UILib;
using ABI_RC.Systems.UI.UILib.UIObjects;

namespace FPVDrone
{
    public static class DroneQuickMenu
    {
        private const string ModName = "FPVDrone";
        private const string PageName = "Main";
        private const string IconName = "drone_2";

        public static void PrepareIcon()
        {
            if (QuickMenuAPI.DoesIconExist(ModName, IconName))
                return;

            var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("FPVDrone.resources.drone_2.png");
            if (stream != null)
            {
                QuickMenuAPI.PrepareIcon(ModName, IconName, stream);
            }
        }

        public static void Build()
        {
            var page = new Page(ModName, PageName, isRootPage: true, tabIcon: IconName);
            page.MenuTitle = "FPV Drone";
            page.MenuSubtitle = "FPV drone flight & configurator";

            var category = page.AddCategory("Controls");

            var modToggle = category.AddToggle("Enable FPV Drone Mod", "Master switch for FPVDrone physics, inputs, and prop binding", Settings.ModEnabled.Value);
            modToggle.OnValueUpdated += val =>
            {
                DroneController.Instance?.SetModEnabled(val);
            };

            category.AddButton("Arm / Disarm Drone", IconName, "Toggle FPV flight mode and drone controls").OnPress += () =>
            {
                DroneController.Instance?.ToggleArm();
            };

            category.AddButton("Open Configurator", IconName, "Open the Pro-FPV Configurator (in-game VR Tablet or Desktop Overlay)").OnPress += () =>
            {
                DroneController.Instance?.OpenConfigMenu();
            };

            var vrCategory = page.AddCategory("VR & Quick Flight");

            vrCategory.AddButton("Reset Drone", IconName, "Reset drone upright in front of pilot").OnPress += () =>
            {
                DroneController.Instance?.ResetDroneToPlayer();
            };

            vrCategory.AddButton("Turtle Flip", IconName, "Trigger anti-turtle flip recovery when upside down").OnPress += () =>
            {
                DroneController.Instance?.TriggerTurtleFlip();
            };

            var modeButton = vrCategory.AddButton("Mode: " + Settings.Mode.Value, IconName, "Cycle flight mode (Acro / Angle / Position Hold)");
            modeButton.OnPress += () =>
            {
                Settings.CycleFlightMode();
                modeButton.ButtonText = "Mode: " + Settings.Mode.Value;
            };

            var losToggle = vrCategory.AddToggle(
                "LOS Fly Mode",
                "Fly Line-of-Sight from your own eyes/camera (Desktop & VR) without FPV goggles or OSD",
                Settings.LosFlyMode.Value);
            losToggle.OnValueUpdated += val =>
            {
                Settings.LosFlyMode.Value = val;
                MelonLoader.MelonPreferences.Save();
            };

            var vrViewButton = vrCategory.AddButton(
                "VR View: " + GetVrViewLabel(Settings.VrView.Value),
                IconName,
                "Cycle VR camera view: 2D Goggles, 3D Goggles, or Full 3D VR cockpit");
            vrViewButton.OnPress += () =>
            {
                Settings.VrView.Value = (VrViewMode)(((int)Settings.VrView.Value + 1) % 3);
                vrViewButton.ButtonText = "VR View: " + GetVrViewLabel(Settings.VrView.Value);
                MelonLoader.MelonPreferences.Save();
            };

            var cockpitHudToggle = vrCategory.AddToggle(
                "Full 3D VR HUD",
                "Show translucent head-tracked Betaflight OSD in Full 3D VR view",
                Settings.CockpitVrShowOsd.Value);
            cockpitHudToggle.OnValueUpdated += val =>
            {
                Settings.CockpitVrShowOsd.Value = val;
                MelonLoader.MelonPreferences.Save();
            };

            var goggleFovSlider = vrCategory.AddSlider(
                "VR Goggle Screen FOV",
                "Angular size (deg) of the virtual FPV goggle screen inside your headset",
                Settings.VrGoggleScreenFov.Value,
                35f,
                110f,
                decimalPlaces: 0,
                defaultValue: 68f,
                allowReset: true);
            goggleFovSlider.OnValueUpdated += val =>
            {
                Settings.VrGoggleScreenFov.Value = val;
                MelonLoader.MelonPreferences.Save();
            };

            page.OnVisibilityChanged += (visible, _) =>
            {
                if (!visible) return;
                modToggle.ToggleValue = Settings.ModEnabled.Value;
                losToggle.ToggleValue = Settings.LosFlyMode.Value;
                modeButton.ButtonText = "Mode: " + Settings.Mode.Value;
                vrViewButton.ButtonText = "VR View: " + GetVrViewLabel(Settings.VrView.Value);
                cockpitHudToggle.ToggleValue = Settings.CockpitVrShowOsd.Value;
                goggleFovSlider.SetSliderValue(Settings.VrGoggleScreenFov.Value);
            };

            var propCategory = page.AddCategory("Spawnable Prop Sync");

            propCategory.AddButton("Spawn Drone Prop", IconName, "Spawn the configured drone prop GUID (if you have permission)").OnPress += () =>
            {
                DroneController.Instance?.SpawnDroneProp();
            };

            propCategory.AddButton("Bind Drone Prop", IconName, "Auto-detect your spawned FPV_* prop (or bind your latest spawned prop)").OnPress += () =>
            {
                DroneController.Instance?.BindPropAuto();
            };

            propCategory.AddButton("Unbind Prop", IconName, "Release the currently bound CVRSpawnable prop").OnPress += () =>
            {
                DroneController.Instance?.UnbindProp();
            };

            var autoSpawnToggle = propCategory.AddToggle("Auto-Spawn Prop on Arm", "Automatically spawn the drone prop when arming if none is bound", Settings.AutoSpawnDroneProp.Value);
            autoSpawnToggle.OnValueUpdated += val =>
            {
                Settings.AutoSpawnDroneProp.Value = val;
                MelonLoader.MelonPreferences.Save();
            };

            var autoBindToggle = propCategory.AddToggle("Auto-Bind FPV Prop", "Automatically bind to your spawned prop with FPV_* parameters", Settings.AutoBindDroneProp.Value);
            autoBindToggle.OnValueUpdated += val =>
            {
                Settings.AutoBindDroneProp.Value = val;
                MelonLoader.MelonPreferences.Save();
            };

            var hideIndicatorToggle = propCategory.AddToggle("Hide Camera Indicator", "Hide the native CVR camera indicator while a prop is bound", Settings.SuppressIndicatorWhenPropBound.Value);
            hideIndicatorToggle.OnValueUpdated += val =>
            {
                Settings.SuppressIndicatorWhenPropBound.Value = val;
                MelonLoader.MelonPreferences.Save();
            };

            var debugAudioToggle = propCategory.AddToggle("Debug: Hear Prop Audio", "Hear the prop's generic 3D AudioSource instead of mod audio", Settings.DebugHearPropAudio.Value);
            debugAudioToggle.OnValueUpdated += val =>
            {
                Settings.DebugHearPropAudio.Value = val;
                MelonLoader.MelonPreferences.Save();
            };
        }

        private static string GetVrViewLabel(VrViewMode mode)
        {
            switch (mode)
            {
                case VrViewMode.Stereo3D: return "3D Goggles";
                case VrViewMode.CockpitVR3D: return "Full 3D VR";
                default: return "2D Goggles";
            }
        }
    }
}
