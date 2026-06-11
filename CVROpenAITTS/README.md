# CVR OpenAI TTS

[![Download](https://img.shields.io/badge/Download-Latest-blue?style=for-the-badge)](https://github.com/LensError/lenserrors-cvr-mods/releases/latest/download/CVROpenAITTS.dll)

Adds two new TTS modules to ChilloutVR that send your chat messages to an OpenAI-compatible `/v1/audio/speech` endpoint instead of using the built-in Windows voice.

## Modules

| Module | Default URL | Use case |
|---|---|---|
| **Local AI TTS** | `http://localhost:8880` | Kokoro-FastAPI, LocalAI, AllTalk, or any OpenAI-compatible local server |
| **F5 FastAPI** | `http://localhost:8000` | F5-TTS FastAPI servers that use `POST /synthesize` |
| **OpenAI TTS** | `https://api.openai.com` | Real OpenAI API or any hosted compatible endpoint |

OpenAI-compatible backends request 16-bit PCM audio at 24 kHz. F5 FastAPI requests WAV output from `/synthesize`; the mod decodes the returned WAV before passing it to the game.

## Requirements

- MelonLoader
- A running OpenAI-compatible TTS server, or an OpenAI API key

## Settings

Open the QuickMenu, navigate to the **LensError's Mods** tab, then open
**CVR AI Voice TTS** settings.

**Local AI TTS**
- **Local AI: Base URL** - base URL of your local server (no trailing slash)
- **Local AI: Model** - model name passed in the request (default: `kokoro`)
- **Voice Preset** - pick from common Kokoro / OpenAI-compatible voice names
- **Local AI: Custom Voice** - type any voice name your server supports; overrides the preset when non-empty
- **Local AI: Speed** - speech speed from 0.25 to 4.0

**F5 FastAPI**
- **Base URL** - base URL of the F5 FastAPI server, usually `http://localhost:8000`
- **Model** - model value sent in the request, default `f5-tts`
- **Custom Voice** - voice name sent as `voice`; set this to the exact `voice_name` used by the uploader
- **Speed** - speech speed from 0.25 to 4.0

### F5 Voice Uploader

`F5VoiceUploader.py` is a small Tkinter GUI for preparing reference voices for F5 FastAPI servers.

Requirements:

- Python 3 with Tkinter
- `ffmpeg` on `PATH`
- A running F5 FastAPI server

Run it with:

```powershell
python CVROpenAITTS\F5VoiceUploader.py
```

or double-click `CVROpenAITTS\F5VoiceUploader.bat`.

The uploader converts MP3, M4A, AAC, FLAC, OGG, or WAV input to mono 24 kHz 16-bit PCM WAV before uploading. Put the exact spoken transcript of the reference audio in **Reference text**; F5 uses that text with the reference audio for voice cloning. Use **Discover** to read `/openapi.json` and select a likely voice upload endpoint, or type the endpoint manually if your server uses a custom route.

The `climatologist/f5-tts` image may need its FastAPI wrapper patched for newer `f5-tts` Python APIs and uploaded reference text:

```powershell
docker cp CVROpenAITTS\PatchF5FastAPIServer.py suspicious_tharp:/tmp/PatchF5FastAPIServer.py
docker exec suspicious_tharp python /tmp/PatchF5FastAPIServer.py
docker restart suspicious_tharp
```

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
