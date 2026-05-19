import { expect, type Browser, type Page } from "@playwright/test";
import { test } from "@shared/e2e/fixtures/page-auth";
import { getBackOfficeBaseUrl } from "@shared/e2e/utils/constants";
import {
  blurActiveElement,
  createTestContext,
  expectToastMessage,
  expectValidationError,
  selectOption
} from "@shared/e2e/utils/test-assertions";
import { logInAsAdmin } from "@shared/e2e/utils/test-data";
import { step } from "@shared/e2e/utils/test-step-wrapper";

const BACK_OFFICE_BASE_URL = getBackOfficeBaseUrl();
const SIDE_EFFECT_FLAGS = ["cal-com-core", "cal-com-workflows", "cal-com-webhooks"] as const;

async function getAntiforgeryHeaders(page: Page): Promise<{ "x-xsrf-token": string }> {
  const token = await page.evaluate(
    () => document.head.querySelector('meta[name="antiforgeryToken"]')?.getAttribute("content") ?? ""
  );
  return { "x-xsrf-token": token };
}

async function enableSideEffectFlags(browser: Browser, ownerPage: Page) {
  const backOfficeContext = await browser.newContext({ baseURL: BACK_OFFICE_BASE_URL, ignoreHTTPSErrors: true });
  const backOfficePage = await backOfficeContext.newPage();

  try {
    await backOfficePage.goto(`${BACK_OFFICE_BASE_URL}/feature-flags`);
    await logInAsAdmin(backOfficePage, `${BACK_OFFICE_BASE_URL}/feature-flags`);
    const headers = await getAntiforgeryHeaders(backOfficePage);

    for (const flagKey of SIDE_EFFECT_FLAGS) {
      const activateResponse = await backOfficePage.request.put(
        `${BACK_OFFICE_BASE_URL}/api/back-office/feature-flags/${flagKey}/activate`,
        { headers }
      );
      if (!activateResponse.ok()) {
        const flag = await getBackOfficeFeatureFlag(backOfficePage, flagKey);
        expect(flag?.isActive).toBe(true);
      }

      const rolloutResponse = await backOfficePage.request.put(
        `${BACK_OFFICE_BASE_URL}/api/back-office/feature-flags/${flagKey}/rollout-percentage`,
        { data: { rolloutPercentage: 100 }, headers }
      );
      if (!rolloutResponse.ok()) {
        const flag = await getBackOfficeFeatureFlag(backOfficePage, flagKey);
        expect(flag?.rolloutPercentage).toBe(100);
      }
    }
  } finally {
    await backOfficeContext.close();
  }

  const ownerHeaders = await getAntiforgeryHeaders(ownerPage);
  const refreshResponse = await ownerPage.request.put("/api/account/feature-flags/compact-view/user-override", {
    data: { enabled: false },
    headers: ownerHeaders
  });
  expect(refreshResponse.ok()).toBe(true);
}

async function getBackOfficeFeatureFlag(page: Page, flagKey: string) {
  const response = await page.request.get(`${BACK_OFFICE_BASE_URL}/api/back-office/feature-flags/`);
  expect(response.ok()).toBe(true);
  const body = (await response.json()) as {
    flags: { key: string; isActive: boolean; rolloutPercentage: number | null }[];
  };
  return body.flags.find((flag) => flag.key === flagKey);
}

async function createSchedule(page: Page, name: string) {
  await page.goto("/availability");
  await page.getByRole("button", { name: "New schedule" }).click();
  await page.getByRole("textbox", { name: "Name" }).fill(name);
  await page.getByRole("button", { name: "Continue" }).click();

  await expect(page.getByRole("textbox", { name: "Schedule name" })).toHaveValue(name);
}

async function createEventType(page: Page, title: string, slug: string, duration: string) {
  await page.goto("/event-types");
  await page.getByRole("button", { name: "New event type" }).click();
  await page.getByRole("textbox", { name: "Title" }).fill(title);
  await page.getByRole("textbox", { name: "Slug" }).fill(slug);
  await page.getByRole("textbox", { name: "Duration", exact: true }).fill(duration);
  await blurActiveElement(page);
  await page.getByRole("button", { name: "Continue" }).click();

  await expect(page.getByRole("textbox", { name: "Title" })).toHaveValue(title);
}

async function openEventTypeDuplicateDialog(page: Page, title: string) {
  await page.goto("/event-types");
  await page.getByRole("textbox", { name: "Search event types" }).fill(title);
  await expect(page.getByRole("link", { name: title })).toBeVisible();
  await page.getByRole("button", { name: "Duplicate event type" }).first().click();

  await expect(page.getByRole("dialog", { name: "Duplicate event type" })).toBeVisible();
}

