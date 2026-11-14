using System;
using System.Threading.Tasks;
using ABI_RC.Systems.Communications.TTS;
using ExternalTTS.Audio;
using ExternalTTS.Services;
using MelonLoader;

namespace ExternalTTS
{
    public class ExternalTTSModule : Comms_TTSModule
    {
        public const string ModuleId = "EXTERNAL_TTS";

        public ExternalTTSModule()
        {
            Channels = 1;
            SampleRate = 48000;
            Voices["DEFAULT"] = "External Voice";
        }

        public override void Initialize()
        {
            Channels = 1;
            SampleRate = 48000;
            if (Voices.Count == 0)
            {
                Voices["DEFAULT"] = "External Voice";
            }
        }

        public override short[] Process(string msg)
        {
            if (!Settings.Enabled || string.IsNullOrWhiteSpace(msg))
            {
                return null;
            }

            try
            {
                Task<WavData?> task = (Settings.Provider == TTSProvider.FishAudio)
                    ? FishAudioClient.GenerateSpeechAsync(msg)
                    : KokoroClient.GenerateSpeechAsync(msg);

                WavData? wavResult = task.GetAwaiter().GetResult();
                if (wavResult.HasValue)
                {
                    WavData data = wavResult.Value;
                    Channels = data.Channels;
                    SampleRate = data.SampleRate;
                    return data.Samples;
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[ExternalTTS] Synthesis processing failed: {ex.Message}");
            }

            return null;
        }
    }
}
