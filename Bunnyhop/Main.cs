using MelonLoader;
using UnityEngine;

[assembly: MelonInfo(typeof(Bunnyhop.Main), "Bunnyhop", "1.0.0", "LensError")]
[assembly: MelonGame(null, "ChilloutVR")]

namespace Bunnyhop
{
    public class Main : MelonMod
    {
        BunnyhopComponent _component;

        public override void OnInitializeMelon()
        {
            QuickMenu.PrepareSharedIcon();
        }

        public override void OnLateInitializeMelon()
        {
            Settings.Init();
            QuickMenu.Build();
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

            _component = new GameObject("[Bunnyhop]").AddComponent<BunnyhopComponent>();
            Object.DontDestroyOnLoad(_component.gameObject);
        }
    }
}