async function deleteCurrentEventType(page: Page, title: string) {
  await page.getByRole("button", { name: "Delete" }).first().click();
  const deleteDialog = page.getByRole("alertdialog");
  await expect(deleteDialog).toBeVisible();
  await expect(deleteDialog.getByText(`This removes the booking page for ${title}.`)).toBeVisible();
  await deleteDialog.getByRole("button", { name: "Delete" }).click();

  await expect(page.getByRole("heading", { name: "Event types" })).toBeVisible();
}

test.describe("@smoke", () => {
  test("should manage workflow and webhook side-effect settings", async ({ browser, ownerPage }) => {
    const context = createTestContext(ownerPage);
    const unique = Date.now();
    const scheduleName = `Side effects schedule ${unique}`;
    const eventTitle = `Side effects event ${unique}`;
    const eventSlug = `side-effects-event-${unique}`;

    await step("Create availability schedule and event type")(async () => {
      await enableSideEffectFlags(browser, ownerPage);
      await ownerPage.reload();
      await createSchedule(ownerPage, scheduleName);
      await expectToastMessage(context, "Schedule created");
      await createEventType(ownerPage, eventTitle, eventSlug, "30");
      await expectToastMessage(context, "Event type created");
    })();

    await step("Create, edit, and delete workflow")(async () => {
      await ownerPage.getByRole("tab", { name: "Workflows" }).click();
      await expect(ownerPage.getByText("Recent deliveries")).toBeVisible();
      await expect(ownerPage.getByText("No delivery attempts yet.")).toBeVisible();
      await ownerPage.getByRole("button", { name: "Add workflow" }).click();
      await ownerPage.getByRole("textbox", { name: "Name" }).fill("Lifecycle email");
      await ownerPage.getByRole("textbox", { name: "Email subject" }).fill("Booking lifecycle update");
      await ownerPage.getByRole("textbox", { name: "Email body" }).fill("A booking changed.");
      await ownerPage.getByRole("button", { name: "Save workflow" }).click();
      await expectToastMessage(context, "Workflow saved");
      await expect(ownerPage.getByText("Lifecycle email")).toBeVisible();

      await ownerPage.getByRole("button", { name: "Edit" }).first().click();
      await ownerPage.getByRole("textbox", { name: "Name" }).fill("Lifecycle email updated");
      await ownerPage.getByRole("button", { name: "Save workflow" }).click();
      await expectToastMessage(context, "Workflow saved");
      await expect(ownerPage.getByText("Lifecycle email updated")).toBeVisible();

      await ownerPage.getByRole("button", { name: "Delete" }).nth(1).click();
      await expectToastMessage(context, "Workflow deleted");
      await expect(ownerPage.getByText("No workflows configured.")).toBeVisible();
    })();

    await step("Validate, create, test, and delete webhook")(async () => {
      await ownerPage.getByRole("tab", { name: "Webhooks" }).click();
      await expect(ownerPage.getByText("Recent deliveries")).toBeVisible();
      await expect(ownerPage.getByText("No delivery attempts yet.")).toBeVisible();
      await ownerPage.getByRole("button", { name: "Add webhook" }).click();
      await ownerPage.getByRole("textbox", { name: "Subscriber URL" }).fill("ftp://example.com/cal/webhook");
      await ownerPage.getByRole("button", { name: "Save webhook" }).click();
      await expectValidationError(context, "Webhook subscriber URL must be an HTTP or HTTPS URL.");

      await ownerPage.getByRole("textbox", { name: "Subscriber URL" }).fill("https://example.com/cal/webhook");
      await ownerPage.getByRole("button", { name: "Save webhook" }).click();
      await expectToastMessage(context, "Webhook saved");
      await expect(ownerPage.getByText("https://example.com/cal/webhook")).toBeVisible();

      await ownerPage.getByRole("button", { name: "Test" }).click();
      await expectToastMessage(context, "Test webhook queued");
      await expect(ownerPage.getByText("WEBHOOK_TEST")).toBeVisible();
      await expect(ownerPage.getByText("Pending")).toBeVisible();

      await ownerPage.getByRole("button", { name: "Delete" }).nth(1).click();
      await expectToastMessage(context, "Webhook deleted");
      await expect(ownerPage.getByText("No webhooks configured.")).toBeVisible();
    })();

    await step("Delete event type & verify cleanup completes")(async () => {
      await deleteCurrentEventType(ownerPage, eventTitle);
      await expectToastMessage(context, "Event type deleted");
    })();
  });

  /**
   * Covers the core owner event type workflow:
   * - Create availability schedule and event type through dialogs
   * - Edit setup, availability, limits, advanced, and recurring editor tabs
   * - Reload persisted detail state
   * - Duplicate and delete the duplicate, then clean up the original event type
   */
  test("should create, edit, persist, duplicate, and delete event types", async ({ ownerPage }) => {
    const context = createTestContext(ownerPage);
    const unique = Date.now();
    const scheduleName = `Event Types Smoke ${unique}`;
    const originalTitle = `Smoke consultation ${unique}`;
    const originalSlug = `smoke-consultation-${unique}`;
    const updatedTitle = `Smoke strategy session ${unique}`;
    const updatedSlug = `smoke-strategy-session-${unique}`;
    const duplicateTitle = `Smoke duplicate ${unique}`;
    const duplicateSlug = `smoke-duplicate-${unique}`;

    // === SETUP ===
    await step("Create availability schedule through dialog & verify schedule detail opens")(async () => {
      await createSchedule(ownerPage, scheduleName);

      await expectToastMessage(context, "Schedule created");
    })();

    await step("Create event type through dialog & verify detail editor opens")(async () => {
      await createEventType(ownerPage, originalTitle, originalSlug, "30");

      await expectToastMessage(context, "Event type created");
    })();

    // === EDIT EVENT TYPE ===
    await step("Update setup fields & verify setup draft changes are visible")(async () => {
      await ownerPage.getByRole("textbox", { name: "Title" }).fill(updatedTitle);
      await ownerPage.getByRole("textbox", { name: "Slug" }).fill(updatedSlug);
      await ownerPage.getByRole("textbox", { name: "Description" }).fill("A focused strategy session.");
      await ownerPage.getByRole("textbox", { name: "Duration", exact: true }).fill("45");
      await ownerPage.getByRole("textbox", { name: "Duration options" }).fill("45, 60");
      await blurActiveElement(ownerPage);
      await selectOption(ownerPage.getByLabel("Booker layout"), ownerPage, "Week");
      await selectOption(ownerPage.getByLabel("Location type"), ownerPage, "Phone");
      await ownerPage.getByRole("textbox", { name: "Primary location" }).fill("+1 555 0100");
      await ownerPage.getByRole("textbox", { name: "Location list" }).fill("phone: +1 555 0100");

      await expect(ownerPage.getByRole("textbox", { name: "Title" })).toHaveValue(updatedTitle);
      await expect(ownerPage.getByRole("textbox", { name: "Primary location" })).toHaveValue("+1 555 0100");
    })();

    await step("Update availability schedule & verify schedule summary follows selection")(async () => {
      await ownerPage.getByRole("tab", { name: "Availability" }).click();
      await selectOption(ownerPage.getByLabel("Schedule"), ownerPage, scheduleName);

      await expect(ownerPage.getByText("Schedule summary")).toBeVisible();
      await expect(ownerPage.getByRole("combobox", { name: "Schedule" })).toContainText(scheduleName);
    })();

    await step("Update limits fields & verify limit draft changes are visible")(async () => {
      await ownerPage.getByRole("tab", { name: "Limits" }).click();
      await ownerPage.getByRole("textbox", { name: "Slot interval" }).fill("15");
      await ownerPage.getByRole("textbox", { name: "Notice" }).fill("120");
      await ownerPage.getByRole("textbox", { name: "Rolling window" }).fill("30");
      await ownerPage.getByRole("textbox", { name: "Bookings per day" }).fill("4");
      await ownerPage.getByRole("textbox", { name: "Before buffer" }).fill("10");
      await ownerPage.getByRole("textbox", { name: "After buffer" }).fill("5");
      await blurActiveElement(ownerPage);

      await expect(ownerPage.getByRole("textbox", { name: "Slot interval" })).toHaveValue("15");
      await expect(ownerPage.getByRole("textbox", { name: "Bookings per day" })).toHaveValue("4");
    })();

    await step("Update advanced fields & verify advanced draft changes are visible")(async () => {
      await ownerPage.getByRole("tab", { name: "Advanced" }).click();
      await ownerPage.getByRole("switch", { name: "Hidden" }).click();
      await ownerPage.getByRole("switch", { name: "Requires confirmation" }).click();
      await ownerPage.getByRole("textbox", { name: "Success URL" }).fill("https://example.com/success");
      await ownerPage.getByRole("textbox", { name: "Cancellation URL" }).fill("https://example.com/cancel");
      await selectOption(ownerPage.getByLabel("Interface language"), ownerPage, "English");
      await ownerPage.getByRole("button", { name: "Add private link" }).click();
      await ownerPage.getByRole("textbox", { name: "Private link" }).fill("vip");
      await ownerPage.getByRole("textbox", { name: "Max uses" }).fill("2");
      await ownerPage.getByRole("button", { name: "Add booking field" }).click();
      await ownerPage.getByRole("textbox", { name: "Label" }).last().fill("Topic");
      await ownerPage.getByRole("textbox", { name: "Name" }).last().fill("topic");
      await ownerPage.getByLabel("Type").click();
      await ownerPage.getByRole("option", { name: "Select", exact: true }).click();
      await ownerPage.getByRole("switch", { name: "Required" }).click();
      await ownerPage.getByRole("button", { name: "Add option" }).click();
      await ownerPage.getByRole("textbox", { name: "Option label" }).fill("Sales");
      await ownerPage.getByRole("textbox", { name: "Option value" }).fill("sales");
      await ownerPage.getByRole("button", { name: "Add option" }).click();
      await ownerPage.getByRole("textbox", { name: "Option label" }).last().fill("Support");
      await ownerPage.getByRole("textbox", { name: "Option value" }).last().fill("support");

      await expect(ownerPage.getByRole("switch", { name: "Hidden" }).first()).toBeChecked();
      await expect(ownerPage.getByRole("textbox", { name: "Success URL" })).toHaveValue("https://example.com/success");
      await expect(ownerPage.getByRole("textbox", { name: "Private link" })).toHaveValue("vip");
      await expect(ownerPage.getByText("Topic").first()).toBeVisible();
    })();

    await step("Update recurring fields and save & verify update toast appears")(async () => {
      await ownerPage.getByRole("tab", { name: "Recurring" }).click();
      await ownerPage.getByRole("switch", { name: "Recurring event" }).click();
      await selectOption(ownerPage.getByLabel("Frequency"), ownerPage, "Monthly");
      await ownerPage.getByRole("textbox", { name: "Interval" }).fill("2");
      await ownerPage.getByRole("textbox", { name: "Occurrences" }).fill("6");
      await blurActiveElement(ownerPage);
      await ownerPage.getByRole("button", { name: "Save" }).click();

      await expectToastMessage(context, "Event type updated");
    })();

    await step("Reload event type detail & verify persisted values are visible")(async () => {
      await ownerPage.reload();
      await ownerPage.getByRole("tab", { name: "Setup" }).click();

      await expect(ownerPage.getByRole("textbox", { name: "Title" })).toHaveValue(updatedTitle);
      await expect(ownerPage.getByRole("textbox", { name: "Slug" })).toHaveValue(updatedSlug);
      await expect(ownerPage.getByRole("textbox", { name: "Duration", exact: true })).toHaveValue("45");
      await ownerPage.getByRole("tab", { name: "Advanced" }).click();
      await expect(ownerPage.getByRole("textbox", { name: "Private link" })).toHaveValue("vip");
      await expect(ownerPage.getByRole("textbox", { name: "Option value" }).first()).toHaveValue("sales");
      await ownerPage.getByRole("tab", { name: "Limits" }).click();
      await expect(ownerPage.getByRole("textbox", { name: "Slot interval" })).toHaveValue("15");
      await ownerPage.getByRole("tab", { name: "Recurring" }).click();
      await expect(ownerPage.getByRole("switch", { name: "Recurring event" })).toBeChecked();
      await expect(ownerPage.getByRole("textbox", { name: "Occurrences" })).toHaveValue("6");
    })();

    // === DUPLICATE AND DELETE ===
    await step("Duplicate event type from list & verify duplicate detail opens")(async () => {
      await openEventTypeDuplicateDialog(ownerPage, updatedTitle);
      await ownerPage.getByRole("textbox", { name: "Title" }).fill(duplicateTitle);
      await ownerPage.getByRole("textbox", { name: "Slug" }).fill(duplicateSlug);
      await ownerPage.getByRole("button", { name: "Create duplicate" }).click();

      await expectToastMessage(context, "Event type duplicated");
      await expect(ownerPage.getByRole("textbox", { name: "Title" })).toHaveValue(duplicateTitle);
    })();

    await step("Delete duplicated event type & verify list returns")(async () => {
      await deleteCurrentEventType(ownerPage, duplicateTitle);

      await expectToastMessage(context, "Event type deleted");
    })();

    await step("Delete original event type & verify cleanup completes")(async () => {
      await ownerPage.getByRole("textbox", { name: "Search event types" }).fill(updatedTitle);
      await ownerPage.getByRole("link", { name: updatedTitle }).click();
      await deleteCurrentEventType(ownerPage, updatedTitle);

      await expectToastMessage(context, "Event type deleted");
    })();
  });
});

