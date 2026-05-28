import { expect } from "@playwright/test";
import { test } from "@shared/e2e/fixtures/page-auth";
import { activateBaseFlags, setAdminTenantOverrides } from "@shared/e2e/utils/feature-flag-helpers";
import { createTestContext, expectToastMessage } from "@shared/e2e/utils/test-assertions";
import { step } from "@shared/e2e/utils/test-step-wrapper";

// Teams admin is gated on the tier-teams flag which is the root of the tier dependency chain (so no
// parents to activate). The reconciler still creates the row inactive, so an admin must Activate
// the base row before the owner can flip the tenant override. Both calls are idempotent so parallel
// workers don't fight each other.
const FLAG_CHAIN = ["tier-teams"] as const;

test.describe("@smoke", () => {
  test.afterEach(async ({ ownerPage, browser }) => {
    await setAdminTenantOverrides(browser, ownerPage, [...FLAG_CHAIN].reverse(), false);
  });

  /**
   * TEAM SETTINGS HAPPY PATH
   *
   * Exercises the tier-teams admin surface:
   * - Activate tier-teams and turn on the tenant override
   * - Navigate to /account/settings/teams and verify the empty state
   * - Create a team via the /new form (slug auto-fills from name)
   * - Verify routing to the team detail page and that General + Members tabs render
   * - Delete the team via the DeleteTeamDialog (requires typing the team name to confirm)
   * - Verify the teams list returns to empty
   */
  test("should create and delete a team via the team settings UI", async ({ ownerPage, browser }) => {
    const context = createTestContext(ownerPage);
    const unique = Date.now();
    const teamName = `e2e-team-${unique}`;

    await step("Activate tier-teams in back-office & enable the tenant override")(async () => {
      await activateBaseFlags(browser, FLAG_CHAIN);
      await setAdminTenantOverrides(browser, ownerPage, FLAG_CHAIN, true);
    })();

    await step("Navigate to /account/settings/teams & verify the empty state renders")(async () => {
      await ownerPage.goto("/account/settings/teams");

      await expect(ownerPage.getByRole("heading", { name: "Teams" })).toBeVisible();
      await expect(ownerPage.getByRole("link", { name: "Create a team" })).toBeVisible();
    })();

    await step("Open the New team form & fill out the team name field")(async () => {
      await ownerPage.getByRole("link", { name: "Create a team" }).click();

      await expect(ownerPage).toHaveURL("/account/settings/teams/new");
      await ownerPage.getByRole("textbox", { name: "Team name" }).fill(teamName);
    })();

    await step("Submit the form & verify routing to the team detail page with success toast")(async () => {
      await ownerPage.getByRole("button", { name: "Create team" }).click();

      await expectToastMessage(context, `Team created: ${teamName}`);
      await expect(ownerPage).toHaveURL(/\/account\/settings\/teams\/[^/]+$/);
      await expect(ownerPage.getByRole("heading", { name: teamName })).toBeVisible();
      await expect(ownerPage.getByRole("tab", { name: "General" })).toBeVisible();
      await expect(ownerPage.getByRole("tab", { name: "Members" })).toBeVisible();
    })();

    await step("Delete the team via the DeleteTeamDialog & verify the list returns to empty")(async () => {
      await ownerPage.getByRole("button", { name: "Delete team" }).click();

      await expect(ownerPage.getByRole("alertdialog", { name: "Delete team" })).toBeVisible();
      // DeleteTeamDialog blocks the Delete action until the user types the team name. We type into
      // the visible "Confirm team name" textbox; the AlertDialogAction button becomes enabled.
      await ownerPage.getByRole("textbox", { name: "Confirm team name" }).fill(teamName);
      await ownerPage.getByRole("alertdialog").getByRole("button", { name: "Delete" }).click();

      await expectToastMessage(context, `Team deleted: ${teamName}`);
      await expect(ownerPage).toHaveURL("/account/settings/teams");
      await expect(ownerPage.getByRole("row").filter({ hasText: teamName })).toHaveCount(0);
    })();
  });
});
