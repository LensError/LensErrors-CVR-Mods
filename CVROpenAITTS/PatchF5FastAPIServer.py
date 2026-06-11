from pathlib import Path


def replace_required(text, old, new):
    if old not in text:
        raise RuntimeError(f"Expected text not found:\n{old}")
    return text.replace(old, new)


main_path = Path("/app/app/main.py")
main_text = main_path.read_text()
main_text = replace_required(
    main_text,
    '''async def upload_voice_sample(
    file: UploadFile = File(...),
    voice_name: str = "custom"
):''',
    '''async def upload_voice_sample(
    file: UploadFile = File(...),
    voice_name: str = "custom",
    ref_text: Optional[str] = None
):''',
)
main_text = replace_required(
    main_text,
    "voice_id = await tts_service.register_voice(file, voice_name)",
    "voice_id = await tts_service.register_voice(file, voice_name, ref_text)",
)
main_path.write_text(main_text)

service_path = Path("/app/app/f5_tts_service.py")
service_text = service_path.read_text()
service_text = service_text.replace(
    "async def register_voice(self, audio_file, voice_name: str) -> str:",
    "async def register_voice(self, audio_file, voice_name: str, ref_text: Optional[str] = None) -> str:",
)
service_text = service_text.replace(
    '"text": f"Hello, this is a sample of the {voice_name} voice."',
    '"text": ref_text or f"Hello, this is a sample of the {voice_name} voice."',
)
service_text = service_text.replace("ref_audio=ref_audio_path", "ref_file=ref_audio_path")
service_text = service_text.replace('\n                exp_name="f5tts_basic",', "")
service_text = service_text.replace(
    "                speed=speed\n            )",
    "                speed=speed,\n                file_wave=str(output_path)\n            )",
)
service_text = service_text.replace(
    "\n            # Save audio\n            torchaudio.save(str(output_path), audio, self.sample_rate)\n",
    "\n",
)
service_path.write_text(service_text)

print("Patched F5 FastAPI server for current F5TTS.infer API and uploaded ref_text.")
