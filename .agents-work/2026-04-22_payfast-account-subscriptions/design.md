# Design — PayFast Account Subscriptions (Type 2 Tokenization)

## Architecture Decision

**subscription_type=2 (Tokenization)** is used for all SaaS billing. PayFast stores the card token;
Nerova Bookings owns every charge, retry, proration, and scheduling decision. This enables full
feature parity with the Stripe billing engine (proration, scheduled downgrades, payment retry, etc.)
and serves as the billing boilerplate for all future SaaS products on this platform.

## Plans

| Plan    | Price (ZAR/month) | Status        |
|---------|-------------------|---------------|
| Trial   | R0 (30 days)      | Active        |
| Starter | R250              | Active        |
| Standard| R350              | Active        |
| Premium | TBD               | Coming soon   |

## Domain Model Changes (Subscription aggregate)

### Removed fields (Stripe)
- `StripeCustomerId`
- `StripeSubscriptionId`
- `BillingInfo` — PayFast doesn't expose billing address
- `PaymentMethod` — PayFast stores card data, not us

### New / replaced fields
```csharp
public string? PayFastToken { get; private set; }        // recurring token from ITN
public string? PayFastPaymentId { get; private set; }   // last pf_payment_id (idempotency)
public SubscriptionStatus Status { get; private set; }
public SubscriptionPlan Plan { get; private set; }
public SubscriptionPlan? ScheduledPlan { get; private set; }  // pending downgrade
public DateTimeOffset TrialEndsAt { get; private set; }
public DateTimeOffset? NextBillingDate { get; private set; }
public DateTimeOffset? CurrentPeriodStart { get; private set; }
public DateTimeOffset? CurrentPeriodEnd { get; private set; }
public DateTimeOffset? FirstPaymentFailedAt { get; private set; }
public DateTimeOffset? CancelledAt { get; private set; }
```

### New enums
```csharp
public enum SubscriptionPlan   { Trial = 0, Starter = 1, Standard = 2, Premium = 3 }
public enum SubscriptionStatus { Trial = 0, Active = 1, PastDue = 2, Cancelled = 3, Expired = 4 }
```

### Subscription lifecycle
```
[New tenant created] → Status=Trial, TrialEndsAt=UtcNow+30d
[User upgrades]      → InitiateSubscription → PayFast Onsite → ITN COMPLETE
                       → Status=Active, NextBillingDate=UtcNow+30d
[BillingJob fires]   → Charge via tokenization API → ITN COMPLETE → extend NextBillingDate
[Charge fails]       → Status=PastDue, FirstPaymentFailedAt=now
[RetryJob fires]     → Retry charge (D+3, D+7) → on success → Status=Active
[Grace period ends]  → ExpireJob → Status=Expired
[User cancels]       → CancelSubscription → PayFast cancel API → Status=Cancelled
[User reactivates]   → InitiateSubscription (new token) → Status=Active
```

## API Contract (account SCS)

```
GET  /api/account/subscriptions/current
     → { plan, status, trialEndsAt, currentPeriodEnd, nextBillingDate,
         cancelledAt, scheduledPlan }

GET  /api/account/subscriptions/plans
     → [ { id, name, priceZar, currency, interval, comingSoon } ]

GET  /api/account/subscriptions/upgrade-preview?targetPlan={plan}
     → { proratedChargeZar, currentPlanName, targetPlanName, effectiveDate }

GET  /api/account/subscriptions/update-card-url
     → { updateCardUrl }  ← https://www.payfast.co.za/eng/recurring/update/{token}?return=...

POST /api/account/subscriptions/initiate
     body: { planId: "Starter" | "Standard" }
     → { uuid }  ← frontend passes to payfast_do_onsite_payment()

POST /api/account/subscriptions/upgrade
     body: { targetPlan }
     → 200 (charges proration immediately via tokenization API)

POST /api/account/subscriptions/schedule-downgrade
     body: { targetPlan }
     → 200 (stored; applied on next billing date by worker)

POST /api/account/subscriptions/cancel-scheduled-downgrade
     → 200 (clears ScheduledPlan)

POST /api/account/subscriptions/cancel
     body: { reason, feedback }
     → 200

POST /api/account/subscriptions/reactivate
     body: { planId }
     → { uuid }  ← new tokenization checkout

POST /api/account/subscriptions/retry-payment
     → 200 (manual retry of failed charge)

POST /api/account/subscriptions/payfast/itn   ← no auth, PayFast IP + MD5 verified
     body: PayFast ITN fields (application/x-www-form-urlencoded)
     → 200 / 400
```

## PayFast Integration Details

### Onsite Payment (subscription_type=2)

**Backend — Initiate endpoint:**
1. Build param dict: `merchant_id`, `merchant_key`, `amount` (plan price or 0.00 if no charge needed),
   `item_name`, `subscription_type=2`, `m_payment_id` (new ULID), `notify_url`, `return_url`, `cancel_url`
2. Generate MD5 signature (sorted key=value pairs + passphrase appended)
3. POST to `https://www.payfast.co.za/onsite/process` (or sandbox)
4. Return `{ uuid }` to frontend

