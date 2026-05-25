import { expect, type Browser, type Page } from "@playwright/test";
import { test } from "@shared/e2e/fixtures/page-auth";
import { getBackOfficeBaseUrl } from "@shared/e2e/utils/constants";
import { logInAsAdmin } from "@shared/e2e/utils/test-data";
import { step } from "@shared/e2e/utils/test-step-wrapper";

const BACK_OFFICE_BASE_URL = getBackOfficeBaseUrl();

// cap-audit-log is a capability flag whose parent is tier-enterprise, which itself depends on
// tier-organizations → tier-teams. The cap flag only resolves Enabled when EVERY parent in the
// chain is also Enabled for the tenant, so every link must be activated in back-office AND
// overridden enabled at the tenant. Both calls are idempotent.
const FLAG_CHAIN = ["tier-teams", "tier-organizations", "tier-enterprise", "cap-audit-log"] as const;

async function getAntiforgeryHeaders(page: Page): Promise<{ "x-xsrf-token": string }> {
  const token = await page.evaluate(
    () => document.head.querySelector('meta[name="antiforgeryToken"]')?.getAttribute("content") ?? ""
  );
  return { "x-xsrf-token": token };
}

async function activateBaseFlags(browser: Browser, flagKeys: readonly string[]): Promise<void> {
  const context = await browser.newContext({ baseURL: BACK_OFFICE_BASE_URL, ignoreHTTPSErrors: true });
  const page = await context.newPage();
  await page.goto(`${BACK_OFFICE_BASE_URL}/feature-flags`);
  await logInAsAdmin(page, `${BACK_OFFICE_BASE_URL}/feature-flags`);
  const headers = await getAntiforgeryHeaders(page);
  for (const flagKey of flagKeys) {
    const response = await page.request.put(
      `${BACK_OFFICE_BASE_URL}/api/back-office/feature-flags/${flagKey}/activate`,
      { headers }
    );
    expect(response.ok()).toBe(true);
  }
  await context.close();
}

async function setOwnerTenantOverrides(ownerPage: Page, flagKeys: readonly string[], enabled: boolean): Promise<void> {
  await ownerPage.goto("/account/settings");
  const headers = await getAntiforgeryHeaders(ownerPage);
  for (const flagKey of flagKeys) {
    const response = await ownerPage.request.put(`/api/account/feature-flags/${flagKey}/tenant-override`, {
      data: { enabled },
      headers
    });
    expect(response.ok()).toBe(true);
  }
}

test.describe("@smoke", () => {
  test.afterEach(async ({ ownerPage }) => {
    await setOwnerTenantOverrides(ownerPage, [...FLAG_CHAIN].reverse(), false);
  });

  /**
   * AUDIT LOG VIEWER HAPPY PATH
   *
   * Exercises the cap-audit-log viewer:
   * - Activate the full tier-teams → tier-organizations → tier-enterprise → cap-audit-log chain
   *   for the worker tenant
   * - Navigate to /account/settings/audit-log and verify the page renders (heading + filters)
   * - Assert the empty-state title renders because a fresh worker tenant produces no audit entries
   *   yet (no significant actions have been performed by this spec)
   * - The "Clear filters" button is hidden when no filters are active — assert that too to lock in
   *   the default-state UX contract
   */
  test("should render the audit log viewer with the empty state for a fresh tenant", async ({ ownerPage, browser }) => {
    await step("Activate the full tier chain + cap-audit-log & enable every tenant override")(async () => {
      await activateBaseFlags(browser, FLAG_CHAIN);
      await setOwnerTenantOverrides(ownerPage, FLAG_CHAIN, true);
    })();

    await step("Navigate to /account/settings/audit-log & verify the viewer renders")(async () => {
      await ownerPage.goto("/account/settings/audit-log");

      await expect(ownerPage.getByRole("heading", { name: "Audit log" })).toBeVisible();
    })();

    await step("Verify the empty-state title renders and Clear filters is hidden by default")(async () => {
      // A fresh worker tenant has no audit entries yet — the empty-state title matches the
      // no-filter variant from AuditLogTable. If a future test pollutes audit history for this
      // tenant, swap to assert that the table renders instead.
      await expect(ownerPage.getByText("No audit entries yet")).toBeVisible();
      await expect(ownerPage.getByRole("button", { name: "Clear filters" })).toHaveCount(0);
    })();
  });
});
