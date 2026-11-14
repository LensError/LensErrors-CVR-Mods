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

        private bool _wasPortableCameraActiveBeforeFlight = false;
        private PortableCamera.CameraPinMode _previousPinMode = PortableCamera.CameraPinMode.Default;

        public void StartSyncing()
        {
            var camera = PortableCamera.Instance;
            if (camera != null)
            {
                _wasPortableCameraActiveBeforeFlight = camera.IsActive();
                _previousPinMode = camera.CurrentPinMode;

                // Pin camera to world coordinates so its transform is decoupled from player/viewport frames
                camera.ApplyCameraPin(PortableCamera.CameraPinMode.PinToWorld);
                camera.SetDroneVisibility(true);

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
                if (!_wasPortableCameraActiveBeforeFlight)
                {
                    camera.SetDroneVisibility(false);
                    camera.ApplyCameraPin(_previousPinMode);

                    var indicator = LocalIndicatorField?.GetValue(camera) as CameraIndicator;
                    indicator?.SetActiveState(false);
                }
                else
                {
                    camera.ApplyCameraPin(_previousPinMode);
                }
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
