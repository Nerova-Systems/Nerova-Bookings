# Cal.diy Port Agent Instructions

This folder is the Codex source of truth for the Cal.diy to NerovaBookings Solo port. This Nerova spec uses AGENTS.md because Codex is the active agent system.

Read these files before touching implementation code:

1. `design.md`
2. `source-inventory.md`
3. `connectors.md`
4. `ui-atom-parity.md`
5. `test-traceability.md`
6. `decisions.md`
7. `implementation.md`

Rules for agents:

- Cal.diy behavior wins unless this spec explicitly replaces it with Nerova architecture, Solo gating, or WhatsApp-only public booking.
- This is a rewrite into .NET, Postgres, React/Rsbuild, generated OpenAPI clients, TanStack Query, Lingui, and `@repo/ui`; do not runtime-embed Cal.diy.
- Public booking pages and embeds are not ported as public web UX. Booking is driven through WhatsApp Business Flow callbacks.
- Cal.com atom behavior and layout take priority for scheduling/admin flows until parity exists.
- Shared styling primitives in `application/shared-webapp/ui` are locked unless the user explicitly approves a separate styling-system change.
- Every implementation task must cite Cal.diy source paths and tests from this folder.
- Every implementation task must include backend/frontend/QA/reviewer/Guardian ownership before it starts.
- After every upstream Cal.diy pull, rerun the inventory and update this folder before coding.
