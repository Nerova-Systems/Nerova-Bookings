from hook_common import bash_command, read_input, write_json


payload = read_input()
command = bash_command(payload)

if "git commit" in command:
    write_json(
        {
            "systemMessage": (
                "CRITICAL: never proactively commit or suggest committing. "
                "Changes must always be reviewed before committing. Only commit "
                "when explicitly instructed to."
            )
        }
    )
