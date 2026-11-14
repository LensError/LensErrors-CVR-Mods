using System;
using System.Collections.Generic;
using ABI_RC.Core.InteractionSystem;
using ABI_RC.Core.Savior;
using ABI_RC.Systems.ChatBox;
using ABI_RC.Systems.Communications.Audio.TTS;
using ABI_RC.Systems.Communications.Settings;
using MelonLoader;

namespace ExternalTTS
{
    public static class GameEvents
    {
        private static bool _initialized;
        private static readonly object _suppressLock = new object();
        private static readonly List<string> _suppressedChatBoxMessages = new List<string>();

        public static void RegisterTTSModule()
        {
            try
            {
                if (Comms_TTSHandler.Instance == null)
                {
                    Comms_TTSHandler.AddModule<ExternalTTSModule>(ExternalTTSModule.ModuleId, "External TTS");
                    MelonLogger.Msg("[ExternalTTS] Registered ExternalTTSModule into Comms_TTSHandler.");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[ExternalTTS] Failed to register TTS module: {ex.Message}");
            }
        }

        public static void InitializeEvents()
        {
            if (_initialized) return;
            _initialized = true;

            try
            {
                ChatBoxAPI.OnMessageSent = (Action<ChatBoxAPI.ChatBoxMessage>)Delegate.Combine(
                    ChatBoxAPI.OnMessageSent,
                    new Action<ChatBoxAPI.ChatBoxMessage>(OnChatBoxMessageSent));
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[ExternalTTS] Failed to subscribe to ChatBox events: {ex.Message}");
            }

            Settings.OnSettingsChanged += SyncActiveModule;
        }

        public static void SyncActiveModule()
        {
            if (MetaPort.Instance == null || MetaPort.Instance.settings == null) return;

            try
            {
                if (Settings.Enabled)
                {
                    if (Comms_SettingsHandler.TTSModule != ExternalTTSModule.ModuleId)
                    {
                        MetaPort.Instance.settings.SetSettingsString("AudioTTSModule", ExternalTTSModule.ModuleId);
                    }
                }
                else
                {
                    if (Comms_SettingsHandler.TTSModule == ExternalTTSModule.ModuleId)
                    {
                        MetaPort.Instance.settings.SetSettingsString("AudioTTSModule", "SYSTEM");
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[ExternalTTS] Failed to sync active TTS module: {ex.Message}");
            }
        }

        private static void OnChatBoxMessageSent(ChatBoxAPI.ChatBoxMessage msg)
        {
            // Only speak for local player messages
            if (MetaPort.Instance == null || msg.SenderGuid != MetaPort.Instance.ownerId)
            {
                return;
            }

            if (ConsumeSuppressedChatBoxMessage(msg.Message))
            {
                return;
            }

            if (!Settings.Enabled || !Settings.SpeakChatBox)
            {
                return;
            }

            if (msg.Source == ChatBoxAPI.MessageSource.OSC && !Settings.SpeakOSC)
            {
                return;
            }

            // Avoid double playback if native game TTS already invoked ProcessMessage for internal keyboard
            if (TextComms.SettingTTSEnabled &&
                msg.Source == ChatBoxAPI.MessageSource.Internal &&
                Comms_SettingsHandler.TTSModule == ExternalTTSModule.ModuleId)
            {
                return;
            }

            SpeakMessage(ExtractSpokenText(msg.Message));
        }

        private static string ExtractSpokenText(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return message;

            const string ogPrefix = "OG: ";
            if (message.StartsWith(ogPrefix, StringComparison.OrdinalIgnoreCase))
            {
                string withoutPrefix = message.Substring(ogPrefix.Length);
                int newlineIndex = withoutPrefix.IndexOfAny(new[] { '\r', '\n' });
                return newlineIndex >= 0 ? withoutPrefix.Substring(0, newlineIndex) : withoutPrefix;
            }

            return message;
        }

        public static void SpeakChatBoxMessage(string text)
        {
            if (!Settings.Enabled || !Settings.SpeakChatBox) return;
            SpeakMessage(text);
        }

        public static void SuppressNextChatBoxMessage(string text)
        {
            if (string.IsNullOrEmpty(text)) return;

            lock (_suppressLock)
            {
                if (_suppressedChatBoxMessages.Count >= 16)
                {
                    _suppressedChatBoxMessages.RemoveAt(0);
                }
                _suppressedChatBoxMessages.Add(text);
            }
        }

        private static bool ConsumeSuppressedChatBoxMessage(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;

            lock (_suppressLock)
            {
                return _suppressedChatBoxMessages.Remove(text);
            }
        }

        public static void SpeakMessage(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            if (Comms_TTSHandler.Instance != null)
            {
                SyncActiveModule();
                Comms_TTSHandler.Instance.ProcessMessage(text);
            }
        }
    }
}
