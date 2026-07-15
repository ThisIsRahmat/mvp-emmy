from fastapi import FastAPI
from fastapi.responses import FileResponse
from backend.models.api_models import (
    PromptRequest,
    PromptResponse,
    SpeechRequest,
    SpeechResponse,
    TranscriptionResponse,
)
from backend.services.llm_service import LLMService
from backend.services.speech_service import SpeechService
from backend.services.transcription_service import TranscriptionService

app = FastAPI()

transcription_service = TranscriptionService()
llm_service = LLMService()
speech_service = SpeechService()


@app.get("/health")
async def health():
    return {"message": "The API is running successfully."}


@app.post("/transcribe", response_model=TranscriptionResponse)
async def transcribe(file_path: str):
    text = transcription_service.transcribe_audio(file_path)
    return TranscriptionResponse(text=text)


@app.post("/prompt", response_model=PromptResponse)
async def prompt(request: PromptRequest):
    response = llm_service.generate_response(request.text)
    return response


@app.post("/speech")
async def speech(request: SpeechRequest) -> FileResponse:
    output_path = speech_service.generate_speech(request.text)

    return FileResponse(
        path=output_path,
        media_type="audio/wav",
        filename="speech.wav",
    )