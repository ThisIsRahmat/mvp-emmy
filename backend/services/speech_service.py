from datetime import datetime
from pathlib import Path
from uuid import uuid4

import numpy as np
import soundfile as sf
from piper import PiperVoice

# Piper - by far the fastest local TTS option tested (sub-1s vs.
# Kokoro's 10-70s+), at the cost of a more synthetic-sounding voice.
# Male voices only, to match the character.
PIPER_MODEL_DIRECTORY = Path(__file__).resolve().parent.parent / "models" / "piper"

VOICE_MODELS = {
    "en_GB-alan-medium": "en_GB-alan-medium.onnx",
    "en_US-ryan-medium": "en_US-ryan-medium.onnx",
    "en_US-joe-medium": "en_US-joe-medium.onnx",
    "en_US-danny-low": "en_US-danny-low.onnx",
    "en_GB-northern_english_male-medium": "en_GB-northern_english_male-medium.onnx",
}

DEFAULT_VOICE = "en_GB-alan-medium"

# One loaded PiperVoice per model, shared across SpeechService
# instances (each /settings call builds a new SpeechService) so
# switching voices doesn't reload models already in memory.
_loaded_voices: dict[str, PiperVoice] = {}


def _get_voice(voice_id: str) -> PiperVoice:
    if voice_id not in VOICE_MODELS:
        raise ValueError(f"Unknown Piper voice: {voice_id}")

    if voice_id not in _loaded_voices:
        model_path = PIPER_MODEL_DIRECTORY / VOICE_MODELS[voice_id]
        _loaded_voices[voice_id] = PiperVoice.load(model_path)

    return _loaded_voices[voice_id]


class SpeechService:
    def __init__(
        self,
        output_directory: Path | str = "outputs/audio",
        voice: str = DEFAULT_VOICE,
        speed: float = 1.0,
    ) -> None:
        self.output_directory = Path(output_directory)
        self.output_directory.mkdir(parents=True, exist_ok=True)
        self.speed = speed

        self.pipeline = _get_voice(voice or DEFAULT_VOICE)

    def generate_speech(self, text: str) -> Path:
        if not text or not text.strip():
            raise ValueError("Speech text cannot be empty.")

        chunks = list(self.pipeline.synthesize(text.strip()))

        if not chunks:
            raise ValueError("Piper returned no audio for this text.")

        audio_data = np.concatenate(
            [chunk.audio_float_array for chunk in chunks]
        )
        sample_rate = chunks[0].sample_rate

        date_directory = self.output_directory / datetime.now().strftime("%Y-%m-%d")
        date_directory.mkdir(parents=True, exist_ok=True)
        filename = f"{datetime.now().strftime('%H%M%S')}_{uuid4().hex[:8]}.wav"
        output_path = date_directory / filename

        sf.write(str(output_path), audio_data, sample_rate)
        return output_path
