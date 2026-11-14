using System;
using System.Reflection;
using BTKUILib;
using BTKUILib.UIObjects;
using BTKUILib.UIObjects.Components;
using ExternalTTS.Services;
using MelonLoader;

namespace ExternalTTS.UI
{
    public static class QuickMenuManager
    {
        private const string SharedQuickMenuModName = "LensErrorsMods";
        private const string SharedQuickMenuPageName = "Main";
        private const string SharedQuickMenuIconName = "lens_errors_mods";

        private static bool _menuBuilt;

        // Main controls
        private static Button _providerBtn;
        private static Button _testVoiceBtn;

        // Fish Audio controls
        private static ToggleButton _fishCloudToggle;
        private static Button _fishEndpointBtn;
        private static Button _fishApiKeyBtn;
        private static ToggleButton _fishProModelToggle;
        private static Button _fishVoiceIdBtn;

        // Kokoro controls
        private static Button _kokoroEndpointBtn;
        private static Button _kokoroPrimaryVoiceBtn;
        private static ToggleButton _kokoroBlendingToggle;
        private static Button _kokoroSecondaryVoiceBtn;
        private static SliderFloat _kokoroBlendSlider;
        private static Button _kokoroCustomBlendBtn;

        public static void PrepareIcon()
        {
            try
            {
                if (QuickMenuAPI.DoesIconExist(SharedQuickMenuModName, SharedQuickMenuIconName))
                {
                    return;
                }

                var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("ExternalTTS.resources.lens_errors_mods.png");
                if (stream != null)
                {
                    QuickMenuAPI.PrepareIcon(SharedQuickMenuModName, SharedQuickMenuIconName, stream);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[ExternalTTS] Failed to prepare shared icon: {ex.Message}");
            }
        }

        public static void BuildMenu()
        {
            if (_menuBuilt) return;
            _menuBuilt = true;

            try
            {
                Page sharedPage = Page.GetOrCreatePage(
                    SharedQuickMenuModName,
                    SharedQuickMenuPageName,
                    isRootPage: true,
                    tabIcon: SharedQuickMenuIconName);

                sharedPage.MenuTitle = "LensError's Mods";
                sharedPage.MenuSubtitle = "Installed mod settings and actions";

                Category cat = sharedPage.AddCategory("External TTS");

                // Main Toggle
                cat.AddToggle("Enabled", "Enable or disable External Text-to-Speech", Settings.Enabled)
                    .OnValueUpdated += Settings.SetEnabled;

                // Provider Switcher Button
                _providerBtn = cat.AddButton(
                    GetProviderButtonText(),
                    "",
                    "Toggle between Fish Audio and Kokoro local TTS");
                _providerBtn.OnPress += OnToggleProvider;

                // ChatBox and OSC Toggles
                cat.AddToggle("Speak ChatBox", "Automatically voice messages sent to the native chatbox", Settings.SpeakChatBox)
                    .OnValueUpdated += Settings.SetSpeakChatBox;

                cat.AddToggle("Speak OSC", "Automatically voice messages received from OSC (/chatbox/input)", Settings.SpeakOSC)
                    .OnValueUpdated += Settings.SetSpeakOSC;

                // Test Voice Button
                _testVoiceBtn = cat.AddButton("Test Voice", "", "Synthesize a test sentence using the current settings");
                _testVoiceBtn.OnPress += () =>
                {
                    GameEvents.SpeakMessage("Testing an External Text To Speech Mod, does it sound good?");
                    QuickMenuAPI.ShowAlertToast("Sent test voice message to TTS pipeline", 2);
                };

                // Subpages
                BuildFishAudioSubpage(cat);
                BuildKokoroSubpage(cat);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[ExternalTTS] Failed to build QuickMenu UI: {ex.Message}");
            }
        }

        private static void BuildFishAudioSubpage(Category rootCategory)
        {
            Page fishPage = rootCategory.AddPage(
                "Fish Audio Settings",
                "",
                "Configure Fish Audio cloud or local options",
                "Fish Audio");

            Category connCat = fishPage.AddCategory("Connection & Mode");

            _fishCloudToggle = connCat.AddToggle("Use Cloud Endpoint", "Toggle between Fish Audio Cloud and local server", Settings.FishUseCloud);
            _fishCloudToggle.OnValueUpdated += val =>
            {
                Settings.SetFishUseCloud(val);
                UpdateFishButtons();
            };

            Button autoFillBtn = connCat.AddButton("Auto-fill Default Endpoint", "", "Auto-fill endpoint based on Cloud/Local mode");
            autoFillBtn.OnPress += () =>
            {
                if (Settings.FishUseCloud)
                {
                    Settings.SetFishCloudEndpoint("https://api.fish.audio/v1/tts");
                }
                else
                {
                    Settings.SetFishLocalEndpoint("http://127.0.0.1:8080/v1/tts");
                }
                UpdateFishButtons();
                QuickMenuAPI.ShowAlertToast("Endpoint set to default for current mode", 2);
            };

            _fishEndpointBtn = connCat.AddButton(GetFishEndpointButtonText(), "", "Click to edit endpoint URL");
            _fishEndpointBtn.OnPress += () =>
            {
                string current = Settings.ActiveFishEndpoint;
                QuickMenuAPI.OpenKeyboard(current, (newVal) =>
                {
                    if (string.IsNullOrWhiteSpace(newVal)) return;
                    if (Settings.FishUseCloud)
                        Settings.SetFishCloudEndpoint(newVal.Trim());
                    else
                        Settings.SetFishLocalEndpoint(newVal.Trim());
                    UpdateFishButtons();
                });
            };

            _fishApiKeyBtn = connCat.AddButton(GetFishApiKeyButtonText(), "", "Click to enter your Fish Audio API Key (Bearer token)");
            _fishApiKeyBtn.OnPress += () =>
            {
                QuickMenuAPI.OpenKeyboard(Settings.FishApiKey, (newKey) =>
                {
                    Settings.SetFishApiKey(newKey != null ? newKey.Trim() : string.Empty);
                    UpdateFishButtons();
                });
            };

            Category modelCat = fishPage.AddCategory("Model & Voice");

            _fishProModelToggle = modelCat.AddToggle("Pro Model (s2.1-pro)", "Enable for s2.1-pro, disable for free tier s2.1-pro-free", Settings.FishUseProModel);
            _fishProModelToggle.OnValueUpdated += val =>
            {
                Settings.SetFishUseProModel(val);
            };

            _fishVoiceIdBtn = modelCat.AddButton(GetFishVoiceIdButtonText(), "", "Click to set a custom Voice Reference ID");
            _fishVoiceIdBtn.OnPress += () =>
            {
                QuickMenuAPI.OpenKeyboard(Settings.FishVoiceId, (newId) =>
                {
                    Settings.SetFishVoiceId(newId != null ? newId.Trim() : string.Empty);
                    UpdateFishButtons();
                });
            };
        }

        private static void BuildKokoroSubpage(Category rootCategory)
        {
            Page kokoroPage = rootCategory.AddPage(
                "Kokoro TTS Settings",
                "",
                "Configure Kokoro local speech and voice blending",
                "Kokoro TTS");

            Category connCat = kokoroPage.AddCategory("Connection");

            Button autoFillBtn = connCat.AddButton("Auto-fill Default Endpoint", "", "Set endpoint to http://127.0.0.1:8880/v1/audio/speech");
            autoFillBtn.OnPress += () =>
            {
                Settings.SetKokoroEndpoint("http://127.0.0.1:8880/v1/audio/speech");
                UpdateKokoroButtons();
                QuickMenuAPI.ShowAlertToast("Kokoro endpoint auto-filled", 2);
            };

            _kokoroEndpointBtn = connCat.AddButton(GetKokoroEndpointButtonText(), "", "Click to edit Kokoro endpoint URL");
            _kokoroEndpointBtn.OnPress += () =>
            {
                QuickMenuAPI.OpenKeyboard(Settings.KokoroEndpoint, (newVal) =>
                {
                    if (!string.IsNullOrWhiteSpace(newVal))
                    {
                        Settings.SetKokoroEndpoint(newVal.Trim());
                        UpdateKokoroButtons();
                    }
                });
            };

            Category voiceCat = kokoroPage.AddCategory("Voice Selection & Blending");

            _kokoroPrimaryVoiceBtn = voiceCat.AddButton(GetKokoroPrimaryVoiceButtonText(), "", "Click to cycle primary voice or pick from roster");
            _kokoroPrimaryVoiceBtn.OnPress += () =>
            {
                CycleKokoroVoice(true);
                UpdateKokoroButtons();
            };

            _kokoroBlendingToggle = voiceCat.AddToggle("Enable Voice Blending", "Combine primary and secondary voices using blend ratio", Settings.KokoroUseBlending);
            _kokoroBlendingToggle.OnValueUpdated += val =>
            {
                Settings.SetKokoroUseBlending(val);
            };

            _kokoroSecondaryVoiceBtn = voiceCat.AddButton(GetKokoroSecondaryVoiceButtonText(), "", "Click to cycle secondary blend voice");
            _kokoroSecondaryVoiceBtn.OnPress += () =>
            {
                CycleKokoroVoice(false);
                UpdateKokoroButtons();
            };

            _kokoroBlendSlider = voiceCat.AddSlider(
                "Blend Ratio",
                "Ratio between Primary (1.0) and Secondary (0.0)",
                Settings.KokoroBlendWeight,
                0.0f,
                1.0f,
                2);
            _kokoroBlendSlider.OnValueUpdated += val =>
            {
                Settings.SetKokoroBlendWeight(val);
            };

            _kokoroCustomBlendBtn = voiceCat.AddButton(GetKokoroCustomBlendButtonText(), "", "Click to enter custom blend expression (e.g. af_bella(2)+af_heart(1))");
            _kokoroCustomBlendBtn.OnPress += () =>
            {
                QuickMenuAPI.OpenKeyboard(Settings.KokoroCustomBlend, (expr) =>
                {
                    Settings.SetKokoroCustomBlend(expr != null ? expr.Trim() : string.Empty);
                    UpdateKokoroButtons();
                });
            };

            Category audioCat = kokoroPage.AddCategory("Audio Modifiers");

            SliderFloat speedSlider = audioCat.AddSlider(
                "Speech Speed",
                "Controls playback speed of speech",
                Settings.KokoroSpeed,
                0.5f,
                2.0f,
                2);
            speedSlider.OnValueUpdated += val =>
            {
                Settings.SetKokoroSpeed(val);
            };
        }

        private static void OnToggleProvider()
        {
            TTSProvider next = (Settings.Provider == TTSProvider.FishAudio) ? TTSProvider.Kokoro : TTSProvider.FishAudio;
            Settings.SetProvider(next);
            if (_providerBtn != null)
            {
                _providerBtn.ButtonText = GetProviderButtonText();
            }
            QuickMenuAPI.ShowAlertToast($"Active TTS Provider: {next}", 2);
        }

        private static void CycleKokoroVoice(bool isPrimary)
        {
            var voices = KokoroClient.AvailableVoices;
            if (voices == null || voices.Count == 0) return;

            string current = isPrimary ? Settings.KokoroPrimaryVoice : Settings.KokoroSecondaryVoice;
            int idx = -1;
            for (int i = 0; i < voices.Count; i++)
            {
                if (string.Equals(voices[i], current, StringComparison.OrdinalIgnoreCase))
                {
                    idx = i;
                    break;
                }
            }

            int nextIdx = (idx + 1) % voices.Count;
            string nextVoice = voices[nextIdx];

            if (isPrimary)
                Settings.SetKokoroPrimaryVoice(nextVoice);
            else
                Settings.SetKokoroSecondaryVoice(nextVoice);
        }

        private static void UpdateFishButtons()
        {
            if (_fishEndpointBtn != null) _fishEndpointBtn.ButtonText = GetFishEndpointButtonText();
            if (_fishApiKeyBtn != null) _fishApiKeyBtn.ButtonText = GetFishApiKeyButtonText();
            if (_fishVoiceIdBtn != null) _fishVoiceIdBtn.ButtonText = GetFishVoiceIdButtonText();
        }

        private static void UpdateKokoroButtons()
        {
            if (_kokoroEndpointBtn != null) _kokoroEndpointBtn.ButtonText = GetKokoroEndpointButtonText();
            if (_kokoroPrimaryVoiceBtn != null) _kokoroPrimaryVoiceBtn.ButtonText = GetKokoroPrimaryVoiceButtonText();
            if (_kokoroSecondaryVoiceBtn != null) _kokoroSecondaryVoiceBtn.ButtonText = GetKokoroSecondaryVoiceButtonText();
            if (_kokoroCustomBlendBtn != null) _kokoroCustomBlendBtn.ButtonText = GetKokoroCustomBlendButtonText();
        }

        private static string GetProviderButtonText() => $"Provider: {Settings.Provider}";

        private static string GetFishEndpointButtonText()
        {
            string url = Settings.ActiveFishEndpoint;
            if (string.IsNullOrWhiteSpace(url)) return "Endpoint: (empty)";
            if (url.Length > 24) url = "..." + url.Substring(url.Length - 21);
            return $"Endpoint: {url}";
        }

        private static string GetFishApiKeyButtonText()
        {
            if (string.IsNullOrWhiteSpace(Settings.FishApiKey)) return "API Key: (Not Set)";
            return "API Key: (Configured)";
        }

        private static string GetFishVoiceIdButtonText()
        {
            if (string.IsNullOrWhiteSpace(Settings.FishVoiceId)) return "Voice ID: Default";
            string id = Settings.FishVoiceId;
            if (id.Length > 16) id = id.Substring(0, 13) + "...";
            return $"Voice ID: {id}";
        }

        private static string GetKokoroEndpointButtonText()
        {
            string url = Settings.KokoroEndpoint;
            if (string.IsNullOrWhiteSpace(url)) return "Endpoint: (empty)";
            if (url.Length > 24) url = "..." + url.Substring(url.Length - 21);
            return $"Endpoint: {url}";
        }

        private static string GetKokoroPrimaryVoiceButtonText() => $"Primary: {Settings.KokoroPrimaryVoice}";
        private static string GetKokoroSecondaryVoiceButtonText() => $"Secondary: {Settings.KokoroSecondaryVoice}";

        private static string GetKokoroCustomBlendButtonText()
        {
            if (string.IsNullOrWhiteSpace(Settings.KokoroCustomBlend)) return "Custom Expression: None";
            string expr = Settings.KokoroCustomBlend;
            if (expr.Length > 16) expr = expr.Substring(0, 13) + "...";
            return $"Custom: {expr}";
        }
    }
}

