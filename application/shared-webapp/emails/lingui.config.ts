import type { LinguiConfig } from "@lingui/conf";

import { formatter } from "@lingui/format-po";

import i18nConfig from "../infrastructure/translations/i18n.config.json";

// Lingui config for the shared `showcase/` templates that ship alongside this package. Per-system
// configs (e.g. account/WebApp/emails/lingui.config.ts) call `createEmailLinguiConfig()` from
// `./lingui.factory` instead.
const config: LinguiConfig = {
  locales: Object.keys(i18nConfig),
  sourceLocale: "en-US",
  catalogs: [
    {
      path: "<rootDir>/translations/locale/{locale}",
      include: ["<rootDir>/showcase/**/*.tsx", "<rootDir>/showcase/**/*.ts"],
      exclude: ["**/node_modules/**", "**/dist/**", "**/*.d.ts"]
    }
  ],
  format: formatter({ origins: false })
};

export default config;
