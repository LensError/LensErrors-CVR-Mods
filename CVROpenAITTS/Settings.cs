using System;
using ABI_RC.Systems.UI.UILib;
using ABI_RC.Systems.UI.UILib.UIObjects;
using ABI_RC.Systems.UI.UILib.UIObjects.Components;
using ABI_RC.Systems.UI.UILib.UIObjects.Objects;
using MelonLoader;

namespace CVROPENAI
{
    enum Backend { Kokoro = 0, Morpheus = 1, OpenAI = 2, F5FastAPI = 3, Custom = 4 }

    static class Settings
    {
        const string CategoryId   = "CVROpenAITTS";
        const string CategoryName = "CVR AI Voice TTS";
        const string SharedQuickMenuModName = "LensErrorsMods";
        const string SharedQuickMenuPageName = "Main";
        const string SharedQuickMenuIconName = "lens_errors_mods";

        static MelonPreferences_Entry<int>    ms_backend     = null!;
        static MelonPreferences_Entry<string> ms_baseUrl     = null!;
        static MelonPreferences_Entry<string> ms_customBaseUrl = null!;
        static MelonPreferences_Entry<string> ms_apiKey      = null!;
        static MelonPreferences_Entry<string> ms_model       = null!;
        static MelonPreferences_Entry<int>    ms_langIdx     = null!;
        static MelonPreferences_Entry<int>    ms_voiceIdx    = null!;
        static MelonPreferences_Entry<int>    ms_kokoroLangIdx = null!;
        static MelonPreferences_Entry<int>    ms_morpheusLangIdx = null!;
        static MelonPreferences_Entry<int>    ms_kokoroVoiceIdx = null!;
        static MelonPreferences_Entry<int>    ms_morpheusVoiceIdx = null!;
        static MelonPreferences_Entry<int>    ms_openAIVoiceIdx = null!;
        static MelonPreferences_Entry<int>    ms_customVoiceIdx = null!;
        static MelonPreferences_Entry<string> ms_customVoice = null!;
        static MelonPreferences_Entry<float>  ms_speed       = null!;

        internal static void Init()
        {
            var cat = MelonPreferences.CreateCategory(CategoryId, CategoryName);

            ms_backend     = cat.CreateEntry("Backend",     0,                       "Backend",      "TTS provider: 0=Kokoro, 1=Morpheus, 2=OpenAI, 3=F5 FastAPI, 4=Custom");
            ms_baseUrl     = cat.CreateEntry("BaseUrl",     "http://localhost:8880",  "Base URL",     "Server base URL (auto-filled when you pick a backend)");
            ms_customBaseUrl = cat.CreateEntry("CustomBaseUrl", ms_baseUrl.Value,     "CustomBaseUrl", "Saved base URL for the custom backend");
            ms_apiKey      = cat.CreateEntry("ApiKey",      "",                       "API Key",      "Bearer token — required for OpenAI, leave blank for local servers");
            ms_model       = cat.CreateEntry("Model",       "kokoro",                 "Model",        "Model name sent in the request (auto-filled when you pick a backend)");
            ms_langIdx     = cat.CreateEntry("LangIdx",     0,                        "LangIdx",      "Selected language group index");
            ms_voiceIdx    = cat.CreateEntry("VoiceIdx",    0,                        "VoiceIdx",     "Selected voice index within the language group");
            ms_kokoroLangIdx = cat.CreateEntry("KokoroLangIdx", ms_langIdx.Value,      "KokoroLangIdx", "Selected Kokoro language group index");
            ms_morpheusLangIdx = cat.CreateEntry("MorpheusLangIdx", ms_langIdx.Value,  "MorpheusLangIdx", "Selected Morpheus language group index");
            ms_kokoroVoiceIdx = cat.CreateEntry("KokoroVoiceIdx", ms_voiceIdx.Value,   "KokoroVoiceIdx", "Selected Kokoro voice index");
            ms_morpheusVoiceIdx = cat.CreateEntry("MorpheusVoiceIdx", ms_voiceIdx.Value, "MorpheusVoiceIdx", "Selected Morpheus voice index");
            ms_openAIVoiceIdx = cat.CreateEntry("OpenAIVoiceIdx", ms_voiceIdx.Value,   "OpenAIVoiceIdx", "Selected OpenAI voice index");
            ms_customVoiceIdx = cat.CreateEntry("CustomVoiceIdx", ms_voiceIdx.Value,   "CustomVoiceIdx", "Selected custom backend voice index");
            ms_customVoice = cat.CreateEntry("CustomVoice", "",                       "Custom Voice", "Voice name override — takes priority over preset when non-empty");
            ms_speed       = cat.CreateEntry("Speed",       1.0f,                     "Speed",        "Speech speed (0.25 – 4.0)");

            BuildUI();
        }

