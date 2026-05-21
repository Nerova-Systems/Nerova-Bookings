import { useContext } from "react";

import { translationContext } from "./translationContext";

/**
 * Returns the current locale string and a setter from Nerova's `translationContext`.
 *
 * Ported from cal.com `packages/lib/hooks/useLocale.ts` (cf2a55c).
 *
 * Deviation: cal.com `useLocale` returns `{ t, i18n, isLocaleReady }` (react-i18next).
 * Nerova uses Lingui. This hook exposes `{ currentLocale, setLocale, locales, getLocaleInfo }`
 * which maps to the Lingui-backed `translationContext`. Consumer features that previously
 * called `const { t } = useLocale()` should import `{ useLingui }` from `@lingui/react`
 * for translation, and use this hook only for locale-switching and locale metadata.
 */
export function useLocale() {
  return useContext(translationContext);
}
