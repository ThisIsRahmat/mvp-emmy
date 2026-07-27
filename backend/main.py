from pathlib import Path
from shutil import copyfileobj
from tempfile import NamedTemporaryFile

from fastapi import FastAPI, File, HTTPException, UploadFile
from fastapi.staticfiles import StaticFiles

from backend.services.transcription_service import TranscriptionService
from backend.services.llm_service import LLMService
from backend.services.speech_service import SpeechService


app = FastAPI()

audio_directory = Path("outputs/audio")
audio_directory.mkdir(parents=True, exist_ok=True)

app.mount(
    "/audio",
    StaticFiles(directory=str(audio_directory)),
    name="audio",
)

transcription_service = TranscriptionService()
llm_service = LLMService()
speech_service = SpeechService(output_directory=audio_directory)


@app.post("/agent/respond")
def agent_respond(audio: UploadFile = File(...)):
    temporary_path = None

    try:
        with NamedTemporaryFile(suffix=".wav", delete=False) as temp_file:
            copyfileobj(audio.file, temp_file)
            temporary_path = Path(temp_file.name)

        transcription = transcription_service.transcribe_audio(
            temporary_path
        )

        llm_result = llm_service.generate_response(
            transcription
        )

        audio_path = speech_service.generate_speech(
            llm_result.response_speech
        )

        relative_path = audio_path.relative_to(audio_directory)

        return {
            "transcription": transcription,
            "response_speech": llm_result.response_speech,
            "response_code": llm_result.response_code,
            "audio_url": f"/audio/{relative_path.as_posix()}",
        }

    except Exception as error:
        raise HTTPException(
            status_code=500,
            detail=str(error),
        ) from error

    finally:
        audio.file.close()

        if temporary_path and temporary_path.exists():
            temporary_path.unlink()