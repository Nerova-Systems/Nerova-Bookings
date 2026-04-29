import { expect } from "@playwright/test";
import { test } from "@shared/e2e/fixtures/page-auth";
import { createTestContext } from "@shared/e2e/utils/test-assertions";
import { step } from "@shared/e2e/utils/test-step-wrapper";

test.describe("@smoke", () => {
  /**
   * Tests the public landing page for unauthenticated users.
   * Covers:
   * - Landing page loads correctly at root URL
   * - Nerova WhatsApp-first positioning, pricing, and call-to-action buttons are visible
   * - Navigation links (login, signup) work correctly
   * - Enterprise is presented as coming soon, not as a live checkout path
   */
  test("should display landing page with navigation for unauthenticated users", async ({ page }) => {
    createTestContext(page);

    await step("Navigate to landing page & verify Nerova content")(async () => {
      await page.goto("/");

      await expect(page.getByRole("heading", { name: /Bookings, reminders, and payments/i })).toBeVisible();
      await expect(page.getByText("Nerova", { exact: true }).first()).toBeVisible();
      await expect(page.getByText(/not an AI chatbot/i).first()).toBeVisible();
      await expect(page.getByText(/fixed WhatsApp flows/i).first()).toBeVisible();
      await expect(page.getByText("PlatformPlatform")).toHaveCount(0);
    })();

    await step("Verify product positioning sections")(async () => {
      await expect(
        page.getByRole("heading", { name: "Built for the way appointment businesses already communicate" })
      ).toBeVisible();
      await expect(page.getByText("WhatsApp-first, not web-form first")).toBeVisible();
      await expect(page.getByText("Fixed flows, not an AI chatbot")).toBeVisible();
      await expect(page.getByText("Account and workspace foundation")).toBeVisible();
      await expect(page.getByText("Users and team controls")).toBeVisible();
      await expect(page.getByText("Subscription operations")).toBeVisible();
    })();

    await step("Verify integrations and payment roadmap language")(async () => {
      await expect(page.getByText("Google Calendar")).toBeVisible();
      await expect(page.getByText("Google Contacts")).toBeVisible();
      await expect(page.getByText("Gmail")).toBeVisible();
      await expect(page.getByText("Microsoft Calendar")).toBeVisible();
      await expect(page.getByText("Nango-powered connectors")).toBeVisible();
      await expect(page.getByText("PayFast")).toBeVisible();
      await expect(page.getByText("Stitch")).toBeVisible();
      await expect(page.getByText("Google Pay")).toBeVisible();
      await expect(page.getByText("Apple Pay")).toBeVisible();
      await expect(page.getByText("Capitec Pay")).toBeVisible();
    })();

    await step("Verify pricing section and Enterprise roadmap state")(async () => {
      await expect(page.getByRole("heading", { name: "Pricing that follows your business" })).toBeVisible();
      await expect(page.getByTestId("pricing-plan-solo")).toBeVisible();
      await expect(page.getByTestId("pricing-plan-studio")).toBeVisible();
      await expect(page.getByTestId("pricing-plan-business")).toBeVisible();
      await expect(page.getByTestId("pricing-plan-enterprise")).toBeVisible();
      await expect(page.getByText("Coming soon").first()).toBeVisible();
      await expect(page.getByRole("link", { name: "Start Enterprise" })).toHaveCount(0);
    })();

    await step("Verify call-to-action buttons & navigation links are visible")(async () => {
      await expect(page.getByRole("link", { name: "Start free trial" }).first()).toBeVisible();
      // Log in link appears in both navigation and CTA section, so use first()
      await expect(page.getByRole("link", { name: "Log in" }).first()).toBeVisible();
    })();

    await step("Open Solutions menu & verify future solution IA")(async () => {
      await page.getByRole("button", { name: "Solutions" }).click();

      await expect(page.getByRole("heading", { name: "By business type" })).toBeVisible();
      await expect(page.getByRole("heading", { name: "By workflow" })).toBeVisible();
      await expect(page.getByText("Solo operators")).toBeVisible();
      await expect(page.getByText("Clinics and practices")).toBeVisible();
      await expect(page.getByText("WhatsApp flows")).toBeVisible();
      await expect(page.getByText("Bookings")).toBeVisible();
      await expect(page.getByText("Custom datasets")).toBeVisible();
      await expect(page.getByText("Coming soon").first()).toBeVisible();
    })();

    await step("Click Start free trial button & verify redirect to signup")(async () => {
      await page.getByRole("link", { name: "Start free trial" }).first().click();

      await expect(page).toHaveURL("/signup");
    })();

    await step("Navigate back to landing page & click Log in button")(async () => {
      await page.goto("/");
      // Use the navigation Log in link (first one)
      await page.getByRole("navigation").getByRole("link", { name: "Log in" }).click();

      await expect(page).toHaveURL("/login");
    })();
  });
});

test.describe("@comprehensive", () => {
  /**
   * Tests authenticated user redirect behavior on landing page.
   * Covers:
   * - Authenticated users are redirected from landing page to /dashboard
   * - Redirect happens automatically without user interaction
   */
  test("should redirect authenticated users to home page", async ({ ownerPage }) => {
    createTestContext(ownerPage);

    await step("Navigate to landing page as authenticated user & verify redirect to home")(async () => {
      await ownerPage.goto("/");

      await expect(ownerPage).toHaveURL("/dashboard");
    })();
  });
});
