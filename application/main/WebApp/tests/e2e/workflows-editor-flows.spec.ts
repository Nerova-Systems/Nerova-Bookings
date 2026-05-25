import { expect } from "@playwright/test";
import { test } from "@shared/e2e/fixtures/page-auth";
import { activateBaseFlags, setOwnerTenantOverrides } from "@shared/e2e/utils/feature-flag-helpers";
import { createTestContext, expectToastMessage } from "@shared/e2e/utils/test-assertions";
import { step } from "@shared/e2e/utils/test-step-wrapper";

// cap-workflows is gated behind the tier-enterprise chain. The full activate/override sequence must
// be applied for the workflows route to resolve. AppGateway routes /api/account/* to the account
// WebApp so the owner-side override endpoint is reachable from the main-app baseURL.
const FLAG_CHAIN = ["tier-teams", "tier-organizations", "tier-enterprise", "cap-workflows"] as const;

test.describe("@smoke", () => {
  test.afterEach(async ({ ownerPage }) => {
    await setOwnerTenantOverrides(ownerPage, [...FLAG_CHAIN].reverse(), false);
  });

  /**
   * WORKFLOWS EDITOR HAPPY PATH
   *
   * Exercises the cap-workflows surface:
   * - Activate the full tier-teams → tier-organizations → tier-enterprise → cap-workflows chain
   * - Navigate to /workflows and verify the empty state + New workflow button
   * - Create a workflow via CreateWorkflowDialog (name + default NewEvent trigger)
   * - Verify routing to /workflows/$workflowId on success
   * - Open DeleteWorkflowDialog from the detail page, type the workflow name to confirm, and delete
   * - Verify the list returns to empty
   */
  test("should create and delete a workflow via the workflows editor UI", async ({ ownerPage, browser }) => {
    const context = createTestContext(ownerPage);
    const unique = Date.now();
    const workflowName = `e2e-workflow-${unique}`;

    await step("Activate the full tier chain + cap-workflows & enable every tenant override")(async () => {
      await activateBaseFlags(browser, FLAG_CHAIN);
      await setOwnerTenantOverrides(ownerPage, FLAG_CHAIN, true);
    })();

    await step("Navigate to /workflows & verify the empty state with the New workflow CTA")(async () => {
      await ownerPage.goto("/workflows");

      await expect(ownerPage.getByRole("heading", { name: "Workflows" })).toBeVisible();
      await expect(ownerPage.getByRole("button", { name: "New workflow" })).toBeVisible();
    })();

    await step("Open the Create dialog, set the workflow name & submit")(async () => {
      await ownerPage.getByRole("button", { name: "New workflow" }).click();

      await expect(ownerPage.getByRole("dialog", { name: "New workflow" })).toBeVisible();
      // The dialog seeds the Name field with "Untitled workflow"; fill() clears+types so the
      // controlled state lands on our unique name.
      await ownerPage.getByRole("textbox", { name: "Name" }).fill(workflowName);
      // Leave the Trigger select on its default (NewEvent) — the happy path doesn't need to
      // exercise the select control, just confirm submit routes to the detail page.
      await ownerPage.getByRole("button", { name: "Create" }).click();

      await expectToastMessage(context, "Workflow created");
      await expect(ownerPage).toHaveURL(/\/workflows\/[^/]+$/);
      await expect(ownerPage.getByRole("heading", { name: workflowName })).toBeVisible();
    })();

    await step("Delete the workflow & verify routing back to the empty workflows list")(async () => {
      await ownerPage.getByRole("button", { name: "Delete", exact: true }).click();

      await expect(ownerPage.getByRole("alertdialog", { name: "Delete workflow?" })).toBeVisible();
      // DeleteWorkflowDialog blocks the Delete action until the typed name matches the workflow.
      await ownerPage.getByRole("alertdialog").getByRole("textbox", { name: "Workflow name" }).fill(workflowName);
      await ownerPage.getByRole("alertdialog").getByRole("button", { name: "Delete" }).click();

      await expectToastMessage(context, "Workflow deleted");
      await expect(ownerPage).toHaveURL("/workflows");
      await expect(ownerPage.getByText(workflowName)).toHaveCount(0);
    })();
  });
});
