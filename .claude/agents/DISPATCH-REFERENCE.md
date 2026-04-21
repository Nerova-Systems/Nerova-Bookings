# DISPATCH-REFERENCE — Read before every task() dispatch

The orchestrator MUST re-read this file before every `task()` call.

## Pre-Dispatch Checklist

- [ ] Read `AGENTS.md` in this session
- [ ] Read the relevant `PLAN.md` section for this task
- [ ] Prompt includes: feature name, task ID, branch, context file paths, task goal (verbatim)
- [ ] Correct agent chosen for the task type (see routing table below)
- [ ] For reviewer dispatches: list all files changed in this session in the prompt
- [ ] No implementation hints added — the engineer is the expert

## Dispatch Template

Copy and fill in all placeholders:

```
task(
  agent_type="[agent-name]",
  prompt="Feature: [feature-name]
Task: [task-id] — [task-title]
Branch: [branch-name]

Read before starting:
- AGENTS.md
- PLAN.md §[section-number] — [section-title]
- .agents-work/[session]/spec.md
- .agents-work/[session]/tasks.yaml

[Task goal — copied verbatim from tasks.yaml]"
)
```

## Context Files Per Agent

| Agent | Must read |
|-------|----------|
| `backend-engineer` | AGENTS.md, PLAN.md §(relevant), spec.md, tasks.yaml, `.claude/rules/backend/*.md` |
| `frontend-engineer` | AGENTS.md, PLAN.md §(relevant), spec.md, tasks.yaml, `.claude/rules/frontend/*.md` |
| `integrations-engineer` | AGENTS.md, PLAN.md §6 (iPaaS), spec.md, tasks.yaml |
| `qa-engineer` | AGENTS.md, spec.md, tasks.yaml, `.claude/rules/end-to-end-tests/end-to-end-tests.md` |
| `backend-reviewer` | AGENTS.md, PLAN.md §10, spec.md, all changed .NET files |
| `frontend-reviewer` | AGENTS.md, PLAN.md §10, spec.md, all changed React/TS files |
| `integrations-reviewer` | AGENTS.md, PLAN.md §6, spec.md, all changed Java files |
| `qa-reviewer` | AGENTS.md, spec.md, all changed test files |

## Agent Routing

| Work type | Engineer | Reviewer |
|-----------|----------|---------|
| .NET backend (Core/Api/Tests/Workers) | `backend-engineer` | `backend-reviewer` |
| React/TypeScript frontend | `frontend-engineer` | `frontend-reviewer` |
| Java/Spring/Camel iPaaS | `integrations-engineer` | `integrations-reviewer` |
| Playwright E2E | `qa-engineer` | `qa-reviewer` |

## Failure Policy

1. If dispatch fails (error / BLOCKED / timeout): retry once with more context.
2. After two failures: enter `ASK_USER` — describe the blocker, do not silently assume the agent's role.
3. Never self-implement when a dispatch fails.
