---
name: test
description: Run backend (.NET) xUnit unit and integration tests via the developer CLI.
---

# Test

```bash
dotnet run --project developer-cli -- test [--self-contained-system <name>] [--filter <expr>] [--no-build] [--exclude-category <cat>] --quiet
```

Use `developer-cli` exactly as written - do not expand to an absolute worktree path.

Backend only - there is no frontend test runner.

- `--self-contained-system <name>` - narrows to one SCS (e.g. `account`, `main`)
- `--filter <expr>` - forwarded to `dotnet test --filter` to scope to a subset of tests
- `--no-build` - skip rebuild before running (faster after a recent build)
- `--exclude-category <cat>` - defaults to `Noisy`; pass an empty string to include them

No arguments runs every test across every SCS.

After `build` succeeds, run `format`, `lint`, `test` in parallel with `--no-build`.

## Examples

```bash
dotnet run --project developer-cli -- test --quiet                                                # all tests
dotnet run --project developer-cli -- test --self-contained-system account --quiet                # one SCS
dotnet run --project developer-cli -- test --filter "FullyQualifiedName~LoginTests" --quiet       # filter by name
dotnet run --project developer-cli -- test --no-build --quiet                                     # after a recent build
```

## Always pass --quiet

Verbose output goes to a log file. On success the CLI prints a one-line summary (totals + duration); on failure it lists the failed test names plus the log path - read the log if you need stack traces. Without `--quiet` every test result streams into the conversation.
