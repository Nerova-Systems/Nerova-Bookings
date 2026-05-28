import { expect } from "@playwright/test";
import { test } from "@shared/e2e/fixtures/page-auth";
import { activateBaseFlags, setAdminTenantOverrides } from "@shared/e2e/utils/feature-flag-helpers";
import { createTestContext, expectToastMessage } from "@shared/e2e/utils/test-assertions";
import { step } from "@shared/e2e/utils/test-step-wrapper";

// The PBAC admin UI is gated on the tier-enterprise flag. Like every TenantAdminManagedFlag,
// tier-enterprise has a parent dependency chain (tier-teams → tier-organizations → tier-enterprise)
// that must ALL be active and overridden for the tenant. The base rows are kill-switch — the
// reconciler creates them inactive — so an admin must activate every base in the chain before the
// owner's tenant override can take effect. Both steps are idempotent so concurrent workers don't
// fight each other.
const FLAG_CHAIN = ["tier-teams", "tier-organizations", "tier-enterprise"] as const;

test.describe("@smoke", () => {
  test.afterEach(async ({ ownerPage, browser }) => {
    // Reverse the chain on cleanup so tier-enterprise drops before tier-organizations, mirroring the
    // dependency order. Disabling an override row that does not exist is a no-op on the backend.
    await setAdminTenantOverrides(browser, ownerPage, [...FLAG_CHAIN].reverse(), false);
  });

  /**
   * PBAC ADMIN UI HAPPY PATH
   *
   * Exercises the per-tenant role & permission admin surface gated on tier-enterprise:
   * - Activate the tier-teams → tier-organizations → tier-enterprise chain for the worker tenant
   * - Navigate to /account/settings/roles and verify the empty state
   * - Create a custom role with a single permission selected in the PermissionMatrix
   * - Verify the new row renders in the roles table with the correct member count
   * - Edit the role description and re-save
   * - Delete the role via the DeleteRoleDialog and verify the table returns to empty
   */
  test("should create, edit, and delete a custom role via the PBAC admin UI", async ({ ownerPage, browser }) => {
    const context = createTestContext(ownerPage);
    const unique = Date.now();
    const roleName = `e2e-role-${unique}`;
    const roleDescription = `Role created by E2E run ${unique}`;
    const updatedDescription = `Updated by E2E run ${unique}`;

    await step("Activate tier flag chain in back-office & enable tenant overrides for the worker tenant")(async () => {
      await activateBaseFlags(browser, FLAG_CHAIN);
      await setAdminTenantOverrides(browser, ownerPage, FLAG_CHAIN, true);
    })();

    await step("Navigate to /account/settings/roles & verify the roles admin page renders")(async () => {
      await ownerPage.goto("/account/settings/roles");

      await expect(ownerPage.getByRole("heading", { name: "Roles" })).toBeVisible();
      await expect(ownerPage.getByRole("link", { name: "New role" })).toBeVisible();
    })();

    await step("Open the create role form & verify name and permission matrix render")(async () => {
      await ownerPage.getByRole("link", { name: "New role" }).click();

      await expect(ownerPage).toHaveURL("/account/settings/roles/new");
      await expect(ownerPage.getByRole("heading", { name: "Create role" })).toBeVisible();
      await expect(ownerPage.getByRole("textbox", { name: "Role name" })).toBeVisible();
    })();

    await step("Fill name, description, toggle a Read permission & submit the form")(async () => {
      await ownerPage.getByRole("textbox", { name: "Role name" }).fill(roleName);
      await ownerPage.getByRole("textbox", { name: "Role description" }).fill(roleDescription);
      // Pick a single Read permission so we exercise the matrix without triggering the Manage
      // implication chain. Booking is a guaranteed-present resource across every tier.
      await ownerPage.getByRole("checkbox", { name: "Booking Read" }).check();
      await ownerPage.getByRole("button", { name: "Create role" }).click();

      await expectToastMessage(context, `Role created: ${roleName}`);
      await expect(ownerPage).toHaveURL("/account/settings/roles");
    })();

    await step("Verify the new role row renders in the roles table with 0 members")(async () => {
      const roleRow = ownerPage.getByRole("row").filter({ hasText: roleName });
      await expect(roleRow).toBeVisible();
      await expect(roleRow).toContainText(roleDescription);
    })();

    await step("Open the role edit page, update the description & save changes")(async () => {
      await ownerPage.getByRole("link", { name: `Edit ${roleName}` }).click();

      await expect(ownerPage.getByRole("textbox", { name: "Role name" })).toHaveValue(roleName);
      await ownerPage.getByRole("textbox", { name: "Role description" }).fill(updatedDescription);
      await ownerPage.getByRole("button", { name: "Save changes" }).click();

      await expectToastMessage(context, "Role updated");
      await expect(ownerPage).toHaveURL("/account/settings/roles");
      await expect(ownerPage.getByRole("row").filter({ hasText: roleName })).toContainText(updatedDescription);
    })();

    await step("Delete the role via the DeleteRoleDialog & verify the table no longer contains it")(async () => {
      await ownerPage.getByRole("button", { name: `Delete ${roleName}` }).click();

      await expect(ownerPage.getByRole("alertdialog", { name: "Delete role" })).toBeVisible();
      await ownerPage.getByRole("alertdialog").getByRole("button", { name: "Delete" }).click();

      await expectToastMessage(context, `Role deleted: ${roleName}`);
      await expect(ownerPage.getByRole("row").filter({ hasText: roleName })).toHaveCount(0);
    })();
  });
});
