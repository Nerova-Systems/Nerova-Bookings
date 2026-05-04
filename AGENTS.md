## Build, Test, and Format

Use the developer CLI skills (`build`, `test`, `format`, `lint`, `e2e`, `aspire-restart`, `team-interrupt`) for all code workflows. They invoke `dotnet run --project developer-cli -- <command>` directly. Never run `dotnet`, `npm`, or `npx` directly - the pre-tool-use Bash hook blocks them.

Run `build` first, then `format`, `lint`, `test` in parallel with `--no-build`.

**Slow:** Aspire restart, backend format, backend lint, end-to-end tests. **Fast:** frontend format/lint, backend test.

**Aspire**: The `aspire-restart` skill manages the AppHost - always use it; never `aspire run`, `aspire restart`, or the developer CLI's `run` command. Use the Aspire MCP `list_resources` tool to look up service URLs (or read `.workspace/port.txt` if you only need the base port). In the agentic workflow, only the Guardian agent restarts Aspire. All other agents must notify the Guardian if they need it restarted.

Never commit, amend, or revert without explicit user instruction each time. Commit messages: one descriptive line in imperative form, no description body.

## Product Management Tool

Whenever you see `[PRODUCT_MANAGEMENT_TOOL]`, replace it with the configured value.

```
PRODUCT_MANAGEMENT_TOOL="Linear"
```

When working with [features] or [tasks], read `.claude/reference/product-management/[PRODUCT_MANAGEMENT_TOOL].md` to learn how to look them up, how to update status, and how generic statuses like [Active], [Review], [Completed] map to the tool. Read the [feature] for full context and the [task] for specific requirements.

## Auto Memory

Never write to or edit any auto memory files (MEMORY.md or any file in a memory directory). These files are managed by the user only.

## Source of Truth

Always verify paths, names, and API routes against the actual codebase. Never rely on memory, cached context, or prior session knowledge for these. Always look them up. Only read files within the git repository unless explicitly asked to look elsewhere.

## Project Structure

This is a mono repository with multiple self-contained systems (SCS), each being a small monolith. All SCSs follow the same structure.

- [application](/application): Contains application code, one folder per SCS, plus shared-kernel and shared-webapp.
- [cloud-infrastructure](/cloud-infrastructure): Bash and Azure Bicep scripts (IaC).
- [developer-cli](/developer-cli): A .NET CLI tool for automating common developer tasks.

## Codex Project Configuration

Repo-local Codex configuration lives in `.codex/config.toml`. Codex hooks live in `.codex/hooks.json` and `.codex/hooks/`. Codex command execution policies live in `.codex/rules/*.rules`.

Custom Codex subagents are configured as `.codex/agents/*.toml`. The copied `.codex/agents/*.md` files are source/reference material only. Do not treat `team-lead.md` or `pair-programmer.md` as spawnable Codex subagents because their own instructions say they are top-level agents and must never be spawned as subagents.

The copied Claude rule docs under `.codex/rules/**/*.md` are reference guidance, not execution-policy `.rules` files. Before working in an area, read the relevant reference docs:

- Backend: `.codex/rules/backend/backend.md`
- Backend commands: `.codex/rules/backend/commands.md`
- Backend queries: `.codex/rules/backend/queries.md`
- Backend repositories: `.codex/rules/backend/repositories.md`
- Database migrations: `.codex/rules/backend/database-migrations.md`
- API endpoints: `.codex/rules/backend/api-endpoints.md`
- API tests: `.codex/rules/backend/api-tests.md`
- Frontend: `.codex/rules/frontend/frontend.md`
- Forms: `.codex/rules/frontend/form-with-validation.md`
- TanStack Query: `.codex/rules/frontend/tanstack-query-api-integration.md`
- Translations: `.codex/rules/frontend/translations.md`
- E2E tests: `.codex/rules/end-to-end-tests/end-to-end-tests.md`
- Infrastructure: `.codex/rules/infrastructure/infrastructure.md`
- Developer CLI: `.codex/rules/developer-cli/developer-cli.md`
