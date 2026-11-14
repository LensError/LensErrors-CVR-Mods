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

            category.AddButton("Arm / Disarm Drone", IconName, "Toggle FPV flight mode and drone controls").OnPress += () =>
            {
                DroneController.Instance?.ToggleArm();
            };

            category.AddButton("Open Configurator", IconName, "Open the on-screen Pro-FPV Configurator overlay").OnPress += () =>
            {
                DroneController.Instance?.OpenConfigMenu();
            };
        }
    }
}
