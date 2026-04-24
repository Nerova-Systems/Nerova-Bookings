import { expect } from "@playwright/test";
import { test } from "@shared/e2e/fixtures/page-auth";
import { createTestContext, expectToastMessage } from "@shared/e2e/utils/test-assertions";
import { step } from "@shared/e2e/utils/test-step-wrapper";

function activeSubscription(overrides: Record<string, unknown> = {}) {
  return {
    id: "sub_mock",
    plan: "Standard",
    scheduledPlan: null,
    status: "Active",
    trialEndsAt: "2026-02-01T00:00:00Z",
    currentPeriodEnd: "2026-03-24T00:00:00Z",
    nextBillingDate: "2026-03-24T00:00:00Z",
    cancelledAt: null,
    ...overrides
  };
}

function billingHistory(transactions: unknown[] = []) {
  return { totalCount: transactions.length, transactions };
}

test.describe("@smoke", () => {
  /**
   * SUBSCRIPTION MANAGEMENT E2E TEST
   *
   * Tests the complete subscription lifecycle using mocked API responses:
   * - Trial plan display with plan comparison cards (no-subscription view)
   * - Subscribe flow showing confirmation dialog (PayFast lightbox is external)
   * - Upgrade from Standard to Premium (subscription page)
   * - Schedule downgrade from Premium to Standard
   * - Downgrade to Trial (free plan) — triggers cancel subscription dialog
   * - Cancelled state with reactivation banner and confirmation dialog
   * - Payment history table with transaction rows
   * - PastDue warning banner display
   * - Billing not configured state handling
   * - Access denied for non-Owner users
   */
  test("should handle complete subscription lifecycle with plan changes and billing states", async ({ ownerPage }) => {
    const context = createTestContext(ownerPage);

    // === TRIAL STATE AND PLAN DISPLAY ===
    await step("Navigate to billing page & verify Trial plan with plan comparison cards")(async () => {
      await ownerPage.goto("/account/billing");

      await expect(ownerPage.getByRole("heading", { name: "Billing" })).toBeVisible();
      await expect(ownerPage.getByText("Choose a plan to get started.")).toBeVisible();

      const starterCard = ownerPage.locator(".grid > div").filter({ hasText: "Starter" }).first();
      await expect(starterCard.getByText("/month")).toBeVisible();
      await expect(starterCard.getByText("5 users")).toBeVisible();
      await expect(starterCard.getByText("10 GB storage")).toBeVisible();
      await expect(starterCard.getByRole("button", { name: "Subscribe" })).toBeVisible();

      const standardCard = ownerPage.locator(".grid > div").filter({ hasText: "Standard" }).first();
      await expect(standardCard.getByText("/month")).toBeVisible();
      await expect(standardCard.getByText("10 users")).toBeVisible();
      await expect(standardCard.getByText("100 GB storage")).toBeVisible();
      await expect(standardCard.getByText("Analytics")).toBeVisible();
      await expect(standardCard.getByRole("button", { name: "Subscribe" })).toBeVisible();

      const premiumCard = ownerPage.locator(".grid > div").filter({ hasText: "Premium" }).first();
      await expect(premiumCard.getByText("/month")).toBeVisible();
      await expect(premiumCard.getByText("Unlimited users")).toBeVisible();
      await expect(premiumCard.getByText("1 TB storage")).toBeVisible();
      await expect(premiumCard.getByText("Priority support")).toBeVisible();
      await expect(premiumCard.getByText("SLA")).toBeVisible();
      await expect(premiumCard.getByRole("button", { name: "Subscribe" })).toBeVisible();
    })();

    // === SUBSCRIBE FLOW (CONFIRMATION DIALOG) ===
    await step("Click Subscribe on Starter plan & verify confirmation dialog & cancel it")(async () => {
      await ownerPage.route("**/api/account/subscriptions/subscribe-preview**", async (route) => {
        await route.fulfill({
          status: 200,
          contentType: "application/json",
          json: { totalAmount: 149.0, taxAmount: 0, currency: "ZAR" }
        });
      });

      const starterCard = ownerPage.locator(".grid > div").filter({ hasText: "Starter" }).first();
      await starterCard.getByRole("button", { name: "Subscribe" }).click();

      await expect(ownerPage.getByRole("dialog", { name: "Subscribe to Starter" })).toBeVisible();
      await expect(ownerPage.getByText("Total")).toBeVisible();
      await expect(ownerPage.getByRole("button", { name: "Subscribe" })).toBeVisible();
      await expect(ownerPage.getByRole("button", { name: "Cancel" })).toBeVisible();

      await ownerPage.getByRole("button", { name: "Cancel" }).click();
      await expect(ownerPage.getByRole("dialog", { name: "Subscribe to Starter" })).not.toBeVisible();

      await ownerPage.unroute("**/api/account/subscriptions/subscribe-preview**");
    })();

    await step("Mock active Standard subscription & verify billing overview with payment history")(async () => {
      await ownerPage.route("**/api/account/subscriptions/current", async (route) => {
        await route.fulfill({
          status: 200,
          contentType: "application/json",
          json: activeSubscription()
        });
      });

      await ownerPage.route("**/api/account/billing/payment-history**", async (route) => {
        await route.fulfill({
          status: 200,
          contentType: "application/json",
          json: billingHistory([
            { id: "txn_mock_1", amount: 299.0, currency: "ZAR", status: "Succeeded", date: "2026-02-24T00:00:00Z", invoiceUrl: null, creditNoteUrl: null }
          ])
        });
      });

      await ownerPage.goto("/account/billing");

      await expect(ownerPage.getByText("Standard", { exact: false }).first()).toBeVisible();
      await expect(ownerPage.getByText("Active")).toBeVisible();
      await expect(ownerPage.getByText("Next billing date:")).toBeVisible();
      await expect(ownerPage.getByRole("button", { name: "Change plan", exact: false }).first()).toBeVisible();

      await expect(ownerPage.getByRole("columnheader", { name: "Date" })).toBeVisible();
      await expect(ownerPage.getByRole("columnheader", { name: "Amount" })).toBeVisible();
      await expect(ownerPage.getByRole("columnheader", { name: "Status" })).toBeVisible();
      await expect(ownerPage.getByText("Succeeded")).toBeVisible();

      await ownerPage.unroute("**/api/account/billing/payment-history**");
      await ownerPage.unroute("**/api/account/subscriptions/current");
    })();

    // === UPGRADE FLOW ===
    await step("Mock Standard subscription & click Upgrade on Premium plan & confirm upgrade dialog")(async () => {
      let currentPlan = "Standard";
      await ownerPage.route("**/api/account/subscriptions/current", async (route) => {
        await route.fulfill({
          status: 200,
          contentType: "application/json",
          json: activeSubscription({ plan: currentPlan })
        });
      });

      await ownerPage.route("**/api/account/subscriptions/upgrade-preview**", async (route) => {
        await route.fulfill({
          status: 200,
          contentType: "application/json",
          json: {
            lineItems: [
              { description: "Premium (prorated)", amount: 450.0, currency: "ZAR", isTax: false },
              { description: "Tax", amount: 0, currency: "ZAR", isTax: true }
            ],
            totalAmount: 450.0,
            currency: "ZAR"
          }
        });
      });

      await ownerPage.route("**/api/account/subscriptions/upgrade", async (route) => {
        currentPlan = "Premium";
        await route.fulfill({ status: 200, contentType: "application/json", json: {} });
      });

      await ownerPage.goto("/account/billing/subscription");

      const premiumCard = ownerPage.locator(".grid > div").filter({ hasText: "Premium" }).first();
      await premiumCard.getByRole("button", { name: "Upgrade" }).click();

      await expect(ownerPage.getByRole("dialog", { name: "Upgrade to Premium" })).toBeVisible();
      await expect(ownerPage.getByText("Total")).toBeVisible();
      await ownerPage.getByRole("button", { name: "Pay and upgrade" }).click();

      await expectToastMessage(context, "Your plan has been upgraded.");
      await ownerPage.unroute("**/api/account/subscriptions/upgrade-preview**");
      await ownerPage.unroute("**/api/account/subscriptions/upgrade");
      await ownerPage.unroute("**/api/account/subscriptions/current");
    })();

    // === DOWNGRADE FLOW (MOCKED PREMIUM STATE) ===
    await step("Mock Premium subscription state & click Downgrade on Standard plan & confirm")(async () => {
      let scheduledPlan: string | null = null;
      await ownerPage.route("**/api/account/subscriptions/current", async (route) => {
        await route.fulfill({
          status: 200,
          contentType: "application/json",
          json: activeSubscription({ plan: "Premium", scheduledPlan })
        });
      });

      await ownerPage.route("**/api/account/subscriptions/schedule-downgrade", async (route) => {
        scheduledPlan = "Standard";
        await route.fulfill({ status: 200, contentType: "application/json", json: {} });
      });

      await ownerPage.goto("/account/billing/subscription");

      const premiumCard = ownerPage.locator(".grid > div").filter({ hasText: "Premium" }).first();
      await expect(premiumCard.getByRole("button", { name: "Current plan" })).toBeDisabled();

      const standardCard = ownerPage.locator(".grid > div").filter({ hasText: "Standard" }).first();
      await standardCard.getByRole("button", { name: "Downgrade" }).click();

      await expect(ownerPage.getByRole("alertdialog")).toBeVisible();
      await expect(ownerPage.getByText("Downgrade to Standard")).toBeVisible();
      await ownerPage.getByRole("button", { name: "Confirm downgrade" }).click();

      await expectToastMessage(context, "Your downgrade has been scheduled.");
      await ownerPage.unroute("**/api/account/subscriptions/schedule-downgrade");
      await ownerPage.unroute("**/api/account/subscriptions/current");
    })();

    // === CANCEL SUBSCRIPTION (DOWNGRADE TO TRIAL / FREE PLAN) ===
    await step("Mock Standard subscription & click Downgrade on Trial card & verify cancel confirmation dialog")(
      async () => {
        let subscriptionStatus = "Active";
        await ownerPage.route("**/api/account/subscriptions/current", async (route) => {
          await route.fulfill({
            status: 200,
            contentType: "application/json",
            json: activeSubscription({ plan: "Standard", status: subscriptionStatus })
          });
        });

        await ownerPage.route("**/api/account/subscriptions/cancel", async (route) => {
          subscriptionStatus = "Cancelled";
          await route.fulfill({ status: 200, contentType: "application/json", json: {} });
        });

        await ownerPage.goto("/account/billing/subscription");

        const trialCard = ownerPage.locator(".grid > div").filter({ hasText: "Trial" }).first();
        await trialCard.getByRole("button", { name: "Downgrade" }).click();

        await expect(ownerPage.getByRole("alertdialog")).toBeVisible();
        await expect(ownerPage.getByText("Cancel subscription")).toBeVisible();
        await expect(ownerPage.getByText("switch to the free plan")).toBeVisible();
      }
    )();

    await step("Select cancellation reason & confirm & verify subscription cancelled toast")(async () => {
      await ownerPage.getByRole("radio", { name: "No longer needed" }).click();

      const dialog = ownerPage.getByRole("alertdialog");
      await dialog.getByRole("button", { name: "Cancel subscription" }).click();

      await expectToastMessage(context, "Your subscription has been cancelled.");
      await ownerPage.unroute("**/api/account/subscriptions/cancel");
      await ownerPage.unroute("**/api/account/subscriptions/current");
    })();

    // === CANCELLED STATE (MOCKED) ===
    await step("Mock cancelled subscription state & verify cancellation banner with reactivate button")(async () => {
      let subscriptionStatus = "Cancelled";
      await ownerPage.route("**/api/account/subscriptions/current", async (route) => {
        await route.fulfill({
          status: 200,
          contentType: "application/json",
          json: activeSubscription({ plan: "Standard", status: subscriptionStatus })
        });
      });

      await ownerPage.route("**/api/account/billing/payment-history**", async (route) => {
        await route.fulfill({ status: 200, contentType: "application/json", json: billingHistory() });
      });

      await ownerPage.goto("/account/billing");
      await expect(ownerPage.getByText("cancelled and will end on")).toBeVisible();
      await expect(ownerPage.getByRole("button", { name: "Reactivate" })).toBeVisible();

      await ownerPage.route("**/api/account/subscriptions/reactivate", async (route) => {
        subscriptionStatus = "Active";
        await route.fulfill({ status: 200, contentType: "application/json", json: { uuid: null } });
      });
    })();

    // === REACTIVATE SUBSCRIPTION ===
    await step("Click Reactivate in banner & confirm dialog & verify subscription reactivated toast")(async () => {
      await ownerPage.getByRole("button", { name: "Reactivate" }).click();

      await expect(ownerPage.getByRole("alertdialog")).toBeVisible();
      await expect(ownerPage.getByText("Reactivate subscription")).toBeVisible();
      await ownerPage.getByRole("alertdialog").getByRole("button", { name: "Reactivate" }).click();

      await expectToastMessage(context, "Your subscription has been reactivated.");
      await ownerPage.unroute("**/api/account/subscriptions/reactivate");
      await ownerPage.unroute("**/api/account/billing/payment-history**");
      await ownerPage.unroute("**/api/account/subscriptions/current");
    })();

    // === PAST DUE BANNER (MOCKED SUBSCRIPTION STATE) ===
    await step("Mock PastDue subscription & verify payment failed banner")(async () => {
      await ownerPage.route("**/api/account/subscriptions/current", async (route) => {
        await route.fulfill({
          status: 200,
          contentType: "application/json",
          json: activeSubscription({ status: "PastDue" })
        });
      });

      await ownerPage.route("**/api/account/billing/payment-history**", async (route) => {
        await route.fulfill({ status: 200, contentType: "application/json", json: billingHistory() });
      });

      await ownerPage.goto("/account/billing");
      await expect(ownerPage.getByText("Payment failed. Your subscription will be suspended soon.")).toBeVisible();
      await expect(ownerPage.getByRole("button", { name: "Retry payment" }).first()).toBeVisible();

      await ownerPage.unroute("**/api/account/billing/payment-history**");
      await ownerPage.unroute("**/api/account/subscriptions/current");
    })();

    // === SUSPENDED STATE (MOCKED TENANT STATE) ===
    await step("Mock tenant Suspended state & verify blocked page for Owner")(async () => {
      await ownerPage.route("**/api/account/tenants/current", async (route) => {
        await route.fulfill({
          status: 200,
          contentType: "application/json",
          json: { id: 1, createdAt: "2026-01-01T00:00:00Z", modifiedAt: null, name: "Test Organization", state: "Suspended", logoUrl: null }
        });
      });

      await ownerPage.goto("/account");
      await expect(ownerPage.getByRole("heading", { name: "Account suspended" })).toBeVisible();
      await expect(
        ownerPage.getByText("Please visit the subscription page to resolve any issues and restore access.")
      ).toBeVisible();
      await expect(ownerPage.getByRole("button", { name: "Manage subscription" })).toBeVisible();
    })();

    await step("Navigate to billing page while Suspended & verify access is allowed")(async () => {
      await ownerPage.goto("/account/billing");
      await expect(ownerPage.getByRole("heading", { name: "Billing", exact: true })).toBeVisible();

      await ownerPage.unroute("**/api/account/tenants/current");
    })();

    // === BILLING NOT CONFIGURED STATE ===
    await step("Mock empty pricing catalog & verify warning message on subscription page")(async () => {
      await ownerPage.route("**/api/account/subscriptions/pricing-catalog", async (route) => {
        await route.fulfill({ status: 200, contentType: "application/json", json: { plans: [] } });
      });

      await ownerPage.goto("/account/billing/subscription");
      await expect(
        ownerPage.getByText("Billing is not configured. Please contact support to enable payment processing.")
      ).toBeVisible();

      await ownerPage.unroute("**/api/account/subscriptions/pricing-catalog");
    })();
  });

  test("should deny billing page access to non-Owner users", async ({ ownerPage }) => {
    createTestContext(ownerPage);

    await step("Mock Member role & navigate to billing page & verify access denied")(async () => {
      await ownerPage.addInitScript(() => {
        let originalFn: (() => { userInfoEnv: { role: string } }) | null = null;
        Object.defineProperty(window, "getApplicationEnvironment", {
          configurable: true,
          set(fn: () => { userInfoEnv: { role: string } }) {
            originalFn = fn;
          },
          get() {
            if (!originalFn) {
              return undefined;
            }
            return () => {
              const env = originalFn?.();
              return { ...env, userInfoEnv: { ...env?.userInfoEnv, role: "Member" } };
            };
          }
        });
      });

      await ownerPage.goto("/account/billing");

      await expect(ownerPage.getByRole("heading", { name: "Access denied" })).toBeVisible();
      await expect(ownerPage.getByText("You do not have permission to access this page.")).toBeVisible();
    })();

    await step("Mock Suspended tenant with Member role & verify contact owner message")(async () => {
      await ownerPage.route("**/api/account/tenants/current", async (route) => {
        await route.fulfill({
          status: 200,
          contentType: "application/json",
          json: { id: 1, createdAt: "2026-01-01T00:00:00Z", modifiedAt: null, name: "Test Organization", state: "Suspended", logoUrl: null }
        });
      });

      await ownerPage.goto("/account");
      await expect(ownerPage.getByRole("heading", { name: "Account suspended" })).toBeVisible();
      await expect(
        ownerPage.getByText("Your account has been suspended. Please contact the account owner to restore access.")
      ).toBeVisible();
      await expect(ownerPage.getByRole("button", { name: "Manage subscription" })).not.toBeVisible();

      await ownerPage.unroute("**/api/account/tenants/current");
    })();
  });
});

