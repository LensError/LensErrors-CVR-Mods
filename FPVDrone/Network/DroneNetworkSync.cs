using System;
using System.Reflection;
using ABI_RC.Core.Player;
using ABI_RC.Systems.Camera;
using UnityEngine;

namespace FPVDrone
{
    public class DroneNetworkSync
    {
        public static bool IsSyncing { get; private set; } = false;
        public static Vector3 CurrentDronePosition { get; private set; } = Vector3.zero;
        public static Quaternion CurrentDroneRotation { get; private set; } = Quaternion.identity;

        private static readonly FieldInfo LocalIndicatorField =
            typeof(PortableCamera).GetField("_localCameraIndicator", BindingFlags.Instance | BindingFlags.NonPublic);

        // GS2 PortableCamera API (CameraPlacement + VisualMods)
        private static readonly PropertyInfo PlacementProp =
            typeof(PortableCamera).GetProperty("Placement", BindingFlags.Instance | BindingFlags.Public);
        private static readonly MethodInfo EnableModByTypeMethod =
            typeof(PortableCamera).GetMethod("EnableModByType", BindingFlags.Instance | BindingFlags.Public);
        private static readonly MethodInfo DisableModByTypeMethod =
            typeof(PortableCamera).GetMethod("DisableModByType", BindingFlags.Instance | BindingFlags.Public);
        private static readonly Type AttachToWorldType =
            typeof(PortableCamera).Assembly.GetType("ABI_RC.Systems.Camera.VisualMods.AttachToWorld");
        private static readonly Type AttachToPlayerType =
            typeof(PortableCamera).Assembly.GetType("ABI_RC.Systems.Camera.VisualMods.AttachToPlayer");

        // Stable PortableCamera API (CameraPinMode)
        private static readonly PropertyInfo CurrentPinModeProp =
            typeof(PortableCamera).GetProperty("CurrentPinMode", BindingFlags.Instance | BindingFlags.Public);
        private static readonly MethodInfo ApplyCameraPinMethod =
            typeof(PortableCamera).GetMethod("ApplyCameraPin", BindingFlags.Instance | BindingFlags.Public);
        private static readonly MethodInfo SetDroneVisibilityMethod =
            typeof(PortableCamera).GetMethod("SetDroneVisibility", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly Type CameraPinModeType =
            typeof(PortableCamera).GetNestedType("CameraPinMode", BindingFlags.Public);

        private static readonly bool IsGs2CameraApi =
            PlacementProp != null && EnableModByTypeMethod != null && DisableModByTypeMethod != null && AttachToWorldType != null;

        private bool _wasPortableCameraActiveBeforeFlight = false;
        private string _previousPlacementName = "Frame";
        private object _previousPinMode = null;

        public void StartSyncing()
        {
            var camera = PortableCamera.Instance;
            if (camera != null)
            {
                _wasPortableCameraActiveBeforeFlight = camera.IsActive();

                if (IsGs2CameraApi)
                {
                    _previousPlacementName = PlacementProp.GetValue(camera)?.ToString() ?? "Frame";
                    if (_previousPlacementName != "World")
                        EnableModByTypeMethod.Invoke(camera, new object[] { AttachToWorldType });
                }
                else if (CurrentPinModeProp != null && ApplyCameraPinMethod != null && CameraPinModeType != null)
                {
                    _previousPinMode = CurrentPinModeProp.GetValue(camera);
                    object pinToWorld = Enum.Parse(CameraPinModeType, "PinToWorld");
                    ApplyCameraPinMethod.Invoke(camera, new object[] { pinToWorld });
                    SetDroneVisibilityMethod?.Invoke(camera, new object[] { true });
                }

                var indicator = LocalIndicatorField?.GetValue(camera) as CameraIndicator;
                indicator?.SetActiveState(true);
            }

            IsSyncing = true;
        }

        public void StopSyncing()
        {
            if (!IsSyncing)
                return;

            IsSyncing = false;

            var camera = PortableCamera.Instance;
            if (camera != null)
            {
                if (IsGs2CameraApi)
                {
                    RestoreGs2Placement(camera, _previousPlacementName);
                }
                else if (ApplyCameraPinMethod != null && _previousPinMode != null)
                {
                    if (!_wasPortableCameraActiveBeforeFlight)
                        SetDroneVisibilityMethod?.Invoke(camera, new object[] { false });

                    ApplyCameraPinMethod.Invoke(camera, new object[] { _previousPinMode });
                }

                if (!_wasPortableCameraActiveBeforeFlight)
                {
                    var indicator = LocalIndicatorField?.GetValue(camera) as CameraIndicator;
                    indicator?.SetActiveState(false);
                }
            }
        }

        private static void RestoreGs2Placement(PortableCamera camera, string placementName)
        {
            switch (placementName)
            {
                case "Frame":
                    DisableModByTypeMethod.Invoke(camera, new object[] { AttachToWorldType });
                    if (AttachToPlayerType != null)
                        DisableModByTypeMethod.Invoke(camera, new object[] { AttachToPlayerType });
                    break;
                case "Player":
                    if (AttachToPlayerType != null)
                        EnableModByTypeMethod.Invoke(camera, new object[] { AttachToPlayerType });
                    break;
                case "World":
                    EnableModByTypeMethod.Invoke(camera, new object[] { AttachToWorldType });
                    break;
            }
        }

        public void UpdateSync(Vector3 dronePosition, Quaternion droneRotation)
        {
            if (!IsSyncing)
                return;

            // Include mechanical camera uptilt angle so remote player indicators face camera line-of-sight
            Quaternion camUptilt = Quaternion.Euler(-Settings.CameraTiltAngle.Value, 0.0f, 0.0f);
            Quaternion camWorldRot = droneRotation * camUptilt;

            CurrentDronePosition = dronePosition;
            CurrentDroneRotation = camWorldRot;

            var camera = PortableCamera.Instance;
            if (camera != null && camera.CameraComponent != null)
            {
                camera.CameraComponent.transform.SetPositionAndRotation(dronePosition, camWorldRot);
            }
        }
    }
}
