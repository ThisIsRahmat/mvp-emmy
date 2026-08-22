import numpy as np
from faster_whisper import WhisperModel


class TranscriptionService:
    def __init__(
        self,
        model_name: str = "tiny.en",
    ):
        # CTranslate2-based reimplementation of Whisper - same
        # accuracy, much faster on CPU (int8 quantized).
        self.client = WhisperModel(
            model_name,
            device="cpu",
            compute_type="int8",
        )

    def transcribe_audio(
        self,
        audio_path
    ) -> str:

        segments, _info = self.client.transcribe(
            str(audio_path),
            language="en",
        )

        return "".join(segment.text for segment in segments).strip()

    def warm_up(self) -> None:
        """Runs one throwaway inference so the first real request
        doesn't pay the model's initial-call cost."""
        silence = np.zeros(16000, dtype=np.float32)
        segments, _info = self.client.transcribe(silence, language="en")
        list(segments)
