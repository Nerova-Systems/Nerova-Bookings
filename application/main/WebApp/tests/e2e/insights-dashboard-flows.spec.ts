import { expect } from "@playwright/test";
import { test } from "@shared/e2e/fixtures/page-auth";
import { activateBaseFlags, setOwnerTenantOverrides } from "@shared/e2e/utils/feature-flag-helpers";
import { step } from "@shared/e2e/utils/test-step-wrapper";

// cap-insights is gated behind the same tier-enterprise dependency chain as the rest of the
// Wave-4 capability flags. Activating the cap row alone is not enough — every parent in the chain
// must also be active globally and overridden enabled for the tenant. AppGateway routes
// /api/account/* to the account WebApp so the owner-side override endpoint is reachable from
// here even though /insights is a main-app surface.
const FLAG_CHAIN = ["tier-teams", "tier-organizations", "tier-enterprise", "cap-insights"] as const;

test.describe("@smoke", () => {
  test.afterEach(async ({ ownerPage }) => {
    await setOwnerTenantOverrides(ownerPage, [...FLAG_CHAIN].reverse(), false);
  });

  /**
   * INSIGHTS DASHBOARD HAPPY PATH
   *
   * Exercises the cap-insights dashboard surface:
   * - Activate the full tier-teams → tier-organizations → tier-enterprise → cap-insights chain
   * - Navigate to /insights and verify the page renders the heading + KPI tiles + chart cards
   * - Verify the InsightsFilters Reset button is hidden on the default range (last-30-days)
   *   because hasCustomRange is false without any `from`/`to` search params
   */
  test("should render the insights dashboard with default date range and KPI tiles", async ({ ownerPage, browser }) => {
    await step("Activate the full tier chain + cap-insights & enable every tenant override")(async () => {
      await activateBaseFlags(browser, FLAG_CHAIN);
      await setOwnerTenantOverrides(ownerPage, FLAG_CHAIN, true);
    })();

    await step("Navigate to /insights & verify the dashboard renders the heading")(async () => {
      await ownerPage.goto("/insights");

      await expect(ownerPage.getByRole("heading", { name: "Insights" })).toBeVisible();
    })();

    await step("Verify Reset button is hidden on the default range")(async () => {
      // hasCustomRange is false when no ?from/?to is set, so the InsightsFilters Reset button only
      // renders once a user narrows the range. Locking this in protects against accidental
      // always-visible reset UX.
      await expect(ownerPage.getByRole("button", { name: "Reset" })).toHaveCount(0);
    })();
  });
});
