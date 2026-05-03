# Revert PayFast To Stripe Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Restore account subscription billing from the PayFast implementation back to the Stripe implementation, one-to-one with the Stripe source currently available at commit `5b7d84f021e287789e4dd378d719627b2ff3d148`.

**Architecture:** Treat `5b7d84f021e287789e4dd378d719627b2ff3d148` as the Stripe source of truth for account billing. Restore the Stripe-owned backend, domain, integration, test, and account billing UI files from that commit, then remove PayFast-only files and references added on `HEAD` (`4fd99fbbb89e80661d45c1ecfecfbb3cd391c8bb`). Preserve unrelated merge work such as Gravatar/auth/session changes, feature flags, teams, and main-app appointment Paystack flows unless the user explicitly broadens the scope.

**Tech Stack:** .NET 10, ASP.NET Core endpoints, EF Core/PostgreSQL migrations, MediatR command/query handlers, Stripe SDK/client abstraction, React/TanStack Router, Lingui catalogs, developer CLI build/format/lint/test wrappers.

---

## Scope Boundaries

- In scope: account subscription billing under `application/account/**`, including Stripe checkout, billing details, subscription lifecycle, Stripe webhooks, Stripe tests, account billing UI, account billing legal/copy, account test setup, and account config.
- Out of scope by default: main app appointment deposits and Paystack work under `application/main/**`. Those are separate appointment-payment features, not the account subscription PayFast replacement.
- Do not commit during execution unless the user explicitly asks. The repo instruction forbids commits without explicit instruction.

## Source Inventory

Use these constants during execution:

```powershell
$StripeSource = "5b7d84f021e287789e4dd378d719627b2ff3d148"
$PayFastHead = "4fd99fbbb89e80661d45c1ecfecfbb3cd391c8bb"
```

Primary Stripe files present in `$StripeSource`:

- `application/account/Api/Endpoints/StripeWebhookEndpoints.cs`
- `application/account/Core/Integrations/Stripe/IStripeClient.cs`
- `application/account/Core/Integrations/Stripe/StripeClient.cs`
- `application/account/Core/Integrations/Stripe/MockStripeClient.cs`
- `application/account/Core/Integrations/Stripe/StripeClientFactory.cs`
- `application/account/Core/Integrations/Stripe/UnconfiguredStripeClient.cs`
- `application/account/Core/Features/Subscriptions/Commands/AcknowledgeStripeWebhook.cs`
- `application/account/Core/Features/Subscriptions/Commands/ProcessPendingEvents.cs`
- `application/account/Core/Features/Subscriptions/Commands/StartSubscriptionCheckout.cs`
- `application/account/Core/Features/Subscriptions/Shared/ProcessPendingStripeEvents.cs`
- `application/account/Core/Features/Subscriptions/Domain/StripeEvent.cs`
- `application/account/Core/Features/Subscriptions/Domain/StripeEventConfiguration.cs`
- `application/account/Core/Features/Subscriptions/Domain/StripeEventRepository.cs`
- `application/account/WebApp/routes/account/billing/-components/CheckoutDialog.tsx`
- `application/account/WebApp/routes/account/billing/-components/CheckoutForm.tsx`
- `application/account/WebApp/routes/account/billing/-components/stripeAppearance.ts`

Primary PayFast files to remove:

- `application/account/Core/Integrations/PayFast/IPayFastClient.cs`
- `application/account/Core/Integrations/PayFast/PayFastClient.cs`
- `application/account/Core/Integrations/PayFast/PayFastSettings.cs`
- `application/account/Core/Integrations/PayFast/PayFastSignature.cs`
- `application/account/Core/Features/Subscriptions/Commands/HandlePayFastItn.cs`
- `application/account/Core/Features/Subscriptions/Commands/InitiateSubscription.cs`
- `application/account/Core/Features/Subscriptions/Commands/RetryFailedCharge.cs`
- `application/account/Core/Features/Subscriptions/Domain/PayFastItnEvent.cs`
- `application/account/Core/Features/Subscriptions/Jobs/BillingJob.cs`
- `application/account/Tests/Integrations/PayFast/PayFastRefundTests.cs`
- `application/account/Tests/Subscriptions/HandlePayFastItnTests.cs`
- `application/account/Tests/Subscriptions/InitiateSubscriptionTests.cs`
- `application/account/Tests/Subscriptions/RetryFailedChargeTests.cs`

