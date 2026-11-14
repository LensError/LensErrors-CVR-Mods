# ExternalTTS

Routes ChilloutVR TTS and ChatBox/OSC messages to Fish Audio (cloud or local) or a local Kokoro-FastAPI server. Configured in `Quick Menu` -> `LensError's Mods` -> `External TTS`.

## Prerequisites
Requires one of the following:
- **Fish Audio**: API key for `https://api.fish.audio/v1/tts`, or a local Fish Audio server.
- **Kokoro**: Local [Kokoro-FastAPI](https://github.com/remsky/Kokoro-FastAPI) server (default `http://127.0.0.1:8880/v1/audio/speech`).

## Network & Privacy
When TTS is triggered, outgoing message text and your configured voice/model settings are sent to your configured Fish Audio or Kokoro endpoint to generate WAV audio.
