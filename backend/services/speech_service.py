from datetime import datetime
from pathlib import Path
from uuid import uuid4

import soundfile as sf
from kokoro_mlx import KokoroTTS


class SpeechService:
    def __init__(
        self,
        output_directory: Path | str = "outputs/audio",
        voice: str = "am_adam",
        speed: float = 1.0,
    ) -> None:
        self.output_directory = Path(output_directory)
        self.output_directory.mkdir(parents=True, exist_ok=True)
        self.voice = voice
        self.speed = speed

        # MLX runs natively on Apple Silicon's GPU - faster than the
        # ONNX+CoreML path for this model, and no execution-provider
        # partitioning/fallback-to-CPU to worry about.
        self.pipeline = KokoroTTS.from_pretrained()

    def generate_speech(self, text: str) -> Path:
        if not text or not text.strip():
            raise ValueError("Speech text cannot be empty.")

        result = self.pipeline.generate(
            text.strip(),
            voice=self.voice,
            speed=self.speed,
        )

        date_directory = self.output_directory / datetime.now().strftime("%Y-%m-%d")
        date_directory.mkdir(parents=True, exist_ok=True)
        filename = f"{datetime.now().strftime('%H%M%S')}_{uuid4().hex[:8]}.wav"
        output_path = date_directory / filename

        sf.write(str(output_path), result.audio, result.sample_rate)
        return output_path
