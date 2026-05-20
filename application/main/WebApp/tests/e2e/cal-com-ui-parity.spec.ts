import { expect, type Page, type TestInfo } from "@playwright/test";
import { test } from "@shared/e2e/fixtures/page-auth";

const publicBookerStates = [
  "unavailable",
  "selecting-date",
  "selecting-time",
  "booking-form",
  "reschedule-form",
  "success"
] as const;

const eventTypesListStates = ["default", "empty", "loading"] as const;

const eventTypeEditorStates = [
  "setup",
  "availability",
  "limits",
  "advanced",
  "recurring",
  "apps",
  "workflows",
  "webhooks"
] as const;

const bookingDashboardStates = [
  "list-upcoming",
  "list-unconfirmed",
  "list-past",
  "list-cancelled",
  "list-empty",
  "list-loading",
  "list-error",
  "calendar-week",
  "calendar-empty",
  "calendar-selected",
  "details-info",
  "details-actions",
  "details-reschedule"
] as const;

const viewports = [
  { name: "desktop", width: 1440, height: 900 },
  { name: "tablet", width: 834, height: 1112 },
  { name: "mobile", width: 390, height: 844 }
] as const;

const eventTypeEditorViewports = viewports.filter((viewport) => viewport.name !== "tablet");

for (const viewport of eventTypeEditorViewports) {
  test.describe(`@smoke event types list visual parity ${viewport.name}`, () => {
    test.describe.configure({ mode: "serial" });
    test.use({ viewport: { width: viewport.width, height: viewport.height } });

    for (const state of eventTypesListStates) {
      test(`captures event types list ${state}`, async ({ page }, testInfo) => {
        await page.goto(`/cal-com-ui-parity?surface=event-types-list&state=${state}`);
        await expect(page.getByTestId("cal-com-ui-parity-fixture")).toBeVisible();
        await expect(page.getByTestId("event-types-list-layout")).toBeVisible();
        await expect(page.getByRole("heading", { name: "Event types" })).toBeVisible();
        await expect(page.getByText("Configure different events for people to book on your calendar.")).toBeVisible();
        await expect(page.getByRole("button", { name: "New" })).toBeVisible();

        if (state === "empty") {
          await expect(page.getByText("No event types yet")).toBeVisible();
        } else if (state === "loading") {
          await expect(page.getByText("Loading event types...")).toBeVisible();
        } else {
          await expect(page.getByRole("textbox", { name: "Search" })).toBeVisible();
          await expect(page.getByRole("heading", { name: "Product Consultation" })).toBeVisible();
          await expect(page.getByText("/visual/product-consultation")).toBeVisible();
          await expect(page.getByText("30m")).toBeVisible();
          await expect(page.getByLabel("Hide event type from profile").first()).toBeVisible();
          if (viewport.name !== "mobile") {
            await expect(page.getByLabel("Preview booking page").first()).toBeVisible();
          }
          await expect(
            viewport.name === "mobile"
              ? page.getByLabel("Event type actions").last()
              : page.getByLabel("Event type actions").first()
          ).toBeVisible();
        }

        await captureScreenshot(page, testInfo, "wave-1-14", `event-types-list-${state}-${viewport.name}.png`);
      });
    }
  });
}

for (const viewport of viewports) {
  test.describe(`@smoke public booker visual parity ${viewport.name}`, () => {
    test.describe.configure({ mode: "serial" });
    test.use({ viewport: { width: viewport.width, height: viewport.height } });

    for (const state of publicBookerStates) {
      test(`captures public booker ${state}`, async ({ page }, testInfo) => {
        await page.route("**/api/public/bookings", async (route) => {
          await route.fulfill({
            status: 200,
            contentType: "application/json",
            body: JSON.stringify({
              id: "book_visual_success",
              startTime: "2026-06-15T09:00:00.000Z",
              endTime: "2026-06-15T09:30:00.000Z",
              status: "confirmed"
            })
          });
        });

        await page.goto(`/cal-com-ui-parity?surface=public-booker&state=${state}`);
        await expect(page.getByTestId("cal-com-ui-parity-fixture")).toBeVisible();
        if (state === "unavailable") {
          await expect(page.getByText("Booking page unavailable")).toBeVisible();
        } else {
          await expect(page.getByTestId("booker-container")).toBeVisible();
        }

        if (state === "success") {
          await submitPublicBookerForm(page);
          await expect(page.getByRole("heading", { name: "Booking confirmed" })).toBeVisible();
        }

        await captureScreenshot(page, testInfo, "wave-1-7", `public-booker-${state}-${viewport.name}.png`);
      });
    }
  });
}

