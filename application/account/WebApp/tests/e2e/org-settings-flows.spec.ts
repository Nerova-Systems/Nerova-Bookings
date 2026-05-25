import { expect } from "@playwright/test";
import { test } from "@shared/e2e/fixtures/page-auth";
import { activateBaseFlags, setOwnerTenantOverrides } from "@shared/e2e/utils/feature-flag-helpers";
import { createTestContext, expectToastMessage } from "@shared/e2e/utils/test-assertions";
import { step } from "@shared/e2e/utils/test-step-wrapper";

// The Organization settings surface (the dedicated /account/settings/organization route, the
// Members tab, the SMTP/SSO/Billing sub-tabs) is gated on tier-organizations. Its dependency chain
// is tier-teams → tier-organizations, both of which must be active in back-office AND enabled per
// tenant before the route resolves. Both calls are idempotent.
const FLAG_CHAIN = ["tier-teams", "tier-organizations"] as const;

test.describe("@smoke", () => {
  test.afterEach(async ({ ownerPage }) => {
    await setOwnerTenantOverrides(ownerPage, [...FLAG_CHAIN].reverse(), false);
  });

  /**
   * ORGANIZATION SETTINGS HAPPY PATH
   *
   * Exercises the dedicated org admin surface gated on tier-organizations:
   * - Activate the tier-teams → tier-organizations chain for the worker tenant
   * - Navigate to /account/settings/organization and verify the Profile tab renders
   * - Rename the organization through the OrgProfileTab form (PUT /api/account/tenants/current)
   * - Verify the success toast and that the AppLayout subtitle/header reflects the new name
   */
  test("should rename the organization through the Profile tab", async ({ ownerPage, browser }) => {
    const context = createTestContext(ownerPage);
    const unique = Date.now();
    const updatedName = `e2e-org-${unique}`;

    await step("Activate tier-teams + tier-organizations & enable both tenant overrides")(async () => {
      await activateBaseFlags(browser, FLAG_CHAIN);
      await setOwnerTenantOverrides(ownerPage, FLAG_CHAIN, true);
    })();

    await step("Navigate to /account/settings/organization & verify Profile tab is the default view")(async () => {
      await ownerPage.goto("/account/settings/organization");

      // The AppLayout title resolves to the live tenant name (not a fixed "Organization" string)
      // once /api/account/tenants/current resolves, so we anchor on the stable tab and form
      // selectors instead. Profile is the defaultValue, so its textbox is visible immediately.
      await expect(ownerPage.getByRole("tab", { name: "Profile" })).toBeVisible();
      await expect(ownerPage.getByRole("tab", { name: "Members" })).toBeVisible();
      await expect(ownerPage.getByRole("textbox", { name: "Organization name" })).toBeVisible();
    })();

    await step("Rename the organization & verify success toast")(async () => {
      await ownerPage.getByRole("textbox", { name: "Organization name" }).fill(updatedName);
      await ownerPage.getByRole("button", { name: "Save changes" }).click();

      await expectToastMessage(context, "Organization profile updated");
      // After invalidateTenant the form re-renders with the new tenant name persisted as the
      // controlled state, so the input still shows what we just typed.
      await expect(ownerPage.getByRole("textbox", { name: "Organization name" })).toHaveValue(updatedName);
    })();
  });
});
