from pathlib import Path

# Every participant's project lives in a different place, so dropped
# context files are read directly from wherever they are on disk -
# this just guards against accidentally loading something huge into
# the LLM's context window.
MAX_CONTEXT_FILE_BYTES = 200_000

GENERATED_FILES_ROOT = Path("outputs/generated")


def resolve_write_target(
    returned_path: str,
    dropped_paths: list[str],
) -> tuple[str, bool]:
    """
    Decides whether a file the LLM returned is an edit to a file the
    user already dropped in (write back to that exact real path, no
    drag-out needed - their editor picks up the external change) or
    a brand new file (needs a session-scoped scratch location).

    Returns (target_path, is_existing_dropped_file).
    """
    for dropped_path in dropped_paths:
        if dropped_path == returned_path:
            return dropped_path, True

    returned_name = Path(returned_path).name
    name_matches = [
        dropped_path
        for dropped_path in dropped_paths
        if Path(dropped_path).name == returned_name
    ]

    if len(name_matches) == 1:
        return name_matches[0], True

    return returned_path, False


class FileTools:
    def __init__(
        self,
        project_directory: Path | str | None = None,
        project_root: Path | str | None = None,
    ):
        # Accept either `project_directory` or `project_root` for compatibility
        root = project_directory or project_root
        if root is None:
            raise ValueError("project_directory or project_root must be provided")

        self.project_directory = Path(root).resolve()

    def list_files(self) -> list[dict]:
        files = []

        for path in self.project_directory.rglob("*.cs"):
            relative_path = path.relative_to(
                self.project_directory
            )

            files.append(
                {
                    "name": path.name,
                    "path": str(relative_path),
                }
            )

        return files

    def read_file(
        self,
        relative_path: str,
    ) -> str:
        file_path = (
            self.project_directory /
            relative_path
        ).resolve()

        if self.project_directory not in file_path.parents:
            raise ValueError(
                "File is outside project directory."
            )

        if not file_path.exists():
            raise FileNotFoundError(
                f"File not found: {relative_path}"
            )

        return file_path.read_text(
            encoding="utf-8"
        )

    def read_files(
        self,
        paths: list[str],
    ) -> dict[str, str]:
        return {
            path: self.read_file(path)
            for path in paths
        }

    def read_context_file(self, path: str) -> str:
        """
        Reads a file dropped in as agent context. Unlike read_file,
        this is not confined to project_directory - each participant's
        files live wherever their own project is, on their own
        machine, so an absolute path is expected (a relative one is
        resolved against project_directory for convenience).
        """
        candidate = Path(path).expanduser()

        if not candidate.is_absolute():
            candidate = self.project_directory / candidate

        candidate = candidate.resolve()

        if not candidate.exists():
            raise FileNotFoundError(f"File not found: {path}")

        if not candidate.is_file():
            raise ValueError(f"Not a file: {path}")

        size = candidate.stat().st_size

        if size > MAX_CONTEXT_FILE_BYTES:
            raise ValueError(
                f"File is too large to use as context "
                f"({size} bytes, max {MAX_CONTEXT_FILE_BYTES}): {path}"
            )

        try:
            return candidate.read_text(encoding="utf-8")
        except UnicodeDecodeError as error:
            raise ValueError(
                f"File is not readable as text: {path}"
            ) from error

    def read_context_files(self, paths: list[str]) -> dict[str, str]:
        """
        Like read_context_file, but tolerates individual failures
        (a file moved/deleted since it was dropped) by substituting
        an error note instead of failing the whole request.
        """
        result = {}

        for path in paths:
            try:
                result[path] = self.read_context_file(path)
            except (FileNotFoundError, ValueError) as error:
                result[path] = f"[Could not read this file: {error}]"

        return result

    def _resolve_safe_path(self, relative_path: str) -> Path:
        candidate = (self.project_directory / relative_path).resolve()

        try:
            candidate.relative_to(self.project_directory)
        except Exception:
            raise ValueError("File is outside project directory.")

        return candidate

    def write_generated_file(
        self,
        session_id: str,
        returned_path: str,
        content: str,
    ) -> dict:
        """
        Writes a brand-new file the agent created (no matching
        dropped file to edit in place) into a session-scoped scratch
        folder, so different sessions/participants never collide and
        each session starts empty. Only the filename from
        returned_path is used - the model's own directory guess isn't
        trusted for where a new file should live on disk.
        """
        filename = Path(returned_path).name

        if not filename:
            raise ValueError(f"No filename in path: {returned_path}")

        session_directory = GENERATED_FILES_ROOT / session_id
        session_directory.mkdir(parents=True, exist_ok=True)

        target = session_directory / filename
        already_existed = target.exists()

        target.write_text(content, encoding="utf-8")

        return {
            "status": "modified" if already_existed else "created",
            "path": str(target.resolve()),
        }

    def write_to_known_path(self, path: str, content: str) -> dict:
        """
        Writes back to a file the user already dropped in as context
        this session - path is trusted because it's an exact path we
        read from earlier in this same request flow, not something
        invented by the LLM. The participant's own editor picks up
        the external change automatically, no drag needed.
        """
        target = Path(path)

        if not target.exists():
            raise FileNotFoundError(f"File no longer exists: {path}")

        # This writes straight to the participant's real project file
        # with no chance to review first - a bad generation (garbled
        # content, wrong file matched) overwrites it silently. Keep
        # a backup of whatever was there before every overwrite, so
        # a bad write is always one copy away from being undone even
        # if the file isn't tracked in git.
        backup_path = target.with_suffix(target.suffix + ".emmy-backup")
        backup_path.write_text(
            target.read_text(encoding="utf-8"),
            encoding="utf-8",
        )

        target.write_text(content, encoding="utf-8")

        return {"status": "modified", "path": str(target)}

    def write_file(self, relative_path: str, content: str) -> dict:
        target = self._resolve_safe_path(relative_path)

        if target.suffix != ".cs":
            raise ValueError("Only .cs files can be written by FileTools")

        already_existed = target.exists()

        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_text(content, encoding="utf-8")

        return {
            "status": "modified" if already_existed else "created",
            "path": str(target),
        }