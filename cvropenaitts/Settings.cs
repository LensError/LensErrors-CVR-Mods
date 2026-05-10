using System;
using System.Reflection;
using ABI_RC.Systems.Communications.Audio.TTS;
using ABI_RC.Systems.Communications.TTS;
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
        static MelonPreferences_Entry<string> ms_localBaseUrl  = null!;
        static MelonPreferences_Entry<string> ms_localModel    = null!;
        static MelonPreferences_Entry<string> ms_localCustom   = null!;
        static MelonPreferences_Entry<float>  ms_localSpeed    = null!;

        // OpenAI entries
        static MelonPreferences_Entry<string> ms_openBaseUrl   = null!;
        static MelonPreferences_Entry<string> ms_openApiKey    = null!;
        static MelonPreferences_Entry<string> ms_openModel     = null!;
        static MelonPreferences_Entry<string> ms_openCustom    = null!;
        static MelonPreferences_Entry<float>  ms_openSpeed     = null!;

        // Cached MultiSelections — needed for SyncSelections()
        static MultiSelection? ms_localSel;
        static MultiSelection? ms_openSel;

        // Reflection handles — resolved once at Init() time
        static readonly MethodInfo?   s_changeVoice   = typeof(Comms_TTSHandler).GetMethod("ChangeVoice",   BindingFlags.Instance | BindingFlags.NonPublic);
        static readonly PropertyInfo? s_currentModule = typeof(Comms_TTSHandler).GetProperty("CurrentModule", BindingFlags.Instance | BindingFlags.NonPublic);

        internal static void Init()
        {
            var cat = MelonPreferences.CreateCategory(CategoryId, CategoryName);

            ms_localBaseUrl = cat.CreateEntry("LocalAI_BaseUrl",     "http://localhost:8880", "Local AI: Base URL",     "Base URL of your local OpenAI-compatible TTS server");
            ms_localModel   = cat.CreateEntry("LocalAI_Model",       "kokoro",                "Local AI: Model",        "Model name sent in the request");
            ms_localCustom  = cat.CreateEntry("LocalAI_CustomVoice", "",                      "Local AI: Custom Voice", "Custom voice name — overrides preset when non-empty");
            ms_localSpeed   = cat.CreateEntry("LocalAI_Speed",       1.0f,                    "Local AI: Speed",        "Speech speed (0.25 – 4.0)");

            ms_openBaseUrl  = cat.CreateEntry("OpenAI_BaseUrl",      "https://api.openai.com","OpenAI: Base URL",       "Base URL — change to use any OpenAI-compatible endpoint");
            ms_openApiKey   = cat.CreateEntry("OpenAI_ApiKey",       "",                      "OpenAI: API Key",        "Bearer token sent in the Authorization header");
            ms_openModel    = cat.CreateEntry("OpenAI_Model",        "tts-1",                 "OpenAI: Model",          "Model name (tts-1, tts-1-hd, …)");
            ms_openCustom   = cat.CreateEntry("OpenAI_CustomVoice",  "",                      "OpenAI: Custom Voice",   "Custom voice name — overrides preset when non-empty");
            ms_openSpeed    = cat.CreateEntry("OpenAI_Speed",        1.0f,                    "OpenAI: Speed",          "Speech speed (0.25 – 4.0)");

            // When a custom voice is typed, push it into the game's voice system
            ms_localCustom.OnEntryValueChangedUntyped.Subscribe(OnCustomVoiceChanged);
            ms_openCustom.OnEntryValueChangedUntyped.Subscribe(OnCustomVoiceChanged);

            BuildUI();
        }

        static void BuildUI()
        {
            var page = new Page(CategoryId, "Main", isRootPage: true);
            page.MenuTitle    = "CVR OpenAI TTS";
            page.MenuSubtitle = "Local AI & OpenAI TTS settings";
            page.OnPageOpen  += SyncSelections;

            // --- Local AI ---
            var localCat = page.AddCategory("Local AI TTS");
            localCat.AddMelonStringInput(ms_localBaseUrl);
            localCat.AddMelonStringInput(ms_localModel);

            ms_localSel = new MultiSelection("Local AI Voice", LocalAITTSModule.Presets, 0);
            ms_localSel.OnOptionUpdated += i =>
            {
                ms_localCustom.Value = "";                    // preset clears any custom override
                ApplyVoice(LocalAITTSModule.Presets[i]);
            };
            localCat.AddButton("Voice Preset", "", "Pick from preset voice list", ButtonStyle.TextOnly)
                .OnPress += () => QuickMenuAPI.OpenMultiSelect(ms_localSel);
            localCat.AddMelonStringInput(ms_localCustom);
            localCat.AddMelonSlider(ms_localSpeed, 0.25f, 4.0f, decimalPlaces: 2, allowReset: true);

            // --- OpenAI ---
            var openCat = page.AddCategory("OpenAI TTS");
            openCat.AddMelonStringInput(ms_openBaseUrl);
            openCat.AddMelonStringInput(ms_openApiKey);
            openCat.AddMelonStringInput(ms_openModel);

            ms_openSel = new MultiSelection("OpenAI Voice", OpenAITTSModule.Presets, 0);
            ms_openSel.OnOptionUpdated += i =>
            {
                ms_openCustom.Value = "";
                ApplyVoice(OpenAITTSModule.Presets[i]);
            };
            openCat.AddButton("Voice Preset", "", "Pick from preset voice list", ButtonStyle.TextOnly)
                .OnPress += () => QuickMenuAPI.OpenMultiSelect(ms_openSel);
            openCat.AddMelonStringInput(ms_openCustom);
            openCat.AddMelonSlider(ms_openSpeed, 0.25f, 4.0f, decimalPlaces: 2, allowReset: true);
        }

        // Called when our BTK page is opened — updates the dropdowns to match CurrentVoice
        static void SyncSelections()
        {
            try
            {
                string voice = GetCurrentVoice();
                if (string.IsNullOrEmpty(voice)) return;

                int localIdx = Array.IndexOf(LocalAITTSModule.Presets, voice);
                if (localIdx >= 0) ms_localSel?.SetSelectedOptionWithoutAction(localIdx);

                int openIdx = Array.IndexOf(OpenAITTSModule.Presets, voice);
                if (openIdx >= 0) ms_openSel?.SetSelectedOptionWithoutAction(openIdx);
            }
            catch (Exception e) { MelonLogger.Error(e); }
        }

        // Called when the user saves a custom voice via keyboard
        static void OnCustomVoiceChanged(object _, object newVal)
        {
            try
            {
                if (newVal is string voice && !string.IsNullOrEmpty(voice))
                    ApplyVoice(voice);
            }
            catch (Exception e) { MelonLogger.Error(e); }
        }

        // Pushes a voice change through the game's own ChangeVoice() so both UIs stay in sync
        static void ApplyVoice(string voiceId)
        {
            var handler = Comms_TTSHandler.Instance;
            if (handler == null) return;

            // Custom voices may not be in the Voices dict yet — add before calling ChangeVoice
            var module = s_currentModule?.GetValue(handler) as Comms_TTSModule;
            if (module != null && !module.Voices.ContainsKey(voiceId))
                module.Voices[voiceId] = voiceId;

            s_changeVoice?.Invoke(handler, new object[] { voiceId });
        }

        static string GetCurrentVoice()
        {
            var handler = Comms_TTSHandler.Instance;
            if (handler == null) return "";
            var module = s_currentModule?.GetValue(handler) as Comms_TTSModule;
            return module?.CurrentVoice ?? "";
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

        // Returns the custom voice, or "" so Process() falls back to CurrentVoice
        public static string GetVoice(string moduleId) =>
            moduleId == "LOCAL_AI"
                ? ms_localCustom?.Value ?? ""
                : ms_openCustom?.Value  ?? "";

        public static float GetSpeed(string moduleId) =>
            moduleId == "LOCAL_AI"
                ? ms_localSpeed?.Value ?? 1.0f
                : ms_openSpeed?.Value  ?? 1.0f;
    }
}
