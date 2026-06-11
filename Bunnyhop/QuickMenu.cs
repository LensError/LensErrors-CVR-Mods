using System.Reflection;
using ABI_RC.Systems.UI.UILib;
using ABI_RC.Systems.UI.UILib.UIObjects;
using ABI_RC.Systems.UI.UILib.UIObjects.Components;

namespace Bunnyhop
{
    static class QuickMenu
    {
        const string SharedQuickMenuModName = "LensErrorsMods";
        const string SharedQuickMenuPageName = "Main";
        const string SharedQuickMenuIconName = "lens_errors_mods";

        static ToggleButton s_currentAvatarToggle;

        internal static void PrepareSharedIcon()
        {
            if (QuickMenuAPI.DoesIconExist(SharedQuickMenuModName, SharedQuickMenuIconName))
                return;

            var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(
                "Bunnyhop.resources.lens_errors_mods.png");

            if (stream != null)
                QuickMenuAPI.PrepareIcon(SharedQuickMenuModName, SharedQuickMenuIconName, stream);
        }

        internal static void Build()
        {
            var page = Page.GetOrCreatePage(
                SharedQuickMenuModName,
                SharedQuickMenuPageName,
                isRootPage: true,
                tabIcon: SharedQuickMenuIconName);

            page.MenuTitle = "LensError's Mods";
            page.MenuSubtitle = "Installed mod settings and actions";

            var category = page.AddCategory("Bunnyhop");

            category.AddToggle(
                    "Enabled",
                    "Increase horizontal speed while repeatedly jumping",
                    Settings.Enabled)
                .OnValueUpdated += Settings.SetEnabled;

            s_currentAvatarToggle = category.AddToggle(
                "Disable For Current Avatar",
                "Always disable Bunnyhop while wearing the current avatar",
                Settings.DisabledForCurrentAvatar);
            s_currentAvatarToggle.OnValueUpdated += Settings.SetDisabledForCurrentAvatar;

            category.AddSlider(
                    "Speed Per Jump",
                    "Horizontal speed multiplier applied on every chained jump",
                    Settings.JumpMultiplier,
                    1f,
                    2f,
                    2,
                    1.5f,
                    true)
                .OnValueUpdated += Settings.SetJumpMultiplier;

            category.AddSlider(
                    "Maximum Speed",
                    "Maximum multiple of CVR's current world-adjusted movement speed",
                    Settings.MaxSpeedMultiplier,
                    1f,
                    10f,
                    1,
                    3f,
                    true)
                .OnValueUpdated += Settings.SetMaxSpeedMultiplier;

            category.AddSlider(
                    "Reset Delay",
                    "Seconds spent grounded before the bunnyhop chain resets",
                    Settings.GroundResetDelay,
                    0.1f,
                    3f,
                    1,
                    0.5f,
                    true)
                .OnValueUpdated += Settings.SetGroundResetDelay;
        }

        internal static void UpdateCurrentAvatarToggle()
        {
            if (s_currentAvatarToggle != null)
                s_currentAvatarToggle.ToggleValue = Settings.DisabledForCurrentAvatar;
        }
    }
}
