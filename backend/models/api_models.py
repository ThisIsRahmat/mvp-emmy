
from pydantic import BaseModel

class PromptRequest(BaseModel):
    prompt: str

class GeneratedFile(BaseModel):
    path: str
    content: str

class PromptResponse(BaseModel):
    response_speech: str
    files: list[GeneratedFile] = []

class TranscriptionResponse(BaseModel):
    text: str   

class SpeechRequest(BaseModel):
    text: str