test.describe("@comprehensive", () => {
  /**
   * SUBSCRIPTION EDGE CASES E2E TEST
   *
   * Tests subscription features not covered by the smoke test:
   * - Tab navigation between Billing and Subscription pages
   * - Scheduled downgrade banner on billing page with Cancel downgrade dialog
   * - Payment history with refunded transaction
   * - Empty payment history state
   */
  test("should handle tab navigation, scheduled downgrade banner, and payment history edge cases", async ({
    ownerPage
  }) => {
    const context = createTestContext(ownerPage);

    // === TAB NAVIGATION ===
    await step("Mock active subscription & navigate to billing page & verify tab navigation to Subscription")(
      async () => {
        await ownerPage.route("**/api/account/subscriptions/current", async (route) => {
          await route.fulfill({
            status: 200,
            contentType: "application/json",
            json: activeSubscription()
          });
        });

        await ownerPage.route("**/api/account/billing/payment-history**", async (route) => {
          await route.fulfill({ status: 200, contentType: "application/json", json: billingHistory() });
        });

        await ownerPage.goto("/account/billing");

        await expect(ownerPage.getByRole("tablist", { name: "Billing tabs" })).toBeVisible();
        await ownerPage.getByRole("tab", { name: "Subscription" }).click();

        await expect(ownerPage).toHaveURL("/account/billing/subscription");
      }
    )();

    await step("Navigate back to Billing tab & verify billing content loads")(async () => {
      await ownerPage.getByRole("tab", { name: "Billing" }).click();

      await expect(ownerPage).toHaveURL("/account/billing");
      await expect(ownerPage.getByRole("heading", { name: "Current plan" })).toBeVisible();
    })();

    // === EMPTY PAYMENT HISTORY ===
    await step("Verify empty payment history state")(async () => {
      await expect(ownerPage.getByRole("heading", { name: "Billing history" })).toBeVisible();
      await expect(ownerPage.getByText("No payment history available.")).toBeVisible();

      await ownerPage.unroute("**/api/account/billing/payment-history**");
      await ownerPage.unroute("**/api/account/subscriptions/current");
    })();

    // === PAYMENT HISTORY WITH REFUNDED TRANSACTION ===
    await step("Mock payment history with refunded transaction & verify rows")(async () => {
      await ownerPage.route("**/api/account/subscriptions/current", async (route) => {
        await route.fulfill({
          status: 200,
          contentType: "application/json",
          json: activeSubscription()
        });
      });

      await ownerPage.route("**/api/account/billing/payment-history**", async (route) => {
        await route.fulfill({
          status: 200,
          contentType: "application/json",
          json: billingHistory([
            { id: "txn_1", amount: 299.0, currency: "ZAR", status: "Succeeded", date: "2026-02-24T00:00:00Z", invoiceUrl: null, creditNoteUrl: null },
            { id: "txn_2", amount: 299.0, currency: "ZAR", status: "Refunded", date: "2026-01-24T00:00:00Z", invoiceUrl: null, creditNoteUrl: null }
          ])
        });
      });

      await ownerPage.goto("/account/billing");

      await expect(ownerPage.getByText("Succeeded")).toBeVisible();
      await expect(ownerPage.getByText("Refunded")).toBeVisible();

      await ownerPage.unroute("**/api/account/billing/payment-history**");
      await ownerPage.unroute("**/api/account/subscriptions/current");
    })();

    // === SCHEDULED DOWNGRADE BANNER ON BILLING OVERVIEW ===
    await step("Mock scheduled downgrade state & verify downgrade banner with cancel button")(async () => {
      let scheduledPlan: string | null = "Standard";
      await ownerPage.route("**/api/account/subscriptions/current", async (route) => {
        await route.fulfill({
          status: 200,
          contentType: "application/json",
          json: activeSubscription({ plan: "Premium", scheduledPlan })
        });
      });

      await ownerPage.route("**/api/account/billing/payment-history**", async (route) => {
        await route.fulfill({ status: 200, contentType: "application/json", json: billingHistory() });
      });

      await ownerPage.route("**/api/account/subscriptions/cancel-scheduled-downgrade", async (route) => {
        scheduledPlan = null;
        await route.fulfill({ status: 200, contentType: "application/json", json: {} });
      });

      await ownerPage.goto("/account/billing");

      await expect(ownerPage.getByText("will be downgraded to Standard")).toBeVisible();
      await expect(ownerPage.getByRole("button", { name: "Cancel downgrade" })).toBeVisible();
    })();

    await step("Click Cancel downgrade & confirm dialog & verify downgrade cancelled toast")(async () => {
      await ownerPage.getByRole("button", { name: "Cancel downgrade" }).click();

      await expect(ownerPage.getByRole("alertdialog")).toBeVisible();
      await expect(ownerPage.getByText("Cancel scheduled downgrade")).toBeVisible();
      await ownerPage.getByRole("alertdialog").getByRole("button", { name: "Cancel downgrade" }).click();

      await expectToastMessage(context, "Your scheduled downgrade has been cancelled.");
      await ownerPage.unroute("**/api/account/subscriptions/cancel-scheduled-downgrade");
      await ownerPage.unroute("**/api/account/billing/payment-history**");
      await ownerPage.unroute("**/api/account/subscriptions/current");
    })();
  });
});
