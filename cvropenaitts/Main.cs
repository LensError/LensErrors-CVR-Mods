using ABI_RC.Systems.Communications.Audio.TTS;
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
        }

        public override void OnLateInitializeMelon()
        {
            Settings.Init();
        }
    }
}
