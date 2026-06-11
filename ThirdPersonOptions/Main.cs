using MelonLoader;
using UnityEngine;

[assembly: MelonInfo(typeof(ThirdPersonOptions.Main), "ThirdPersonOptions", "1.0.0", "LensError")]
[assembly: MelonGame(null, "ChilloutVR")]

namespace ThirdPersonOptions
{
    public class Main : MelonMod
    {
        ThirdPersonOptionsComponent _component;

        public override void OnInitializeMelon()
        {
            GameEvents.Init(HarmonyInstance);
        }

        public override void OnLateInitializeMelon()
        {
            Settings.Init();
            MelonCoroutines.Start(WaitForPlayer());
        }

        public override void OnDeinitializeMelon()
        {
            if (_component != null)
                Object.Destroy(_component.gameObject);

            _component = null;
        }

        System.Collections.IEnumerator WaitForPlayer()
        {
            while (ABI_RC.Core.RootLogic.Instance == null)
                yield return null;

            while (ABI_RC.Core.Player.PlayerSetup.Instance == null)
                yield return null;

            _component = new GameObject("[ThirdPersonOptions]").AddComponent<ThirdPersonOptionsComponent>();
        }
    }
}
