# Future Work

Source policy: Cal.com is primary; Cal.diy is secondary reference only.

## Deferred Nerova Product Systems

These systems are intentionally outside the Cal.com port:

- Meta Business management.
- Client management portal.
- Smart CSV/Excel import and export.
- Loyalty system.
- Dynamic per-appointment rating system.
- Broader smart data and business intelligence.

They must not be implemented until the Cal.com port is stable, end-to-end user flows pass, and Cal.com parity tests have target equivalents.

## Deferred Cal.com Product Areas

These Cal.com areas are not in the first Solo delivery unless a shared dependency requires a narrow generic pattern:

- Full public web booker and embeds.
- Payments.
- CRM integrations.
- Analytics integrations.
- Advanced routing forms.
- Full workflow automation.
- Full Organizations UI.
- Full Teams UI.
- Enterprise-only controls.

Team and organization seams should still influence data boundaries so later work does not require a destructive rewrite.

## Pull-Forward Rule

A deferred area can move into scope only when a concrete implementation task states:

- Which included feature depends on it.
- Which exact Cal.com source paths and tests become required.
- Which Nerova target files and tests will own it.
- Which existing v1 scope item is expanded or displaced.
- Which validation gates prove it works.
