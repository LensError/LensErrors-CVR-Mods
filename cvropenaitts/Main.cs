using ABI_RC.Systems.Communications.Audio.TTS;
using MelonLoader;

[assembly: MelonInfo(typeof(CVROPENAI.Main), "CVR OpenAI TTS", "1.0.0", "LensError")]
[assembly: MelonGame(null, "ChilloutVR")]

namespace CVROPENAI
{
    public class Main : MelonMod
    {
        public override void OnInitializeMelon()
        {
            // Must be called before Comms_TTSHandler.Instance is created (i.e. before Unity Awake)
            Comms_TTSHandler.AddModule<LocalAITTSModule>("LOCAL_AI", "Local AI TTS");
            Comms_TTSHandler.AddModule<OpenAITTSModule>("OPENAI_TTS", "OpenAI TTS");
        }

        public override void OnLateInitializeMelon()
        {
            Settings.Init();
        }
    }
}
