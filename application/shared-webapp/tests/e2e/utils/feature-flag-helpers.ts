import type { Browser, Page } from "@playwright/test";
import { getBackOfficeBaseUrl } from "./constants";
import { logInAsAdmin } from "./test-data";

const BACK_OFFICE_BASE_URL = getBackOfficeBaseUrl();

/**
 * Read the antiforgery token injected by the SPA into a <meta name="antiforgeryToken"> tag.
 * Uses page.evaluate so it runs inside the browser and works regardless of origin.
 */
export async function getAntiforgeryHeaders(page: Page): Promise<{ "x-xsrf-token": string }> {
  const token = await page.evaluate(
    () => document.head.querySelector('meta[name="antiforgeryToken"]')?.getAttribute("content") ?? ""
  );
  return { "x-xsrf-token": token };
}

/**
 * Make an HTTP request via browser-side fetch (page.evaluate) rather than page.request.*.
 *
 * Why: Playwright's page.request API uses Node.js undici (native libuv DNS), which does NOT
 * resolve *.localhost subdomains on Windows. Chromium has RFC 6761 localhost resolution
 * built-in, so routing HTTP calls through page.evaluate bypasses the Windows DNS limitation.
 */
async function browserFetch(
  page: Page,
  url: string,
  method: string,
  headers: Record<string, string>,
  body?: unknown
): Promise<{ ok: boolean; status: number; text: string }> {
  return page.evaluate(
    async ({ url, method, headers, body }) => {
      const init: RequestInit = {
        method,
        headers: body !== undefined ? { ...headers, "content-type": "application/json" } : headers,
        body: body !== undefined ? JSON.stringify(body) : undefined
      };
      const response = await fetch(url, init);
      const text = await response.text();
      return { ok: response.ok, status: response.status, text };
    },
    { url, method, headers, body }
  );
}

/**
 * Activate feature flags via the back-office admin API.
 * Creates its own browser context, logs in as admin, and activates each flag in the chain.
 * All HTTP calls go through Chromium to avoid Node.js DNS failures on *.localhost (Windows).
 */
export async function activateBaseFlags(browser: Browser, flagKeys: readonly string[]): Promise<void> {
  const context = await browser.newContext({ baseURL: BACK_OFFICE_BASE_URL, ignoreHTTPSErrors: true });
  const page = await context.newPage();
  try {
    await page.goto(`${BACK_OFFICE_BASE_URL}/feature-flags`);
    await logInAsAdmin(page, `${BACK_OFFICE_BASE_URL}/feature-flags`);
    const headers = await getAntiforgeryHeaders(page);
    for (const flagKey of flagKeys) {
      const { ok } = await browserFetch(
        page,
        `${BACK_OFFICE_BASE_URL}/api/back-office/feature-flags/${flagKey}/activate`,
        "PUT",
        headers
      );
      if (!ok) throw new Error(`activateBaseFlags: failed to activate feature flag '${flagKey}'`);
    }
  } finally {
    await context.close();
  }
}

/**
 * Set tenant-level feature flag overrides for the owner's tenant.
 * Navigates to /account/settings so the SPA shell loads and the antiforgery meta tag is in the DOM.
 * All HTTP calls go through Chromium to avoid Node.js DNS failures on *.localhost (Windows).
 */
export async function setOwnerTenantOverrides(
  ownerPage: Page,
  flagKeys: readonly string[],
  enabled: boolean
): Promise<void> {
  await ownerPage.goto("/account/settings");
  const headers = await getAntiforgeryHeaders(ownerPage);
  for (const flagKey of flagKeys) {
    const { ok } = await browserFetch(
      ownerPage,
      `/api/account/feature-flags/${flagKey}/tenant-override`,
      "PUT",
      headers,
      { enabled }
    );
    if (!ok) throw new Error(`setOwnerTenantOverrides: failed to set override for flag '${flagKey}'`);
  }
}
