using MelonLoader;

namespace PlayerHistory
{
    public class Main : MelonMod
    {
        public override void OnLateInitializeMelon()
        {
            HistoryData.Load();
            Settings.Init();
            GameEvents.Init();
        }
    }
}
