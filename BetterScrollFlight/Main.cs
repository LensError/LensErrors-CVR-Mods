using MelonLoader;

[assembly: MelonInfo(typeof(BetterScrollFlight.Main), "BetterScrollFlight", "1.0.0", "LensError")]
[assembly: MelonGame(null, "ChilloutVR")]

namespace BetterScrollFlight
{
    public class Main : MelonMod
    {
        FlightComponent m_component;

        public override void OnLateInitializeMelon()
        {
            Settings.Init();
            MelonLoader.MelonCoroutines.Start(WaitForRootLogic());
        }

        public override void OnDeinitializeMelon()
        {
            if (m_component != null)
                UnityEngine.Object.Destroy(m_component.gameObject);
            m_component = null;
        }

        System.Collections.IEnumerator WaitForRootLogic()
        {
            while (ABI_RC.Core.RootLogic.Instance == null)
                yield return null;
            while (ABI_RC.Core.Player.PlayerSetup.Instance == null)
                yield return null;

            m_component = new UnityEngine.GameObject("[BetterScrollFlight]").AddComponent<FlightComponent>();
        }
    }
}
