using System;
using BTKUILib;
using BTKUILib.UIObjects;
using BTKUILib.UIObjects.Components;
using BTKUILib.UIObjects.Objects;
using MelonLoader;

namespace CVROPENAI
{
    static class Settings
    {
        const string CategoryId   = "CVROpenAITTS";
        const string CategoryName = "CVR OpenAI TTS";

        // Local AI entries
        static MelonPreferences_Entry<string> ms_localBaseUrl    = null!;
        static MelonPreferences_Entry<string> ms_localModel      = null!;
        static MelonPreferences_Entry<int>    ms_localPresetIdx  = null!;
        static MelonPreferences_Entry<string> ms_localCustom     = null!;
        static MelonPreferences_Entry<float>  ms_localSpeed      = null!;

        // OpenAI entries
        static MelonPreferences_Entry<string> ms_openBaseUrl     = null!;
        static MelonPreferences_Entry<string> ms_openApiKey      = null!;
        static MelonPreferences_Entry<string> ms_openModel       = null!;
        static MelonPreferences_Entry<int>    ms_openPresetIdx   = null!;
        static MelonPreferences_Entry<string> ms_openCustom      = null!;
        static MelonPreferences_Entry<float>  ms_openSpeed       = null!;

        internal static void Init()
        {
            var cat = MelonPreferences.CreateCategory(CategoryId, CategoryName);

            ms_localBaseUrl   = cat.CreateEntry("LocalAI_BaseUrl",       "http://localhost:8880", "Local AI: Base URL",     "Base URL of your local OpenAI-compatible TTS server");
            ms_localModel     = cat.CreateEntry("LocalAI_Model",         "kokoro",                "Local AI: Model",        "Model name sent in the request");
            ms_localPresetIdx = cat.CreateEntry("LocalAI_PresetVoiceIdx", 0,                      "Local AI: Preset Voice", "Index of the selected preset voice");
            ms_localCustom    = cat.CreateEntry("LocalAI_CustomVoice",   "",                      "Local AI: Custom Voice", "Custom voice name — overrides preset when non-empty");
            ms_localSpeed     = cat.CreateEntry("LocalAI_Speed",         1.0f,                    "Local AI: Speed",        "Speech speed (0.25 – 4.0)");

            ms_openBaseUrl    = cat.CreateEntry("OpenAI_BaseUrl",        "https://api.openai.com","OpenAI: Base URL",       "Base URL — change to use any OpenAI-compatible endpoint");
            ms_openApiKey     = cat.CreateEntry("OpenAI_ApiKey",         "",                      "OpenAI: API Key",        "Bearer token sent in the Authorization header");
            ms_openModel      = cat.CreateEntry("OpenAI_Model",          "tts-1",                 "OpenAI: Model",          "Model name (tts-1, tts-1-hd, …)");
            ms_openPresetIdx  = cat.CreateEntry("OpenAI_PresetVoiceIdx", 0,                       "OpenAI: Preset Voice",   "Index of the selected preset voice");
            ms_openCustom     = cat.CreateEntry("OpenAI_CustomVoice",    "",                      "OpenAI: Custom Voice",   "Custom voice name — overrides preset when non-empty");
            ms_openSpeed      = cat.CreateEntry("OpenAI_Speed",          1.0f,                    "OpenAI: Speed",          "Speech speed (0.25 – 4.0)");

            BuildUI();
        }

        static void BuildUI()
        {
            var page = new Page(CategoryId, "Main", isRootPage: true);
            page.MenuTitle    = "CVR OpenAI TTS";
            page.MenuSubtitle = "Local AI & OpenAI TTS settings";

            // --- Local AI ---
            var localCat = page.AddCategory("Local AI TTS");
            localCat.AddMelonStringInput(ms_localBaseUrl);
            localCat.AddMelonStringInput(ms_localModel);

            var localSel = new MultiSelection(
                "Local AI Voice",
                LocalAITTSModule.Presets,
                Math.Clamp(ms_localPresetIdx.Value, 0, LocalAITTSModule.Presets.Length - 1));
            localSel.OnOptionUpdated += i =>
            {
                ms_localPresetIdx.Value = i;
                ms_localCustom.Value    = "";   // preset clears custom override
            };
            localCat.AddButton("Voice Preset", "", "Pick from preset voice list", ButtonStyle.TextOnly)
                .OnPress += () => QuickMenuAPI.OpenMultiSelect(localSel);
            localCat.AddMelonStringInput(ms_localCustom);
            localCat.AddMelonSlider(ms_localSpeed, 0.25f, 4.0f, decimalPlaces: 2, allowReset: true);

            // --- OpenAI ---
            var openCat = page.AddCategory("OpenAI TTS");
            openCat.AddMelonStringInput(ms_openBaseUrl);
            openCat.AddMelonStringInput(ms_openApiKey);
            openCat.AddMelonStringInput(ms_openModel);

            var openSel = new MultiSelection(
                "OpenAI Voice",
                OpenAITTSModule.Presets,
                Math.Clamp(ms_openPresetIdx.Value, 0, OpenAITTSModule.Presets.Length - 1));
            openSel.OnOptionUpdated += i =>
            {
                ms_openPresetIdx.Value = i;
                ms_openCustom.Value    = "";
            };
            openCat.AddButton("Voice Preset", "", "Pick from preset voice list", ButtonStyle.TextOnly)
                .OnPress += () => QuickMenuAPI.OpenMultiSelect(openSel);
            openCat.AddMelonStringInput(ms_openCustom);
            openCat.AddMelonSlider(ms_openSpeed, 0.25f, 4.0f, decimalPlaces: 2, allowReset: true);
        }

        // Accessors used by HttpTTSModule
        public static string GetBaseUrl(string moduleId) =>
            moduleId == "LOCAL_AI"
                ? ms_localBaseUrl?.Value ?? "http://localhost:8880"
                : ms_openBaseUrl?.Value  ?? "https://api.openai.com";

        public static string GetApiKey(string moduleId) =>
            moduleId == "LOCAL_AI" ? "" : ms_openApiKey?.Value ?? "";

        public static string GetModel(string moduleId) =>
            moduleId == "LOCAL_AI"
                ? ms_localModel?.Value ?? "kokoro"
                : ms_openModel?.Value  ?? "tts-1";

        public static string GetVoice(string moduleId)
        {
            if (moduleId == "LOCAL_AI")
            {
                string custom = ms_localCustom?.Value ?? "";
                if (!string.IsNullOrEmpty(custom)) return custom;
                int idx = Math.Clamp(ms_localPresetIdx?.Value ?? 0, 0, LocalAITTSModule.Presets.Length - 1);
                return LocalAITTSModule.Presets[idx];
            }
            else
            {
                string custom = ms_openCustom?.Value ?? "";
                if (!string.IsNullOrEmpty(custom)) return custom;
                int idx = Math.Clamp(ms_openPresetIdx?.Value ?? 0, 0, OpenAITTSModule.Presets.Length - 1);
                return OpenAITTSModule.Presets[idx];
            }
        }

        public static float GetSpeed(string moduleId) =>
            moduleId == "LOCAL_AI"
                ? ms_localSpeed?.Value ?? 1.0f
                : ms_openSpeed?.Value  ?? 1.0f;
    }
}
