from pathlib import Path

# Every participant's project lives in a different place, so dropped
# context files are read directly from wherever they are on disk -
# this just guards against accidentally loading something huge into
# the LLM's context window.
MAX_CONTEXT_FILE_BYTES = 200_000


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