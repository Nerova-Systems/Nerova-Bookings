---
name: build
description: Build (compile) the solution via the developer CLI - backend (.NET), frontend (React/TypeScript), and the developer CLI itself.
---

# Build

```bash
dotnet run --project developer-cli -- build [--backend] [--frontend] [--cli] [--self-contained-system <name>] --quiet
```

Use `developer-cli` exactly as written - do not expand to an absolute worktree path.

- `--backend` - .NET
- `--frontend` - React/TypeScript
- `--cli` - the developer CLI itself
- `--self-contained-system <name>` - narrows the backend build to one SCS (e.g. `account`, `main`)

No arguments builds everything.

## Examples

```bash
dotnet run --project developer-cli -- build --quiet                                           # everything
dotnet run --project developer-cli -- build --backend --quiet                                 # all backend
dotnet run --project developer-cli -- build --frontend --quiet                                # frontend
dotnet run --project developer-cli -- build --backend --self-contained-system main --quiet    # one SCS
```

## Always pass --quiet

Verbose output goes to a log file. On success the CLI prints a single line; on failure it prints a short error summary - read the log if you need the full errors. Without `--quiet` the build floods the conversation.