### Task 1: Preflight Snapshot

**Files:**
- Read: git index and current merge metadata
- No source edits

- [ ] **Step 1: Confirm merge conflicts are resolved**

Run:

```powershell
git diff --name-only --diff-filter=U
```

Expected: no output.

- [ ] **Step 2: Capture current provider references**

Run:

```powershell
git grep -n -e PayFast -e payfast -e Paystack -e paystack -- application/account application/main application/shared-webapp
```

Expected: PayFast references exist mainly under `application/account/**`; Paystack references exist under `application/main/**` and remain out of scope unless requested.

- [ ] **Step 3: Confirm Stripe source files exist**

Run:

```powershell
git ls-tree -r --name-only $StripeSource -- application/account/Core/Integrations/Stripe application/account/Core/Features/Subscriptions application/account/Tests/Subscriptions application/account/WebApp/routes/account/billing
```

Expected: the Stripe files listed in Source Inventory are present.

### Task 2: Restore Stripe Backend Integration

**Files:**
- Restore: `application/account/Core/Integrations/Stripe/*.cs`
- Delete: `application/account/Core/Integrations/PayFast/*.cs`
- Modify: `application/account/Core/Configuration.cs`
- Modify: `application/account/Core/Account.csproj`

- [ ] **Step 1: Restore Stripe integration files**

Run:

```powershell
git restore --source $StripeSource -- application/account/Core/Integrations/Stripe
```

Expected: `IStripeClient`, `StripeClient`, `MockStripeClient`, `StripeClientFactory`, and `UnconfiguredStripeClient` are restored.

- [ ] **Step 2: Delete PayFast integration files**

Remove:

```text
application/account/Core/Integrations/PayFast/IPayFastClient.cs
application/account/Core/Integrations/PayFast/PayFastClient.cs
application/account/Core/Integrations/PayFast/PayFastSettings.cs
application/account/Core/Integrations/PayFast/PayFastSignature.cs
```

- [ ] **Step 3: Restore Stripe dependency injection**

In `application/account/Core/Configuration.cs`, replace PayFast registration with:

```csharp
services.AddMemoryCache();
services.AddSingleton<MockStripeState>();
services.AddKeyedScoped<IStripeClient, StripeClient>("stripe");
services.AddKeyedScoped<IStripeClient, MockStripeClient>("mock-stripe");
services.AddKeyedScoped<IStripeClient, UnconfiguredStripeClient>("unconfigured-stripe");
services.AddScoped<StripeClientFactory>();
```

Remove:

```csharp
builder.Services.Configure<PayFastSettings>(builder.Configuration.GetSection("PayFast"));
services.AddHttpClient<IPayFastClient, PayFastClient>(client => { client.Timeout = TimeSpan.FromSeconds(30); });
```

Use:

```csharp
using Account.Features.Subscriptions.Shared;
using Account.Integrations.Stripe;
```

Remove:

```csharp
using Account.Features.Subscriptions.Jobs;
using Account.Integrations.PayFast;
```

Keep unrelated registrations such as `FeatureFlagEvaluator` only if their feature remains part of the branch.

- [ ] **Step 4: Restore Stripe package references**

Compare `application/account/Core/Account.csproj` with `$StripeSource`.

Run:

```powershell
git diff $StripeSource -- application/account/Core/Account.csproj
```

Expected implementation: Stripe package references match `$StripeSource`; PayFast-specific package references are removed if present.

### Task 3: Restore Stripe Subscription Domain And Commands

