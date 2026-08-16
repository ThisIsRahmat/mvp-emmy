from pydantic import BaseModel, Field
from datetime import datetime
from pathlib import Path
from uuid import uuid4

class ConversationMessage(BaseModel):
    timestamp: datetime
    role: str
    content: str

class ConversationSession(BaseModel):
    session_id: str
    started_at: datetime

    agent_type: str
    tts_voice: str

    messages: list[ConversationMessage] = Field(
        default_factory=list
    )

class ConversationService:
    def __init__(
        self,
        output_directory: str | Path = "outputs/conversations",
    ):
        self.output_directory = Path(output_directory)

        self.output_directory.mkdir(
            parents=True,
            exist_ok=True,
        )

        self.current_session: ConversationSession | None = None

    def start_session(
        self,
        agent_type: str,
        tts_voice: str,
    ) -> ConversationSession:

        session_id = (
            datetime.now().strftime("%Y%m%d_%H%M%S")
            + "_"
            + uuid4().hex[:6]
        )

        self.current_session = ConversationSession(
            session_id=session_id,
            started_at=datetime.now(),
            agent_type=agent_type,
            tts_voice=tts_voice,
        )

        self.save()

        return self.current_session

    def add_user_message(
        self,
        content: str,
    ) -> ConversationMessage:

        message = ConversationMessage(
            timestamp=datetime.now(),
            role="user",
            content=content,
        )

        self._add_message(message)

        return message

    def add_agent_message(
        self,
        content: str,
    ) -> ConversationMessage:

        message = ConversationMessage(
            timestamp=datetime.now(),
            role="assistant",
            content=content,
        )

        self._add_message(message)

        return message

    def _add_message(
        self,
        message: ConversationMessage,
    ) -> None:

        if self.current_session is None:
            raise RuntimeError(
                "No conversation session has been started."
            )

        self.current_session.messages.append(message)

        self.save()

    def save(self) -> Path:

        if self.current_session is None:
            raise RuntimeError(
                "There is no active conversation to save."
            )

        file_path = (
            self.output_directory
            / f"{self.current_session.session_id}.json"
        )

        file_path.write_text(
            self.current_session.model_dump_json(
                indent=2
            ),
            encoding="utf-8",
        )

        return file_path

    def load(
        self,
        session_id: str,
    ) -> ConversationSession:

        file_path = (
            self.output_directory
            / f"{session_id}.json"
        )

        if not file_path.exists():
            raise FileNotFoundError(
                f"Conversation session '{session_id}' was not found."
            )

        session = ConversationSession.model_validate_json(
            file_path.read_text(
                encoding="utf-8"
            )
        )

        self.current_session = session

        return session
 

