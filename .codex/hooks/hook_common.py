import json
import os
import re
import sys
from pathlib import Path
from typing import Any


EXCUSE_PATTERN = re.compile(r"pre-?existing", re.IGNORECASE)

EXCUSE_REMINDER = (
    "REMINDER: that phrase is not an accepted excuse.\n\n"
    "Main is always clean -- CI enforces this. Any failure on the branch was "
    "introduced by us and must be fixed before any approval, handoff, or commit. "
    "The Boy Scout Rule applies: leave the code in a better state than you found it.\n\n"
    "Pull the andon cord: stop and fix the failure. If it is outside your scope "
    "and you are in multi-agent mode, escalate to the team lead. Never approve, "
    "hand off, or commit with known failures."
)


def read_input() -> dict[str, Any]:
    raw = sys.stdin.read()
    if not raw.strip():
        return {}

    try:
        value = json.loads(raw)
    except json.JSONDecodeError:
        return {}

    return value if isinstance(value, dict) else {}


def write_json(value: dict[str, Any]) -> None:
    sys.stdout.write(json.dumps(value, separators=(",", ":")))


def bash_command(payload: dict[str, Any]) -> str:
    tool_input = payload.get("tool_input")
    if isinstance(tool_input, dict):
        command = tool_input.get("command")
        if isinstance(command, str):
            return command

    command = payload.get("command")
    return command if isinstance(command, str) else ""


def strings(value: Any):
    if isinstance(value, str):
        yield value
    elif isinstance(value, dict):
        for nested in value.values():
            yield from strings(nested)
    elif isinstance(value, list):
        for nested in value:
            yield from strings(nested)


def contains_excuse(value: Any) -> bool:
    return any(EXCUSE_PATTERN.search(item) for item in strings(value))


def transcript_last_assistant_text(path: str | None) -> str:
    if not path:
        return ""

    transcript = Path(path)
    if not transcript.is_file():
        return ""

    try:
        lines = transcript.read_text(encoding="utf-8", errors="replace").splitlines()
    except OSError:
        return ""

    for line in reversed(lines[-200:]):
        try:
            event = json.loads(line)
        except json.JSONDecodeError:
            continue

        if event.get("type") != "assistant":
            continue

        message = event.get("message")
        if not isinstance(message, dict):
            continue

        chunks = []
        for item in message.get("content", []):
            if isinstance(item, dict):
                text = item.get("text") or item.get("thinking")
                if isinstance(text, str):
                    chunks.append(text)

        if chunks:
            return " ".join(chunks)

    return ""


def home_dir() -> Path:
    return Path(os.path.expanduser("~"))
