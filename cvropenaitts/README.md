# CVR OpenAI TTS

[![Download](https://img.shields.io/badge/Download-Latest-blue?style=for-the-badge)](https://github.com/LensError/lenserrors-cvr-mods/releases/latest/download/CVROpenAITTS.dll)

Adds two new TTS modules to ChilloutVR that send your chat messages to an OpenAI-compatible `/v1/audio/speech` endpoint instead of using the built-in Windows voice.

## Modules

| Module | Default URL | Use case |
|---|---|---|
| **Local AI TTS** | `http://localhost:8880` | Kokoro-FastAPI, LocalAI, AllTalk, or any local server |
| **OpenAI TTS** | `https://api.openai.com` | Real OpenAI API or any hosted compatible endpoint |

Both modules request 16-bit PCM audio at 24 kHz, which the game resamples and plays through its normal TTS audio path.

## Requirements

- MelonLoader
- [BTKUILib](https://github.com/BTK-Development/BTKUILib)
- A running OpenAI-compatible TTS server, or an OpenAI API key

## Settings

Open the QuickMenu and navigate to the **CVR OpenAI TTS** tab.

**Local AI TTS**
- **Local AI: Base URL** - base URL of your local server (no trailing slash)
- **Local AI: Model** - model name passed in the request (default: `kokoro`)
- **Voice Preset** - pick from common Kokoro / OpenAI-compatible voice names
- **Local AI: Custom Voice** - type any voice name your server supports; overrides the preset when non-empty
- **Local AI: Speed** - speech speed from 0.25 to 4.0

**OpenAI TTS**
- **OpenAI: Base URL** - change this to use any OpenAI-compatible hosted endpoint
- **OpenAI: API Key** - Bearer token sent in the `Authorization` header
- **OpenAI: Model** - model name (default: `tts-1`, also `tts-1-hd`)
- **Voice Preset** - pick from the standard OpenAI voice list
- **OpenAI: Custom Voice** - type any voice name your endpoint supports; overrides the preset when non-empty
- **OpenAI: Speed** - speech speed from 0.25 to 4.0

Settings are saved to `UserData/MelonPreferences.cfg` and persist between sessions.

## Switching modules

The active TTS module is selected in the CVR audio settings (same place the built-in SYSTEM voice is chosen). **Local AI TTS** and **OpenAI TTS** will appear alongside it once the mod is loaded.
