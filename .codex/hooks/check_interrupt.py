from pathlib import Path

from hook_common import home_dir, read_input, write_json


payload = read_input()

agent_name = payload.get("agent_type") or payload.get("agent_name")
if not isinstance(agent_name, str) or not agent_name:
    raise SystemExit(0)

teams_dir = home_dir() / ".claude" / "teams"
if not teams_dir.is_dir():
    raise SystemExit(0)

for signals_dir in teams_dir.glob("*/signals"):
    signal_file = signals_dir / f"{agent_name}.signal"
    claimed_file = signal_file.with_name(f"{signal_file.name}.claimed")

    try:
        signal_file.rename(claimed_file)
    except OSError:
        continue

    try:
        message = claimed_file.read_text(encoding="utf-8", errors="replace").strip()
    finally:
        try:
            claimed_file.unlink()
        except OSError:
            pass

    if not message:
        continue

    write_json(
        {
            "decision": "block",
            "reason": (
                f"INTERRUPT: {message}. If you have already processed a message "
                "starting with that ID, continue your current work from there. "
                "Otherwise stop and wait for it."
            ),
        }
    )
    break
