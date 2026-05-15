# Cal.diy Port Decisions

## ADR-001: Use specs/cal-diy-port As Source Of Truth

### Context

Cal.diy uses a repo-local spec workflow. Nerova uses Codex as the active agent system.

### Decision

Create a root `specs/cal-diy-port` tree with `AGENTS.md`, design, inventory, traceability, UI parity, connector, Linear, implementation, and decision docs.

### Consequences

Agents read repo-local specs before implementation. Linear references this folder instead of becoming the technical source of truth. Upstream Cal.diy pulls require this folder to be refreshed before coding.

## ADR-002: Linear Comes After Inventory

### Context

Creating Linear projects/tasks before source classification risks stale or missing work.

### Decision

Generate Linear initiative/projects/tasks only after source inventory and traceability are complete.

### Consequences

Linear tasks can include exact Cal.diy source paths and tests. Work is easier to assign to backend/frontend/QA/reviewer/Guardian agents.

## ADR-003: Replace Public Web Booking With WhatsApp Flow

### Context

Cal.diy public booking pages and embeds are large, but the target product books through a fixed WhatsApp Flow bot.

### Decision

Do not expose Solo public booking pages or embeds in v1. Use those source files as behavior references for WhatsApp slot selection, attendee details, confirmation, reschedule, cancellation, and success states.

### Consequences

Backend scheduling and lifecycle behavior remains source-faithful. Frontend public Booker parity is not required. WhatsApp Flow receives dedicated API and E2E tests.

## ADR-004: Shared Styling Primitives Are Locked

### Context

Cal.com atoms must guide behavior/layout, but Nerova owns its styling system.

### Decision

Use local scheduling wrappers and `@repo/ui` composition. Do not modify shared UI primitives unless the user explicitly approves a separate styling-system change.

### Consequences

Frontend tasks need visual parity evidence with accepted deviations. Atom behavior takes priority without destabilizing global Nerova UI.
