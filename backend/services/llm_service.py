import re

from ollama import chat
from backend.models.api_models import GeneratedFile, PromptResponse

# implemented regex to pull our code blocks from LLM response_speech
FENCED_CODE_BLOCK = re.compile(
    r"(?:\*{0,2}\s*([\w./-]+\.cs)[:*\s]*\n+)?"
    # A space instead of a newline after the language tag (e.g.
    # "```csharp .code-viewer {") used to break this entirely - \s*
    # alone (no required \n) tolerates either.
    r"```(?:csharp|cs|c#)?\s*(.*?)\n?```",
    re.DOTALL | re.IGNORECASE,
)

#back up for regex to pull out code blocks from LLM response_speech
LEFTOVER_FILENAME_HEADER = re.compile(
    r"^\s*\*{0,2}\s*[\w./-]+\.cs\s*[:*]*\s*$",
    re.MULTILINE,
)

CLASS_NAME = re.compile(r"\bclass\s+(\w+)")


def extract_code_blocks_from_speech(
    response_speech: str,
) -> tuple[str, list[GeneratedFile]]:
    extracted: list[GeneratedFile] = []

    def replace(match: re.Match) -> str:
        filename = match.group(1)
        code = match.group(2).strip()

        # Generation sometimes gets cut off right as a fence opens,
        # leaving an empty/near-empty block. This is valid JSON (so
        # the retry-on-invalid-JSON check never catches it) but
        # writing it would silently overwrite a real file with
        # nothing - never treat near-empty content as real code.
        if len(code) < 10:
            return ""

        if not filename:
            class_match = CLASS_NAME.search(code)
            filename = f"{class_match.group(1)}.cs" if class_match else "GeneratedFile.cs"

        extracted.append(GeneratedFile(path=filename, content=code))

        return ""

    cleaned = FENCED_CODE_BLOCK.sub(replace, response_speech)

    if extracted:
        cleaned = LEFTOVER_FILENAME_HEADER.sub("", cleaned)

    cleaned = re.sub(r"\n{3,}", "\n\n", cleaned).strip()

    if not cleaned and extracted:
        cleaned = "Done - check the new file."

    return cleaned, extracted

# FILES_FIELD_INSTRUCTIONS = (
#     "You MUST ALWAYS respond in json using two fields:\n"
#     "- response_speech: ONLY natural spoken language, this is READ ALOUD by "
#     "text-to-speech. It must NEVER contain code, brace characters, semicolons, "
#     "or anything that looks like a code block \n"
#     "- files: a list of {path, content} objects, one per file you are "
#     "creating or changing. content is the COMPLETE file, ready to save "
#     "exactly as given - not a description of it. Leave files empty ([]) "
#     "if this turn doesn't need a file change. If you are editing a file "
#     "that was shown to you above under 'These project files are currently "
#     "selected', you MUST use that exact same path string, character for "
#     "character - do not shorten it or invent a new one. Only make up a new "
#     "path when the file genuinely does not exist yet.\n"
#     "If a file is being written, response_speech must NOT also contain that file's code - the code belongs ONLY in files.\n\n"
#     "file's code - the code belongs ONLY in files. AND MUST NEVER BE A PART OF response_speech\n\n"
# )


response_instructions = (
     "This is always a Unity C# project - never ask what language or framework to use, and never ask clarifying questions before writing code. If a request is a bit ambiguous, make a reasonable assumption and write the file anyway; you can mention the assumption briefly in response_speech.\n"
     "You must always respond in JSON using two fields:\n"
                    "- response_speech: ONLY natural spoken language, this is your response in the conversation and is READ ALOUD by a text-to-speech engine. It must NEVER under ANY CIRCUMSTANCE contain ANY code, brace characters, semicolons, or anything that is not part of natural speech. It is your written response to the user and should be concise, clear, and helpful.\n"
                    "- files: a list of {path, content} objects, one per file you are creating or changing. content is the COMPLETE file, ready to save exactly as given - not a description of it. Leave files empty ([]) if this turn doesn't need a file change. If you are editing a file that was shown to you above under 'These project files are currently selected', you MUST use that exact same path string, character for character - do not shorten it or invent a new one. Only make up a new path when the file genuinely does not exist yet.\n"
                    "You may write more than one file in a single turn if the request needs it - add one entry per file.\n\n" )

example_json = "{\"response_speech\": \"<your spoken response here>\", \"files\": [{\"path\": \"<file path>\", \"content\": \"<file content>\"}]}"

class LLMService:
    def __init__(self,
                 model: str = "qwen2.5-coder:7b",
                 agent_type: str = "Peer",
                 custom_prompt: str = ""):
        self.model = model
        self.agent_type = agent_type
        self.custom_prompt = custom_prompt

    def get_system_prompt(self) -> str:
        if self.agent_type == "Instructor":
            return (
                "You are an AI programming agent in instructor mode, embodied as a character in Unity that the learner can see and talk to.\n\n"
                "Acts as a guide for the learner through programming problems a in pAIr programming session with the developer as the driver .\n"
                "Explain concepts clearly.\n"
                "Ask guiding questions where appropriate.\n"
                 + response_instructions + 
                 "Return valid JSON using this schema:" + example_json
            )

        if self.agent_type == "Peer":
            return (
                "You are an AI pair-programming agent in peer mode, embodied as a character the developer can see and talk to.\n\n"
                "Act as a teammate collaborating with the developer in a pAIr programming session with the developer as the driver.\n"
                "Discuss ideas, trade-offs and implementation choices when asked.\n"
                "Give concise and practical coding assistance.\n"
                           + response_instructions + 
                                 "Return valid JSON using this schema:" + example_json
            )

        if self.agent_type == "Other" and self.custom_prompt.strip():
            self.custom_prompt = self.custom_prompt.strip() + "\n\n" + response_instructions + "Return valid JSON using this schema:" + example_json
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
            # 768 was enough for short new scripts, but editing an
            # existing file means echoing its entire content back -
            # a real file (plus JSON string-escaping overhead) can
            # easily blow past that, cutting generation off mid-file
            # with technically-invalid JSON.
            options={"num_predict": 2048, "repeat_penalty": 1.3},
        )

        print(f"Raw LLM JSON: {response['message']['content']}")

        result = PromptResponse.model_validate_json(response["message"]["content"])

        print(f"LLM response: {result.response_speech}")
        print(f"LLM files: {result.files}")

        cleaned_speech, extracted_files = extract_code_blocks_from_speech(
            result.response_speech
        )

        if extracted_files:
            result.response_speech = cleaned_speech
            result.files = result.files + extracted_files

        return result