using ABI_RC.Systems.Communications.Audio.TTS;
using BTKUILib;
using MelonLoader;

[assembly: MelonInfo(typeof(CVROPENAI.Main), "CVR AI Voice TTS", "2.0.0", "LensError")]
[assembly: MelonGame(null, "ChilloutVR")]

namespace CVROPENAI
{
    public class Main : MelonMod
    {
        public override void OnInitializeMelon()
        {
            Comms_TTSHandler.AddModule<AIVoiceTTSModule>("AI_VOICE", "AI Voice");

            QuickMenuAPI.PrepareIcon("CVROpenAITTS", "voice_chat",
                System.Reflection.Assembly.GetExecutingAssembly()
                      .GetManifestResourceStream("CVROpenAITTS.voice_chat_256dp_E3E3E3_FILL0_wght400_GRAD0_opsz48.png"));
        }

        public override void OnLateInitializeMelon()
        {
            Settings.Init();
        }
    }
}
