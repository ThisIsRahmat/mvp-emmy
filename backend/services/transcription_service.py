
import whisper

class TranscriptionService:
    def __init__(self, transcription_client):
        self.transcription_client = transcription_client
    
    def transcribe_audio(self, audio_path: str) -> str:
        model = whisper.load_model("base")
        result = model.transcribe(audio_path, fp16=False)
        return result["text"]