using System;
using MelonLoader;

namespace ExternalTTS
{
    public enum TTSProvider
    {
        FishAudio = 0,
        Kokoro = 1
    }

    public static class Settings
    {
        private const string CategoryName = "ExternalTTS";

        private static MelonPreferences_Category _category;

        private static MelonPreferences_Entry<bool> _entryEnabled;
        private static MelonPreferences_Entry<TTSProvider> _entryProvider;
        private static MelonPreferences_Entry<bool> _entrySpeakChatBox;
        private static MelonPreferences_Entry<bool> _entrySpeakOSC;
        private static MelonPreferences_Entry<bool> _entryFallbackToSystem;

        // Fish Audio
        private static MelonPreferences_Entry<bool> _entryFishUseCloud;
        private static MelonPreferences_Entry<string> _entryFishCloudEndpoint;
        private static MelonPreferences_Entry<string> _entryFishLocalEndpoint;
        private static MelonPreferences_Entry<string> _entryFishApiKey;
        private static MelonPreferences_Entry<bool> _entryFishUseProModel;
        private static MelonPreferences_Entry<string> _entryFishCustomModel;
        private static MelonPreferences_Entry<string> _entryFishVoiceId;

        // Kokoro
        private static MelonPreferences_Entry<string> _entryKokoroEndpoint;
        private static MelonPreferences_Entry<string> _entryKokoroPrimaryVoice;
        private static MelonPreferences_Entry<string> _entryKokoroSecondaryVoice;
        private static MelonPreferences_Entry<bool> _entryKokoroUseBlending;
        private static MelonPreferences_Entry<float> _entryKokoroBlendWeight;
        private static MelonPreferences_Entry<string> _entryKokoroCustomBlend;
        private static MelonPreferences_Entry<float> _entryKokoroSpeed;

        public static Action OnSettingsChanged;

        public static bool Enabled => _entryEnabled?.Value ?? true;
        public static TTSProvider Provider => _entryProvider?.Value ?? TTSProvider.FishAudio;
        public static bool SpeakChatBox => _entrySpeakChatBox?.Value ?? true;
        public static bool SpeakOSC => _entrySpeakOSC?.Value ?? true;
        public static bool FallbackToSystem => _entryFallbackToSystem?.Value ?? false;

        // Fish Audio Getters
        public static bool FishUseCloud => _entryFishUseCloud?.Value ?? true;
        public static string FishCloudEndpoint => _entryFishCloudEndpoint?.Value ?? "https://api.fish.audio/v1/tts";
        public static string FishLocalEndpoint => _entryFishLocalEndpoint?.Value ?? "http://127.0.0.1:8080/v1/tts";
        public static string FishApiKey => _entryFishApiKey?.Value ?? string.Empty;
        public static bool FishUseProModel => _entryFishUseProModel?.Value ?? true;
        public static string FishCustomModel => _entryFishCustomModel?.Value ?? string.Empty;
        public static string FishVoiceId => _entryFishVoiceId?.Value ?? string.Empty;