for (const viewport of eventTypeEditorViewports) {
  test.describe(`@smoke event type editor visual parity ${viewport.name}`, () => {
    test.describe.configure({ mode: "serial" });
    test.use({ viewport: { width: viewport.width, height: viewport.height } });

    for (const state of eventTypeEditorStates) {
      test(`captures event type editor ${state}`, async ({ page }, testInfo) => {
        await mockEventTypeSideEffectApis(page);
        await page.goto(`/cal-com-ui-parity?surface=event-type-editor&state=${state}`);
        await expect(page.getByTestId("cal-com-ui-parity-fixture")).toBeVisible();
        await expect(page.getByTestId("event-type-layout")).toBeVisible();
        await expect(page.getByRole("heading", { name: "Product Consultation" })).toBeVisible();
        for (const tabName of [
          "Basics",
          "Availability",
          "Limits",
          "Advanced",
          "Recurring",
          "Apps",
          "Workflows",
          "Webhooks"
        ]) {
          await expect(page.getByRole("button", { name: new RegExp(tabName) })).toBeVisible();
        }
        if (state === "advanced") {
          await expect(page.getByText("Calendar event name")).toBeVisible();
          await expect(page.getByText("Add to calendar")).toBeVisible();
          await expect(page.getByText("Layout")).toBeVisible();
          await expect(page.getByText("Booking questions")).toBeVisible();
          await expect(page.getByText("Require cancellation reason").first()).toBeVisible();
          await expect(page.getByText("Disable rescheduling", { exact: true })).toBeVisible();
        }
        if (viewport.name === "mobile") {
          await expect(page.getByRole("button", { name: "Event type actions" })).toBeVisible();
        }

        await captureScreenshot(page, testInfo, "wave-1-14", `event-type-editor-${state}-${viewport.name}.png`);
      });
    }
  });
}

for (const viewport of viewports) {
  test.describe(`@smoke booking dashboard visual parity ${viewport.name}`, () => {
    test.describe.configure({ mode: "serial" });
    test.use({ viewport: { width: viewport.width, height: viewport.height } });

    for (const state of bookingDashboardStates) {
      test(`captures booking dashboard ${state}`, async ({ page }, testInfo) => {
        await page.goto(`/cal-com-ui-parity?surface=booking-dashboard&state=${state}`);
        await expect(page.getByTestId("cal-com-ui-parity-fixture")).toBeVisible();
        await expect(page.getByTestId("booking-dashboard-fixture")).toBeVisible();

        if (state.startsWith("calendar")) {
          await expect(page.getByTestId("bookings-calendar-view")).toBeVisible();
        } else if (state.startsWith("details")) {
          await expect(page.getByText("Product Consultation").first()).toBeVisible();
          if (state === "details-actions") {
            await page.getByTestId("booking-actions-dropdown").last().click();
            await expect(page.getByText("Edit event")).toBeVisible();
          }
        } else if (state === "list-error") {
          await expect(page.getByText("Bookings could not be loaded")).toBeVisible();
        } else {
          await expect(page.getByTestId("booking-list-dashboard")).toBeVisible();
        }

        await captureScreenshot(page, testInfo, "wave-1-8", `booking-dashboard-${state}-${viewport.name}.png`);
      });
    }
  });
}

async function submitPublicBookerForm(page: Page) {
  await page.getByRole("textbox", { name: "Name" }).fill("Visual Booker");
  await page.getByRole("textbox", { name: "Email" }).fill("visual-booker@example.com");
  await page.getByRole("button", { name: "Confirm booking" }).click();
}

async function mockEventTypeSideEffectApis(page: Page) {
  await page.route("**/api/connectors/core/accounts", async (route) => {
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({
        accounts: [
          {
            accountEmail: "owner@example.com",
            calendars: [
              { externalId: "primary", name: "Primary calendar", primary: true },
              { externalId: "team-product", name: "Team product", primary: false }
            ],
            displayName: "Nerova Product",
            id: "cred_visual_google",
            integration: "google-calendar"
          },
          {
            accountEmail: "owner@example.com",
            calendars: [],
            displayName: "Nerova Zoom",
            id: "cred_visual_zoom",
            integration: "zoom-video"
          }
        ],
        integrations: [
          { configured: true, integration: "google-calendar" },
          { configured: true, integration: "office365-calendar" },
          { configured: true, integration: "zoom-video" }
        ]
      })
    });
  });
  await page.route("**/api/event-types/etype_visual_product_consultation/workflows", async (route) => {
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({
        workflows: [
          {
            active: true,
            id: "wf_visual_confirmation",
            name: "Booking confirmation",
            scheduledOffsetMinutes: null,
            steps: [{ kind: "email", recipient: "booker", subject: "Your booking is confirmed", body: null }],
            trigger: "BOOKING_CONFIRMED"
          }
        ]
      })
    });
  });
  await page.route("**/api/event-types/etype_visual_product_consultation/webhooks", async (route) => {
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({
        webhooks: [
          {
            active: true,
            id: "wh_visual_booking_created",
            payloadFormat: "cal-com",
            payloadVersion: "v1",
            secret: "visual-secret",
            subscriberUrl: "https://example.com/cal-webhook",
            triggers: ["BOOKING_CREATED", "BOOKING_RESCHEDULED"]
          }
        ]
      })
    });
  });
  await page.route("**/api/event-types/etype_visual_product_consultation/side-effect-deliveries", async (route) => {
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({
        deliveries: [
          {
            attempts: 1,
            bookingId: "book_visual_success",
            createdAt: "2026-06-15T09:00:00.000Z",
            id: "delivery_visual_email",
            kind: "email",
            status: "sent",
            trigger: "BOOKING_CONFIRMED"
          },
          {
            attempts: 2,
            bookingId: "book_visual_success",
            createdAt: "2026-06-15T09:00:00.000Z",
            id: "delivery_visual_webhook",
            kind: "webhook",
            status: "pending",
            trigger: "BOOKING_CREATED"
          }
        ]
      })
    });
  });
}

async function captureScreenshot(page: Page, testInfo: TestInfo, wave: string, name: string) {
  const screenshotPath = testInfo.outputPath("cal-com-ui-parity", wave, name);
  await page.screenshot({ path: screenshotPath, fullPage: false });
  await testInfo.attach(name, { path: screenshotPath, contentType: "image/png" });
}
