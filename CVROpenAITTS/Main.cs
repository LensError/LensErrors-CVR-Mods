using ABI_RC.Systems.Communications.Audio.TTS;
using ABI_RC.Systems.UI.UILib;
using MelonLoader;
using System.Reflection;

[assembly: MelonInfo(typeof(CVROPENAI.Main), "CVR AI Voice TTS", "1.0.0", "LensError")]
[assembly: MelonGame(null, "ChilloutVR")]

namespace CVROPENAI
{
    public class Main : MelonMod
    {
        const string SharedQuickMenuModName = "LensErrorsMods";
        const string SharedQuickMenuIconName = "lens_errors_mods";

        public override void OnInitializeMelon()
        {
            Comms_TTSHandler.AddModule<AIVoiceTTSModule>("AI_VOICE", "AI Voice");

            QuickMenuAPI.PrepareIcon("CVROpenAITTS", "voice_chat",
                Assembly.GetExecutingAssembly()
                      .GetManifestResourceStream("CVROpenAITTS.voice_chat_256dp_E3E3E3_FILL0_wght400_GRAD0_opsz48.png"));

            if (QuickMenuAPI.DoesIconExist(SharedQuickMenuModName, SharedQuickMenuIconName))
                return;

            var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(
                "CVROpenAITTS.resources.lens_errors_mods.png");

            if (stream != null)
                QuickMenuAPI.PrepareIcon(SharedQuickMenuModName, SharedQuickMenuIconName, stream);
        }

        public override void OnLateInitializeMelon()
        {
            Settings.Init();
        }
    }
}
