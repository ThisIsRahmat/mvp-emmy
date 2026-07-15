from backend.models.api_models import  PromptResponse
from ollama import chat
from urllib import response

class LLMService:
    def __init__(self, llm_client):
        self.llm_client = llm_client

    def generate_response(self, prompt: str) -> PromptResponse:
        response = chat(
        model='gemma3:4b',
        messages=[{'role': 'user', 'content': prompt }],)
        llm_response = response['message']['content']
        return llm_response