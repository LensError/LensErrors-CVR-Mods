using System;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using ABI_RC.Systems.Communications.TTS;
using MelonLoader;

namespace CVROPENAI
{
    abstract class HttpTTSModule : Comms_TTSModule
    {
        // Shared across all HTTP modules — HttpClient is thread-safe
        static readonly HttpClient s_http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

        protected abstract string ModuleId { get; }
        protected abstract string[] PresetVoices { get; }

        public override void Initialize()
        {
            SampleRate = 24000;
            Channels = 1;
            foreach (string v in PresetVoices)
                Voices[v] = v;

            // Include any custom voice saved in settings so the game's voice selector sees it
            string saved = Settings.GetVoice(ModuleId);
            if (!string.IsNullOrEmpty(saved) && !Voices.ContainsKey(saved))
                Voices[saved] = saved;
        }

        public override short[] Process(string msg)
        {
            try
            {
                string baseUrl = Settings.GetBaseUrl(ModuleId).TrimEnd('/');
                string apiKey  = Settings.GetApiKey(ModuleId);
                string model   = Settings.GetModel(ModuleId);
                string voice   = Settings.GetVoice(ModuleId);
                float  speed   = Settings.GetSpeed(ModuleId);

                if (string.IsNullOrEmpty(voice))
                    voice = CurrentVoice;

                string json = BuildJson(model, msg, voice, speed);

                using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/v1/audio/speech");
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");

                if (!string.IsNullOrEmpty(apiKey))
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

                var response = s_http.SendAsync(request).GetAwaiter().GetResult();
                response.EnsureSuccessStatusCode();

                byte[] bytes = response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
                // OpenAI PCM format: 16-bit signed little-endian @ 24 kHz, no header
                int sampleCount = bytes.Length / 2;
                short[] pcm = new short[sampleCount];
                Buffer.BlockCopy(bytes, 0, pcm, 0, sampleCount * 2);
                return pcm;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[CVROpenAITTS/{ModuleId}] {ex.Message}");
                return System.Array.Empty<short>();
            }
        }

        static string BuildJson(string model, string input, string voice, float speed)
        {
            return "{" +
                $"\"model\":{JsonString(model)}," +
                $"\"input\":{JsonString(input)}," +
                $"\"voice\":{JsonString(voice)}," +
                "\"response_format\":\"pcm\"," +
                $"\"speed\":{speed.ToString("F2", CultureInfo.InvariantCulture)}" +
                "}";
        }

        static string JsonString(string s) =>
            "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"")
                    .Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t") + "\"";
    }
}
