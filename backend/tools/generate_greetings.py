"""
One-off utility to (re)generate the startup greeting clip for each
voice. Run manually with: python -m backend.tools.generate_greetings
"""

from pathlib import Path

from backend.services.speech_service import SpeechService

VOICES = [
    "en_GB-alan-medium",
    "en_US-ryan-medium",
    "en_US-joe-medium",
    "en_US-danny-low",
    "en_GB-northern_english_male-medium",
]

GREETING_TEXT = "Hey, I'm Emmy. What are we building today?"

OUTPUT_DIRECTORY = Path("Assets/Resources/Greetings")


def main() -> None:
    OUTPUT_DIRECTORY.mkdir(parents=True, exist_ok=True)

    for voice in VOICES:
        print(f"Generating greeting_{voice}.wav")

        speech_service = SpeechService(
            voice=voice,
            output_directory=OUTPUT_DIRECTORY,
        )

        generated_path = speech_service.generate_speech(GREETING_TEXT)
        target_path = OUTPUT_DIRECTORY / f"greeting_{voice}.wav"

        generated_path.rename(target_path)

    for leftover in OUTPUT_DIRECTORY.glob("2*"):
        if leftover.is_dir():
            leftover.rmdir()

    print("Done.")


if __name__ == "__main__":
    main()
