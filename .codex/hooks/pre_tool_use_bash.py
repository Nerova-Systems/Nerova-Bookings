from hook_common import bash_command, read_input, write_json


DENY_RULES = [
    ("cd ", "Do not change directory. Use --project flags or relative paths from the repo root."),
    ("dotnet build", "Use the build skill (`dotnet run --project developer-cli -- build --quiet`)."),
    ("dotnet test", "Use the test skill (`dotnet run --project developer-cli -- test --quiet`)."),
    ("dotnet format", "Use the format skill (`dotnet run --project developer-cli -- format --quiet`)."),
    ("npm run format", "Use the format skill (`dotnet run --project developer-cli -- format --quiet`)."),
    ("npm test", "Use the test skill (`dotnet run --project developer-cli -- test --quiet`)."),
    ("npm run build", "Use the build skill (`dotnet run --project developer-cli -- build --quiet`)."),
    ("npx playwright test", "Use the e2e skill (`dotnet run --project developer-cli -- e2e --quiet`)."),
]


payload = read_input()
command = bash_command(payload)

for needle, reason in DENY_RULES:
    if needle in command:
        write_json(
            {
                "hookSpecificOutput": {
                    "hookEventName": "PreToolUse",
                    "permissionDecision": "deny",
                    "permissionDecisionReason": reason,
                }
            }
        )
        raise SystemExit(0)
