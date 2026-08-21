import whisper

class TranscriptionService:
    def __init__(
        self,
        model: str = "whisper",
        model_name: str | None = None
    ):
        self.model = model

        if self.model == "whisper":
            whisper_model = model_name or "base.en"

            self.client = whisper.load_model(
                whisper_model
            )
        else:
            raise ValueError(
                f"Unsupported STT model: {self.model}"
            )

    def transcribe_audio(
        self,
        audio_path
    ) -> str:

        if self.model == "whisper":
            result = self.client.transcribe(
                str(audio_path), fp16=False, language='English'
            )

            return result["text"].strip()

        else:
            raise ValueError(
                f"Unsupported STT model: {self.model}"
            )