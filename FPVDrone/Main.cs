using System.Collections;
using ABI_RC.Core;
using ABI_RC.Core.Player;
using MelonLoader;
using UnityEngine;

[assembly: MelonInfo(typeof(FPVDrone.Main), "FPV Drone", "1.0.0", "LensError")]
[assembly: MelonGame(null, "ChilloutVR")]

namespace FPVDrone
{
    public sealed class Main : MelonMod
    {
        private GameObject _droneHost;

        public override void OnInitializeMelon()
        {
            DroneQuickMenu.PrepareIcon();
        }

        public override void OnLateInitializeMelon()
        {
            Settings.Initialize();
            DroneQuickMenu.Build();
            MelonCoroutines.Start(WaitForPlayer());
        }

        public override void OnSceneWasUnloaded(int buildIndex, string sceneName)
        {
            DroneController.Instance?.OnWorldUnload();
        }

        public override void OnDeinitializeMelon()
        {
            if (_droneHost != null)
            {
                Object.Destroy(_droneHost);
                _droneHost = null;
            }
        }

        private IEnumerator WaitForPlayer()
        {
            while (RootLogic.Instance == null || PlayerSetup.Instance == null)
            {
                yield return null;
            }

            _droneHost = new GameObject("[FPVDrone_Host]");
            _droneHost.AddComponent<DroneController>();
            Object.DontDestroyOnLoad(_droneHost);
        }
    }
}

