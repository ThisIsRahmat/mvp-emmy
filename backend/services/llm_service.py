from ollama import chat
from backend.models.api_models import PromptResponse


class LLMService:
    def __init__(self,
     model: str = "gemma3:4b", 
     agent_type: str = "Peer",
     custom_prompt: str = ""):
        self.model = model
        self.agent_type = agent_type
        self.custom_prompt = custom_prompt

    def get_system_prompt(self) -> str:
        if self.agent_type == "Instructor":
            return """
                You are an AI programming instructor.

                Guide the learner through programming problems.
                Explain concepts clearly.
                Ask guiding questions where appropriate.
                Avoid immediately solving everything for them.
                """   

        if self.agent_type == "Peer":
         return """
            You are an AI pair-programming companion.

            Collaborate with the developer as a teammate.
            Discuss ideas, trade-offs and implementation choices.
            Give concise and practical coding assistance.
            """

        if (
            self.agent_type == "Other"
            and self.custom_prompt.strip()
        ):
            return self.custom_prompt.strip()

        return "You are a helpful AI coding companion."

    def generate_response(self, prompt: str) -> PromptResponse:

        response = chat(
            model=self.model,
            messages=[
                {
                    "role": "system",
                    "content": self.get_system_prompt(),
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