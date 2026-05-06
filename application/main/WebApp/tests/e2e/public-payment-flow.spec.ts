import { expect } from "@playwright/test";
import { test } from "@shared/e2e/fixtures/page-auth";
import { createTestContext } from "@shared/e2e/utils/test-assertions";
import { step } from "@shared/e2e/utils/test-step-wrapper";

test.describe("@smoke", () => {
  test("should show public payment details outside dashboard and resume Paystack checkout", async ({ page }) => {
    createTestContext(page);

    await page.route("**/api/main/public/pay/demo-token", async (route) => {
      await route.fulfill({
        contentType: "application/json",
        body: JSON.stringify({
          business: {
            name: "Sea Point Studio",
            logoUrl: null,
            brandColor: "#111827"
          },
          appointment: {
            reference: "NB-20260506",
            serviceName: "Studio consultation",
            startAt: "2026-05-06T09:00:00+02:00",
            endAt: "2026-05-06T10:00:00+02:00",
            location: "Sea Point"
          },
          payment: {
            amountCents: 15000,
            currency: "ZAR",
            status: "Pending",
            expiresAt: "2026-05-07T09:00:00+02:00"
          }
        })
      });
    });

    await page.route("**/api/main/public/pay/demo-token/initialize", async (route) => {
      expect(route.request().method()).toBe("POST");
      await route.fulfill({
        contentType: "application/json",
        body: JSON.stringify({
          reference: "ps_test_reference",
          accessCode: "access_code_test",
          authorizationUrl: "https://checkout.paystack.test/ps_test_reference",
          amountCents: 15000
        })
      });
    });

    await page.route("https://js.paystack.co/v2/inline.js", async (route) => {
      await route.fulfill({
        contentType: "application/javascript",
        body: `
          window.PaystackPop = function PaystackPop() {
            this.resumeTransaction = function resumeTransaction(accessCode) {
              window.__paystackAccessCode = accessCode;
              window.location.assign("/book/payment/callback?reference=ps_test_reference");
            };
          };
        `
      });
    });

    await page.route("**/api/main/payments/paystack/confirm?reference=ps_test_reference", async (route) => {
      await route.fulfill({
        contentType: "application/json",
        body: JSON.stringify({ appointmentReference: "NB-20260506", status: "Paid" })
      });
    });

    await step("Open public payment page without dashboard shell")(async () => {
      await page.goto("/pay/demo-token");

      await expect(page.getByRole("heading", { name: "Sea Point Studio" })).toBeVisible();
      await expect(page.getByRole("heading", { name: "Studio consultation" })).toBeVisible();
      await expect(page.getByText("NB-20260506")).toBeVisible();
      await expect(page.getByText("Sea Point").last()).toBeVisible();
      await expect(page.getByRole("link", { name: "Dashboard" })).toHaveCount(0);
      await expect(page.getByRole("navigation")).toHaveCount(0);
    })();

    await step("Initialize and resume hosted checkout")(async () => {
      await page.getByRole("button", { name: "Pay securely" }).click();

      await expect(page).toHaveURL(/\/book\/payment\/callback\?reference=ps_test_reference/);
      await expect.poll(async () => page.evaluate(() => window.__paystackAccessCode)).toBe("access_code_test");
      await expect(page.getByText("Payment verified for booking NB-20260506.")).toBeVisible();
    })();
  });
});

declare global {
  interface Window {
    __paystackAccessCode?: string;
  }
}
