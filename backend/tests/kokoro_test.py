from backend.services.speech_service import SpeechService
import time

service = SpeechService(
    output_directory="outputs/test",
    voice="am_adam",
)

text = "Let's start by making the paddle move left and right."

for i in range(3):
    start = time.perf_counter()

    path = service.generate_speech(text)

    elapsed = time.perf_counter() - start

    print(
        f"Run {i + 1}: "
        f"{elapsed:.2f}s | "
        f"{len(text.split())} words"
    )