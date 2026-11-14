using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ExternalTTS.Audio;
using MelonLoader;
using Newtonsoft.Json.Linq;

namespace ExternalTTS.Services
{
    public static class FishAudioClient
    {
        private static readonly HttpClient HttpClient = new HttpClient();

        public static async Task<WavData?> GenerateSpeechAsync(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            string endpoint = Settings.ActiveFishEndpoint;
            if (string.IsNullOrWhiteSpace(endpoint))
            {
                MelonLogger.Warning("[ExternalTTS] Fish Audio endpoint is empty.");
                return null;
            }

            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));

                var request = new HttpRequestMessage(HttpMethod.Post, endpoint);

                // Add Authorization header if using cloud and API key is present
                if (Settings.FishUseCloud && !string.IsNullOrWhiteSpace(Settings.FishApiKey))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Settings.FishApiKey.Trim());
                }

                // Add model header
                string model = Settings.ActiveFishModel;
                if (!string.IsNullOrWhiteSpace(model))
                {
                    request.Headers.Add("model", model);
                }

                // Build JSON payload
                var payload = new JObject
                {
                    ["text"] = text,
                    ["format"] = "wav",
                    ["sample_rate"] = 44100
                };

                if (!string.IsNullOrWhiteSpace(Settings.FishVoiceId))
                {
                    payload["reference_id"] = Settings.FishVoiceId.Trim();
                }

                request.Content = new StringContent(payload.ToString(), Encoding.UTF8, "application/json");

                HttpResponseMessage response = await HttpClient.SendAsync(request, cts.Token).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    string errContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    MelonLogger.Error($"[ExternalTTS] Fish Audio HTTP error: {response.StatusCode} - {errContent}");
                    return null;
                }

                byte[] wavBytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                if (WavUtility.TryParseWav(wavBytes, out WavData wavData))
                {
                    return wavData;
                }

                MelonLogger.Error("[ExternalTTS] Fish Audio returned invalid WAV audio bytes.");
                return null;
            }
            catch (OperationCanceledException)
            {
                MelonLogger.Warning("[ExternalTTS] Fish Audio request timed out after 8 seconds.");
                return null;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[ExternalTTS] Fish Audio request failed: {ex.Message}");
                return null;
            }
        }
    }
}

