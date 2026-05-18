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

async function createBookedEvent(
  page: Page,
  context: ReturnType<typeof createTestContext>,
  options: { requiresConfirmation?: boolean } = {}
) {
  const unique = Date.now();
  const scheduleName = `Bookings smoke schedule ${unique}`;
  const eventTitle = `Bookings smoke event ${unique}`;
  const eventSlug = `bookings-smoke-event-${unique}`;
  const bookerName = `Bookings Booker ${unique}`;
  const bookerEmail = `bookings-booker-${unique}@example.com`;
  const bookingNotes = "Bookings dashboard";
  const startTime = getNextMondayAtNine();

  await createSchedule(page, scheduleName);
  await expectToastMessage(context, "Schedule created");
  await createEventType(page, eventTitle, eventSlug);
  await expectToastMessage(context, "Event type created");
  if (options.requiresConfirmation === true) {
    await page.getByRole("tab", { name: "Advanced" }).click();
    await page.getByRole("switch", { name: "Requires confirmation" }).click();
    await page.getByRole("button", { name: "Save" }).click();
    await expectToastMessage(context, "Event type updated");
  }

  const profile = await page.evaluate(async () => {
    const response = await fetch("/api/scheduling/profile");
    if (!response.ok) throw new Error(await response.text());
    return (await response.json()) as { handle: string };
  });

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
  await page.getByRole("textbox", { name: "Additional notes" }).fill(bookingNotes);
  await page.getByRole("button", { name: "Confirm booking" }).click();
  await expect(page.getByRole("heading", { name: "Booking confirmed" })).toBeVisible();

  return { eventTitle, eventSlug, handle: profile.handle, bookerName, bookerEmail, bookingNotes, startTime };
}

