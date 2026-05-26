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
      let lastStatus = 0;
      let lastText = "";
      let succeeded = false;
      for (let attempt = 0; attempt < 4; attempt++) {
        if (attempt > 0) await new Promise((r) => setTimeout(r, 150 * attempt));
        const { ok, status, text } = await browserFetch(
          page,
          `${BACK_OFFICE_BASE_URL}/api/back-office/feature-flags/${flagKey}/activate`,
          "PUT",
          headers
        );
        if (ok) {
          succeeded = true;
          break;
        }
        lastStatus = status;
        lastText = text;
        // 409 Conflict = already active (another worker beat us) — treat as success
        if (status === 409) {
          succeeded = true;
          break;
        }
      }
      if (!succeeded)
        throw new Error(
          `activateBaseFlags: failed to activate feature flag '${flagKey}' (status ${lastStatus}): ${lastText}`
        );
    }
  } finally {
    await context.close();
  }
}

/**
 * Set tenant-level feature flag overrides for the owner's tenant via the back-office admin API.
 * Required for TenantAdminManagedFlags (tier-*, cap-*) which cannot be set via the account app.
 * Fetches the real TenantId from /api/account/users/me, then calls the back-office override endpoint.
 * All HTTP calls go through Chromium to avoid Node.js DNS failures on *.localhost (Windows).
 */
export async function setAdminTenantOverrides(
  browser: Browser,
  ownerPage: Page,
  flagKeys: readonly string[],
  enabled: boolean
): Promise<void> {
  await ownerPage.goto("/account/settings");
  const ownerHeaders = await getAntiforgeryHeaders(ownerPage);
  const meResult = await browserFetch(ownerPage, "/api/account/users/me", "GET", ownerHeaders);
  if (!meResult.ok) {
    throw new Error(`setAdminTenantOverrides: failed to get current user (status ${meResult.status})`);
  }
  const me = JSON.parse(meResult.text) as { tenantId: string };
  const { tenantId } = me;

  const context = await browser.newContext({ baseURL: BACK_OFFICE_BASE_URL, ignoreHTTPSErrors: true });
  const page = await context.newPage();
  try {
    await page.goto(`${BACK_OFFICE_BASE_URL}/feature-flags`);
    await logInAsAdmin(page, `${BACK_OFFICE_BASE_URL}/feature-flags`);
    const headers = await getAntiforgeryHeaders(page);
    for (const flagKey of flagKeys) {
      const { ok, status } = await browserFetch(
        page,
        `${BACK_OFFICE_BASE_URL}/api/back-office/feature-flags/${flagKey}/tenant-override`,
        "PUT",
        headers,
        { tenantId, enabled }
      );
      if (!ok) {
        throw new Error(`setAdminTenantOverrides: failed to set override for flag '${flagKey}' (status ${status})`);
      }
    }
  } finally {
    await context.close();
  }

  // Force ownerPage's JWT to be refreshed so the updated tenant overrides are reflected in the
  // feature-flag bootstrap (import.meta.user_info_env.featureFlags) on the next full page load.
  //
  // The back-office override API intentionally does NOT emit x-refresh-authentication-tokens-required
  // (it runs as the system admin, not the tenant owner), so the owner's access token still encodes
  // the pre-override flag set. AppGateway only refreshes the JWT when the access token is absent or
  // expired, so we delete it from the browser's cookie jar and navigate to force a full refresh cycle:
  //   1. No __Host-access-token cookie → AppGateway calls the refresh endpoint with the refresh token
  //   2. Account API re-evaluates flags from DB (now includes the activated overrides)
  //   3. New JWT is issued and set as cookies; HTML bootstrap carries the updated flag set
  await ownerPage.context().clearCookies({ name: "__Host-access-token" });
  await ownerPage.goto("/account/settings");
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
