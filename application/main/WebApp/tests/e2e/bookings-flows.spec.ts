import { expect, type Page } from "@playwright/test";
import { test } from "@shared/e2e/fixtures/page-auth";
import { blurActiveElement, createTestContext, expectToastMessage } from "@shared/e2e/utils/test-assertions";
import { step } from "@shared/e2e/utils/test-step-wrapper";

async function createSchedule(page: Page, name: string) {
  await page.goto("/availability");
  await page.getByRole("button", { name: "New schedule" }).click();
  await page.getByRole("textbox", { name: "Name" }).fill(name);
  await page.getByRole("button", { name: "Continue" }).click();

  await expect(page.getByRole("textbox", { name: "Schedule name" })).toHaveValue(name);
}

async function createEventType(page: Page, title: string, slug: string) {
  await page.goto("/event-types");
  await page.getByRole("button", { name: "New event type" }).click();
  await page.getByRole("textbox", { name: "Title" }).fill(title);
  await page.getByRole("textbox", { name: "Slug" }).fill(slug);
  await page.getByRole("textbox", { name: "Duration", exact: true }).fill("30");
  await blurActiveElement(page);
  await page.getByRole("button", { name: "Continue" }).click();

  await expect(page.getByRole("textbox", { name: "Title" })).toHaveValue(title);
}

async function createBookedEvent(page: Page, context: ReturnType<typeof createTestContext>) {
  const unique = Date.now();
  const scheduleName = `Bookings smoke schedule ${unique}`;
  const eventTitle = `Bookings smoke event ${unique}`;
  const eventSlug = `bookings-smoke-event-${unique}`;
  const bookerName = `Bookings Booker ${unique}`;
  const bookerEmail = `bookings-booker-${unique}@example.com`;
  const startTime = getNextMondayAtNine();

  await createSchedule(page, scheduleName);
  await expectToastMessage(context, "Schedule created");
  await createEventType(page, eventTitle, eventSlug);
  await expectToastMessage(context, "Event type created");

  const profileResponse = await page.request.get("/api/scheduling/profile");
  expect(profileResponse.ok(), await profileResponse.text()).toBeTruthy();
  const profile = (await profileResponse.json()) as { handle: string };

  const publicBookingSearch = new URLSearchParams({
    date: formatDateOnly(startTime),
    slot: startTime.toISOString(),
    duration: "30",
    timezone: "Africa/Johannesburg"
  });
  await page.goto(`/${profile.handle}/${eventSlug}?${publicBookingSearch.toString()}`);
  await expect(page.getByTestId("public-booker-form")).toBeVisible();
  await page.getByRole("textbox", { name: "Name" }).fill(bookerName);
  await page.getByRole("textbox", { name: "Email" }).fill(bookerEmail);
  await page.getByRole("textbox", { name: "Additional notes" }).fill("Bookings dashboard");
  await page.getByRole("button", { name: "Confirm booking" }).click();
  await expect(page.getByRole("heading", { name: "Booking confirmed" })).toBeVisible();

  return { eventTitle, bookerName, bookerEmail };
}

test.describe("@smoke", () => {
  /**
   * Covers the Cal-like bookings dashboard shell:
   * - Status tabs with filters hidden behind a trigger
   * - Filter application with active count reflected in the toolbar and URL
   * - List/calendar view toggle, week navigation, calendar booking block, and details sheet
   */
  test("should show bookings filters behind a trigger and switch to calendar view", async ({ ownerPage }) => {
    const context = createTestContext(ownerPage);
    const booking = await createBookedEvent(ownerPage, context);

    await step("Open upcoming bookings & verify Cal-style list toolbar")(async () => {
      await ownerPage.goto("/bookings/upcoming");

      await expect(ownerPage.getByRole("button", { name: "Upcoming" })).toBeVisible();
      await expect(ownerPage.getByRole("button", { name: "Filter No filters" })).toBeVisible();
      await expect(ownerPage.getByRole("button", { name: "List view" })).toBeVisible();
      await expect(ownerPage.getByRole("button", { name: "Calendar view" })).toBeVisible();
      await expect(ownerPage.getByRole("dialog", { name: "Filter bookings" })).not.toBeVisible();
    })();

    await step("Apply search filter & verify active filter count")(async () => {
      await ownerPage.getByRole("button", { name: "Filter No filters" }).click();
      await expect(ownerPage.getByRole("dialog", { name: "Filter bookings" })).toBeVisible();
      await ownerPage.getByRole("textbox", { name: "Search bookings" }).fill(booking.eventTitle);
      await ownerPage.getByRole("button", { name: "Apply filters" }).click();

      await expect(ownerPage.getByRole("button", { name: "Filter 1" })).toBeVisible();
      await expect(ownerPage.getByText(booking.eventTitle)).toBeVisible();
      expect(new URL(ownerPage.url()).searchParams.get("search")).toBe(booking.eventTitle);
    })();

    await step("Switch to calendar and navigate week & verify booking details open")(async () => {
      await ownerPage.getByRole("button", { name: "Calendar view" }).click();
      await expect(ownerPage.getByTestId("bookings-calendar-view")).toBeVisible();
      expect(new URL(ownerPage.url()).searchParams.get("view")).toBe("calendar");

      await ownerPage.getByRole("button", { name: "View next week" }).click();
      await expect(ownerPage.getByTestId("bookings-calendar-view").getByText(booking.eventTitle)).toBeVisible();
      await ownerPage.getByTestId("bookings-calendar-view").getByText(booking.eventTitle).click();

      const bookingDetails = ownerPage.getByRole("dialog", { name: booking.eventTitle });
      await expect(bookingDetails).toBeVisible();
      await expect(bookingDetails.getByText(booking.bookerName)).toBeVisible();
      await expect(bookingDetails.getByText(booking.bookerEmail)).toBeVisible();
    })();
  });
});

function getNextMondayAtNine() {
  const date = new Date();
  const daysUntilMonday = (8 - date.getDay()) % 7 || 7;
  date.setDate(date.getDate() + daysUntilMonday);
  date.setHours(9, 0, 0, 0);
  return date;
}

function formatDateOnly(date: Date) {
  return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, "0")}-${String(date.getDate()).padStart(2, "0")}`;
}
