# Cal.com Port Decisions

Source policy: Cal.com is primary; Cal.diy is secondary reference only.

## ADR-001: Use `specs/cal-com-port` As Source Of Truth

### Context

The original planning pack was Cal.diy-framed. The commercial Cal.com repo is now available and approved for commercial use by the user.

### Decision

Rename the spec namespace to `specs/cal-com-port` and make this folder the repo-local source of truth for the Cal.com to NerovaBookings port.

### Consequences

Implementation tasks must cite this folder. Linear is a planning mirror, not the technical source of truth. Any stale previous-namespace references are invalid.

## ADR-002: Cal.com Is Primary, Cal.diy Is Secondary

### Context

Cal.com contains richer scheduling, booking, team, organization, routing, workflow, connector, and test coverage than Cal.diy.

### Decision

Cal.com is the primary behavior, layout, atom, connector, and test oracle. Cal.diy is retained only as a simplified comparison reference.

### Consequences

Inventory and traceability must be regenerated from Cal.com before coding. Cal.diy rows from the prior pack are superseded.

## ADR-003: Feature-Sliced Rewrite, Not Literal Transplant

### Context

Nerova already has account, subscription, feature flag, AppGateway, shared UI, build, and test systems.

### Decision

Port Cal.com behavior feature by feature into Nerova .NET/Postgres/React architecture. Do not runtime-embed Cal.com or replace existing Nerova systems.

### Consequences

The implementation team uses Cal.com for behavior and tests, but Nerova owns module boundaries, APIs, persistence, generated clients, and runtime operations.

## ADR-004: Strict Core Scheduling Parity First

### Context

Scheduling correctness is the foundation for WhatsApp booking, admin setup, connectors, and later team/org product work.

### Decision

The first implementation wave targets strict Cal.com parity for solo-relevant scheduling, availability, slots, reservations, stale-slot rejection, and booking lifecycle behavior.

### Consequences

MVP speed comes from deferring team/org UI and advanced commercial branches, not from weakening the scheduling algorithm.

## ADR-005: Replace Solo Public Web Booking With WhatsApp Flow

### Context

The target Solo product books through a fixed rich WhatsApp Flow bot, not AI and not a public web booker.

### Decision

Do not expose Cal.com public booking pages for Solo. Use their behavior and tests as reference, then implement WhatsApp Flow callbacks against the Nerova scheduling backend.

### Consequences

Public booking UI, embeds, and web booker tests are replaced by WhatsApp Flow tests. Admin scheduling UI remains Cal.com-layout-parity work.

## ADR-006: Future Nerova Business Systems Are Deferred

### Context

Nerova will later include Meta Business management, a client portal, smart CSV/Excel import/export, loyalty, ratings, and smart data systems.

### Decision

Do not implement those systems in the Cal.com port. Preserve clean boundaries so they can attach after Cal.com parity passes.

### Consequences

Booking attendees and scheduling contacts should store parity-required scheduling data now. Final client/customer ownership is a later Nerova domain decision.

## ADR-007: Styling Primitives Are Locked

### Context

Cal.com atoms are important for behavior and layout parity, but Nerova owns its shared UI system.

### Decision

Use Cal.com atom behavior and layout as the UX oracle while preserving Nerova `@repo/ui` styling primitives unless a separate approved task changes them.

### Consequences

Frontend tasks must document accepted deviations and include desktop/mobile parity evidence for touched flows.