        static void BuildUI()
        {
            var sharedPage = Page.GetOrCreatePage(
                SharedQuickMenuModName,
                SharedQuickMenuPageName,
                isRootPage: true,
                tabIcon: SharedQuickMenuIconName);
            sharedPage.MenuTitle = "LensError's Mods";
            sharedPage.MenuSubtitle = "Installed mod settings and actions";

            var category = sharedPage.AddCategory(CategoryName);
            var page = category.AddPage(
                "Settings",
                "voice_chat",
                "Configure TTS provider and voice",
                CategoryId);
            page.MenuTitle    = "AI Voice TTS";
            page.MenuSubtitle = "Configure TTS provider and voice";

            // --- Provider ---
            var provCat = page.AddCategory("Provider");

            var backendSel = new MultiSelection(
                "Backend",
                new[] { "Kokoro (local)", "Morpheus (local)", "OpenAI", "F5 FastAPI", "Custom" },
                Math.Clamp(ms_backend.Value, 0, 4));

            backendSel.OnOptionUpdated += i =>
            {
                SaveCurrentBackendSettings();
                ms_backend.Value  = i;
                switch ((Backend)i)
                {
                    case Backend.Kokoro:
                        ms_baseUrl.Value = "http://localhost:8880";
                        ms_model.Value   = "kokoro";
                        break;
                    case Backend.Morpheus:
                        ms_baseUrl.Value = "http://localhost:5005";
                        ms_model.Value   = "tts-1";
                        break;
                    case Backend.OpenAI:
                        ms_baseUrl.Value = "https://api.openai.com";
                        ms_model.Value   = "tts-1";
                        break;
                    case Backend.F5FastAPI:
                        ms_baseUrl.Value = "http://localhost:8000";
                        ms_model.Value   = "f5-tts";
                        break;
                    case Backend.Custom:
                        ms_baseUrl.Value = ms_customBaseUrl.Value;
                        break;
                }
            };

            provCat.AddButton("Backend", "", "Select TTS provider — auto-fills URL and model", ButtonStyle.TextOnly)
                .OnPress += () => QuickMenuAPI.OpenMultiSelect(backendSel);

            AddPreferenceTextInput(provCat, ms_baseUrl, "Base URL");
            AddPreferenceTextInput(provCat, ms_apiKey, "API Key", InputType.Password);
            AddPreferenceTextInput(provCat, ms_model, "Model");

            // --- Voice ---
            var voiceCat = page.AddCategory("Voice");

            // Language button — relevant for Kokoro and Morpheus; shows a note for others
            voiceCat.AddButton("Language", "", "Pick language group (Kokoro & Morpheus)", ButtonStyle.TextOnly)
                .OnPress += () =>
                {
                    Backend backend = CurrentBackend;
                    string[] langs = backend switch
                    {
                        Backend.Kokoro   => KokoroVoices.Languages,
                        Backend.Morpheus => MorpheusVoices.Languages,
                        _                => new[] { "Not applicable — use Custom Voice" },
                    };
                    int clampedIdx = Math.Clamp(GetCurrentLanguageIndex(), 0, langs.Length - 1);
                    var langSel = new MultiSelection("Language", langs, clampedIdx);
                    langSel.OnOptionUpdated += i =>
                    {
                        if (backend == Backend.Kokoro || backend == Backend.Morpheus)
                        {
                            SetCurrentLanguageIndex(i);
                            SetCurrentVoiceIndex(0);
                        }
                    };
                    QuickMenuAPI.OpenMultiSelect(langSel);
                };

            // Voice preset button — list is built fresh each press based on current backend + language
            voiceCat.AddButton("Voice Preset", "", "Pick from preset voices for the selected backend", ButtonStyle.TextOnly)
                .OnPress += () =>
                {
                    Backend backend  = CurrentBackend;
                    string[] voices  = GetVoiceList(backend);
                    int clampedIdx   = Math.Clamp(GetCurrentVoiceIndex(), 0, voices.Length - 1);
                    var voiceSel     = new MultiSelection("Voice", voices, clampedIdx);
                    voiceSel.OnOptionUpdated += i =>
                    {
                        SetCurrentVoiceIndex(i);
                        ms_customVoice.Value = "";
                    };
                    QuickMenuAPI.OpenMultiSelect(voiceSel);
                };

            AddPreferenceTextInput(voiceCat, ms_customVoice, "Custom Voice");

            // --- Audio ---
            var audioCat = page.AddCategory("Audio");
            audioCat.AddSlider(
                    "Speed",
                    "Speech speed",
                    ms_speed.Value,
                    0.25f,
                    4.0f,
                    2,
                    1.0f,
                    true)
                .OnValueUpdated += value => ms_speed.Value = value;
        }

        static void AddPreferenceTextInput(
            Category category,
            MelonPreferences_Entry<string> entry,
            string placeholder,
            InputType inputType = InputType.Text)
        {
            var input = category.AddTextInput(entry.Value, placeholder, inputType);
            input.OnTextUpdate += value => entry.Value = value ?? "";
        }

