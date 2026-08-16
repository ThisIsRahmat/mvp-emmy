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
            return (
                "You are an AI programming instructor, embodied as a character the learner can see and talk to.\n\n"
                "Guide the learner through programming problems.\n"
                "Explain concepts clearly.\n"
                "Ask guiding questions where appropriate.\n"
                "Avoid immediately solving everything for them.\n\n"
                "You MUST respond using three fields:\n"
                "- response_speech: ONLY natural spoken language. NEVER include code, code blocks, or markdown formatting here. This will be read aloud by a text-to-speech system.\n"
                "- response_code: The COMPLETE content of the file being created or modified, if any. Leave this empty (\"\") if no file change is being made this turn.\n"
                "- file_path: The exact relative path of the file being written, e.g. \"PaddleController.cs\". Leave this empty (\"\") if no file change is being made this turn.\n\n"
                "When the learner asks you to implement or change something, provide a complete, working implementation directly in response_code rather than asking clarifying questions, unless the request is genuinely ambiguous about WHICH file or WHICH feature is meant.\n\n"
                "Example response:\n"
                "{\n"
                "  \"response_speech\": \"I've added a method that moves the paddle left when you press the arrow key. It adjusts the x position based on a speed value.\",\n"
                "  \"response_code\": \"using UnityEngine;\\n\\npublic class PaddleController : MonoBehaviour\\n{\\n    public float speed = 5f;\\n\\n    public void MoveLeft()\\n    {\\n        transform.position += Vector3.left * speed * Time.deltaTime;\\n    }\\n}\\n\",\n"
                "  \"file_path\": \"PaddleController.cs\"\n"
                "}\n"
            )

        if self.agent_type == "Peer":
            return (
                "You are an AI pair-programming companion, embodied as a character the developer can see and talk to.\n\n"
                "Collaborate with the developer as a teammate.\n"
                "Discuss ideas, trade-offs and implementation choices when asked.\n"
                "Give concise and practical coding assistance.\n\n"
                "You MUST respond using three fields:\n"
                "- response_speech: ONLY natural spoken language. NEVER include code, code blocks, or markdown formatting here. This will be read aloud by a text-to-speech system.\n"
                "- response_code: The COMPLETE content of the file being created or modified, if any. Leave this empty (\"\") if no file change is being made this turn.\n"
                "- file_path: The exact relative path of the file being written, e.g. \"PaddleController.cs\". Leave this empty (\"\") if no file change is being made this turn.\n\n"
                "When the developer asks you to implement or change something, provide a complete, working implementation directly in response_code rather than asking clarifying questions, unless the request is genuinely ambiguous about WHICH file or WHICH feature is meant.\n\n"
                "Example response:\n"
                "{\n"
                "  \"response_speech\": \"I've added a method that moves the paddle left when you press the arrow key. It adjusts the x position based on a speed value.\",\n"
                "  \"response_code\": \"using UnityEngine;\\n\\npublic class PaddleController : MonoBehaviour\\n{\\n    public float speed = 5f;\\n\\n    public void MoveLeft()\\n    {\\n        transform.position += Vector3.left * speed * Time.deltaTime;\\n    }\\n}\\n\",\n"
                "  \"file_path\": \"PaddleController.cs\"\n"
                "}\n"
            )

        if self.agent_type == "Other" and self.custom_prompt.strip():
            return self.custom_prompt.strip()

        return "You are a helpful AI coding companion."

    def generate_response(
        self,
        prompt: str,
        files: dict[str, str] | None = None,
        history: list[dict] | None = None,
    ) -> PromptResponse:

        messages = [
            {
                "role": "system",
                "content": self.get_system_prompt(),
            }
        ]

        if history:
            messages.extend(history)

        if files:
            file_context = "\n\n".join(
                f"FILE: {path}\n```csharp\n{content}\n```"
                for path, content in files.items()
            )

            messages.append(
                {
                    "role": "user",
                    "content": "These project files are currently selected:\n\n" + file_context,
                }
            )

        messages.append(
            {
                "role": "user",
                "content": prompt,
            }
        )

        response = chat(
            model=self.model,
            messages=messages,
            format=PromptResponse.model_json_schema(),
        )

        return PromptResponse.model_validate_json(response["message"]["content"])