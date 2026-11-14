using ABI_RC.Core.Player;
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
}

