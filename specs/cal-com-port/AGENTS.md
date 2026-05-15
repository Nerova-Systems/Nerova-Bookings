# Cal.com Port Agent Instructions

This folder is the Codex source of truth for the Cal.com to NerovaBookings port. Codex is the active agent system; do not add Claude-specific agent files or instructions here.

Source policy: Cal.com is primary; Cal.diy is secondary reference only.

Before implementation work:

- Read this folder first.
- Verify the current `cal.com` checkout and update `source-inventory.md` and `test-traceability.md` if it changed.
- Treat `cal.com` as the primary behavior, layout, atom, connector, and test oracle.
- Treat `cal.diy` only as a secondary simplified comparison reference.
- Keep Nerova architecture authoritative for .NET, Postgres, React/Rsbuild, OpenAPI clients, TanStack Query, Lingui, `@repo/ui`, account, subscriptions, feature flags, AppGateway, build, test, and deployment.

Non-negotiable port rules:

- Implement a feature-sliced rewrite. Do not runtime-embed Cal.com or replace existing Nerova systems.
- Strict Cal.com core scheduling parity comes first.
- Solo ships first, with Teams and Organizations seams preserved from Cal.com.
- Solo public booking is WhatsApp Flow only; Cal.com public booking UI is behavior reference, not a public web deliverable.
- Nerova styling primitives remain locked unless a separate approved task changes them.
- Cal.com atoms and admin layout behavior take priority until Nerova reaches parity.
- Future Meta Business management, client portal, smart CSV/Excel import/export, loyalty, appointment ratings, and smart data systems are deferred until after Cal.com parity passes.

Every implementation task must cite:

- Cal.com source paths and tests.
- The owning spec section in this folder.
- Target Nerova backend, frontend, data, API, and test files.
- UI parity requirements when a frontend flow is touched.
- Validation evidence from build, format, lint, tests, and relevant E2E checks.
