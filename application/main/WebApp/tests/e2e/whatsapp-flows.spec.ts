import { expect } from "@playwright/test";
import { test } from "@shared/e2e/fixtures/page-auth";
import { getBaseUrl } from "@shared/e2e/utils/constants";
import { createTestContext, expectToastMessage } from "@shared/e2e/utils/test-assertions";
import { completeSignupFlow, testUser } from "@shared/e2e/utils/test-data";
import { step } from "@shared/e2e/utils/test-step-wrapper";

/**
 * Cookie name that instructs the backend MetaGraphClientFactory to use MockMetaGraphClient
 * instead of the real Meta Graph client. Only honoured when Meta:AllowMockProvider=true (set
 * in AppHost for all non-production environments).
 */
const MOCK_PROVIDER_COOKIE = "__Test_Use_Mock_Provider";

test.describe("@smoke", () => {
  /**
   * WhatsApp onboarding entry point tests.
   * Covers:
   * - The /channels/whatsapp route is accessible when PUBLIC_WHATSAPP_SIGNUP_ENABLED=true
   * - Page heading and WhatsApp Business connection card render correctly
   * - "Connect WhatsApp" button is present (disabled in test env — no FB SDK loaded)
   * - Channels navigation link appears in the main sidebar
   *
   * Uses a fresh tenant via signup to guarantee the disconnected state regardless of
   * previous test runs (ownerPage reuses a cached tenant that may already be connected).
   */
  test("should render WhatsApp onboarding page with connect button for new tenant", async ({ page }) => {
    const context = createTestContext(page);
    const owner = testUser();

    await step("Complete owner signup flow & verify landing on dashboard")(async () => {
      await completeSignupFlow(page, expect, owner, context);
    })();

    await step(
      "Navigate to /channels/whatsapp & verify page heading, card title, connect button and sidebar nav link"
    )(async () => {
      await page.goto("/channels/whatsapp");

      await expect(page.getByRole("heading", { name: "WhatsApp", level: 1 })).toBeVisible();
      // CardTitle renders as <div data-slot="card-title">, not a heading element.
      // Use exact: true to avoid matching page subtitle and card description that contain this substring.
      await expect(page.getByText("WhatsApp Business", { exact: true })).toBeVisible();
      await expect(page.getByRole("button", { name: "Connect WhatsApp" })).toBeVisible();
      await expect(
        page.getByRole("navigation", { name: "Main navigation" }).getByRole("link", { name: "Channels" })
      ).toBeVisible();
    })();
  });
});

test.describe("@comprehensive", () => {
  /**
   * WhatsApp full onboarding and messaging flow.
   * Covers:
   * - Completing embedded signup via direct API call (mock Meta client, idempotent)
   * - Connected state renders correctly (description, business name from mock)
   * - Messages panel and send message form appear after connection
   * - Submitting the send message form shows the "Message sent" success toast
   */
  test("should complete WhatsApp onboarding and send a message via mock provider", async ({ ownerPage }) => {
    const context = createTestContext(ownerPage);

    await step("Navigate to /channels/whatsapp & verify page heading renders")(async () => {
      await ownerPage.goto("/channels/whatsapp");

      await expect(ownerPage.getByRole("heading", { name: "WhatsApp", level: 1 })).toBeVisible();
    })();

    await step("Set mock provider cookie & call complete-signup API with mock credentials")(async () => {
      // Activate the mock Meta client for all subsequent requests in this context. The cookie is
      // checked per-request by MetaGraphClientFactory when Meta:AllowMockProvider=true (AppHost).
      await ownerPage.context().addCookies([{ name: MOCK_PROVIDER_COOKIE, value: "1", url: getBaseUrl() }]);

      // Use browser-side fetch so the call shares the page's network stack and all auth/mock
      // cookies. Playwright's page.request uses a separate Node.js HTTP client which can fail
      // to resolve dev subdomains on some Windows setups.
      const status = await ownerPage.evaluate(async () => {
        const antiforgeryToken =
          document.head.querySelector('meta[name="antiforgeryToken"]')?.getAttribute("content") ?? "";
        const response = await fetch("/api/main/whatsapp/embedded-signup/complete", {
          method: "POST",
          headers: { "x-xsrf-token": antiforgeryToken, "content-type": "application/json" },
          body: JSON.stringify({ code: "mock-code", wabaId: "mock-waba-id", phoneNumberId: "mock-phone-id" })
        });
        return response.status;
      });

      expect(status).toBe(200);
    })();

    await step("Reload WhatsApp page & verify connected state, mock business details and send message form")(
      async () => {
        await ownerPage.goto("/channels/whatsapp");

        await expect(ownerPage.getByText("Your account is connected to WhatsApp.")).toBeVisible();
        await expect(ownerPage.getByText("Mock WhatsApp Business")).toBeVisible();
        // CardTitle renders as <div data-slot="card-title">, not a heading element — use getByText.
        await expect(ownerPage.getByText("Send a message")).toBeVisible();
        await expect(ownerPage.getByRole("textbox", { name: "Recipient" })).toBeVisible();
        await expect(ownerPage.getByRole("textbox", { name: "Message" })).toBeVisible();
      }
    )();

    await step("Fill and submit send message form & verify Message sent toast")(async () => {
      await ownerPage.getByRole("textbox", { name: "Recipient" }).fill("+15550100");
      await ownerPage.getByRole("textbox", { name: "Message" }).fill("Hello from E2E test");
      await ownerPage.getByRole("button", { name: "Send message" }).click();

      await expectToastMessage(context, "Message sent");
    })();
  });
});
