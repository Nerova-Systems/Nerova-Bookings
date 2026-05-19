import { expect } from "@playwright/test";
import { test } from "@shared/e2e/fixtures/page-auth";
import { createTestContext } from "@shared/e2e/utils/test-assertions";
import { step } from "@shared/e2e/utils/test-step-wrapper";

test.describe("@smoke", () => {
  /**
   * Tests the app shell asset references served through AppGateway.
   * Covers:
   * - The HTML shell references current /static assets, not stale /assets/index.* chunks
   * - Every shell JS/CSS asset is reachable through the gateway
   * - Local development JS/CSS assets disable browser caching
   */
  test("should serve current app shell assets without stale cached bundles", async ({ page }) => {
    createTestContext(page);

    await step("Load app shell & verify current asset references")(async () => {
      await page.goto("/");
      await expect(page.getByRole("heading", { name: "Welcome to PlatformPlatform" })).toBeVisible();

      const assetUrls = await page.evaluate(() =>
        Array.from(
          document.querySelectorAll<HTMLScriptElement | HTMLLinkElement>("script[src], link[rel='stylesheet'][href]")
        )
          .map((element) => ("src" in element ? element.src : element.href))
          .filter((url) => url.includes("/static/") || url.includes("/account/static/"))
      );

      expect(assetUrls.length).toBeGreaterThan(0);
      expect(assetUrls.some((url) => url.includes("/assets/index."))).toBe(false);

      for (const assetUrl of assetUrls) {
        const response = await page.context().request.get(assetUrl);
        const cacheControl = response.headers()["cache-control"] ?? "";
        expect(response.status(), `${assetUrl} should be reachable`).toBe(200);
        expect(cacheControl, `${assetUrl} should not be cached in local development`).toContain("no-store");
        expect(cacheControl, `${assetUrl} should not be cached in local development`).toContain("no-cache");
        expect(cacheControl, `${assetUrl} should not be cached in local development`).toContain("must-revalidate");
      }
    })();
  });
});
