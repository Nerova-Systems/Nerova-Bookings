import type * as React from "react";

import { translationContext as baseTranslationContext } from "@repo/ui/hooks/translationContext";

import type { Locale, LocaleInfo } from "./Translation";

export type { Locale, LocaleInfo } from "./Translation";

export type SetLocalFunction = (locale: Locale) => Promise<void>;

export type TranslationContext = {
  currentLocale: Locale;
  setLocale: SetLocalFunction;
  locales: Locale[];
  getLocaleInfo(locale: Locale): LocaleInfo;
};

// The context lives in @repo/ui so date-aware UI components can read it without importing from
// infrastructure (TS rootDir boundary). Narrow the value type with our app-specific Locale union.
export const translationContext = baseTranslationContext as unknown as React.Context<TranslationContext>;
