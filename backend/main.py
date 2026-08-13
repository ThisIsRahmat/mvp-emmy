from pathlib import Path
from shutil import copyfileobj
from tempfile import NamedTemporaryFile

from fastapi import FastAPI, File, HTTPException, UploadFile
from fastapi.staticfiles import StaticFiles
from pydantic import BaseModel

from backend.services.transcription_service import TranscriptionService
from backend.services.conversation_service import ConversationService
from backend.services.llm_service import LLMService
from backend.services.speech_service import SpeechService


app = FastAPI()

audio_directory = Path("outputs/audio")
audio_directory.mkdir(parents=True, exist_ok=True)
transcription_service = TranscriptionService()

app.mount(
    "/audio",
    StaticFiles(directory=str(audio_directory)),
    name="audio",
)


class AgentSettings(BaseModel):
    agent_type: str
    llm: str
    tts: str
    tts_voice: str = ""
    custom_prompt: str = ""


# Changes the UI names to actual backend model names
llm_models = {
    "Gemma 3": "gemma3:4b",
    "Devstral Small 2": "devstral-small-2",
    # "DeepSeek-v4 Flash": "deepseek-v4-flash:latest",
}


tts_models = {
    "Kokoro": "kokoro",
    "Piper": "piper"
}



default_settings = AgentSettings(
    agent_type="Peer",
    llm="Gemma 3",
    tts="Kokoro",
    # add a default voice for TTS if needed
    tts_voice="",
    custom_prompt="",
)

current_settings = default_settings 
conversation_service = ConversationService()

def build_services(settings: AgentSettings):
    if settings.llm not in llm_models:
        raise ValueError(f"Unsupported LLM selection: {settings.llm}")
    if settings.tts not in tts_models:
        raise ValueError(f"Unsupported TTS selection: {settings.tts}")


    llm_service = LLMService(model=llm_models[settings.llm], agent_type=settings.agent_type, custom_prompt=settings.custom_prompt)
    speech_service = SpeechService(model=tts_models[settings.tts], voice=settings.tts_voice, output_directory=audio_directory)

    return llm_service, speech_service

# Initialize default services so endpoints work before a /settings call
try:
    llm_service, speech_service = build_services(current_settings)
except Exception:
    llm_service = None
    speech_service = None

@app.get("/health")
def health():
    return {
        "message": "Backend is running.",
        "settings": current_settings,
    }


@app.post("/settings")
def update_settings(settings: AgentSettings):
    global current_settings
    global llm_service
    global speech_service
    try:
        # build and assign backend services for the selected models
        llm_service, speech_service = build_services(settings)

        current_settings = settings

        conversation_service.start_session(
            agent_type=settings.agent_type,
            llm=settings.llm,
            tts=settings.tts,
            voice=settings.tts_voice,
        )

        return {
            "message": "Agent configured successfully.",
            "settings": settings,
        }

    except Exception as error:
        raise HTTPException(
            status_code=400,
            detail=str(error),
        ) from error

@app.post("/agent/greeting")
def agent_greeting():
    greeting_text = (
        "Hi, I'm your AI companion. "
        "Hold the space bar when you're ready to talk."
    )

    audio_path = speech_service.generate_speech(
        greeting_text
    )

    relative_path = audio_path.relative_to(
        audio_directory
    )

    return {
        "text": greeting_text,
        "audio_url": f"/audio/{relative_path.as_posix()}",
    }

@app.post("/agent/respond")
def agent_respond(
    audio: UploadFile = File(...)
):
    temporary_path = None

    try:
        with NamedTemporaryFile(
            suffix=".wav",
            delete=False,
        ) as temp_file:

            copyfileobj(
                audio.file,
                temp_file,
            )

            temporary_path = Path(
                temp_file.name
            )

        transcription = (
            transcription_service.transcribe_audio(
                temporary_path
            )
        )

        llm_result = (
            llm_service.generate_response(
                transcription
            )
        )

        conversation_service.add_agent_message(
            llm_result.response_speech
        )

        audio_path = (
            speech_service.generate_speech(
                llm_result.response_speech
            )
        )

        relative_path = audio_path.relative_to(
            audio_directory
        )

        return {
            "transcription": transcription,
            "response_speech": llm_result.response_speech,
            "response_code": llm_result.response_code,
            "audio_url": (
                f"/audio/{relative_path.as_posix()}"
            ),
        }

    except Exception as error:
        raise HTTPException(
            status_code=500,
            detail=str(error),
        ) from error

    finally:
        audio.file.close()

        if (
            temporary_path
            and temporary_path.exists()
        ):
            temporary_path.unlink()

@app.get("/conversation")
def get_conversation():
    if conversation_service.current_session is None:
        return {"messages": []}
    return {"messages": [m.model_dump() for m in conversation_service.current_session.messages]}