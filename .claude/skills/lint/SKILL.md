---
name: lint
description: Lint code via the developer CLI - backend (.NET via JetBrains inspectcode), frontend (oxlint), and the developer CLI itself.
---

# Lint

```bash
dotnet run --project developer-cli -- lint [--backend] [--frontend] [--cli] [--self-contained-system <name>] [--no-build] --quiet
```

Use `developer-cli` exactly as written - do not expand to an absolute worktree path.

- `--backend` - .NET (JetBrains inspectcode)
- `--frontend` - React/TypeScript (oxlint)
- `--cli` - the developer CLI itself
- `--self-contained-system <name>` - narrows backend linting to one SCS (e.g. `account`, `main`)
- `--no-build` - skip the rebuild step (faster after a recent build)

No arguments lints everything. Every finding fails CI regardless of severity - fix all of them.

After `build` succeeds, run `format`, `lint`, `test` in parallel with `--no-build`. Backend lint is slow - run last. Frontend lint often needs code rewrites - run after each bigger change.

## Examples

```bash
dotnet run --project developer-cli -- lint --quiet                                          # everything
dotnet run --project developer-cli -- lint --backend --quiet                                # all backend
dotnet run --project developer-cli -- lint --frontend --quiet                               # frontend
dotnet run --project developer-cli -- lint --backend --self-contained-system main --quiet   # one SCS
```

## Always pass --quiet

Verbose output goes to a log file. On success the CLI prints a single line; on failure it prints where to find the findings and exits 1.
