/// <reference types="@rsbuild/core/types" />
/// <reference path="./environment.d.ts" />

// Restrict ambiguous date formatting methods that produce locale-dependent d/m/y or m/d/y output.
// Use useFormatDate() from @repo/ui/hooks/useSmartDate instead, which formats dates consistently
// according to the app's selected language (e.g., "Apr 5, 2026" or "5. apr. 2026").
interface Date {
  /** @deprecated Use useFormatDate() from @repo/ui/hooks/useSmartDate instead. */
  toLocaleDateString(locales?: string | string[], options?: Intl.DateTimeFormatOptions): string;

  /** @deprecated Use useFormatDate() from @repo/ui/hooks/useSmartDate instead. */
  toLocaleString(locales?: string | string[], options?: Intl.DateTimeFormatOptions): string;

  /** @deprecated Use useFormatDate() from @repo/ui/hooks/useSmartDate instead. */
  toLocaleTimeString(locales?: string | string[], options?: Intl.DateTimeFormatOptions): string;
}