**Files:**
- Restore: `application/account/Core/Features/Subscriptions/Commands/AcknowledgeStripeWebhook.cs`
- Restore: `application/account/Core/Features/Subscriptions/Commands/ProcessPendingEvents.cs`
- Restore: `application/account/Core/Features/Subscriptions/Commands/StartSubscriptionCheckout.cs`
- Restore: `application/account/Core/Features/Subscriptions/Shared/ProcessPendingStripeEvents.cs`
- Restore: `application/account/Core/Features/Subscriptions/Domain/StripeEvent*.cs`
- Modify or restore: `Subscription.cs`, `SubscriptionConfiguration.cs`, `SubscriptionRepository.cs`, `SubscriptionTypes.cs`
- Delete: PayFast ITN and manual billing job files listed in Source Inventory

- [ ] **Step 1: Restore Stripe subscription command/domain files**

Run:

```powershell
git restore --source $StripeSource -- `
  application/account/Core/Features/Subscriptions/Commands/AcknowledgeStripeWebhook.cs `
  application/account/Core/Features/Subscriptions/Commands/ProcessPendingEvents.cs `
  application/account/Core/Features/Subscriptions/Commands/StartSubscriptionCheckout.cs `
  application/account/Core/Features/Subscriptions/Shared/ProcessPendingStripeEvents.cs `
  application/account/Core/Features/Subscriptions/Domain/StripeEvent.cs `
  application/account/Core/Features/Subscriptions/Domain/StripeEventConfiguration.cs `
  application/account/Core/Features/Subscriptions/Domain/StripeEventRepository.cs