test.describe("@comprehensive", () => {
  /**
   * Covers event type hardening surfaces:
   * - Duplicate slug validation through the duplicate dialog
   * - Mobile editor tab navigation and save action
   * - Dependency placeholder tab rendering without unavailable API calls
   */
  test("should handle duplicate validation, mobile editing, and dependency placeholders", async ({ ownerPage }) => {
    const context = createTestContext(ownerPage);
    const unique = Date.now();
    const scheduleName = `Event Types Comprehensive ${unique}`;
    const originalTitle = `Comprehensive consult ${unique}`;
    const originalSlug = `comprehensive-consult-${unique}`;
    const duplicateTitle = `Comprehensive duplicate ${unique}`;
    const duplicateSlug = `comprehensive-duplicate-${unique}`;

    // === SETUP ===
    await step("Create availability schedule through dialog & verify schedule detail opens")(async () => {
      await createSchedule(ownerPage, scheduleName);

      await expectToastMessage(context, "Schedule created");
    })();

    await step("Create event type through dialog & verify detail editor opens")(async () => {
      await createEventType(ownerPage, originalTitle, originalSlug, "25");

      await expectToastMessage(context, "Event type created");
    })();

    // === VALIDATION ===
    await step("Submit duplicate with existing slug & verify validation message")(async () => {
      await openEventTypeDuplicateDialog(ownerPage, originalTitle);
      await ownerPage.getByRole("textbox", { name: "Title" }).fill(duplicateTitle);
      await ownerPage.getByRole("textbox", { name: "Slug" }).fill(originalSlug);
      await ownerPage.getByRole("button", { name: "Create duplicate" }).click();

      await expectToastMessage(context, 400, `An event type with slug '${originalSlug}' already exists.`);
      await expectValidationError(context, `An event type with slug '${originalSlug}' already exists.`);
    })();

    await step("Correct duplicate slug & verify duplicate detail opens")(async () => {
      await ownerPage.getByRole("textbox", { name: "Slug" }).fill(duplicateSlug);
      await ownerPage.getByRole("button", { name: "Create duplicate" }).click();

      await expectToastMessage(context, "Event type duplicated");
      await expect(ownerPage.getByRole("textbox", { name: "Title" })).toHaveValue(duplicateTitle);
    })();

    // === MOBILE AND DEPENDENCIES ===
    await step("Open editor at mobile width and update limits & verify save remains usable")(async () => {
      await ownerPage.setViewportSize({ width: 390, height: 844 });
      await ownerPage.getByRole("tab", { name: "Limits" }).click();
      await ownerPage.getByRole("textbox", { name: "Notice" }).fill("90");
      await blurActiveElement(ownerPage);
      await expect(ownerPage.getByRole("button", { name: "Save" })).toBeEnabled();
      await ownerPage.getByRole("button", { name: "Save" }).click();

      await expectToastMessage(context, "Event type updated");
    })();

    await step("Open dependency tab & verify disabled placeholder states render")(async () => {
      await ownerPage.getByRole("tab", { name: "Dependencies" }).click();

      await expect(ownerPage.getByText("Requires another booking")).toBeVisible();
      await expect(ownerPage.getByText("Blocks other event types")).toBeVisible();
      await expect(ownerPage.getByText("Managed relationship rules")).toBeVisible();
      await expect(ownerPage.getByText("Not available").first()).toBeVisible();
    })();

    // === CLEANUP ===
    await step("Delete duplicated event type & verify list returns")(async () => {
      await deleteCurrentEventType(ownerPage, duplicateTitle);

      await expectToastMessage(context, "Event type deleted");
    })();

    await step("Delete original event type & verify cleanup completes")(async () => {
      await ownerPage.setViewportSize({ width: 1280, height: 720 });
      await ownerPage.goto("/event-types");
      await ownerPage.getByRole("textbox", { name: "Search event types" }).fill(originalTitle);
      await ownerPage.getByRole("link", { name: originalTitle }).click();
      await deleteCurrentEventType(ownerPage, originalTitle);

      await expectToastMessage(context, "Event type deleted");
    })();
  });
});
