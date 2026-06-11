import json
import mimetypes
import os
import subprocess
import tempfile
import threading
import urllib.error
import urllib.parse
import urllib.request
import uuid
from pathlib import Path
from tkinter import Tk, StringVar, BooleanVar, END, filedialog, messagebox
from tkinter import ttk


DEFAULT_BASE_URL = "http://localhost:8000"
DEFAULT_ENDPOINTS = ("/upload-voice", "/upload_voice", "/register_voice", "/voices/upload", "/upload_audio/", "/voices")


class VoiceUploader(Tk):
    def __init__(self):
        super().__init__()

        self.title("F5 FastAPI Voice Uploader")
        self.geometry("720x520")
        self.minsize(640, 460)

        self.base_url = StringVar(value=DEFAULT_BASE_URL)
        self.endpoint = StringVar(value=DEFAULT_ENDPOINTS[0])
        self.language = StringVar(value="en")
        self.voice_name = StringVar()
        self.ref_text = StringVar()
        self.input_file = StringVar()
        self.keep_wav = BooleanVar(value=True)
        self.output_wav = StringVar()

        self._build_ui()

    def _build_ui(self):
        root = ttk.Frame(self, padding=12)
        root.grid(row=0, column=0, sticky="nsew")
        self.columnconfigure(0, weight=1)
        self.rowconfigure(0, weight=1)

        root.columnconfigure(1, weight=1)
        root.rowconfigure(8, weight=1)

        ttk.Label(root, text="Base URL").grid(row=0, column=0, sticky="w", pady=3)
        ttk.Entry(root, textvariable=self.base_url).grid(row=0, column=1, sticky="ew", pady=3)
        ttk.Button(root, text="Discover", command=self.discover_threaded).grid(row=0, column=2, padx=(8, 0), pady=3)

        ttk.Label(root, text="Upload endpoint").grid(row=1, column=0, sticky="w", pady=3)
        self.endpoint_combo = ttk.Combobox(root, textvariable=self.endpoint, values=DEFAULT_ENDPOINTS)
        self.endpoint_combo.grid(row=1, column=1, sticky="ew", pady=3)
        ttk.Button(root, text="Health", command=self.health_threaded).grid(row=1, column=2, padx=(8, 0), pady=3)

        ttk.Label(root, text="Voice name").grid(row=2, column=0, sticky="w", pady=3)
        ttk.Entry(root, textvariable=self.voice_name).grid(row=2, column=1, sticky="ew", pady=3)

        ttk.Label(root, text="Language").grid(row=3, column=0, sticky="w", pady=3)
        ttk.Entry(root, textvariable=self.language, width=12).grid(row=3, column=1, sticky="w", pady=3)

        ttk.Label(root, text="Reference text").grid(row=4, column=0, sticky="w", pady=3)
        ttk.Entry(root, textvariable=self.ref_text).grid(row=4, column=1, sticky="ew", pady=3)

        ttk.Label(root, text="Audio file").grid(row=5, column=0, sticky="w", pady=3)
        ttk.Entry(root, textvariable=self.input_file).grid(row=5, column=1, sticky="ew", pady=3)
        ttk.Button(root, text="Browse", command=self.pick_file).grid(row=5, column=2, padx=(8, 0), pady=3)

        ttk.Label(root, text="Converted WAV").grid(row=6, column=0, sticky="w", pady=3)
        ttk.Entry(root, textvariable=self.output_wav).grid(row=6, column=1, sticky="ew", pady=3)
        ttk.Button(root, text="Save As", command=self.pick_output).grid(row=6, column=2, padx=(8, 0), pady=3)

        ttk.Checkbutton(root, text="Keep converted WAV", variable=self.keep_wav).grid(row=7, column=1, sticky="w", pady=3)

        buttons = ttk.Frame(root)
        buttons.grid(row=8, column=0, columnspan=3, sticky="ew", pady=(10, 8))
        ttk.Button(buttons, text="Convert Only", command=self.convert_threaded).pack(side="left")
        ttk.Button(buttons, text="Convert + Upload", command=self.upload_threaded).pack(side="left", padx=(8, 0))

        self.log = ttk.Treeview(root, columns=("message",), show="headings", height=10)
        self.log.heading("message", text="Log")
        self.log.column("message", anchor="w", stretch=True)
        self.log.grid(row=9, column=0, columnspan=3, sticky="nsew")

        scrollbar = ttk.Scrollbar(root, orient="vertical", command=self.log.yview)
        self.log.configure(yscrollcommand=scrollbar.set)
        scrollbar.grid(row=9, column=3, sticky="ns")

    def pick_file(self):
        path = filedialog.askopenfilename(
            filetypes=[
                ("Audio files", "*.wav *.mp3 *.m4a *.aac *.flac *.ogg"),
                ("All files", "*.*"),
            ]
        )
        if not path:
            return

        self.input_file.set(path)
        if not self.voice_name.get():
            self.voice_name.set(Path(path).stem)
        if not self.output_wav.get():
            self.output_wav.set(str(Path(path).with_suffix(".cvr_voice.wav")))

    def pick_output(self):
        path = filedialog.asksaveasfilename(
            defaultextension=".wav",
            filetypes=[("WAV audio", "*.wav"), ("All files", "*.*")],
        )
        if path:
            self.output_wav.set(path)

    def health_threaded(self):
        self._thread(self.health)

    def discover_threaded(self):
        self._thread(self.discover)

    def convert_threaded(self):
        self._thread(lambda: self.convert_audio(require_output=True))

    def upload_threaded(self):
        self._thread(self.convert_and_upload)

    def _thread(self, target):
        threading.Thread(target=self._run_guarded, args=(target,), daemon=True).start()

    def _run_guarded(self, target):
        try:
            target()
        except Exception as exc:
            message = str(exc)
            self.write_log(f"ERROR: {message}")
            self.after(0, lambda: messagebox.showerror("F5 Voice Uploader", message))

    def health(self):
        url = self.join_url("/health")
        self.write_log(f"GET {url}")
        with urllib.request.urlopen(url, timeout=10) as response:
            body = response.read().decode("utf-8", errors="replace")
        self.write_log(f"Health OK: {body[:300]}")

    def discover(self):
        url = self.join_url("/openapi.json")
        self.write_log(f"GET {url}")
        with urllib.request.urlopen(url, timeout=10) as response:
            data = json.loads(response.read().decode("utf-8"))

        candidates = []
        for path, methods in data.get("paths", {}).items():
            lowered = path.lower()
            post = methods.get("post")
            if post and any(token in lowered for token in ("voice", "upload", "audio")):
                request_body = post.get("requestBody", {})
                content = request_body.get("content", {})
                if "multipart/form-data" in content or "application/x-www-form-urlencoded" in content:
                    candidates.append(path)

        if not candidates:
            self.write_log("No upload-like POST endpoint found in OpenAPI. Keeping current endpoint.")
            return

        self.endpoint.set(candidates[0])
        self.endpoint_combo.configure(values=candidates)
        self.write_log("Upload endpoint candidates: " + ", ".join(candidates))
        self.write_log(f"Selected {candidates[0]}")

    def convert_and_upload(self):
        wav_path = self.convert_audio(require_output=False)
        try:
            self.upload_voice(wav_path)
        finally:
            if not self.keep_wav.get() and wav_path != self.output_wav.get():
                try:
                    os.remove(wav_path)
                except OSError:
                    pass

    def convert_audio(self, require_output):
        source = self.input_file.get().strip()
        if not source:
            raise ValueError("Pick an audio file first.")

        source_path = Path(source)
        if not source_path.exists():
            raise FileNotFoundError(source)

        target = self.output_wav.get().strip()
        if not target:
            if require_output or self.keep_wav.get():
                target = str(source_path.with_suffix(".cvr_voice.wav"))
                self.output_wav.set(target)
            else:
                target = str(Path(tempfile.gettempdir()) / f"f5_voice_{uuid.uuid4().hex}.wav")

        target_path = Path(target)
        target_path.parent.mkdir(parents=True, exist_ok=True)

        command = [
            "ffmpeg",
            "-y",
            "-i",
            str(source_path),
            "-ac",
            "1",
            "-ar",
            "24000",
            "-c:a",
            "pcm_s16le",
            str(target_path),
        ]
        self.write_log("Converting to 24 kHz mono PCM WAV...")
        result = subprocess.run(command, capture_output=True, text=True)
        if result.returncode != 0:
            raise RuntimeError("ffmpeg failed. Install ffmpeg and make sure it is on PATH.\n" + result.stderr[-1200:])

        self.write_log(f"WAV ready: {target_path}")
        return str(target_path)

    def upload_voice(self, wav_path):
        selected_endpoint = self.endpoint.get().strip() or DEFAULT_ENDPOINTS[0]
        name = self.voice_name.get().strip() or Path(wav_path).stem
        language = self.language.get().strip() or "en"
        ref_text = self.ref_text.get().strip()

        endpoints = [selected_endpoint]
        endpoints.extend(endpoint for endpoint in DEFAULT_ENDPOINTS if endpoint not in endpoints)

        errors = []
        for endpoint in endpoints:
            if endpoint.rstrip("/") == "/upload-voice":
                query = {"voice_name": name}
                if ref_text:
                    query["ref_text"] = ref_text
                url = self.join_url(endpoint) + "?" + urllib.parse.urlencode(query)
                fields = {}
                file_fields = ("file",)
            else:
                url = self.join_url(endpoint)
                fields = {
                    "name": name,
                    "voice_name": name,
                    "label": name,
                    "audio_file_label": name,
                    "language": language,
                    "lang": language,
                    "ref_text": ref_text,
                    "reference_text": ref_text,
                    "transcript": ref_text,
                }
                file_fields = ("file", "audio_file", "voice_file")

            body, content_type = build_multipart(fields, file_fields, wav_path)
            request = urllib.request.Request(url, data=body, method="POST")
            request.add_header("Content-Type", content_type)
            request.add_header("Accept", "application/json, text/plain, */*")

            self.write_log(f"POST {url}")
            self.write_log(f"Uploading voice '{name}' ({language})...")
            try:
                with urllib.request.urlopen(request, timeout=120) as response:
                    payload = response.read().decode("utf-8", errors="replace")
                    self.endpoint.set(endpoint)
                    self.write_log(f"Upload OK: HTTP {response.status} {payload[:500]}")
                    self.handle_upload_response(payload)
                    return
            except urllib.error.HTTPError as exc:
                payload = exc.read().decode("utf-8", errors="replace")
                errors.append(f"{endpoint}: HTTP {exc.code} {exc.reason} {payload[:300]}")
                if exc.code in (404, 405):
                    self.write_log(f"{endpoint} rejected upload with HTTP {exc.code}; trying next candidate.")
                    continue
                raise RuntimeError(f"Upload failed: HTTP {exc.code} {exc.reason}\n{payload}")

        raise RuntimeError("Upload failed on all candidate endpoints:\n" + "\n".join(errors))

    def handle_upload_response(self, payload):
        try:
            data = json.loads(payload)
        except json.JSONDecodeError:
            return

        voice_id = data.get("voice_id")
        if not voice_id:
            return

        self.voice_name.set(voice_id)
        self.clipboard_clear()
        self.clipboard_append(voice_id)
        self.write_log(f"Use this exact Custom Voice value in CVROpenAITTS: {voice_id}")
        self.write_log("Voice ID copied to clipboard.")

    def join_url(self, path):
        base = self.base_url.get().strip().rstrip("/")
        if not base:
            base = DEFAULT_BASE_URL
        if not path.startswith("/"):
            path = "/" + path
        return base + path

    def write_log(self, message):
        self.after(0, lambda: self._append_log(message))

    def _append_log(self, message):
        self.log.insert("", END, values=(message,))
        rows = self.log.get_children()
        if rows:
            self.log.see(rows[-1])


def build_multipart(fields, file_fields, file_path):
    boundary = "----CVROpenAITTS" + uuid.uuid4().hex
    parts = []

    for name, value in fields.items():
        parts.append(f"--{boundary}\r\n".encode("utf-8"))
        parts.append(f'Content-Disposition: form-data; name="{name}"\r\n\r\n'.encode("utf-8"))
        parts.append(str(value).encode("utf-8"))
        parts.append(b"\r\n")

    filename = Path(file_path).name
    content_type = mimetypes.guess_type(filename)[0] or "audio/wav"
    with open(file_path, "rb") as handle:
        file_bytes = handle.read()

    for name in file_fields:
        parts.append(f"--{boundary}\r\n".encode("utf-8"))
        parts.append(
            f'Content-Disposition: form-data; name="{name}"; filename="{filename}"\r\n'.encode("utf-8")
        )
        parts.append(f"Content-Type: {content_type}\r\n\r\n".encode("utf-8"))
        parts.append(file_bytes)
        parts.append(b"\r\n")

    parts.append(f"--{boundary}--\r\n".encode("utf-8"))
    return b"".join(parts), f"multipart/form-data; boundary={boundary}"


if __name__ == "__main__":
    VoiceUploader().mainloop()