```

- [ ] **Step 2: Restore subscription aggregate Stripe fields**

In `application/account/Core/Features/Subscriptions/Domain/Subscription.cs`, restore Stripe fields and methods from `$StripeSource`, including:

```csharp
public StripeCustomerId? StripeCustomerId { get; private set; }
public StripeSubscriptionId? StripeSubscriptionId { get; private set; }
```

Remove PayFast-specific fields:

```csharp
public string? PayFastToken { get; private set; }
public string? PayFastPaymentId { get; private set; }
```

- [ ] **Step 3: Restore Stripe repository predicates**

In `application/account/Core/Features/Subscriptions/Domain/SubscriptionRepository.cs`, restore Stripe predicates from `$StripeSource`; remove predicates based on `PayFastToken`, `NextBillingDate`, and PayFast manual billing state.

- [ ] **Step 4: Delete PayFast command/domain/job files**

Delete the PayFast-only files listed in Source Inventory. Also remove `BillingDunningService`, `BillingJob`, and `BillingReconciliationJob` registrations unless a non-PayFast feature still references them after the Stripe restore.

- [ ] **Step 5: Restore commands changed from Stripe to PayFast**

For each file below, restore from `$StripeSource`, then manually reapply unrelated branch changes only if they are not payment-provider-specific:

```text
application/account/Core/Features/Billing/Commands/ConfirmPaymentMethodSetup.cs
application/account/Core/Features/Billing/Commands/RetryPendingInvoicePayment.cs
application/account/Core/Features/Billing/Commands/StartPaymentMethodSetup.cs
application/account/Core/Features/Billing/Commands/UpdateBillingInfo.cs
application/account/Core/Features/Billing/Queries/GetPaymentHistory.cs
application/account/Core/Features/Subscriptions/Commands/CancelScheduledDowngrade.cs
application/account/Core/Features/Subscriptions/Commands/CancelSubscription.cs
application/account/Core/Features/Subscriptions/Commands/ReactivateSubscription.cs
application/account/Core/Features/Subscriptions/Commands/ScheduleDowngrade.cs
application/account/Core/Features/Subscriptions/Commands/UpgradeSubscription.cs
application/account/Core/Features/Subscriptions/Queries/GetCheckoutPreview.cs
application/account/Core/Features/Subscriptions/Queries/GetCurrentSubscription.cs
application/account/Core/Features/Subscriptions/Queries/GetPricingCatalog.cs
application/account/Core/Features/Subscriptions/Queries/GetSubscribePreview.cs
application/account/Core/Features/Subscriptions/Queries/GetUpgradePreview.cs
```

Expected: `git grep -n -e PayFast -e payfast -- application/account/Core/Features/Billing application/account/Core/Features/Subscriptions` returns no output after this task.

### Task 4: Restore Stripe Endpoints And API Shape

**Files:**
- Restore: `application/account/Api/Endpoints/StripeWebhookEndpoints.cs`
- Modify: `application/account/Api/Endpoints/SubscriptionEndpoints.cs`
- Modify: `application/account/Api/Endpoints/BillingEndpoints.cs`
- Delete if PayFast-only: `application/account/Api/Endpoints/BillingEndpointRetry.cs`

- [ ] **Step 1: Restore Stripe webhook endpoint**

Run:

```powershell
git restore --source $StripeSource -- application/account/Api/Endpoints/StripeWebhookEndpoints.cs
```

- [ ] **Step 2: Remove PayFast ITN route**

In `application/account/Api/Endpoints/SubscriptionEndpoints.cs`, delete the `/payfast/itn` route and related `HandlePayFastItnCommand` usage.

- [ ] **Step 3: Restore Stripe subscription routes**

Compare and restore Stripe route handlers from `$StripeSource` for:

```text
application/account/Api/Endpoints/SubscriptionEndpoints.cs
application/account/Api/Endpoints/BillingEndpoints.cs
```

Expected routes include Stripe checkout/session, Stripe webhook handling, payment method setup confirmation, pending invoice retry, plan preview, upgrade, downgrade, cancel, and reactivate flows matching `$StripeSource`.

### Task 5: Restore EF Model And Migrations

**Files:**
- Modify: `application/account/Core/Database/AccountDbContext.cs`
- Modify: `application/account/Core/Database/Migrations/20260303023200_Initial.cs`
- Delete or replace: `application/account/Core/Database/Migrations/20260428120000_AddBillingHardeningLedgers.cs`
- Review: newer non-payment migrations such as `20260501120000_AddFeatureFlagsAndTeams.cs`

- [ ] **Step 1: Restore Stripe DbContext model references**

In `AccountDbContext.cs`, restore Stripe event set/configuration from `$StripeSource`:

```csharp
public DbSet<StripeEvent> StripeEvents => Set<StripeEvent>();
```

Remove:

```csharp
public DbSet<PayFastItnEvent> PayFastItnEvents => Set<PayFastItnEvent>();
```

- [ ] **Step 2: Restore subscription column mappings**

In `SubscriptionConfiguration.cs`, restore Stripe column mappings from `$StripeSource` and remove PayFast token/payment id, manual billing cycle, reconciliation, and ITN mappings.

- [ ] **Step 3: Reconcile migrations**

Delete PayFast-only migration `20260428120000_AddBillingHardeningLedgers.cs` if it only adds PayFast/manual billing ledgers. Keep unrelated migrations such as feature flags/teams after confirming they do not depend on PayFast columns.

- [ ] **Step 4: Build to generate model/OpenAPI feedback**

Run:

```powershell
dotnet run --project developer-cli -- build --backend --self-contained-system account --quiet
```

Expected: no EF/model compile errors.

### Task 6: Restore Stripe Tests

**Files:**
- Restore: deleted Stripe tests from `$StripeSource`
- Delete: PayFast tests listed in Source Inventory
- Modify: `application/account/Tests/EndpointBaseTest.cs`
- Modify: `application/account/Tests/appsettings.json`

- [ ] **Step 1: Restore Stripe tests**

Run:

```powershell
git restore --source $StripeSource -- `
  application/account/Tests/Billing/ConfirmPaymentMethodSetupTests.cs `
  application/account/Tests/Billing/RetryPendingInvoicePaymentTests.cs `
  application/account/Tests/Billing/UpdateBillingInfoTests.cs `
  application/account/Tests/Subscriptions/AcknowledgeStripeWebhookTests.cs `
  application/account/Tests/Subscriptions/GetCheckoutPreviewTests.cs `
  application/account/Tests/Subscriptions/GetPricingCatalogTests.cs `
  application/account/Tests/Subscriptions/GetUpgradePreviewTests.cs `
  application/account/Tests/Subscriptions/StartSubscriptionCheckoutTests.cs
