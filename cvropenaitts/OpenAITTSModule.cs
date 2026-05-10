namespace CVROPENAI
{
    class OpenAITTSModule : HttpTTSModule
    {
        protected override string ModuleId => "OPENAI_TTS";

        internal static readonly string[] Presets =
        {
            "alloy", "ash", "ballad", "coral", "echo", "fable", "nova", "onyx", "sage", "shimmer",
        };

        protected override string[] PresetVoices => Presets;
    }
}
