# Source Inventory

Source policy: Cal.com is primary; Cal.diy is secondary reference only.

## Status

The previous Cal.diy inventory is superseded. It must not be used to create implementation issues.

This document now defines the required Cal.com inventory process. The next planning pass must regenerate the tables from `cal.com` before coding starts.

## Current Snapshot

- Primary source path: `cal.com`
- Primary source commit: `cf2a55c42363ab79982eef11610e1de8151b45ce`
- Primary source branch: `fix/team-attributes-for-org-admins`
- Secondary reference path: `cal.diy`
- Secondary reference commit: `180ede28f0bddf2738933a6e60a8e80f6116d7da`
- Secondary reference branch: `main`
- Cal.com files visible through `rg`: 10,070
- Cal.com filename test/spec/e2e matches visible through `rg`: 1,080

## Required Inventory Tables

Regenerate this document with Cal.com as primary source and classify every source area as `include`, `adapt`, `replace`, `defer`, `reference`, or `not-applicable`.

Required areas:

- `cal.com/apps/api/v2`
- `cal.com/apps/web`
- `cal.com/apps/docs`
- `cal.com/packages/app-store`
- `cal.com/packages/app-store-cli`
- `cal.com/packages/features`
- `cal.com/packages/platform`
- `cal.com/packages/ui`
- `cal.com/packages/lib`
- `cal.com/packages/dayjs`
- `cal.com/packages/emails`
- `cal.com/packages/sms`
- `cal.com/packages/embeds`
- `cal.com/packages/trpc`
- `cal.com/packages/prisma`
- `cal.com/packages/testing`
- `cal.com/packages/types`
- `cal.com/packages/i18n`
- `cal.com/packages/config`

## Classification Rules

- `include`: required for Solo Cal.com parity.
- `adapt`: behavior matters, but Nerova architecture owns the runtime shape.
- `replace`: behavior reference only because Nerova intentionally replaces the surface.
- `defer`: not required for Solo but must be preserved as a later seam.
- `reference`: documentation, examples, or implementation intent only.
- `not-applicable`: irrelevant to Nerova and not needed as a shared dependency.

## Known High-Priority Areas

- Scheduling core: include.
- Event types and availability: include.
- Slots and reservations: include.
- Booking lifecycle: include.
- Calendar connectors: include for selected workforce providers.
- Conferencing connectors: include for Google Meet, Microsoft Teams, and Zoom.
- App-store platform: adapt.
- Public web booking: replace with WhatsApp Flow for Solo.
- Embeds: replace or defer unless needed for atom parity reference.
- Teams and Organizations: defer UI and full product surface, preserve data and service seams.
- Routing forms, workflows, attributes, PBAC, and insights: defer unless a core dependency requires a narrow adapted pattern.

## Delta Appendix Requirement

After the Cal.com inventory is complete, add a Cal.com vs Cal.diy appendix that records:

- Cal.com-only areas that affect future Teams and Organizations.
- Cal.com-only scheduling and booking tests.
- Cal.diy simplifications that are useful for understanding smaller behavior paths.
- Any source divergence that changes implementation priority.

## Implementation Gate

If a task discovers an unlisted Cal.com source area, stop and update this inventory before coding.