test.describe("@smoke", () => {
  /**
   * Covers the Cal-like bookings dashboard shell:
   * - Status tabs with filters hidden behind a trigger
   * - Filter application with active count reflected in the toolbar and URL
   * - Quick action menu, disabled downstream actions, cancellation, list/calendar view, and details sheet
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
      await expect(ownerPage.getByTestId("active-booking-filters")).toBeVisible();
      await expect(ownerPage.getByText(`Search: ${booking.eventTitle}`)).toBeVisible();
      await expect(ownerPage.getByTestId("booking-item").filter({ hasText: booking.eventTitle }).first()).toBeVisible();
      expect(new URL(ownerPage.url()).searchParams.get("search")).toBe(booking.eventTitle);
    })();

    await step("Open booking details from list & verify selected row state")(async () => {
      const bookingRow = ownerPage.getByTestId("booking-item").filter({ hasText: booking.eventTitle }).first();
      await bookingRow.click();

      await expect(ownerPage.getByRole("dialog", { name: booking.eventTitle })).toBeVisible();
      await expect(ownerPage.locator('[data-testid="booking-item"][data-state="selected"]')).toContainText(
        booking.eventTitle
      );
      await ownerPage.keyboard.press("Escape");
    })();

    await step("Open booking actions without opening details")(async () => {
      const bookingRow = ownerPage.getByTestId("booking-item").filter({ hasText: booking.eventTitle }).first();
      await bookingRow.getByTestId("booking-actions-dropdown").click();

      await expect(ownerPage.getByText("Edit event")).toBeVisible();
      await expect(ownerPage.getByRole("menuitem", { name: "Reschedule booking" })).toBeVisible();
      await expect(ownerPage.getByRole("menuitem", { name: "Request reschedule" })).toBeVisible();
      await expect(ownerPage.getByRole("menuitem", { name: "Edit location" })).toBeVisible();
      await expect(ownerPage.getByRole("menuitem", { name: "Add guests" })).toBeVisible();
      await expect(ownerPage.getByRole("dialog", { name: booking.eventTitle })).not.toBeVisible();
      await ownerPage.keyboard.press("Escape");
    })();

    await step("Edit location and add a guest through dashboard dialogs")(async () => {
      const bookingRow = ownerPage.getByTestId("booking-item").filter({ hasText: booking.eventTitle }).first();

      await bookingRow.getByTestId("booking-actions-dropdown").click();
      await ownerPage.getByRole("menuitem", { name: "Edit location" }).click();
      await expect(ownerPage.getByRole("dialog", { name: "Edit location" })).toBeVisible();
      await ownerPage.getByRole("textbox", { name: "Location type" }).fill("in-person");
      await ownerPage.getByRole("textbox", { name: "Location", exact: true }).fill("Suite 5");
      await ownerPage.getByRole("button", { name: "Save" }).click();
      await expectToastMessage(context, "Location updated");

      await bookingRow.getByTestId("booking-actions-dropdown").click();
      await ownerPage.getByRole("menuitem", { name: "Add guests" }).click();
      await expect(ownerPage.getByRole("dialog", { name: "Add guests" })).toBeVisible();
      await ownerPage.getByRole("textbox", { name: "Attendee name" }).fill("Guest Booker");
      await ownerPage.getByRole("textbox", { name: "Attendee email" }).fill("guest-booker@example.com");
      await ownerPage.getByRole("button", { name: "Add guests" }).click();
      await expectToastMessage(context, "Guests added");

      await bookingRow.click();
      const bookingDetails = ownerPage.getByRole("dialog", { name: booking.eventTitle });
      await expect(bookingDetails.getByText("Suite 5")).toBeVisible();
      await expect(bookingDetails.getByText("guest-booker@example.com")).toBeVisible();
      await ownerPage.keyboard.press("Escape");
    })();

    await step("Create a replacement booking through direct reschedule")(async () => {
      const rescheduleReason = "Need a later time from dashboard";
      const replacementStartTime = addMinutes(booking.startTime, 30);
      const bookingRow = ownerPage.getByTestId("booking-item").filter({ hasText: booking.eventTitle }).first();
      await bookingRow.getByTestId("booking-actions-dropdown").click();
      await ownerPage.getByRole("menuitem", { name: "Reschedule booking" }).click();

      await expect(ownerPage).toHaveURL(new RegExp(`/${booking.handle}/${booking.eventSlug}`));
      const rescheduleUrl = new URL(ownerPage.url());
      expect(rescheduleUrl.searchParams.get("rescheduleUid")).toBeTruthy();
      rescheduleUrl.searchParams.set("date", formatDateOnly(replacementStartTime));
      rescheduleUrl.searchParams.set("slot", replacementStartTime.toISOString());
      rescheduleUrl.searchParams.set("duration", "30");
      rescheduleUrl.searchParams.set("timezone", "Africa/Johannesburg");

      await ownerPage.goto(`${rescheduleUrl.pathname}${rescheduleUrl.search}`);
      await expect(ownerPage.getByTestId("public-booker-form")).toBeVisible();
      await expect(ownerPage.getByRole("heading", { name: "Reschedule booking" })).toBeVisible();
      await expect(ownerPage.getByRole("textbox", { name: "Name" })).toHaveValue(booking.bookerName);
      await expect(ownerPage.getByRole("textbox", { name: "Email" })).toHaveValue(booking.bookerEmail);
      await expect(ownerPage.getByRole("textbox", { name: "Additional notes" })).toHaveValue(booking.bookingNotes);
      await ownerPage.getByRole("textbox", { name: "Reschedule reason" }).fill(rescheduleReason);
      await ownerPage.getByRole("button", { name: "Reschedule booking" }).click();
      await expect(ownerPage.getByRole("heading", { name: "Booking confirmed" })).toBeVisible();

      await ownerPage.goto(`/bookings/cancelled?view=list&search=${encodeURIComponent(booking.eventTitle)}`);
      const cancelledBookingRow = ownerPage.getByTestId("booking-item").filter({ hasText: booking.eventTitle }).first();
      await expect(cancelledBookingRow).toBeVisible();
      await cancelledBookingRow.click();
      const cancelledDetails = ownerPage.getByRole("dialog", { name: booking.eventTitle });
      await expect(cancelledDetails.getByText("This booking was marked for reschedule.")).toBeVisible();
      await expect(cancelledDetails.getByText(rescheduleReason)).toBeVisible();
      await ownerPage.keyboard.press("Escape");

      await ownerPage.goto(`/bookings/upcoming?view=list&search=${encodeURIComponent(booking.eventTitle)}`);
      await expect(ownerPage.getByTestId("booking-item").filter({ hasText: booking.eventTitle }).first()).toBeVisible();
    })();

    await step("Request reschedule through dashboard dialog")(async () => {
      const rescheduleBooking = await createBookedEvent(ownerPage, context);
      await ownerPage.goto(`/bookings/upcoming?view=list&search=${encodeURIComponent(rescheduleBooking.eventTitle)}`);
      const bookingRow = ownerPage
        .getByTestId("booking-item")
        .filter({ hasText: rescheduleBooking.eventTitle })
        .first();
      await bookingRow.getByTestId("booking-actions-dropdown").click();
      await ownerPage.getByRole("menuitem", { name: "Request reschedule" }).click();
      await expect(ownerPage.getByRole("dialog", { name: "Request reschedule" })).toBeVisible();
      await ownerPage.getByRole("textbox", { name: "Reschedule reason" }).fill("Need a different time");
      await ownerPage.getByRole("button", { name: "Request reschedule" }).click();
      await expectToastMessage(context, "Reschedule requested");

      await ownerPage.goto(`/bookings/cancelled?view=list&search=${encodeURIComponent(rescheduleBooking.eventTitle)}`);
      await expect(ownerPage.getByText(rescheduleBooking.eventTitle)).toBeVisible();
    })();

    await step("Confirm and reject pending bookings from the dashboard")(async () => {
      const confirmBooking = await createBookedEvent(ownerPage, context, { requiresConfirmation: true });
      await ownerPage.goto(`/bookings/unconfirmed?view=list&search=${encodeURIComponent(confirmBooking.eventTitle)}`);
      const confirmRow = ownerPage.getByTestId("booking-item").filter({ hasText: confirmBooking.eventTitle }).first();
      await expect(confirmRow).toBeVisible();
      await confirmRow.getByTestId("booking-actions-dropdown").click();
      await ownerPage.getByRole("menuitem", { name: "Confirm booking" }).click();
      await expect(ownerPage.getByRole("dialog", { name: "Confirm booking" })).toBeVisible();
      await ownerPage.getByRole("button", { name: "Confirm booking" }).click();
      await expectToastMessage(context, "Booking confirmed");

      await ownerPage.goto(`/bookings/upcoming?view=list&search=${encodeURIComponent(confirmBooking.eventTitle)}`);
      await expect(
        ownerPage.getByTestId("booking-item").filter({ hasText: confirmBooking.eventTitle }).first()
      ).toBeVisible();

      const rejectBooking = await createBookedEvent(ownerPage, context, { requiresConfirmation: true });
      const rejectionReason = "The requested time no longer works";
      await ownerPage.goto(`/bookings/unconfirmed?view=list&search=${encodeURIComponent(rejectBooking.eventTitle)}`);
      const rejectRow = ownerPage.getByTestId("booking-item").filter({ hasText: rejectBooking.eventTitle }).first();
      await expect(rejectRow).toBeVisible();
      await rejectRow.getByTestId("booking-actions-dropdown").click();
      await ownerPage.getByRole("menuitem", { name: "Reject booking" }).click();
      await expect(ownerPage.getByRole("dialog", { name: "Reject booking" })).toBeVisible();
      await ownerPage.getByRole("textbox", { name: "Rejection reason" }).fill(rejectionReason);
      await ownerPage.getByRole("button", { name: "Reject booking" }).click();
      await expectToastMessage(context, "Booking rejected");

      await ownerPage.goto(`/bookings/cancelled?view=list&search=${encodeURIComponent(rejectBooking.eventTitle)}`);
      const rejectedBookingRow = ownerPage
        .getByTestId("booking-item")
        .filter({ hasText: rejectBooking.eventTitle })
        .first();
      await expect(rejectedBookingRow).toBeVisible();
      await rejectedBookingRow.click();
      await expect(
        ownerPage.getByRole("dialog", { name: rejectBooking.eventTitle }).getByText(rejectionReason)
      ).toBeVisible();
      await ownerPage.keyboard.press("Escape");
    })();

    await step("Switch to calendar and navigate week & verify booking details open")(async () => {
      await ownerPage.goto(`/bookings/upcoming?view=list&search=${encodeURIComponent(booking.eventTitle)}`);
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

    await step("Cancel booking from details quick action menu")(async () => {
      const bookingDetails = ownerPage.getByRole("dialog", { name: booking.eventTitle });
      await bookingDetails.getByTestId("booking-actions-dropdown").click();
      await ownerPage.getByRole("menuitem", { name: "Cancel event" }).click();
      await expect(ownerPage.getByRole("alertdialog", { name: "Cancel event?" })).toBeVisible();
      await ownerPage.getByRole("button", { name: "Cancel event" }).click();
      await expectToastMessage(context, "Booking cancelled");
      await expect(bookingDetails).not.toBeVisible();

      await ownerPage.goto("/bookings/cancelled?view=list");
      await expect(ownerPage.getByText(booking.eventTitle)).toBeVisible();
    })();
  });
});

test.describe("@comprehensive", () => {
  /**
   * Covers responsive Cal-like bookings row behavior:
   * - Mobile list layout keeps the booking action menu reachable
   * - Disabled downstream actions keep their reason text visible
   * - Active filters remain represented outside the filter dialog
   */
  test("should keep mobile booking actions and active filters usable", async ({ ownerPage }) => {
    const context = createTestContext(ownerPage);
    const booking = await createBookedEvent(ownerPage, context);

    await ownerPage.setViewportSize({ width: 390, height: 844 });

    await step("Apply mobile search filter & verify active filter strip")(async () => {
      await ownerPage.goto("/bookings/upcoming?view=list");
      await ownerPage.getByRole("button", { name: "Filter No filters" }).click();
      await ownerPage.getByRole("textbox", { name: "Search bookings" }).fill(booking.bookerEmail);
      await ownerPage.getByRole("button", { name: "Apply filters" }).click();

      await expect(ownerPage.getByTestId("active-booking-filters")).toBeVisible();
      await expect(ownerPage.getByText(`Search: ${booking.bookerEmail}`)).toBeVisible();
      await expect(ownerPage.getByText(booking.eventTitle)).toBeVisible();
    })();

    await step("Open mobile booking actions & verify disabled reasons")(async () => {
      const bookingRow = ownerPage.getByTestId("booking-item").filter({ hasText: booking.eventTitle }).first();
      await bookingRow.getByTestId("booking-actions-dropdown").click();

      await expect(ownerPage.getByRole("menu")).toBeVisible();
      await expect(ownerPage.getByText("Edit event")).toBeVisible();
      await expect(ownerPage.getByRole("menuitem", { name: "Request reschedule" })).toBeVisible();
      await expect(ownerPage.getByRole("menuitem", { name: "Edit location" })).toBeVisible();
      await expect(ownerPage.getByRole("menuitem", { name: "Add guests" })).toBeVisible();
      await ownerPage.keyboard.press("Escape");
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

function addMinutes(date: Date, minutes: number) {
  const nextDate = new Date(date);
  nextDate.setMinutes(nextDate.getMinutes() + minutes);
  return nextDate;
}

function formatDateOnly(date: Date) {
  return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, "0")}-${String(date.getDate()).padStart(2, "0")}`;
}
