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

            category.AddButton("Open Configurator", IconName, "Open the on-screen Pro-FPV Configurator overlay").OnPress += () =>
            {
                DroneController.Instance?.OpenConfigMenu();
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
    }
}