**Frontend — trigger lightbox:**
```html
<script src="https://www.payfast.co.za/onsite/engine.js"></script>
```
```js
window.payfast_do_onsite_payment({ uuid }, (result) => {
  if (result === true) {
    // Poll GET /api/account/subscriptions/current until Status=Active
  } else {
    // User cancelled — show plan cards again
  }
});
```

### ITN Processing (HandlePayFastItn)
1. Verify MD5 signature (sort params excluding `signature`, hash with passphrase appended)
2. Verify PayFast IP (sandbox: `197.97.145.144`; live: PayFast IP range from config)
3. Skip if `pf_payment_id == subscription.PayFastPaymentId` (idempotency)
4. Check `payment_status`:
   - `COMPLETE` → set `Status=Active`, capture `PayFastToken` (if present in payload),
     set `NextBillingDate=UtcNow+30d`, `CurrentPeriodStart=UtcNow`, `CurrentPeriodEnd=UtcNow+30d`,
     append `PaymentTransaction`, clear `FirstPaymentFailedAt`
   - `FAILED` → set `Status=PastDue`, set `FirstPaymentFailedAt`
   - `CANCELLED` → set `Status=Cancelled`, clear token fields
5. Save `PayFastPaymentId` for idempotency
6. Return 200 — PayFast retries on non-200

### Tokenization Charge API (used by BillingJob and UpgradeSubscription)
```
POST https://api.payfast.co.za/subscriptions/{token}/charge
Headers:
  merchant-id: {merchantId}
  version: v1
  timestamp: {ISO8601}
  signature: MD5(merchant-id + passphrase + timestamp)  ← sorted key=value
Body (JSON):
  { "amount": 250.00, "item_name": "Nerova Bookings Starter - Monthly" }
```
ITN callback confirms success. On 4xx response → treat as failed.

### Cancel API
```
PUT https://api.payfast.co.za/subscriptions/{token}/cancel
Headers: merchant-id, version, timestamp, signature
```

### Update Card URL
```
GET https://www.payfast.co.za/eng/recurring/update/{token}?return={returnUrl}
```
Backend provides this URL. Frontend redirects to it (new tab or full redirect).
Return URL = `/account/billing`.

### Proration Calculation (UpgradeSubscription)
```csharp
var daysRemaining = (CurrentPeriodEnd.Value - DateTimeOffset.UtcNow).Days;
var daysInCycle = (CurrentPeriodEnd.Value - CurrentPeriodStart.Value).Days;
var ratio = (decimal)daysRemaining / daysInCycle;
var upgradeCharge = (targetPlanPrice - currentPlanPrice) * ratio;
```
Minimum charge: R5.00 (PayFast minimum). Round to 2 decimals.

## Worker Jobs (account/Workers)

### BillingJob (daily, 02:00 UTC)
1. Find all `Status=Active` subscriptions where `NextBillingDate <= UtcNow`
2. Apply `ScheduledPlan` if set (change plan, clear `ScheduledPlan`)
3. Call PayFast charge API with current plan price
4. On API error: set `Status=PastDue`, set `FirstPaymentFailedAt`
5. On success: extend `NextBillingDate += 30 days`
6. ITN confirms actual success

### RetryJob (daily, 03:00 UTC)
1. Find `Status=PastDue` subscriptions
2. Retry on D+3 and D+7 since `FirstPaymentFailedAt`
3. On second retry failure: leave as PastDue (ExpireJob handles expiry)

### ExpireJob (daily, 04:00 UTC)
1. Find `Status=PastDue` where `FirstPaymentFailedAt < UtcNow - 14 days`
2. Set `Status=Expired`, call PayFast cancel API to remove token

### TrialExpiryNotificationJob (daily, 08:00 UTC)
1. Find `Status=Trial` where `TrialEndsAt` is 7, 3, or 1 day away
2. Send "Your trial ends in X days" email

## Frontend Changes

### Delete entirely
- `routes/account/billing/-components/` (all existing components)
- `routes/account/billing/subscription/index.tsx`
- All Stripe npm packages from `account/WebApp/package.json`

### Main billing page (rewrite `routes/account/billing/index.tsx`)
State matrix:
- `Status=Trial`: trial countdown banner + plan cards + "Start Subscription" button
- `Status=Active`: `CurrentPlanSection` + "Upgrade"/"Schedule Downgrade" buttons + "Update Card" + "Cancel" + `BillingHistoryTable`
- `Status=PastDue`: warning banner + "Retry Payment" button + "Update Card" button
- `Status=Expired`: hard gate + plan cards
- `Status=Cancelled`: reactivation prompt + plan cards

