
from pydantic import BaseModel 

class PromptRequest(BaseModel):
    prompt: str

class PromptResponse(BaseModel):
    response_speech: str
    response_code: str = ""
    file_path: str | None = None
    file_content: str | None = None

class TranscriptionResponse(BaseModel):
    text: str   

class SpeechRequest(BaseModel):
    text: str

