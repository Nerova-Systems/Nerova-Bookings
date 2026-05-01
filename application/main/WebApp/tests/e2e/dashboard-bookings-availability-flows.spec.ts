import { expect } from "@playwright/test";
import { test } from "@shared/e2e/fixtures/page-auth";
import { createTestContext } from "@shared/e2e/utils/test-assertions";
import { step } from "@shared/e2e/utils/test-step-wrapper";

test.describe("@smoke", () => {
  test("should expose bookings, availability, out of office, and installed payment management", async ({ ownerPage }) => {
    createTestContext(ownerPage);

    await step("Redirect calendar to Bookings calendar view")(async () => {
      await ownerPage.goto("/dashboard/calendar");

      await expect(ownerPage).toHaveURL(/\/dashboard\/bookings.*view=calendar/);
      await expect(ownerPage.getByRole("button", { name: "Calendar view" })).toBeVisible();
      await expect(ownerPage.getByText("Upcoming")).toBeVisible();
    })();

    await step("Switch Bookings to list view")(async () => {
      await ownerPage.getByRole("button", { name: "List view" }).click();

      await expect(ownerPage).toHaveURL(/\/dashboard\/bookings/);
      await expect(ownerPage.getByText("Unconfirmed")).toBeVisible();
      await expect(ownerPage.getByText("Canceled")).toBeVisible();
    })();

    await step("Open Availability and edit working hours")(async () => {
      await ownerPage.goto("/dashboard/availability");

      await expect(ownerPage.getByRole("heading", { name: "Availability" })).toBeVisible();
      const workingHoursCard = ownerPage.getByRole("button", { name: /Working hours/ });
      await expect(workingHoursCard).toBeVisible();
      await workingHoursCard.click();

      await expect(ownerPage).toHaveURL(/\/dashboard\/availability\/default/);
      await expect(ownerPage.getByRole("textbox", { name: "Schedule name" })).toHaveValue("Working hours");
      await expect(ownerPage.getByText("Timezone")).toBeVisible();
    })();

    await step("Open Out of office tabs")(async () => {
      await ownerPage.goto("/dashboard/settings/out-of-office");

      await expect(ownerPage).toHaveURL(/\/user\/out-of-office/);
      await expect(ownerPage.getByRole("heading", { name: "Out of office" })).toBeVisible();
      await expect(ownerPage.getByRole("button", { name: "My OOO" })).toBeVisible();
      await ownerPage.getByRole("button", { name: "Holidays" }).click();
      await expect(ownerPage.getByText("We will automatically mark you as unavailable")).toBeVisible();
      await expect(ownerPage.getByText("Workers' Day").first()).toBeVisible();
    })();

    await step("Redirect payments into Installed apps Payment")(async () => {
      await ownerPage.goto("/dashboard/payments");

      await expect(ownerPage).toHaveURL(/\/dashboard\/apps\/installed.*category=Payment/);
      await expect(ownerPage.getByRole("heading", { name: "Payments", exact: true })).toBeVisible();
      await expect(ownerPage.getByText("Manage Paystack payouts")).toBeVisible();
    })();
  });
});
