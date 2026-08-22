import json
import time
from pathlib import Path
from shutil import copyfileobj
from tempfile import NamedTemporaryFile

from fastapi import FastAPI, File, HTTPException, UploadFile, Form
from fastapi.staticfiles import StaticFiles
from pydantic import BaseModel

from backend.services.transcription_service import TranscriptionService
from backend.services.conversation_service import ConversationService
from backend.services.llm_service import LLMService
from backend.services.speech_service import SpeechService
from backend.tools.file_tools import FileTools


app = FastAPI()

audio_directory = Path("outputs/audio")
audio_directory.mkdir(parents=True, exist_ok=True)
transcription_service = TranscriptionService()
file_tools = FileTools(project_root="../Assets/Scripts")

app.mount(
    "/audio",
    StaticFiles(directory=str(audio_directory)),
    name="audio",
)


class AgentSettings(BaseModel):
    agent_type: str
    tts: str
    tts_voice: str = ""
    custom_prompt: str = ""


# Changed the UI names to actual backend model names
llm_models = {
    "Gemma 3": "gemma3:4b",
    "Devstral Small 2": "devstral-small-2",
}


# How many past messages to replay to the LLM, roughly five turns.
# Kept small so the prompt stays cheap for the local model.
MAX_HISTORY_MESSAGES = 10



default_settings = AgentSettings(
    agent_type="Peer",
    llm="Gemma 3",
    tts="Kokoro",

    tts_voice="",
    custom_prompt="",
)

current_settings = default_settings 
conversation_service = ConversationService()

def build_services(settings: AgentSettings):

    llm_service = LLMService(agent_type=settings.agent_type, custom_prompt=settings.custom_prompt)
    speech_service = SpeechService( voice=settings.tts_voice or "en_GB-alan-medium", output_directory=audio_directory)

    return llm_service, speech_service


def warm_up(llm_service: LLMService, speech_service: SpeechService) -> None:
    """
    Ollama unloads the model between requests when idle, and Kokoro's
    first inference pays a one-off compile/cache cost - both showed
    up as 20-150s added to the first real turn. Eating that cost here
    instead, at startup, keeps it off the user-facing latency.
    """
    try:
        transcription_service.warm_up()
    except Exception as error:
        print(f"STT warm-up failed (non-fatal): {error}")

    try:
        speech_service.generate_speech("Ready.")
    except Exception as error:
        print(f"TTS warm-up failed (non-fatal): {error}")

    try:
        llm_service.generate_response(prompt="Hello", history=[])
    except Exception as error:
        print(f"LLM warm-up failed (non-fatal): {error}")


# Initialize default services so endpoints work before a /settings call
try:
    llm_service, speech_service = build_services(current_settings)
    warm_up(llm_service, speech_service)
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
        warm_up(llm_service, speech_service)

        current_settings = settings

        conversation_service.start_session(
            agent_type=settings.agent_type,
            tts_voice=settings.tts_voice,
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




# file_tools = FileTools(project_root="/absolute/path/to/your/UnityProject/Assets/Scripts")


@app.post("/agent/respond")
def agent_respond(audio: UploadFile = File(...),  
                  selected_files: str = Form("[]"),):
    temporary_path = None

    file_paths = json.loads(
    selected_files
    )

    file_contents = file_tools.read_context_files(
        file_paths
    )

    try:
        with NamedTemporaryFile(suffix=".wav", delete=False) as temp_file:
            copyfileobj(audio.file, temp_file)
            temporary_path = Path(temp_file.name)

   

        t0 = time.time()


        transcription = transcription_service.transcribe_audio(temporary_path)

        # Snapshot history BEFORE adding the new user message.
        # Built as plain role/content dicts (not the pydantic
        # ConversationMessage objects) since that's what the LLM
        # client's messages list expects.
        history_for_llm = (
            [
                {"role": m.role, "content": m.content}
                for m in conversation_service.current_session.messages[
                    -MAX_HISTORY_MESSAGES:
                ]
            ]
            if conversation_service.current_session
            else []
        )

        conversation_service.add_user_message(transcription)

        t1 = time.time()

        # Send prompt + selected files to LLM

        llm_result = llm_service.generate_response(
            prompt=transcription,
            files=file_contents,
            history=history_for_llm,
        )

        conversation_service.add_agent_message(llm_result.response_speech)

        t2 = time.time()

        audio_path = speech_service.generate_speech(llm_result.response_speech)
        relative_path = audio_path.relative_to(audio_directory)

        t3 = time.time()

        print(
            f"STT: {t1-t0:.1f}s | LLM: {t2-t1:.1f}s | TTS: {t3-t2:.1f}s | "
            f"reply_len: {len(llm_result.response_speech)} chars | "
            f"history_msgs: {len(history_for_llm)}"
        )




        # Only attempt a file write if the model actually returned file info
        file_result = None
        if llm_result.response_code and getattr(llm_result, "file_path", None):
            file_result = file_tools.write_file(
                llm_result.file_path,
                llm_result.response_code,
            )

        return {
            "transcription": transcription,
            "response_speech": llm_result.response_speech,
            "response_code": llm_result.response_code,
            "audio_url": f"/audio/{relative_path.as_posix()}",
            "file_status": file_result["status"] if file_result else None,
            "file_path": file_result["path"] if file_result else None,
        }

    except Exception as error:
        raise HTTPException(status_code=500, detail=str(error)) from error

    finally:
        audio.file.close()
        if temporary_path and temporary_path.exists():
            temporary_path.unlink()

class ImportFileRequest(BaseModel):
    path: str


@app.post("/files/import")
def import_file(payload: ImportFileRequest):
    """
    Registers a dropped file as agent context. Participants' files
    live wherever their own project is, so this reads directly from
    the given path rather than copying anything into a shared
    project folder, and records it as a message in the conversation
    history so it flows to the LLM on subsequent turns.
    """
    try:
        content = file_tools.read_context_file(payload.path)
    except (FileNotFoundError, ValueError) as error:
        raise HTTPException(status_code=400, detail=str(error)) from error

    if conversation_service.current_session is None:
        conversation_service.start_session(
            agent_type=current_settings.agent_type,
            tts_voice=current_settings.tts_voice,
        )

    file_name = Path(payload.path).name

    conversation_service.add_context_file_message(
        file_name,
        payload.path,
        content,
    )

    return {
        "status": "added",
        "path": payload.path,
        "name": file_name,
    }


@app.get("/files")
def list_files():
    return {
        "files": file_tools.list_files()
    }

@app.get("/files/content")
def get_file_content(path: str):
    try:
        content = file_tools.read_context_file(path)
    except (FileNotFoundError, ValueError) as error:
        raise HTTPException(status_code=400, detail=str(error)) from error

    return {
        "path": path,
        "content": content,
    }
    
@app.get("/conversation")
def get_conversation():
    if conversation_service.current_session is None:
        return {"messages": []}
    return {"messages": [m.model_dump() for m in conversation_service.current_session.messages]}