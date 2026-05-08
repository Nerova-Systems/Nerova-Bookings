from hook_common import (
    EXCUSE_PATTERN,
    EXCUSE_REMINDER,
    read_input,
    transcript_last_assistant_text,
    write_json,
)


payload = read_input()
last_message = payload.get("last_assistant_message")

if not isinstance(last_message, str) or not last_message:
    transcript_path = payload.get("transcript_path")
    last_message = transcript_last_assistant_text(
        transcript_path if isinstance(transcript_path, str) else None
    )

if EXCUSE_PATTERN.search(last_message or ""):
    write_json({"decision": "block", "reason": EXCUSE_REMINDER})
else:
    write_json({})