### New components (rebuild with PayFast)
- `CheckoutDialog` — loads `payfast_do_onsite_payment()`, shows spinner during UUID fetch, polls on success
- `UpdatePaymentMethodDialog` — shows redirect warning, opens `/eng/recurring/update/{token}`
- `UpgradeConfirmationDialog` — shows proration preview from `GET /upgrade-preview`, confirm charges
- `ScheduleDowngradeDialog` — confirm scheduling downgrade at period end
- `CancelDowngradeDialog` — confirm cancelling scheduled downgrade
- `RetryPaymentDialog` — confirm retry attempt
- `CancelSubscriptionDialog` — collect reason + feedback, confirm cancel
- `ReactivateDialog` — starts new `CheckoutDialog` flow
- `BillingHistoryTable` — paginated table of `PaymentTransaction` records
- `CurrentPlanSection` — plan name, price, next billing date, period
- `SubscriptionBanner` — status-aware banners (trial/past-due/expired/cancelled)
- `PlanCard` + `PlanCardGrid` — plan display, "coming soon" for Premium
- `BillingTabNavigation` — tab nav for billing sections

### No `/return` route needed
PayFast lightbox uses JS callback — no redirect return page. The callback polls `GET /subscriptions/current`.

## Migration Strategy
PLAN.md §1: "No live tenants yet. Migrations can be drop-and-rebuild without backfill."
- Delete existing subscription migrations
- Recreate with new schema

## Files to Delete (backend)
```
Core/Features/Subscriptions/Domain/StripeEvent.cs
Core/Features/Subscriptions/Domain/StripeEventConfiguration.cs
Core/Features/Subscriptions/Domain/StripeEventRepository.cs
Core/Features/Subscriptions/Shared/ProcessPendingStripeEvents.cs
Core/Features/Subscriptions/Commands/AcknowledgeStripeWebhook.cs
Core/Features/Subscriptions/Commands/StartSubscriptionCheckout.cs
Core/Features/Subscriptions/Commands/ProcessPendingEvents.cs
Core/Features/Billing/Commands/StartPaymentMethodSetup.cs
Core/Features/Billing/Commands/ConfirmPaymentMethodSetup.cs
Core/Features/Billing/Commands/RetryPendingInvoicePayment.cs   ← replaced by RetryFailedCharge
Core/Features/Billing/Commands/UpdateBillingInfo.cs            ← not needed (PayFast handles)
Core/Features/Subscriptions/Queries/GetCheckoutPreview.cs
```

## Retained / Rewritten (backend)
```
Core/Features/Subscriptions/Domain/Subscription.cs             → new PayFast fields
Core/Features/Subscriptions/Domain/SubscriptionTypes.cs        → new enums
Core/Features/Subscriptions/Queries/GetCurrentSubscription.cs  → updated projection
Core/Features/Subscriptions/Queries/GetPricingCatalog.cs       → hardcoded plan definitions
Core/Features/Subscriptions/Queries/GetUpgradePreview.cs       → proration calculation (rewritten)
Core/Features/Billing/Queries/GetPaymentHistory.cs             → updated for new PaymentTransaction model
Core/Features/Subscriptions/Commands/CancelSubscription.cs     → calls PayFast cancel API
Core/Features/Subscriptions/Commands/ReactivateSubscription.cs → new tokenization checkout
```

## New files (backend)
```
Core/Features/Subscriptions/Commands/InitiateSubscription.cs   → POST onsite/process → {uuid}
Core/Features/Subscriptions/Commands/HandlePayFastItn.cs       → ITN verification + processing
Core/Features/Subscriptions/Commands/UpgradeSubscription.cs    → proration + charge API (rewrite)
Core/Features/Subscriptions/Commands/ScheduleDowngrade.cs      → store ScheduledPlan (rewrite)
Core/Features/Subscriptions/Commands/CancelScheduledDowngrade.cs → clear ScheduledPlan (rewrite)
Core/Features/Subscriptions/Commands/RetryFailedCharge.cs      → charge API retry
Core/Features/Subscriptions/Queries/GetUpdateCardUrl.cs        → build update card URL
Core/Features/Subscriptions/Infrastructure/PayFastClient.cs    → HTTP client for PayFast API
Core/Features/Subscriptions/Infrastructure/PayFastSignature.cs → MD5 signature generation
Workers/Jobs/BillingJob.cs
Workers/Jobs/RetryJob.cs
Workers/Jobs/ExpireJob.cs
Workers/Jobs/TrialExpiryNotificationJob.cs
```

## AppHost Changes
- Remove `ConfigureStripeParameters()` and all Stripe env vars on `accountApi`
- Remove `AddStripeCliContainer()`
- Add `ConfigurePayFastParameters()`:
  - `PayFast__MerchantId`
  - `PayFast__MerchantKey`
  - `PayFast__Passphrase`
  - `PayFast__Sandbox` (true in dev/staging)
  - `PayFast__NotifyUrl` (ngrok URL in dev, real URL in prod)
  - `PayFast__ReturnUrl`
  - `PayFast__CancelUrl`
  - `PayFast__AllowedIps` (comma-separated PayFast IP range)

## Security Notes
- ITN endpoint: no authentication, rely solely on IP verification + MD5 signature
- PayFast credentials stored as Aspire secrets, never in code
- `PayFastClient` uses `IOptions<PayFastSettings>` — no hardcoded values
- All signature generation done server-side only
- Amount on charge API must match plan price — validate before calling
