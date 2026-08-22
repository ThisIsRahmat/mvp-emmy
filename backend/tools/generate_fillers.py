"""
One-off utility to (re)generate filler audio clips played while the
agent is "Thinking" - covers the same voices as the pre-recorded
greetings. Run manually with: python -m backend.tools.generate_fillers
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

FILLERS = {
    "filler_1": "Let me dig into that.",
    "filler_2": "Just having a think.",
    "filler_3": "Almost there.",
    "filler_4": "Give me a second.",
    "filler_5": "Okay, let's see.",
    "filler_6": "One moment.",
}

OUTPUT_ROOT = Path("Assets/Resources/Fillers")


def main() -> None:
    for voice in VOICES:
        voice_directory = OUTPUT_ROOT / voice
        voice_directory.mkdir(parents=True, exist_ok=True)

        speech_service = SpeechService(
            voice=voice,
            output_directory=voice_directory,
        )

        for slug, phrase in FILLERS.items():
            print(f"Generating {voice}/{slug}: {phrase!r}")

            generated_path = speech_service.generate_speech(phrase)
            target_path = voice_directory / f"{slug}.wav"

            generated_path.rename(target_path)

        # generate_speech creates a dated subfolder we don't want here
        for leftover in voice_directory.glob("2*"):
            if leftover.is_dir():
                leftover.rmdir()

    print("Done.")


if __name__ == "__main__":
    main()