```

- [ ] **Step 2: Restore Stripe test setup**

In `EndpointBaseTest.cs`, use:

```csharp
using Account.Integrations.Stripe;
```

Set environment variables:

```csharp
Environment.SetEnvironmentVariable("Stripe__AllowMockProvider", "true");
Environment.SetEnvironmentVariable("Stripe__PublishableKey", "pk_test_mock_publishable_key");
```

Expose:

```csharp
protected MockStripeState StripeState => _webApplicationFactory.Services.GetRequiredService<MockStripeState>();
```

Remove `IPayFastClient` substitutes and PayFast in-memory config.

- [ ] **Step 3: Delete PayFast tests**

Delete:

```text
application/account/Tests/Integrations/PayFast/PayFastRefundTests.cs
application/account/Tests/Subscriptions/HandlePayFastItnTests.cs
application/account/Tests/Subscriptions/InitiateSubscriptionTests.cs
application/account/Tests/Subscriptions/RetryFailedChargeTests.cs
application/account/Tests/Subscriptions/BillingDunningServiceTests.cs
```

- [ ] **Step 4: Run account tests**

Run:

```powershell
dotnet run --project developer-cli -- test --self-contained-system account --no-build --quiet
```

Expected: all account tests pass with Stripe mocks.

### Task 7: Restore Account Billing UI

**Files:**
- Restore: Stripe checkout components from `$StripeSource`
- Modify: `application/account/WebApp/routes/account/billing/**`
- Modify: `application/account/WebApp/shared/lib/api/subscriptionPlan.ts`
- Modify: `application/account/WebApp/routes/legal/*.md`
- Modify: account WebApp package/dependency files only as needed

- [ ] **Step 1: Restore Stripe UI components**

Run:

```powershell
git restore --source $StripeSource -- `
  application/account/WebApp/routes/account/billing/-components/CheckoutDialog.tsx `
  application/account/WebApp/routes/account/billing/-components/CheckoutForm.tsx `
  application/account/WebApp/routes/account/billing/-components/stripeAppearance.ts `
  application/account/WebApp/routes/account/billing/-components/BillingInfoSection.tsx `
  application/account/WebApp/routes/account/billing/-components/PaymentMethodSection.tsx `
  application/account/WebApp/routes/account/billing/-components/BillingPageDialogs.tsx
```

- [ ] **Step 2: Remove PayFast UI language and flows**

Remove UI text and code references to:

```text
Managed by PayFast
Opening PayFast in a new tab to update your card.
Update on PayFast
PayFast lightbox
```

Expected replacement behavior: Stripe checkout and Stripe setup intent flows from `$StripeSource`.

- [ ] **Step 3: Restore Stripe dependencies**

Compare:

```powershell
git diff $StripeSource -- application/account/WebApp/package.json application/package-lock.json
```

Expected: Stripe frontend dependencies from `$StripeSource` are present, and PayFast-specific script/dependency usage is removed.

- [ ] **Step 4: Restore legal/copy references**

In account legal/copy files, replace PayFast payment processing references with Stripe equivalents from `$StripeSource`.

Run:

```powershell
git grep -n -e PayFast -e payfast -- application/account/WebApp
```

Expected: no output.

### Task 8: Remove Account PayFast Configuration Surface

**Files:**
- Modify: `application/account/Tests/appsettings.json`
- Modify: `.github/workflows/account.yml`
- Modify: local/example config files if they contain `PayFast`
- Review: `application/main/WebApp/public/index.html`

- [ ] **Step 1: Remove account PayFast settings**

Delete account-level settings:

```json
"PayFast": {
  "MerchantId": "...",
  "MerchantKey": "...",
  "Passphrase": "...",
  "Sandbox": true,
  "NotifyUrl": "...",
  "ReturnUrl": "...",
  "CancelUrl": "..."
}
```

Restore Stripe settings from `$StripeSource`, including publishable key, secret key, webhook secret, price ids, and mock provider toggle names used by tests.

- [ ] **Step 2: Restore CI environment names**

In `.github/workflows/account.yml`, restore Stripe secret/env names from `$StripeSource`; remove PayFast secrets.

- [ ] **Step 3: Review main-app PayFast script separately**

`application/main/WebApp/public/index.html` contains PayFast lightbox script/style. Remove it only if it was added solely for account subscription billing and is not used by the main appointment Paystack flows. If uncertain, leave it out of this account Stripe revert and open a separate task.

### Task 9: Regenerate Generated Assets

**Files:**
- Generated: `application/account/WebApp/shared/lib/api/*.json`
- Generated: `application/account/BackOffice/shared/lib/api/*.json`
- Generated: Lingui `.po` catalogs under account WebApp

- [ ] **Step 1: Run build to regenerate OpenAPI/client artifacts**

Run:

```powershell
dotnet run --project developer-cli -- build --quiet
```

Expected: build succeeds and generated OpenAPI artifacts reflect Stripe endpoints.

- [ ] **Step 2: Refresh translations**

Run:

```powershell
dotnet run --project developer-cli -- format --frontend --no-build --quiet
```

Expected: Lingui catalogs remove PayFast strings and include Stripe strings. If the Windows `|| true` package script fails, capture the log and run `dotnet run --project developer-cli -- lint --frontend --no-build --quiet` after staging generated formatter output.

- [ ] **Step 3: Verify provider references**

Run:

```powershell
git grep -n -e PayFast -e payfast -- application/account
```

Expected: no output.

Run:

```powershell
git grep -n -e Stripe -e stripe -- application/account/Core application/account/Api application/account/Tests application/account/WebApp/routes/account/billing
```

Expected: Stripe references exist in integration, subscription, billing, tests, and billing UI.

### Task 10: Final Verification

**Files:**
- No source edits unless verification exposes issues

- [ ] **Step 1: Build**

Run:

```powershell
dotnet run --project developer-cli -- build --quiet
```

Expected: `Build succeeded.`

- [ ] **Step 2: Run format, lint, and test after build**

Run in parallel:

```powershell
dotnet run --project developer-cli -- format --no-build --quiet
dotnet run --project developer-cli -- lint --no-build --quiet
dotnet run --project developer-cli -- test --no-build --quiet
```

Expected:

```text
Code format completed
Code linting completed
Test summary: total: <count>; failed: 0
```

If backend format/lint fail with `[MSB4247] Could not load SDK Resolver` from SQL Server Management Studio MSBuild, record it as an environment blocker and do not claim backend format/lint pass.

- [ ] **Step 3: Resolve frontend max-lines blockers before completion**

If lint reports max-lines in these current branch files, split them before claiming lint success:

```text
application/account/WebApp/federated-modules/public/PublicNavigation.tsx
application/main/WebApp/routes/dashboard/payments/-components/PaystackSetupDialogParts.tsx
application/main/WebApp/shared/lib/appointmentContracts.ts
application/main/WebApp/routes/dashboard/-components/CommandPalette.tsx
application/main/WebApp/routes/book/$businessSlug.tsx
```

These are not Stripe account-billing files except `PublicNavigation.tsx`. Fixing them may belong in a separate cleanup if the user keeps main Paystack appointment work out of scope.

## Self-Review

- Spec coverage: The plan restores backend Stripe client/DI, Stripe subscription commands, Stripe events, tests, account billing UI, config, generated assets, and verification.
- Scope gap: Main-app Paystack appointment deposits are deliberately not reverted because the user asked for PayFast to Stripe account subscription billing. Include them only if the user confirms Paystack appointment payments should also be reverted.
- Placeholder scan: No TBD/TODO placeholders remain.
- Type consistency: The plan consistently uses `StripeCustomerId`, `StripeSubscriptionId`, `IStripeClient`, `StripeClientFactory`, and `MockStripeState` for restored Stripe code.

## Execution Handoff

Plan complete and saved to `docs/superpowers/plans/2026-05-03-revert-payfast-to-stripe.md`.

Two execution options:

1. Subagent-Driven (recommended): dispatch focused workers for backend Stripe restore, tests, frontend billing UI, and generated assets.
2. Inline Execution: execute the plan in this session with checkpoints after each task group.