        static Backend CurrentBackend =>
            (Backend)Math.Clamp(ms_backend?.Value ?? 0, 0, 4);

        static void SaveCurrentBackendSettings()
        {
            if (CurrentBackend == Backend.Custom)
                ms_customBaseUrl.Value = ms_baseUrl.Value;
        }

        static int GetCurrentLanguageIndex()
        {
            return CurrentBackend switch
            {
                Backend.Kokoro   => ms_kokoroLangIdx?.Value ?? ms_langIdx?.Value ?? 0,
                Backend.Morpheus => ms_morpheusLangIdx?.Value ?? ms_langIdx?.Value ?? 0,
                _                => ms_langIdx?.Value ?? 0,
            };
        }

        static void SetCurrentLanguageIndex(int value)
        {
            ms_langIdx.Value = value;
            switch (CurrentBackend)
            {
                case Backend.Kokoro:
                    ms_kokoroLangIdx.Value = value;
                    break;
                case Backend.Morpheus:
                    ms_morpheusLangIdx.Value = value;
                    break;
            }
        }

        static int GetCurrentVoiceIndex()
        {
            return CurrentBackend switch
            {
                Backend.Kokoro   => ms_kokoroVoiceIdx?.Value ?? ms_voiceIdx?.Value ?? 0,
                Backend.Morpheus => ms_morpheusVoiceIdx?.Value ?? ms_voiceIdx?.Value ?? 0,
                Backend.OpenAI   => ms_openAIVoiceIdx?.Value ?? ms_voiceIdx?.Value ?? 0,
                Backend.Custom   => ms_customVoiceIdx?.Value ?? ms_voiceIdx?.Value ?? 0,
                _                => ms_voiceIdx?.Value ?? 0,
            };
        }

        static void SetCurrentVoiceIndex(int value)
        {
            ms_voiceIdx.Value = value;
            switch (CurrentBackend)
            {
                case Backend.Kokoro:
                    ms_kokoroVoiceIdx.Value = value;
                    break;
                case Backend.Morpheus:
                    ms_morpheusVoiceIdx.Value = value;
                    break;
                case Backend.OpenAI:
                    ms_openAIVoiceIdx.Value = value;
                    break;
                case Backend.Custom:
                    ms_customVoiceIdx.Value = value;
                    break;
            }
        }

        static string[] GetVoiceList(Backend backend)
        {
            switch (backend)
            {
                case Backend.Kokoro:
                    int kLang = Math.Clamp(GetCurrentLanguageIndex(), 0, KokoroVoices.VoicesByLanguage.Length - 1);
                    return KokoroVoices.VoicesByLanguage[kLang];
                case Backend.Morpheus:
                    int mLang = Math.Clamp(GetCurrentLanguageIndex(), 0, MorpheusVoices.VoicesByLanguage.Length - 1);
                    return MorpheusVoices.VoicesByLanguage[mLang];
                case Backend.OpenAI:
                    return OpenAIVoices.Presets;
                case Backend.F5FastAPI:
                    return new[] { "Set Custom Voice field below" };
                default:
                    return new[] { "Set Custom Voice field below" };
            }
        }

        // Accessors used by HttpTTSModule
        public static string GetBaseUrl() => ms_baseUrl?.Value ?? "http://localhost:8880";
        public static string GetApiKey()  => ms_apiKey?.Value  ?? "";
        public static string GetModel()   => ms_model?.Value   ?? "kokoro";
        public static float  GetSpeed()   => ms_speed?.Value   ?? 1.0f;
        public static bool   IsF5FastAPI() => CurrentBackend == Backend.F5FastAPI;

        public static string GetVoice()
        {
            string custom = ms_customVoice?.Value ?? "";
            if (!string.IsNullOrEmpty(custom)) return custom;

            Backend backend  = CurrentBackend;
            int langIdx      = GetCurrentLanguageIndex();
            int voiceIdx     = GetCurrentVoiceIndex();

            switch (backend)
            {
                case Backend.Kokoro:
                    langIdx = Math.Clamp(langIdx, 0, KokoroVoices.VoicesByLanguage.Length - 1);
                    string[] kv = KokoroVoices.VoicesByLanguage[langIdx];
                    return kv[Math.Clamp(voiceIdx, 0, kv.Length - 1)];
                case Backend.Morpheus:
                    langIdx = Math.Clamp(langIdx, 0, MorpheusVoices.VoicesByLanguage.Length - 1);
                    string[] mv = MorpheusVoices.VoicesByLanguage[langIdx];
                    return mv[Math.Clamp(voiceIdx, 0, mv.Length - 1)];
                case Backend.OpenAI:
                    return OpenAIVoices.Presets[Math.Clamp(voiceIdx, 0, OpenAIVoices.Presets.Length - 1)];
                case Backend.F5FastAPI:
                    return "";
                default:
                    return "";
            }
        }
    }
}
