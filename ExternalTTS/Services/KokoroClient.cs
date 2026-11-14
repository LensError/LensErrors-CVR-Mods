using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ExternalTTS.Audio;
using MelonLoader;
using Newtonsoft.Json.Linq;

namespace ExternalTTS.Services
{
    public static class KokoroClient
    {
        private static readonly HttpClient HttpClient = new HttpClient();

        public static readonly string[] DefaultVoices = new[]
        {
            "af_heart",
            "af_bella",
            "af_nicole",
            "af_aoede",
            "af_kore",
            "af_sarah",
            "af_nova",
            "af_sky",
            "af_alloy",
            "af_jessica",
            "af_river",
            "am_adam",
            "am_echo",
            "am_eric",
            "am_fenrir",
            "am_liam",
            "am_michael",
            "am_onyx",
            "am_puck",
            "bf_alice",
            "bf_emma",
            "bf_isabella",
            "bf_lily",
            "bm_daniel",
            "bm_fable",
            "bm_george",
            "bm_lewis"
        };

        private static List<string> _availableVoices = new List<string>(DefaultVoices);
        public static IReadOnlyList<string> AvailableVoices => _availableVoices;

        public static string GetActiveVoiceString()
        {
            if (!string.IsNullOrWhiteSpace(Settings.KokoroCustomBlend))
            {
                return Settings.KokoroCustomBlend.Trim();
            }

            if (Settings.KokoroUseBlending)
            {
                string v1 = string.IsNullOrWhiteSpace(Settings.KokoroPrimaryVoice) ? "af_heart" : Settings.KokoroPrimaryVoice.Trim();
                string v2 = string.IsNullOrWhiteSpace(Settings.KokoroSecondaryVoice) ? "af_bella" : Settings.KokoroSecondaryVoice.Trim();
                float w1 = (float)Math.Round(Settings.KokoroBlendWeight, 2);
                float w2 = (float)Math.Round(1.0f - Settings.KokoroBlendWeight, 2);
                return $"{v1}({w1:0.00})+{v2}({w2:0.00})";
            }

            return string.IsNullOrWhiteSpace(Settings.KokoroPrimaryVoice) ? "af_heart" : Settings.KokoroPrimaryVoice.Trim();
        }

        public static async Task<WavData?> GenerateSpeechAsync(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            string endpoint = Settings.KokoroEndpoint;
            if (string.IsNullOrWhiteSpace(endpoint))
            {
                MelonLogger.Warning("[ExternalTTS] Kokoro endpoint is empty.");
                return null;
            }

            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));

                string voice = GetActiveVoiceString();

                var payload = new JObject
                {
                    ["model"] = "kokoro",
                    ["input"] = text,
                    ["voice"] = voice,
                    ["response_format"] = "wav",
                    ["speed"] = Settings.KokoroSpeed
                };

                var content = new StringContent(payload.ToString(), Encoding.UTF8, "application/json");

                HttpResponseMessage response = await HttpClient.PostAsync(endpoint, content, cts.Token).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    string errContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    MelonLogger.Error($"[ExternalTTS] Kokoro HTTP error: {response.StatusCode} - {errContent}");
                    return null;
                }

                byte[] wavBytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                if (WavUtility.TryParseWav(wavBytes, out WavData wavData))
                {
                    return wavData;
                }

                MelonLogger.Error("[ExternalTTS] Kokoro returned invalid WAV audio bytes.");
                return null;
            }
            catch (OperationCanceledException)
            {
                MelonLogger.Warning("[ExternalTTS] Kokoro request timed out after 8 seconds.");
                return null;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[ExternalTTS] Kokoro request failed: {ex.Message}");
                return null;
            }
        }

        public static async Task FetchServerVoicesAsync()
        {
            try
            {
                string endpoint = Settings.KokoroEndpoint;
                if (string.IsNullOrWhiteSpace(endpoint)) return;

                string voicesUrl = endpoint.Replace("/speech", "/voices");
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(4));
                HttpResponseMessage response = await HttpClient.GetAsync(voicesUrl, cts.Token).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode) return;

                string jsonStr = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                JToken token = JToken.Parse(jsonStr);

                var list = new List<string>();
                if (token is JArray arr)
                {
                    foreach (var item in arr)
                    {
                        string name = item.Value<string>();
                        if (!string.IsNullOrWhiteSpace(name) && !list.Contains(name))
                            list.Add(name);
                    }
                }
                else if (token is JObject obj && obj["voices"] is JArray voicesArr)
                {
                    foreach (var item in voicesArr)
                    {
                        string name = item.Value<string>();
                        if (!string.IsNullOrWhiteSpace(name) && !list.Contains(name))
                            list.Add(name);
                    }
                }

                if (list.Count > 0)
                {
                    _availableVoices = list;
                }
            }
            catch
            {
                // Fall back to built-in list
            }
        }
    }
}

