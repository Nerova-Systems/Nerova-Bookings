# Nerova Bookings — Agent Contract

Every agent must read this file before starting work.

## Role Boundaries

| Agent | Does | Must Not |
|-------|------|---------|
| `orchestrator` | Manages workflow, dispatches agents, enforces gates | Write code, touch application files |
| `backend-engineer` | Implements .NET 10 code (Core, Api, Tests, Workers) | Touch frontend, Java/Camel, or E2E files |
| `frontend-engineer` | Implements React/TypeScript code | Touch backend, Java/Camel, or E2E files |
| `integrations-engineer` | Implements Java/Spring/Camel iPaaS routes | Touch .NET or React files |
| `qa-engineer` | Writes Playwright E2E tests | Write non-test code |
| `backend-reviewer` | Reviews .NET code | Implement fixes |
| `frontend-reviewer` | Reviews React/TS code | Implement fixes |
| `integrations-reviewer` | Reviews Java/Camel code | Implement fixes |
| `qa-reviewer` | Reviews Playwright tests | Implement fixes |
| `pair-programmer` | Ad-hoc full-access collaboration | — |

## Mandatory Pipeline

Every implementation task must complete this pipeline before being marked done:

```
engineer → reviewer → (qa-engineer → qa-reviewer if behavior changed)
```

Engineers self-delegate to their reviewer after passing mandatory validation. No orchestrator confirmation needed for the handoff.

## Output Format

**Engineers** confirm completion:
```
✅ COMPLETE — [branch] — [what was implemented] — Reviewer approved
```

**Reviewers** return exactly one of:
```
✅ APPROVED — [brief summary of what was reviewed]
❌ CHANGES REQUIRED — [specific issues with file:line references]
```

## Build Rules (engineers only)

Use developer-cli MCP tools — never raw `dotnet`/`npm`:

| Need | MCP call |
|------|---------|
| Build all | `execute_command(command='build', backend=true, frontend=true)` |
| Build backend | `execute_command(command='build', backend=true)` |
| Build frontend | `execute_command(command='build', frontend=true)` |
| Backend tests | `execute_command(command='test', backend=true, noBuild=true)` |
| Format backend | `execute_command(command='format', backend=true, noBuild=true)` |
| Lint frontend | `execute_command(command='lint', frontend=true, noBuild=true)` |
| E2E tests | `end_to_end(searchTerms=["test-name"])` |

> **Java/Camel integrations SCS:** use `mvn verify -q` from `application/integrations/` directly — not yet wired into developer-cli yet.

## Zero-Tolerance Rules

1. No raw external API calls from `main` or `account` SCS for WhatsApp, Google Calendar, or Twilio — these route via iPaaS Camel. **Exception: PayFast is called directly from both `account` Core (subscription billing) and `main` Core (appointment payments) — it is not an iPaaS route.**
2. No WhatsApp, Google Calendar, or Twilio logic in `main` Core directly — use iPaaS.
3. No `SaveChanges()` calls in .NET handlers — `UnitOfWorkPipelineBehavior` handles commits.
4. No hard-coded English strings in JSX — all user-visible text via `t()`.
5. No `page.waitForTimeout()` in Playwright tests.
6. Build warnings are failures — zero tolerance.
7. All code must compile + tests pass + format/lint clean before calling reviewer.

## Security Checklist (high-risk tasks)

Reviewers apply this checklist when the task touches auth, tenant data, payments, or public endpoints:

- [ ] All database queries scoped by `TenantId` (or explicitly bypass with `IgnoreQueryFilters`)
- [ ] Correct authorization policy applied to every endpoint
- [ ] All inputs validated with FluentValidation, sensible max lengths
- [ ] No PII written to logs
- [ ] Webhook payloads verified before processing (signature/ITN check)
- [ ] No secrets in code — env vars and Azure Key Vault only
- [ ] No privilege escalation paths

## Task Status Lifecycle

Per `tasks.yaml`:

```
not-started → in-progress → implemented → completed
```

- `in-progress`: set by engineer when work begins
- `implemented`: set by engineer after build/test/format pass (pre-review)
- `completed`: set by orchestrator after all gates pass

Engineers must NOT set `completed` directly.

## Session Artifacts

All session artifacts live in `.agents-work/YYYY-MM-DD_slug/`:
- `spec.md` — goals, acceptance criteria
- `tasks.yaml` — task list and status
- `status.json` — workflow state, retry counts, user decisions
- `report.md` — completion summary (written at DONE)

## Agent Name Registry

Exact names for `task()` calls:

`orchestrator` | `backend-engineer` | `frontend-engineer` | `integrations-engineer` | `qa-engineer` | `backend-reviewer` | `frontend-reviewer` | `integrations-reviewer` | `qa-reviewer` | `pair-programmer`
