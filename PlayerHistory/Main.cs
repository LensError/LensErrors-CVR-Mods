using MelonLoader;

[assembly: MelonInfo(typeof(PlayerHistory.Main), "Player History", "1.0.0", "LensError")]
[assembly: MelonGame(null, "ChilloutVR")]

namespace PlayerHistory
{
    public class Main : MelonMod
    {
        public override void OnInitializeMelon()
        {
            BTKUILib.QuickMenuAPI.PrepareIcon("PlayerHistory", "groups",
                System.Reflection.Assembly.GetExecutingAssembly()
                      .GetManifestResourceStream("PlayerHistory.groups_256dp_E3E3E3_FILL0_wght400_GRAD0_opsz48.png"));
        }

        public override void OnLateInitializeMelon()
        {
            HistoryData.Load();
            Settings.Init();
            GameEvents.Init();
        }
    }
}