        public static string ActiveFishEndpoint => FishUseCloud ? FishCloudEndpoint : FishLocalEndpoint;
        public static string ActiveFishModel
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(FishCustomModel))
                    return FishCustomModel.Trim();
                return FishUseProModel ? "s2.1-pro" : "s2.1-pro-free";
            }
        }

        // Kokoro Getters
        public static string KokoroEndpoint => _entryKokoroEndpoint?.Value ?? "http://127.0.0.1:8880/v1/audio/speech";
        public static string KokoroPrimaryVoice => _entryKokoroPrimaryVoice?.Value ?? "af_heart";
        public static string KokoroSecondaryVoice => _entryKokoroSecondaryVoice?.Value ?? "af_bella";
        public static bool KokoroUseBlending => _entryKokoroUseBlending?.Value ?? false;
        public static float KokoroBlendWeight => _entryKokoroBlendWeight?.Value ?? 0.5f;
        public static string KokoroCustomBlend => _entryKokoroCustomBlend?.Value ?? string.Empty;
        public static float KokoroSpeed => _entryKokoroSpeed?.Value ?? 1.0f;

        public static void Initialize()
        {
            _category = MelonPreferences.CreateCategory(CategoryName, "External Text-to-Speech");

            _entryEnabled = _category.CreateEntry("Enabled", true, "Enable Mod", "Enable or disable External TTS");
            _entryProvider = _category.CreateEntry("Provider", TTSProvider.FishAudio, "Active Provider", "Active TTS provider (Fish Audio or Kokoro)");
            _entrySpeakChatBox = _category.CreateEntry("SpeakChatBox", true, "Speak ChatBox", "Automatically speak outgoing ChatBox messages");
            _entrySpeakOSC = _category.CreateEntry("SpeakOSC", true, "Speak OSC Messages", "Automatically speak messages received through OSC /chatbox/input");
            _entryFallbackToSystem = _category.CreateEntry("FallbackToSystem", false, "Fallback to System", "Fallback to native system TTS if external request fails");

            // Fish Audio
            _entryFishUseCloud = _category.CreateEntry("FishUseCloud", true, "Fish Cloud Mode", "Use Fish Audio Cloud API instead of local");
            _entryFishCloudEndpoint = _category.CreateEntry("FishCloudEndpoint", "https://api.fish.audio/v1/tts", "Fish Cloud Endpoint", "Fish Audio Cloud API endpoint");
            _entryFishLocalEndpoint = _category.CreateEntry("FishLocalEndpoint", "http://127.0.0.1:8080/v1/tts", "Fish Local Endpoint", "Fish Audio local server endpoint");
            _entryFishApiKey = _category.CreateEntry("FishApiKey", string.Empty, "Fish API Key", "Fish Audio API Bearer Token");
            _entryFishUseProModel = _category.CreateEntry("FishUseProModel", true, "Fish Pro Model", "Use s2.1-pro (true) or s2.1-pro-free (false)");
            _entryFishCustomModel = _category.CreateEntry("FishCustomModel", string.Empty, "Fish Custom Model", "Custom model override if set");
            _entryFishVoiceId = _category.CreateEntry("FishVoiceId", string.Empty, "Fish Custom Voice ID", "Custom reference_id model ID");

            // Kokoro
            _entryKokoroEndpoint = _category.CreateEntry("KokoroEndpoint", "http://127.0.0.1:8880/v1/audio/speech", "Kokoro Endpoint", "Kokoro-FastAPI speech endpoint");
            _entryKokoroPrimaryVoice = _category.CreateEntry("KokoroPrimaryVoice", "af_heart", "Kokoro Primary Voice", "Primary voice ID");
            _entryKokoroSecondaryVoice = _category.CreateEntry("KokoroSecondaryVoice", "af_bella", "Kokoro Secondary Voice", "Secondary voice ID for blending");
            _entryKokoroUseBlending = _category.CreateEntry("KokoroUseBlending", false, "Kokoro Voice Blending", "Enable combining primary and secondary voices");
            _entryKokoroBlendWeight = _category.CreateEntry("KokoroBlendWeight", 0.5f, "Kokoro Blend Ratio", "Ratio between primary (1.0) and secondary (0.0)");
            _entryKokoroCustomBlend = _category.CreateEntry("KokoroCustomBlend", string.Empty, "Kokoro Custom Expression", "Raw custom blend expression (e.g. af_bella(2)+af_heart(1))");
            _entryKokoroSpeed = _category.CreateEntry("KokoroSpeed", 1.0f, "Kokoro Speed", "Voice speech speed");
        }

        public static void SetEnabled(bool value)
        {
            if (_entryEnabled != null) _entryEnabled.Value = value;
            NotifyChanged();
        }

        public static void SetProvider(TTSProvider provider)
        {
            if (_entryProvider != null) _entryProvider.Value = provider;
            NotifyChanged();
        }

        public static void SetSpeakChatBox(bool value)
        {
            if (_entrySpeakChatBox != null) _entrySpeakChatBox.Value = value;
            NotifyChanged();
        }

        public static void SetSpeakOSC(bool value)
        {
            if (_entrySpeakOSC != null) _entrySpeakOSC.Value = value;
            NotifyChanged();
        }

        public static void SetFishUseCloud(bool value)
        {
            if (_entryFishUseCloud != null) _entryFishUseCloud.Value = value;
            NotifyChanged();
        }

        public static void SetFishCloudEndpoint(string value)
        {
            if (_entryFishCloudEndpoint != null) _entryFishCloudEndpoint.Value = value;
            NotifyChanged();
        }

        public static void SetFishLocalEndpoint(string value)
        {
            if (_entryFishLocalEndpoint != null) _entryFishLocalEndpoint.Value = value;
            NotifyChanged();
        }

        public static void SetFishApiKey(string value)
        {
            if (_entryFishApiKey != null) _entryFishApiKey.Value = value;
            NotifyChanged();
        }

        public static void SetFishUseProModel(bool value)
        {
            if (_entryFishUseProModel != null) _entryFishUseProModel.Value = value;
            NotifyChanged();
        }

        public static void SetFishVoiceId(string value)
        {
            if (_entryFishVoiceId != null) _entryFishVoiceId.Value = value;
            NotifyChanged();
        }

        public static void SetKokoroEndpoint(string value)
        {
            if (_entryKokoroEndpoint != null) _entryKokoroEndpoint.Value = value;
            NotifyChanged();
        }

        public static void SetKokoroPrimaryVoice(string value)
        {
            if (_entryKokoroPrimaryVoice != null) _entryKokoroPrimaryVoice.Value = value;
            NotifyChanged();
        }

        public static void SetKokoroSecondaryVoice(string value)
        {
            if (_entryKokoroSecondaryVoice != null) _entryKokoroSecondaryVoice.Value = value;
            NotifyChanged();
        }

        public static void SetKokoroUseBlending(bool value)
        {
            if (_entryKokoroUseBlending != null) _entryKokoroUseBlending.Value = value;
            NotifyChanged();
        }

        public static void SetKokoroBlendWeight(float value)
        {
            if (_entryKokoroBlendWeight != null) _entryKokoroBlendWeight.Value = value;
            NotifyChanged();
        }

        public static void SetKokoroCustomBlend(string value)
        {
            if (_entryKokoroCustomBlend != null) _entryKokoroCustomBlend.Value = value;
            NotifyChanged();
        }

        public static void SetKokoroSpeed(float value)
        {
            if (_entryKokoroSpeed != null) _entryKokoroSpeed.Value = value;
            NotifyChanged();
        }

        private static void NotifyChanged()
        {
            _category?.SaveToFile(false);
            OnSettingsChanged?.Invoke();
        }
    }
}

