# Spec — PayFast Account Subscriptions

## Goal
Replace the Stripe-based SaaS subscription billing in the `account` SCS with a PayFast-based implementation. This covers how Nerova Bookings tenants pay for the platform (not how clients pay for appointments — that is Phase 5 of PLAN.md).

## Subscription Plans (redesigned)

| Plan     | Price     | Notes                        |
|----------|-----------|------------------------------|
| Trial    | Free      | 30 days from account creation |
| Starter  | R250/month | PayFast recurring             |
| Standard | R350/month | PayFast recurring             |
| Premium  | Coming Soon | Disabled — UI shows badge    |

Currency: ZAR (South African Rand). PayFast handles international card conversion for UK/US/AU buyers — merchant receives ZAR.

## Acceptance Criteria

1. **Trial**: New tenant account automatically gets a 30-day trial. No payment required.
2. **Upgrade**: Tenant can select Starter or Standard → redirected to PayFast sandbox → on successful payment, subscription status becomes `Active`.
3. **Recurring billing**: PayFast ITN `COMPLETE` notification updates `CurrentPeriodEnd` and adds a `PaymentTransaction` record.
4. **Payment failure**: PayFast ITN `FAILED` notification sets `Status = PastDue`, records `FirstPaymentFailedAt`.
5. **Cancel**: Tenant can cancel subscription via UI → PayFast recurring billing is stopped → `CancelAtPeriodEnd = true`.
6. **Trial expiry**: When trial period ends and no paid plan is active, status becomes `Expired`. Frontend shows upgrade gate.
7. **Premium**: Visible on plan selection page with "Coming Soon" badge. Not selectable.
8. **AppHost**: stripe-cli container removed. PayFast merchant credentials (ID, key, passphrase, sandbox flag) replace Stripe parameters.
9. **Frontend**: All `@stripe/react-stripe-js` and `@stripe/stripe-js` dependencies removed. No embedded card UI — redirect to PayFast only.
10. **Build & tests**: Backend builds clean, all xUnit tests pass, no Stripe references remaining.

## Out of Scope

- Phase 5 booking payments (client pays for appointment) — separate session
- Proration / mid-cycle plan switching (PayFast does not support this)
- Saved payment methods UI (PayFast handles card capture on their side)
- Invoice PDF generation
- Tax management
- Multi-currency pricing display (all prices in ZAR)
- `UpdateBillingInfo` / billing address form (no PayFast equivalent for saved billing details)

## Architectural Decision

PayFast ITN webhook is verified directly in the `account` SCS (pragmatic exception to the iPaaS rule). A TODO is added in the handler file to migrate to `application/integrations/` in Phase 1. This matches how Stripe webhooks were handled in the boilerplate.
