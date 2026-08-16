from pathlib import Path


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