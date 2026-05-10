namespace CVROPENAI
{
    class LocalAITTSModule : HttpTTSModule
    {
        protected override string ModuleId => "LOCAL_AI";

        internal static readonly string[] Presets =
        {
            "af_heart", "af_sarah", "af_sky", "af_bella", "af_nicole",
            "am_adam", "am_michael",
            "bf_emma", "bf_isabella",
            "bm_george", "bm_lewis",
            "alloy", "echo", "nova", "onyx", "shimmer",
        };

        protected override string[] PresetVoices => Presets;
    }
}
