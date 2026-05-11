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
        static readonly HttpClient s_http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

        public override void Initialize()
        {
            SampleRate = 24000;
            Channels   = 1;
            Voices["_qm"] = "Change voice in Quick Menu";
        }

        public override short[] Process(string msg)
        {
            try
            {
                string baseUrl = Settings.GetBaseUrl().TrimEnd('/');
                string apiKey  = Settings.GetApiKey();
                string model   = Settings.GetModel();
                string voice   = Settings.GetVoice();
                float  speed   = Settings.GetSpeed();

                string json = BuildJson(model, msg, voice, speed);

                using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/v1/audio/speech");
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");

                if (!string.IsNullOrEmpty(apiKey))
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

                var response = s_http.SendAsync(request).GetAwaiter().GetResult();
                response.EnsureSuccessStatusCode();

                byte[] bytes = response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
                int sampleCount = bytes.Length / 2;
                short[] pcm = new short[sampleCount];
                Buffer.BlockCopy(bytes, 0, pcm, 0, sampleCount * 2);
                return pcm;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[CVROpenAITTS] {ex.Message}");
                return System.Array.Empty<short>();
            }
        }

        static string BuildJson(string model, string input, string voice, float speed)
        {
            return "{" +
                $"\"model\":{Esc(model)}," +
                $"\"input\":{Esc(input)}," +
                $"\"voice\":{Esc(voice)}," +
                "\"response_format\":\"pcm\"," +
                $"\"speed\":{speed.ToString("F2", CultureInfo.InvariantCulture)}" +
                "}";
        }

        static string Esc(string s) =>
            "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"")
                    .Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t") + "\"";
    }
}
