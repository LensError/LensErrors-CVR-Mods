using MelonLoader;

[assembly: MelonInfo(typeof(CVRTrainer.Main), "CVR Trainer", "0.1.0", "LensError")]
[assembly: MelonGame(null, "ChilloutVR")]

namespace CVRTrainer
{
    public class Main : MelonMod
    {
        TrainerMenuComponent _component;

        public override void OnLateInitializeMelon()
        {
            Settings.Init();
            MelonCoroutines.Start(WaitForRootLogic());
        }

        public override void OnDeinitializeMelon()
        {
            if (_component != null)
                UnityEngine.Object.Destroy(_component.gameObject);

            _component = null;
        }

        System.Collections.IEnumerator WaitForRootLogic()
        {
            while (ABI_RC.Core.RootLogic.Instance == null)
                yield return null;
            while (ABI_RC.Core.Player.PlayerSetup.Instance == null)
                yield return null;

            _component = new UnityEngine.GameObject("[CVRTrainer]").AddComponent<TrainerMenuComponent>();
        }
    }
}
