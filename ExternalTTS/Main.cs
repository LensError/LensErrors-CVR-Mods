using System;
using ExternalTTS;
using ExternalTTS.Services;
using ExternalTTS.UI;
using MelonLoader;

[assembly: MelonInfo(typeof(ExternalTTSMod), "ExternalTTS", "1.0.0", "LensError")]
[assembly: MelonGame(null, "ChilloutVR")]

namespace ExternalTTS
{
    public class ExternalTTSMod : MelonMod
    {
        public override void OnInitializeMelon()
        {
            Settings.Initialize();
            GameEvents.RegisterTTSModule();
            QuickMenuManager.PrepareIcon();
        }

        public override void OnLateInitializeMelon()
        {
            GameEvents.InitializeEvents();
            QuickMenuManager.BuildMenu();

            // Asynchronously probe Kokoro local voices in background
            _ = KokoroClient.FetchServerVoicesAsync();
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            GameEvents.SyncActiveModule();
        }
    }
}

