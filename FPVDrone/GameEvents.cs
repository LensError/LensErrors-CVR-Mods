using ABI_RC.Core.InteractionSystem;
using ABI_RC.Core.Player;
using ABI_RC.Systems.InputManagement;
using ABI_RC.Systems.InputManagement.InputModules;
using HarmonyLib;

namespace FPVDrone
{
    [HarmonyPatch(typeof(PlayerSetup), "UpdatePlayerAvatarMovementData")]
    internal static class Patch_PlayerSetup_UpdatePlayerAvatarMovementData
    {
        [HarmonyPostfix]
        private static void Postfix(PlayerSetup __instance)
        {
            if (!DroneNetworkSync.IsSyncing)
                return;

            try
            {
                PlayerAvatarMovementData data = __instance.GetPlayerMovementData();
                if (data != null)
                {
                    data.CameraEnabled = true;
                    data.CameraPosition = DroneNetworkSync.CurrentDronePosition;
                    data.CameraRotation = DroneNetworkSync.CurrentDroneRotation.eulerAngles;
                }
            }
            catch (System.Exception ex)
            {
                MelonLoader.MelonLogger.Error("Error syncing drone camera movement data: " + ex);
            }
        }
    }

    [HarmonyPatch(typeof(CVRInputModule_XR), "Update_Movement")]
    internal static class Patch_CVRInputModuleXR_UpdateMovement
    {
        [HarmonyPrefix]
        private static bool Prefix()
        {
            return DroneController.Instance == null || !DroneController.Instance.IsArmed;
        }
    }

    [HarmonyPatch(typeof(CVRInputModule_XR), "Update_Look")]
    internal static class Patch_CVRInputModuleXR_UpdateLook
    {
        [HarmonyPrefix]
        private static bool Prefix()
        {
            return DroneController.Instance == null || !DroneController.Instance.IsArmed;
        }
    }

    [HarmonyPatch(typeof(CVRInputModule_XR), "Update_Binds")]
    internal static class Patch_CVRInputModuleXR_UpdateBinds
    {
        [HarmonyPrefix]
        private static bool Prefix()
        {
            return DroneController.Instance == null || !DroneController.Instance.IsArmed;
        }
    }

    [HarmonyPatch(typeof(CVRInputModule_XR), "Update_Emotes")]
    internal static class Patch_CVRInputModuleXR_UpdateEmotes
    {
        [HarmonyPrefix]
        private static bool Prefix()
        {
            return DroneController.Instance == null || !DroneController.Instance.IsArmed;
        }
    }

    [HarmonyPatch(typeof(CVRInputModule_XR), "Update_Vehicle")]
    internal static class Patch_CVRInputModuleXR_UpdateVehicle
    {
        [HarmonyPrefix]
        private static bool Prefix()
        {
            return DroneController.Instance == null || !DroneController.Instance.IsArmed;
        }
    }

    [HarmonyPatch(typeof(CVRInputModule_XR), "Update_Interaction")]
    internal static class Patch_CVRInputModuleXR_UpdateInteraction
    {
        [HarmonyPostfix]
        private static void Postfix()
        {
            bool isArmed = DroneController.Instance != null && DroneController.Instance.IsArmed;
            bool tabletPointed = UI.VrConfigTablet.IsPointingAtTablet;
            if (!isArmed && !tabletPointed)
                return;

            // Keep menu laser interactions working when CVR's own menus are open
            bool cvrMenuOpen = (ViewManager.Instance != null && ViewManager.Instance.IsAnyMenuOpen) ||
                               (CVR_MenuManager.Instance != null && CVR_MenuManager.Instance.IsViewShown);
            if (cvrMenuOpen)
                return;

            var im = CVRInputManager.Instance;
            if (im == null)
                return;

            // Preserve mainMenuButton / quickMenuButton and voice so user can always access menus/mic
            im.interactLeftDown = false;
            im.interactLeftUp = false;
            im.interactLeftDouble = false;
            im.interactLeftValue = 0f;
            im.gripLeftDown = false;
            im.gripLeftUp = false;
            im.gripLeftValue = 0f;

            im.interactRightDown = false;
            im.interactRightUp = false;
            im.gripRightDown = false;
            im.gripRightUp = false;
            im.gripRightValue = 0f;
            im.interactRightValue = 0f;

            im.drop = false;
            im.objectPushPull = 0f;
            im.objectRotation = false;
            im.scrollValue = 0f;
        }
    }
}

