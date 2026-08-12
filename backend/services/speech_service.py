from datetime import datetime
from pathlib import Path
from uuid import uuid4

import soundfile as sf
from pykokoro import GenerationConfig, KokoroPipeline, PipelineConfig


class SpeechService:
    def __init__(
        self,
        model: str,
        output_directory: Path | str = "outputs/audio",
        voice: str = "bf_emma",
        speed: float = 1.0,
    ) -> None:
        self.model = model
        self.output_directory = Path(output_directory)
        self.output_directory.mkdir(parents=True, exist_ok=True)
        self.voice = voice
        self.speed = speed

        if self.model == "kokoro":
            config = PipelineConfig(
                voice=self.voice,
                generation=GenerationConfig(speed=self.speed),
            )
            self.pipeline = KokoroPipeline(config)
        else:
            raise ValueError(f"Unsupported TTS model: {self.model}")

    def generate_speech(self, text: str) -> Path:
        if not text or not text.strip():
            raise ValueError("Speech text cannot be empty.")

        # Dispatch by model, since each backend's call signature differs
        if self.model == "kokoro":
            result = self.pipeline.run(text.strip())
            audio_data, sample_rate = result.audio, result.sample_rate
        else:
            raise ValueError(f"Unsupported TTS model: {self.model}")

        date_directory = self.output_directory / datetime.now().strftime("%Y-%m-%d")
        date_directory.mkdir(parents=True, exist_ok=True)
        filename = f"{datetime.now().strftime('%H%M%S')}_{uuid4().hex[:8]}.wav"
        output_path = date_directory / filename

        sf.write(str(output_path), audio_data, sample_rate)
        return output_path