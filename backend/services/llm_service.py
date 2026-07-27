from ollama import chat

from backend.models.api_models import PromptResponse


class LLMService:
    def __init__(self, model: str = "gemma3:4b"):
        self.model = model

    def generate_response(self, prompt: str) -> PromptResponse:
        response = chat(
            model=self.model,
            messages=[
                {
                    "role": "system",
                    "content": """
You are an AI peer-programming companion.

Return JSON with:
- response_speech: a concise spoken explanation
- response_code: relevant code, or an empty string if none is needed

Do not put code inside response_speech.
""",
                },
                {
                    "role": "user",
                    "content": prompt,
                },
            ],
            format=PromptResponse.model_json_schema(),
        )

        return PromptResponse.model_validate_json(
            response["message"]["content"]
        )