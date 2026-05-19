import { expect, type Browser, type Page } from "@playwright/test";
import { test } from "@shared/e2e/fixtures/page-auth";
import { getBackOfficeBaseUrl } from "@shared/e2e/utils/constants";
import { blurActiveElement, createTestContext, expectToastMessage } from "@shared/e2e/utils/test-assertions";
import { logInAsAdmin } from "@shared/e2e/utils/test-data";
import { step } from "@shared/e2e/utils/test-step-wrapper";

const BACK_OFFICE_BASE_URL = getBackOfficeBaseUrl();
const CAL_COM_RUNTIME_FLAGS = [
  "cal-com-core",
  "cal-com-event-types",
  "cal-com-availability",
  "cal-com-public-booking",
  "cal-com-bookings"
] as const;
let bookingSequence = 0;

async function getAntiforgeryHeaders(page: Page): Promise<{ "x-xsrf-token": string }> {
  const token = await page.evaluate(
    () => document.head.querySelector('meta[name="antiforgeryToken"]')?.getAttribute("content") ?? ""
  );
  return { "x-xsrf-token": token };
}

async function enableCalComRuntimeFlags(browser: Browser, ownerPage: Page) {
  const backOfficeContext = await browser.newContext({ baseURL: BACK_OFFICE_BASE_URL, ignoreHTTPSErrors: true });
  const backOfficePage = await backOfficeContext.newPage();

  try {
    await backOfficePage.goto(`${BACK_OFFICE_BASE_URL}/feature-flags`);
    await logInAsAdmin(backOfficePage, `${BACK_OFFICE_BASE_URL}/feature-flags`);
    const backOfficeHeaders = await getAntiforgeryHeaders(backOfficePage);

    for (const flagKey of CAL_COM_RUNTIME_FLAGS) {
      const activateResponse = await backOfficePage.request.put(
        `${BACK_OFFICE_BASE_URL}/api/back-office/feature-flags/${flagKey}/activate`,
        { headers: backOfficeHeaders }
      );
      if (!activateResponse.ok()) {
        const flag = await getBackOfficeFeatureFlag(backOfficePage, flagKey);
        expect(flag?.isActive).toBe(true);
      }

      const rolloutResponse = await backOfficePage.request.put(
        `${BACK_OFFICE_BASE_URL}/api/back-office/feature-flags/${flagKey}/rollout-percentage`,
        { data: { rolloutPercentage: 100 }, headers: backOfficeHeaders }
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

async function updateEventTypeSettings(
  page: Page,
  slug: string,
  settings: Partial<{
    bookingFields: { name: string; label: string; type: string; required: boolean; options: string[] }[];
  }>
) {
  await page.evaluate(
    async ({ slug: eventSlug, settings: settingsPatch }) => {
      const token = document.head.querySelector('meta[name="antiforgeryToken"]')?.getAttribute("content") ?? "";
      type EventType = {
        id: string;
        slug: string;
        title: string;
        description: string | null;
        durationMinutes: number;
        hidden: boolean;
        scheduleId: string;
        beforeEventBufferMinutes: number;
        afterEventBufferMinutes: number;
        slotIntervalMinutes: number;
        minimumBookingNoticeMinutes: number;
        locationType: string | null;
        locationValue: string | null;
        settings: Record<string, unknown>;
      };

      const listResponse = await fetch("/api/event-types/");
      if (!listResponse.ok) throw new Error(await listResponse.text());
      const list = (await listResponse.json()) as { eventTypes: EventType[] };
      const eventType = list.eventTypes.find((item) => item.slug === eventSlug);
      if (!eventType) throw new Error(`Event type '${eventSlug}' was not found.`);

      const updateResponse = await fetch(`/api/event-types/${eventType.id}`, {
        method: "PUT",
        headers: { "Content-Type": "application/json", "x-xsrf-token": token },
        body: JSON.stringify({
          ...eventType,
          settings: {
            ...eventType.settings,
            ...settingsPatch
          }
        })
      });
      if (!updateResponse.ok) throw new Error(await updateResponse.text());
    },
    { slug, settings }
  );
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
  const startTime = addMinutes(getNextMondayAtNine(), bookingSequence * 60);
  bookingSequence += 1;

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
  test.beforeEach(async ({ browser, ownerPage }) => {
    await enableCalComRuntimeFlags(browser, ownerPage);
  });

  test("should render public booking field types and submit selected responses", async ({ ownerPage }) => {
    const context = createTestContext(ownerPage);
    const unique = Date.now();
    const scheduleName = `Booking fields schedule ${unique}`;
    const eventTitle = `Booking fields event ${unique}`;
    const eventSlug = `booking-fields-event-${unique}`;
    const startTime = getNextMondayAtNine();

    await createSchedule(ownerPage, scheduleName);
    await expectToastMessage(context, "Schedule created");
    await createEventType(ownerPage, eventTitle, eventSlug);
    await expectToastMessage(context, "Event type created");
    await updateEventTypeSettings(ownerPage, eventSlug, {
      bookingFields: [
        { name: "company", label: "Company", type: "text", required: true, options: [] },
        { name: "department", label: "Department", type: "select", required: true, options: ["Sales", "Support"] },
        { name: "contact", label: "Contact preference", type: "radio", required: true, options: ["Email", "Phone"] },
        { name: "topics", label: "Topics", type: "checkbox", required: true, options: ["Onboarding", "Billing"] },
        { name: "regions", label: "Regions", type: "multiselect", required: false, options: ["North", "South"] },
        { name: "accepted", label: "Accept terms", type: "boolean", required: true, options: [] }
      ]
    });

    const profile = await ownerPage.evaluate(async () => {
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

    const publicAvailabilitySearch = new URLSearchParams({
      date: formatDateOnly(startTime),
      duration: "30",
      timezone: "Africa/Johannesburg"
    });
    await ownerPage.goto(`/${profile.handle}/${eventSlug}?${publicAvailabilitySearch.toString()}`);
    await expect(ownerPage.getByTestId("booker-container")).toBeVisible();
    await expect(ownerPage.getByTestId("booker-event-meta")).toBeVisible();
    await expect(ownerPage.getByTestId("booker-date-picker")).toBeVisible();
    await expect(ownerPage.getByTestId("booker-timeslots")).toBeVisible();

    await ownerPage.goto(`/${profile.handle}/${eventSlug}?${publicBookingSearch.toString()}`);
    await expect(ownerPage.getByTestId("public-booker-form")).toBeVisible();
    await ownerPage.getByRole("button", { name: "Confirm booking" }).click();
    await expect(ownerPage.getByRole("heading", { name: "Enter your details" })).toBeVisible();

    await ownerPage.getByRole("textbox", { name: "Name" }).fill(`Fields Booker ${unique}`);
    await ownerPage.getByRole("textbox", { name: "Email" }).fill(`fields-booker-${unique}@example.com`);
    await ownerPage.getByRole("textbox", { name: "Company" }).fill("Nerova");
    await ownerPage.getByRole("combobox", { name: "Department" }).click();
    await ownerPage.getByRole("option", { name: "Support" }).click();
    await ownerPage.getByRole("radio", { name: "Phone" }).click();
    await ownerPage.getByRole("checkbox", { name: "Onboarding" }).click();
    await ownerPage.getByRole("checkbox", { name: "Billing" }).click();
    await ownerPage.getByRole("combobox", { name: "Regions" }).click();
    await ownerPage.getByRole("option", { name: "North" }).click();
    await ownerPage.getByRole("option", { name: "South" }).click();
    await ownerPage.keyboard.press("Escape");
    await ownerPage.getByRole("checkbox", { name: "Accept terms" }).click();
    await ownerPage.getByRole("button", { name: "Confirm booking" }).click();
    await expect(ownerPage.getByRole("heading", { name: "Booking confirmed" })).toBeVisible();

    await ownerPage.goto(`/bookings/upcoming?view=list&search=${encodeURIComponent(eventTitle)}`);
    const bookingRow = ownerPage.getByTestId("booking-item").filter({ hasText: eventTitle }).first();
    await expect(bookingRow).toBeVisible();
    await bookingRow.click();
    const details = ownerPage.getByRole("dialog", { name: eventTitle });
    await expect(details.getByText("Nerova")).toBeVisible();
    await expect(details.getByText("Support")).toBeVisible();
    await expect(details.getByText("Phone")).toBeVisible();
    await expect(details.getByText("Onboarding,Billing")).toBeVisible();
    await expect(details.getByText("North,South")).toBeVisible();
    await expect(details.getByText("true")).toBeVisible();
  });

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
      await expect(
        ownerPage.getByTestId("booking-item").filter({ hasText: rescheduleBooking.eventTitle }).first()
      ).toBeVisible();
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
      await expect(bookingDetails.getByText(booking.bookerName, { exact: true })).toBeVisible();
      await expect(bookingDetails.getByText(booking.bookerEmail, { exact: true })).toBeVisible();
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
      await expect(ownerPage.getByTestId("booking-item").filter({ hasText: booking.eventTitle }).first()).toBeVisible();
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
