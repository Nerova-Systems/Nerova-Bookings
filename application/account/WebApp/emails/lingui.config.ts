import type { LinguiConfig } from "@lingui/conf";

import { formatter } from "@lingui/format-po";

import i18nConfig from "../../../shared-webapp/infrastructure/translations/i18n.config.json";

const config: LinguiConfig = {
  locales: Object.keys(i18nConfig),
  sourceLocale: "en-US",
  catalogs: [
    {
      path: "<rootDir>/translations/locale/{locale}",
      include: ["<rootDir>/templates/**/*.tsx", "<rootDir>/templates/**/*.ts"],
      exclude: ["**/node_modules/**", "**/dist/**", "**/*.d.ts"]
    }
  ],
  format: formatter({ origins: false })
};

export default config;
