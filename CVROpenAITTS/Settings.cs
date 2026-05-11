using System;
using BTKUILib;
using BTKUILib.UIObjects;
using BTKUILib.UIObjects.Components;
using BTKUILib.UIObjects.Objects;
using MelonLoader;

namespace CVROPENAI
{
    enum Backend { Kokoro = 0, Morpheus = 1, OpenAI = 2, Custom = 3 }

    static class Settings
    {
        const string CategoryId   = "CVROpenAITTS";
        const string CategoryName = "CVR AI Voice TTS";

        static MelonPreferences_Entry<int>    ms_backend     = null!;
        static MelonPreferences_Entry<string> ms_baseUrl     = null!;
        static MelonPreferences_Entry<string> ms_apiKey      = null!;
        static MelonPreferences_Entry<string> ms_model       = null!;
        static MelonPreferences_Entry<int>    ms_langIdx     = null!;
        static MelonPreferences_Entry<int>    ms_voiceIdx    = null!;
        static MelonPreferences_Entry<string> ms_customVoice = null!;
        static MelonPreferences_Entry<float>  ms_speed       = null!;

        internal static void Init()
        {
            var cat = MelonPreferences.CreateCategory(CategoryId, CategoryName);

            ms_backend     = cat.CreateEntry("Backend",     0,                       "Backend",      "TTS provider: 0=Kokoro, 1=Morpheus, 2=OpenAI, 3=Custom");
            ms_baseUrl     = cat.CreateEntry("BaseUrl",     "http://localhost:8880",  "Base URL",     "Server base URL (auto-filled when you pick a backend)");
            ms_apiKey      = cat.CreateEntry("ApiKey",      "",                       "API Key",      "Bearer token — required for OpenAI, leave blank for local servers");
            ms_model       = cat.CreateEntry("Model",       "kokoro",                 "Model",        "Model name sent in the request (auto-filled when you pick a backend)");
            ms_langIdx     = cat.CreateEntry("LangIdx",     0,                        "LangIdx",      "Selected language group index");
            ms_voiceIdx    = cat.CreateEntry("VoiceIdx",    0,                        "VoiceIdx",     "Selected voice index within the language group");
            ms_customVoice = cat.CreateEntry("CustomVoice", "",                       "Custom Voice", "Voice name override — takes priority over preset when non-empty");
            ms_speed       = cat.CreateEntry("Speed",       1.0f,                     "Speed",        "Speech speed (0.25 – 4.0)");

            BuildUI();
        }

        static void BuildUI()
        {
            var page = new Page(CategoryId, "Main", isRootPage: true, tabIcon: "voice_chat");
            page.MenuTitle    = "AI Voice TTS";
            page.MenuSubtitle = "Configure TTS provider and voice";

            // --- Provider ---
            var provCat = page.AddCategory("Provider");

            var backendSel = new MultiSelection(
                "Backend",
                new[] { "Kokoro (local)", "Morpheus (local)", "OpenAI", "Custom" },
                Math.Clamp(ms_backend.Value, 0, 3));

            backendSel.OnOptionUpdated += i =>
            {
                ms_backend.Value  = i;
                ms_langIdx.Value  = 0;
                ms_voiceIdx.Value = 0;
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
                    // Custom: leave whatever the user has
                }
            };

            provCat.AddButton("Backend", "", "Select TTS provider — auto-fills URL and model", ButtonStyle.TextOnly)
                .OnPress += () => QuickMenuAPI.OpenMultiSelect(backendSel);

            provCat.AddMelonStringInput(ms_baseUrl);
            provCat.AddMelonStringInput(ms_apiKey);
            provCat.AddMelonStringInput(ms_model);

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
                    int clampedIdx = Math.Clamp(ms_langIdx.Value, 0, langs.Length - 1);
                    var langSel = new MultiSelection("Language", langs, clampedIdx);
                    langSel.OnOptionUpdated += i =>
                    {
                        ms_langIdx.Value  = i;
                        ms_voiceIdx.Value = 0;
                    };
                    QuickMenuAPI.OpenMultiSelect(langSel);
                };

            // Voice preset button — list is built fresh each press based on current backend + language
            voiceCat.AddButton("Voice Preset", "", "Pick from preset voices for the selected backend", ButtonStyle.TextOnly)
                .OnPress += () =>
                {
                    Backend backend  = CurrentBackend;
                    string[] voices  = GetVoiceList(backend);
                    int clampedIdx   = Math.Clamp(ms_voiceIdx.Value, 0, voices.Length - 1);
                    var voiceSel     = new MultiSelection("Voice", voices, clampedIdx);
                    voiceSel.OnOptionUpdated += i =>
                    {
                        ms_voiceIdx.Value    = i;
                        ms_customVoice.Value = "";
                    };
                    QuickMenuAPI.OpenMultiSelect(voiceSel);
                };

            voiceCat.AddMelonStringInput(ms_customVoice);

            // --- Audio ---
            var audioCat = page.AddCategory("Audio");
            audioCat.AddMelonSlider(ms_speed, 0.25f, 4.0f, decimalPlaces: 2, allowReset: true);
        }

        static Backend CurrentBackend =>
            (Backend)Math.Clamp(ms_backend?.Value ?? 0, 0, 3);

        static string[] GetVoiceList(Backend backend)
        {
            switch (backend)
            {
                case Backend.Kokoro:
                    int kLang = Math.Clamp(ms_langIdx?.Value ?? 0, 0, KokoroVoices.VoicesByLanguage.Length - 1);
                    return KokoroVoices.VoicesByLanguage[kLang];
                case Backend.Morpheus:
                    int mLang = Math.Clamp(ms_langIdx?.Value ?? 0, 0, MorpheusVoices.VoicesByLanguage.Length - 1);
                    return MorpheusVoices.VoicesByLanguage[mLang];
                case Backend.OpenAI:
                    return OpenAIVoices.Presets;
                default:
                    return new[] { "Set Custom Voice field below" };
            }
        }

        // Accessors used by HttpTTSModule
        public static string GetBaseUrl() => ms_baseUrl?.Value ?? "http://localhost:8880";
        public static string GetApiKey()  => ms_apiKey?.Value  ?? "";
        public static string GetModel()   => ms_model?.Value   ?? "kokoro";
        public static float  GetSpeed()   => ms_speed?.Value   ?? 1.0f;

        public static string GetVoice()
        {
            string custom = ms_customVoice?.Value ?? "";
            if (!string.IsNullOrEmpty(custom)) return custom;

            Backend backend  = CurrentBackend;
            int langIdx      = ms_langIdx?.Value  ?? 0;
            int voiceIdx     = ms_voiceIdx?.Value ?? 0;

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
                default:
                    return "";
            }
        }
    }
}
