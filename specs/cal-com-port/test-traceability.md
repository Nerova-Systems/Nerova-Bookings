# Test Traceability

Source policy: Cal.com is primary; Cal.diy is secondary reference only.

## Status

The previous Cal.diy test traceability matrix is superseded. It must not be used to create implementation issues.

This document now defines the required Cal.com test traceability process. The next planning pass must regenerate the matrix from `cal.com` before coding starts.

## Current Snapshot

- Primary source path: `cal.com`
- Primary source commit: `cf2a55c42363ab79982eef11610e1de8151b45ce`
- Filename test/spec/e2e matches visible through `rg`: 1,080

## Required Mapping

Every Cal.com test file must be mapped to one of:

- `port-equivalent`: implement an equivalent Nerova test with the same behavior expectation.
- `replace-with-nerova-equivalent`: behavior matters, but auth, account, API shape, or WhatsApp replacement changes the test shape.
- `defer`: future Teams, Organizations, routing, workflow, analytics, payment, or enterprise scope.
- `not-applicable`: source test covers behavior not used in Nerova.
- `reference-only`: useful for intent but not directly testable in the target architecture.

## Required Coverage Areas

API tests must cover:

- Endpoint contracts.
- Auth and permissions.
- Validation.
- Idempotency.
- Error shapes.
- Versioned behavior where Cal.com has versioned APIs.

Domain tests must cover:

- Availability and timezone behavior.
- Working hours and date overrides.
- Busy-time merging.
- Slot generation.
- Slot reservations and stale-slot rejection.
- Booking creation and lifecycle transitions.
- Connector failures.
- Background task retries.
- Webhook delivery.
- Audit semantics.

Frontend tests must cover:

- Ported admin UI modules.
- App-store setup flows.
- Connector installed, uninstalled, reconnect, loading, empty, error, and disabled states.
- Responsive desktop and mobile layouts.
- Cal.com atom behavior parity.

WhatsApp tests must cover:

- Meta webhook verification.
- Flow data exchange.
- Version mismatch.
- Duplicate callbacks.
- Invalid connector state.
- Stale slot rejection.
- Booking happy path.
- Reschedule and cancel flows where supported in v1.

E2E tests must cover:

- Solo onboarding through event type setup.
- Connector setup.
- WhatsApp booking.
- Reschedule and cancel.
- Non-Solo access denial.
- Admin booking management.

## Implementation Gate

No implementation issue is valid until it cites the Cal.com test files that prove the intended behavior and states the exact Nerova test type that will own the equivalent coverage.
