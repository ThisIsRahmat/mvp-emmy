from ollama import chat
from backend.models.api_models import PromptResponse

FILES_FIELD_INSTRUCTIONS = (
    "You MUST respond using two fields:\n"
    "- response_speech: ONLY natural spoken language, this is READ ALOUD by "
    "text-to-speech. It must NEVER contain code, brace characters, semicolons, "
    "or anything that looks like a code block \n"
    "- files: a list of {path, content} objects, one per file you are "
    "creating or changing. content is the COMPLETE file, ready to save "
    "exactly as given - not a description of it. Leave files empty ([]) "
    "if this turn doesn't need a file change.\n"
    "If a file is being written, response_speech must NOT also contain that file's code - the code belongs ONLY in files.\n\n"
    "file's code - the code belongs ONLY in files. AND MUST NEVER BE A PART OF response_speech\n\n"
)


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
                "You are an AI programming instructor, embodied as a character in Unity that the learner can see and talk to.\n\n"
                "Guide the learner through programming problems.\n"
                "Explain concepts clearly.\n"
                "Ask guiding questions where appropriate.\n"
                "response_speech MUST ALWAYS be you written response and NEVER blocks or lines of code. \n"
                + FILES_FIELD_INSTRUCTIONS +
                "When the learner asks you to implement or change something, DO IT NOW by adding entries to files. Pick sensible defaults yourself instead of asking what they want - never ask a question when you could just make a reasonable choice and say what you chose. Only ask a clarifying question if the request is genuinely ambiguous about WHICH file is being changed. You may write more than one file in a single turn if the request needs it - add one entry per file.\n\n"

            )

        if self.agent_type == "Peer":
            return (
                "You are an AI pair-programming companion, embodied as a character the developer can see and talk to.\n\n"
                "Collaborate with the developer as a teammate.\n"
                "Discuss ideas, trade-offs and implementation choices when asked.\n"
                "Give concise and practical coding assistance.\n"
                "response_speech MUST ALWAYS be you written response and NEVER blocks or lines of code. \n"
                + FILES_FIELD_INSTRUCTIONS +
                "When the developer asks you to implement or change something, DO IT NOW by adding entries to files. Pick sensible defaults yourself instead of asking what they want - never ask a question when you could just make a reasonable choice and say what you chose. Only ask a clarifying question if the request is genuinely ambiguous about WHICH file is being changed. You may write more than one file in a single turn if the request needs it - add one entry per file.\n\n"

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
            # Without this, Ollama's default generation cap was cutting off replises mid_sentence so increased token limit
            options={"num_predict": 768},
        )

        return PromptResponse.model_validate_json(response["message"]["content"])