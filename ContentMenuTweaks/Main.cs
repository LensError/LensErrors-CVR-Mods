using MelonLoader;
using UnityEngine;

[assembly: MelonInfo(typeof(ContentMenuTweaks.Main), "Content Menu Tweaks", "0.1.0", "LensError")]
[assembly: MelonGame(null, "ChilloutVR")]

namespace ContentMenuTweaks
{
    public class Main : MelonMod
    {
        RecentContentComponent _component;

        public override void OnInitializeMelon()
        {
            Settings.Init();
            GameEvents.Init(HarmonyInstance);
        }

        public override void OnLateInitializeMelon()
        {
            MelonCoroutines.Start(WaitForGame());
        }

        public override void OnDeinitializeMelon()
        {
            Settings.FlushSave();

            if (_component != null)
                Object.Destroy(_component.gameObject);

            _component = null;
            GameEvents.Deinit();
        }

        System.Collections.IEnumerator WaitForGame()
        {
            while (ABI_RC.Core.RootLogic.Instance == null)
                yield return null;

            _component = new GameObject("[ContentMenuTweaks]").AddComponent<RecentContentComponent>();
            Object.DontDestroyOnLoad(_component.gameObject);
        }
    }
}
