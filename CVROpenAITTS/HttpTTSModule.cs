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
                bool   isF5    = Settings.IsF5FastAPI();

                string json = isF5
                    ? BuildF5Json(model, msg, voice, speed)
                    : BuildOpenAIJson(model, msg, voice, speed);

                using var request = new HttpRequestMessage(
                    HttpMethod.Post,
                    $"{baseUrl}{(isF5 ? "/synthesize" : "/v1/audio/speech")}");
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");

                if (!string.IsNullOrEmpty(apiKey))
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

                var response = s_http.SendAsync(request).GetAwaiter().GetResult();
                if (!response.IsSuccessStatusCode)
                {
                    string error = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                    MelonLogger.Error($"[CVROpenAITTS] TTS request failed: {(int)response.StatusCode} {response.ReasonPhrase} {error}");
                    return Array.Empty<short>();
                }

                byte[] bytes = response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
                if (TryDecodeWave(bytes, out short[] wavPcm, out int wavSampleRate, out int wavChannels))
                {
                    SampleRate = wavSampleRate;
                    Channels = wavChannels;
                    return wavPcm;
                }

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

        static string BuildOpenAIJson(string model, string input, string voice, float speed)
        {
            return "{" +
                $"\"model\":{Esc(model)}," +
                $"\"input\":{Esc(input)}," +
                $"\"voice\":{Esc(voice)}," +
                "\"response_format\":\"pcm\"," +
                $"\"speed\":{speed.ToString("F2", CultureInfo.InvariantCulture)}" +
                "}";
        }

        static string BuildF5Json(string model, string text, string voice, float speed)
        {
            if (string.IsNullOrEmpty(voice))
                voice = "default";

            return "{" +
                $"\"text\":{Esc(text)}," +
                "\"language\":\"en\"," +
                $"\"voice\":{Esc(voice)}," +
                $"\"speed\":{speed.ToString("F2", CultureInfo.InvariantCulture)}" +
                "}";
        }

        static bool TryDecodeWave(byte[] bytes, out short[] pcm, out int sampleRate, out int channels)
        {
            pcm = Array.Empty<short>();
            sampleRate = 24000;
            channels = 1;

            if (bytes.Length < 44 ||
                Encoding.ASCII.GetString(bytes, 0, 4) != "RIFF" ||
                Encoding.ASCII.GetString(bytes, 8, 4) != "WAVE")
                return false;

            int offset = 12;
            ushort audioFormat = 0;
            ushort bitsPerSample = 0;
            int dataOffset = -1;
            int dataSize = 0;

            while (offset + 8 <= bytes.Length)
            {
                string chunkId = Encoding.ASCII.GetString(bytes, offset, 4);
                int chunkSize = BitConverter.ToInt32(bytes, offset + 4);
                int chunkData = offset + 8;

                if (chunkSize < 0 || chunkData + chunkSize > bytes.Length)
                    break;

                if (chunkId == "fmt " && chunkSize >= 16)
                {
                    audioFormat = BitConverter.ToUInt16(bytes, chunkData);
                    channels = BitConverter.ToUInt16(bytes, chunkData + 2);
                    sampleRate = BitConverter.ToInt32(bytes, chunkData + 4);
                    bitsPerSample = BitConverter.ToUInt16(bytes, chunkData + 14);
                }
                else if (chunkId == "data")
                {
                    dataOffset = chunkData;
                    dataSize = chunkSize;
                }

                offset = chunkData + chunkSize + (chunkSize & 1);
            }

            if (audioFormat != 1 || bitsPerSample != 16 || dataOffset < 0 || dataSize <= 0)
                return false;

            int sampleCount = dataSize / 2;
            pcm = new short[sampleCount];
            Buffer.BlockCopy(bytes, dataOffset, pcm, 0, sampleCount * 2);
            return true;
        }

        static string Esc(string s) =>
            "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"")
                    .Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t") + "\"";
    }
}
