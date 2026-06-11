using ABI_RC.Systems.Movement;
using ABI_RC.Systems.InputManagement;
using HarmonyLib;
using MelonLoader;
using System;
using System.Reflection;
using UnityEngine;

namespace ThirdPersonOptions
{
    static class GameEvents
    {
        static readonly FieldInfo DesktopCameraField = typeof(BetterCharacterLook).GetField(
            "_desktopCamera",
            BindingFlags.Instance | BindingFlags.NonPublic);

        static readonly FieldInfo CameraPivotTransformField = typeof(BetterCharacterLook).GetField(
            "cameraPivotTransform",
            BindingFlags.Instance | BindingFlags.NonPublic);

        internal static void Init(HarmonyLib.Harmony harmony)
        {
            try
            {
                var cameraPositionMethod = typeof(BetterCharacterLook).GetMethod(
                    "ApplyCameraLocalPosition",
                    BindingFlags.Instance | BindingFlags.NonPublic);

                var cameraPositionPostfix = typeof(GameEvents).GetMethod(
                    nameof(ApplyCameraLocalPositionPostfix),
                    BindingFlags.Static | BindingFlags.NonPublic);

                var collisionMethod = typeof(BetterCharacterLook).GetMethod(
                    "ApplyCollisionToTarget",
                    BindingFlags.Instance | BindingFlags.NonPublic);

                var collisionPrefix = typeof(GameEvents).GetMethod(
                    nameof(ApplyCollisionToTargetPrefix),
                    BindingFlags.Static | BindingFlags.NonPublic);

                var collisionPostfix = typeof(GameEvents).GetMethod(
                    nameof(ApplyCollisionToTargetPostfix),
                    BindingFlags.Static | BindingFlags.NonPublic);

                var thirdPersonDistanceMethod = typeof(BetterCharacterLook).GetMethod(
                    "UpdateThirdPersonDistance",
                    BindingFlags.Instance | BindingFlags.NonPublic);

                var thirdPersonDistancePrefix = typeof(GameEvents).GetMethod(
                    nameof(UpdateThirdPersonDistancePrefix),
                    BindingFlags.Static | BindingFlags.NonPublic);

                if (cameraPositionMethod == null || cameraPositionPostfix == null
                    || collisionMethod == null || collisionPrefix == null || collisionPostfix == null
                    || thirdPersonDistanceMethod == null || thirdPersonDistancePrefix == null)
                {
                    MelonLogger.Warning("Could not find BetterCharacterLook third-person camera methods.");
                    return;
                }

                harmony.Patch(cameraPositionMethod, postfix: new HarmonyMethod(cameraPositionPostfix));
                harmony.Patch(
                    collisionMethod,
                    prefix: new HarmonyMethod(collisionPrefix),
                    postfix: new HarmonyMethod(collisionPostfix));
                harmony.Patch(thirdPersonDistanceMethod, prefix: new HarmonyMethod(thirdPersonDistancePrefix));
            }
            catch (Exception ex)
            {
                MelonLogger.Error(ex);
            }
        }

        static void UpdateThirdPersonDistancePrefix()
        {
            try
            {
                if (!ThirdPersonOptionsComponent.IsCtrlTHeld())
                    return;

                if (CVRInputManager.Instance != null)
                    CVRInputManager.Instance.toggleThirdPerson = false;
            }
            catch (Exception ex)
            {
                MelonLogger.Error(ex);
            }
        }

        static void ApplyCollisionToTargetPrefix(BetterCharacterLook __instance, out Quaternion? __state)
        {
            __state = null;
            if (!IsFrontFacing(__instance))
                return;

            var pivot = CameraPivotTransformField?.GetValue(__instance) as Transform;
            if (pivot == null)
                return;

            __state = pivot.rotation;
            pivot.rotation *= Quaternion.Euler(0f, 180f, 0f);
        }

        static void ApplyCollisionToTargetPostfix(BetterCharacterLook __instance, Quaternion? __state)
        {
            if (!__state.HasValue)
                return;

            var pivot = CameraPivotTransformField?.GetValue(__instance) as Transform;
            if (pivot != null)
                pivot.rotation = __state.Value;
        }

        static void ApplyCameraLocalPositionPostfix(BetterCharacterLook __instance)
        {
            try
            {
                var camera = DesktopCameraField?.GetValue(__instance) as Camera;
                if (camera == null)
                    return;

                var transform = camera.transform;
                if (IsFrontFacing(__instance))
                {
                    var position = transform.localPosition;
                    position.z = -position.z;
                    transform.localPosition = position;
                    transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                }
                else
                {
                    transform.localRotation = Quaternion.identity;
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error(ex);
            }
        }

        static bool IsFrontFacing(BetterCharacterLook look)
        {
            return Settings.Enabled
                && look.IsInThirdPerson
                && Settings.CameraPosition == Settings.CameraPositionOption.Front;
        }
    }
}
