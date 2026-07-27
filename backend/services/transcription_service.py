import whisper


class TranscriptionService:
    def __init__(
        self,
        transcription_client=None,
        model_name: str = "base",
    ):
        self.transcription_client = (
            transcription_client
            if transcription_client is not None
            else whisper.load_model(model_name)
        )

    def transcribe_audio(self, audio_path: str) -> str:
        result = self.transcription_client.transcribe(
            str(audio_path)
        )

        return result["text"].strip()