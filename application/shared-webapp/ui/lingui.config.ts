import type { LinguiConfig } from "@lingui/conf";

import { formatter } from "@lingui/format-po";

const config: LinguiConfig = {
  locales: ["en-US", "da-DK"],
  sourceLocale: "en-US",
  catalogs: [
    {
      path: "<rootDir>/translations/locale/{locale}",
      include: ["<rootDir>/**/*.ts", "<rootDir>/**/*.tsx"],
      exclude: ["**/node_modules/**", "**/dist", "**/*.d.ts", "**/*.test.*", "**/.*"]
    }
  ],
  format: formatter({ origins: false })
};

export default config;
