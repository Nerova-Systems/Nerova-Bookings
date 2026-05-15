# Cal.diy Source Inventory

## Snapshot

- Cal.diy path: `cal.diy`
- Branch: `main`
- Commit: `180ede28f0bddf2738933a6e60a8e80f6116d7da`
- Status: `## main...origin/main`
- Package manager: `yarn@4.12.0`
- `rg --files` count: 7256
- Direct inventory count including additional support files: 7686
- Test/spec/e2e related files from `rg`: 526
- Test/spec/e2e related files in direct inventory: 534

## Top-Level Areas

| Path | Class | Files | Tests | Target |
| --- | --- | --- | --- | --- |
| cal.diy/apps/api | source | 640 | 17 | backend/domain mapping |
| cal.diy/apps/web | source | 1414 | 143 | frontend/UX mapping |
| cal.diy/apps/docs | source | 43 | 0 | intent reference |
| cal.diy/packages | source | 5018 | 373 | backend/domain mapping |
| cal.diy/specs | support | 16 | 0 | support/defer as needed |
| cal.diy/__checks__ | support | 4 | 1 | support/defer as needed |
| cal.diy/example-apps | support | 11 | 0 | support/defer as needed |
| cal.diy/scripts | support | 20 | 0 | support/defer as needed |
| cal.diy/deploy | support | 3 | 0 | support/defer as needed |
| cal.diy/agents | support | 104 | 0 | support/defer as needed |
| cal.diy/docs | support | 1 | 0 | support/defer as needed |
| cal.diy/vitest-mocks | support | 2 | 0 | support/defer as needed |

## Root Scripts

| Script | Intent |
| --- | --- |
| app-store | yarn app-store-cli cli |
| app-store-cli | yarn workspace @calcom/app-store-cli |
| app-store:build | yarn turbo build --filter=@calcom/app-store-cli |
| app-store:watch | yarn app-store-cli watch |
| build | turbo run build --filter=@calcom/web... |
| build:ai | turbo run build --filter="@calcom/ai" |
| changesets-add | yarn changeset add |
| changesets-release | NODE_OPTIONS='--max_old_space_size=12288' turbo run build-npm --filter=@calcom/atoms && yarn changeset publish |
| changesets-version | yarn changeset version |
| clean | find . -name node_modules -o -name .next -o -name .turbo -o -name dist -type d -prune \| xargs rm -rf |
| create-app | yarn app-store create |
| create-app-template | yarn app-store create-template |
| db-deploy | turbo run db-deploy |
| db-seed | turbo run db-seed |
| db-studio | yarn prisma studio |
| delete-app | yarn app-store delete |
| delete-app-template | yarn app-store delete-template |
| deploy | turbo run deploy |
| deploy:trigger:prod | turbo run deploy:trigger:prod --filter="@calcom/features" |
| deploy:trigger:staging | turbo run deploy:trigger:staging --filter="@calcom/features" |
| dev | turbo run dev --filter="@calcom/web" |
| dev:ai | turbo run dev --filter="@calcom/web" --filter="@calcom/api-proxy" --filter="@calcom/ai" |
| dev:all | turbo run dev --filter="@calcom/web" --filter="@calcom/website" --filter="@calcom/console" |
| dev:api | turbo run dev --filter="@calcom/web" --filter="@calcom/api-proxy" |
| dev:api:console | turbo run dev --filter="@calcom/web" --filter="@calcom/api-proxy" --filter="@calcom/console" |
| dev:console | turbo run dev --filter="@calcom/web" --filter="@calcom/console" |
| dev:swagger | turbo run dev --filter="@calcom/api-proxy" --filter="@calcom/swagger" |
| dev:trigger | turbo run dev:trigger --filter="@calcom/features" |
| dev:website | turbo run dev --filter="@calcom/web" --filter="@calcom/website" |
| dx | turbo run dx |
| e2e | NEXT_PUBLIC_IS_E2E=1 yarn playwright test --project=@calcom/web |
| e2e:app-store | NEXT_PUBLIC_IS_E2E=1 QUICK=true yarn playwright test --project=@calcom/app-store |
| e2e:embed | NEXT_PUBLIC_IS_E2E=1 yarn playwright test --project=@calcom/embed-core |
| e2e:embed-react | QUICK=true yarn playwright test --project=@calcom/embed-react |
| edit-app | yarn app-store edit |
| edit-app-template | yarn app-store edit-template |
| embed-tests | turbo run embed-tests |
| embed-tests-quick | turbo run embed-tests-quick |
| env-check:app-store | dotenv-checker --schema .env.appStore.example --env .env.appStore |
| env-check:common | dotenv-checker --schema .env.example --env .env |
| format | biome format --write . |
| heroku-postbuild | turbo run @calcom/web#build |
| i-dev | infisical run -- turbo run dev --filter="@calcom/web" |
| i-dx | infisical run -- turbo run dx |
| i-gen-app-store-example-env | infisical secrets generate-example-env --tags=appstore > .env.appStore.example |
| i-gen-web-example-env | infisical secrets generate-example-env --tags=web > .env.example |
| lint | turbo lint |
| lint-staged | lint-staged |
| lint:fix | turbo lint:fix |
| lint:report | turbo lint:report |
| postinstall | husky install && turbo run post-install |
| pre-commit | lint-staged |
| predev | echo 'Checking env files' |
| prisma | yarn workspace @calcom/prisma prisma |
| publish-embed | yarn withEmbedPublishEnv workspace @calcom/embed-core build && yarn withEmbedPublishEnv workspace @calcom/embed-snippet build && yarn workspaces foreach --from= |
| start | turbo run start --filter="@calcom/web" |
| tdd | vitest watch |
| test | TZ=UTC vitest run |
| test-e2e | yarn db-seed && yarn e2e |
| test-e2e:app-store | yarn db-seed && yarn e2e:app-store |
| test-e2e:embed | yarn db-seed && yarn e2e:embed |
| test-e2e:embed-react | yarn db-seed && yarn e2e:embed-react |
| test-playwright | yarn playwright test --config=playwright.config.ts |
| test:ui | TZ=UTC vitest --ui |
| type-check | turbo run type-check |
| type-check:ci | turbo run type-check:ci --log-prefix=none |
| web | yarn workspace @calcom/web |
| withEmbedPublishEnv | NEXT_PUBLIC_EMBED_LIB_URL='https://app.cal.com/embed/embed.js' NEXT_PUBLIC_WEBAPP_URL='https://app.cal.com' yarn |

## API Modules: apps/api/v2/src/modules

| Name | Class | Files | Tests | Target |
| --- | --- | --- | --- | --- |
| api-keys | adapt | 8 | 0 | account/main compatibility and guards |
| apps | include | 3 | 0 | application/main Core+Api |
| atoms | include | 22 | 0 | application/main Core+Api |
| auth | adapt | 43 | 2 | account/main compatibility and guards |
| booking-seat | defer | 2 | 0 | out of Solo v1 unless dependency emerges |
| cal-unified-calendars | include | 21 | 6 | application/main Core+Api |
| conferencing | include | 12 | 0 | application/main Core+Api |
| credentials | include | 2 | 0 | application/main Core+Api |
| deployments | defer | 3 | 0 | out of Solo v1 unless dependency emerges |
| destination-calendars | include | 7 | 0 | application/main Core+Api |
| email | include | 2 | 0 | application/main Core+Api |
| event-types | include | 4 | 0 | application/main Core+Api |
| jwt | adapt | 2 | 0 | account/main compatibility and guards |
| kysely | include | 3 | 0 | application/main |
| memberships | adapt | 3 | 0 | account/main compatibility and guards |
| oauth-clients | adapt | 30 | 0 | account/main compatibility and guards |
| ooo | include | 5 | 0 | application/main Core+Api |
| organizations | adapt | 2 | 0 | account/main compatibility and guards |
| prisma | include | 4 | 0 | application/main |
| profiles | adapt | 2 | 0 | account/main compatibility and guards |
| redis | include | 2 | 0 | application/main |
| selected-calendars | include | 7 | 0 | application/main Core+Api |
| slots | include | 23 | 1 | application/main Core+Api |
| stripe | defer | 8 | 0 | out of Solo v1 unless dependency emerges |
| teams | adapt | 6 | 0 | account/main compatibility and guards |
| timezones | include | 3 | 0 | application/main Core+Api |
| tokens | adapt | 3 | 0 | account/main compatibility and guards |
| users | include | 17 | 1 | application/main Core+Api |
| verified-resources | adapt | 13 | 0 | account/main compatibility and guards |
| webhooks | include | 23 | 1 | application/main Core+Api |

## Web Modules: apps/web/modules

| Name | Class | Files | Tests | Target |
| --- | --- | --- | --- | --- |
| api-keys | adapt | 2 | 0 | Nerova shell/account/runtime equivalents |
| apps | include | 23 | 1 | application/main/WebApp |
| auth | adapt | 14 | 0 | Nerova shell/account/runtime equivalents |
| availability | include | 3 | 0 | application/main/WebApp |
| blocklist | defer | 9 | 0 | out of Solo v1 or replaced |
| booking-audit | include | 2 | 0 | application/main/WebApp |
| bookings | include | 105 | 5 | application/main/WebApp |
| calendar-view | defer | 1 | 0 | out of Solo v1 or replaced |
| calendars | include | 19 | 0 | application/main/WebApp |
| connect-and-join | defer | 1 | 0 | out of Solo v1 or replaced |
| d | defer | 1 | 0 | out of Solo v1 or replaced |
| data-table | include | 49 | 1 | application/main/WebApp |
| embed | defer | 2 | 0 | out of Solo v1 or replaced |
| event-types | include | 39 | 1 | application/main/WebApp |
| feature-flags | adapt | 4 | 0 | Nerova shell/account/runtime equivalents |
| filters | adapt | 2 | 0 | Nerova shell/account/runtime equivalents |
| form-builder | include | 3 | 1 | application/main/WebApp |
| formbricks | defer | 2 | 0 | out of Solo v1 or replaced |
| getting-started | adapt | 1 | 0 | Nerova shell/account/runtime equivalents |
| maintenance | adapt | 1 | 0 | Nerova shell/account/runtime equivalents |
| more | adapt | 1 | 0 | Nerova shell/account/runtime equivalents |
| notifications | include | 2 | 0 | application/main/WebApp |
| onboarding | include | 28 | 1 | application/main/WebApp |
| schedules | include | 9 | 1 | application/main/WebApp |
| settings | include | 35 | 0 | application/main/WebApp |
| shell | include | 19 | 1 | application/main/WebApp |
| timezone | include | 2 | 1 | application/main/WebApp |
| troubleshooter | adapt | 9 | 0 | Nerova shell/account/runtime equivalents |
| upgrade | defer | 1 | 0 | out of Solo v1 or replaced |
| users | include | 32 | 3 | application/main/WebApp |
| videos | include | 7 | 1 | application/main/WebApp |
| webhooks | include | 15 | 0 | application/main/WebApp |

## Web Components: apps/web/components

| Name | Class | Files | Tests | Target |
| --- | --- | --- | --- | --- |
| apps | include | 35 | 1 | local main WebApp wrappers over @repo/ui |
| auth | adapt | 2 | 0 | Nerova account/shell equivalents |
| booking | include | 13 | 2 | local main WebApp wrappers over @repo/ui |
| dialog | include | 10 | 1 | local main WebApp wrappers over @repo/ui |
| error | adapt | 2 | 0 | Nerova account/shell equivalents |
| eventtype | include | 1 | 0 | local main WebApp wrappers over @repo/ui |
| getting-started | include | 9 | 0 | local main WebApp wrappers over @repo/ui |
| integrations | include | 1 | 0 | local main WebApp wrappers over @repo/ui |
| layouts | include | 1 | 0 | local main WebApp wrappers over @repo/ui |
| phone-input | include | 4 | 1 | local main WebApp wrappers over @repo/ui |
| schemas | include | 1 | 0 | local main WebApp wrappers over @repo/ui |
| security | adapt | 5 | 0 | Nerova account/shell equivalents |
| settings | include | 7 | 0 | local main WebApp wrappers over @repo/ui |
| setup | include | 2 | 0 | local main WebApp wrappers over @repo/ui |
| ui | include | 11 | 0 | local main WebApp wrappers over @repo/ui |

## Web Routes And API Routes

| Path | Class | Target |
| --- | --- | --- |
| cal.diy/apps/web/app/(booking-page-wrapper)/[user]/[type]/embed/page.tsx | replace | WhatsApp Flow behavior reference; no public web page |
| cal.diy/apps/web/app/(booking-page-wrapper)/[user]/[type]/page.tsx | replace | WhatsApp Flow behavior reference; no public web page |
| cal.diy/apps/web/app/(booking-page-wrapper)/[user]/embed/page.tsx | replace | WhatsApp Flow behavior reference; no public web page |
| cal.diy/apps/web/app/(booking-page-wrapper)/[user]/page.tsx | replace | WhatsApp Flow behavior reference; no public web page |
| cal.diy/apps/web/app/(booking-page-wrapper)/booking-successful/[uid]/page.tsx | replace | WhatsApp Flow behavior reference; no public web page |
| cal.diy/apps/web/app/(booking-page-wrapper)/booking/[uid]/embed/page.tsx | replace | WhatsApp Flow behavior reference; no public web page |
| cal.diy/apps/web/app/(booking-page-wrapper)/booking/[uid]/page.tsx | replace | WhatsApp Flow behavior reference; no public web page |
| cal.diy/apps/web/app/(booking-page-wrapper)/booking/dry-run-successful/page.tsx | replace | WhatsApp Flow behavior reference; no public web page |
| cal.diy/apps/web/app/(booking-page-wrapper)/d/[link]/[slug]/page.tsx | replace | WhatsApp Flow behavior reference; no public web page |
| cal.diy/apps/web/app/(booking-page-wrapper)/layout.tsx | replace | WhatsApp Flow behavior reference; no public web page |
| cal.diy/apps/web/app/(use-page-wrapper)/(main-nav)/availability/page.tsx | include | authenticated admin route |
| cal.diy/apps/web/app/(use-page-wrapper)/(main-nav)/booking/[uid]/logs/page.tsx | include | authenticated admin route |
| cal.diy/apps/web/app/(use-page-wrapper)/(main-nav)/bookings/[status]/page.tsx | include | authenticated admin route |
| cal.diy/apps/web/app/(use-page-wrapper)/(main-nav)/event-types/page.tsx | include | authenticated admin route |
| cal.diy/apps/web/app/(use-page-wrapper)/(main-nav)/layout.tsx | include | authenticated admin route |
| cal.diy/apps/web/app/(use-page-wrapper)/apps/(homepage)/page.tsx | include | authenticated admin route |
| cal.diy/apps/web/app/(use-page-wrapper)/apps/[slug]/page.tsx | include | authenticated admin route |
| cal.diy/apps/web/app/(use-page-wrapper)/apps/[slug]/setup/page.tsx | include | authenticated admin route |
| cal.diy/apps/web/app/(use-page-wrapper)/apps/categories/[category]/page.tsx | include | authenticated admin route |
| cal.diy/apps/web/app/(use-page-wrapper)/apps/categories/layout.tsx | include | authenticated admin route |
| cal.diy/apps/web/app/(use-page-wrapper)/apps/categories/page.tsx | include | authenticated admin route |
| cal.diy/apps/web/app/(use-page-wrapper)/apps/installation/[[...step]]/page.tsx | include | authenticated admin route |
| cal.diy/apps/web/app/(use-page-wrapper)/apps/installed/[category]/page.tsx | include | authenticated admin route |
| cal.diy/apps/web/app/(use-page-wrapper)/auth/error/page.tsx | include | authenticated admin route |
| cal.diy/apps/web/app/(use-page-wrapper)/auth/forgot-password/[id]/page.tsx | include | authenticated admin route |
| cal.diy/apps/web/app/(use-page-wrapper)/auth/forgot-password/page.tsx | include | authenticated admin route |
| cal.diy/apps/web/app/(use-page-wrapper)/auth/login/page.tsx | include | authenticated admin route |
| cal.diy/apps/web/app/(use-page-wrapper)/auth/logout/page.tsx | include | authenticated admin route |
| cal.diy/apps/web/app/(use-page-wrapper)/auth/oauth2/authorize/page.tsx | include | authenticated admin route |
| cal.diy/apps/web/app/(use-page-wrapper)/auth/setup/page.tsx | include | authenticated admin route |
| cal.diy/apps/web/app/(use-page-wrapper)/auth/signin/page.tsx | include | authenticated admin route |
| cal.diy/apps/web/app/(use-page-wrapper)/auth/verify-email-change/page.tsx | include | authenticated admin route |
| cal.diy/apps/web/app/(use-page-wrapper)/auth/verify-email/page.tsx | include | authenticated admin route |
| cal.diy/apps/web/app/(use-page-wrapper)/auth/verify/page.tsx | include | authenticated admin route |
| cal.diy/apps/web/app/(use-page-wrapper)/availability/[schedule]/page.tsx | include | authenticated admin route |
| cal.diy/apps/web/app/(use-page-wrapper)/availability/troubleshoot/layout.tsx | include | authenticated admin route |
| cal.diy/apps/web/app/(use-page-wrapper)/availability/troubleshoot/page.tsx | include | authenticated admin route |
| cal.diy/apps/web/app/(use-page-wrapper)/enterprise/page.tsx | include | authenticated admin route |
| cal.diy/apps/web/app/(use-page-wrapper)/event-types/[type]/page.tsx | include | authenticated admin route |
| cal.diy/apps/web/app/(use-page-wrapper)/getting-started/[[...step]]/page.tsx | include | authenticated admin route |
| cal.diy/apps/web/app/(use-page-wrapper)/layout.tsx | include | authenticated admin route |
| cal.diy/apps/web/app/(use-page-wrapper)/maintenance/page.tsx | include | authenticated admin route |
| cal.diy/apps/web/app/(use-page-wrapper)/more/page.tsx | include | authenticated admin route |
| cal.diy/apps/web/app/(use-page-wrapper)/onboarding/getting-started/page.tsx | include | authenticated admin route |
| cal.diy/apps/web/app/(use-page-wrapper)/onboarding/layout.tsx | include | authenticated admin route |
| cal.diy/apps/web/app/(use-page-wrapper)/onboarding/personal/calendar/page.tsx | include | authenticated admin route |
| cal.diy/apps/web/app/(use-page-wrapper)/onboarding/personal/profile/page.tsx | include | authenticated admin route |
| cal.diy/apps/web/app/(use-page-wrapper)/onboarding/personal/settings/page.tsx | include | authenticated admin route |
| cal.diy/apps/web/app/(use-page-wrapper)/payment/[uid]/page.tsx | include | authenticated admin route |
| cal.diy/apps/web/app/(use-page-wrapper)/refer/page.tsx | include | authenticated admin route |
| cal.diy/apps/web/app/(use-page-wrapper)/settings/(admin-layout)/admin/apps/[category]/page.tsx | include | authenticated admin route |
| cal.diy/apps/web/app/(use-page-wrapper)/settings/(admin-layout)/admin/flags/page.tsx | include | authenticated admin route |
| cal.diy/apps/web/app/(use-page-wrapper)/settings/(admin-layout)/admin/lockedSMS/page.tsx | include | authenticated admin route |
| cal.diy/apps/web/app/(use-page-wrapper)/settings/(admin-layout)/admin/oauth/page.tsx | include | authenticated admin route |
| cal.diy/apps/web/app/(use-page-wrapper)/settings/(admin-layout)/admin/page.tsx | include | authenticated admin route |
| cal.diy/apps/web/app/(use-page-wrapper)/settings/(admin-layout)/admin/playground/date-range-filter/page.tsx | include | authenticated admin route |
| cal.diy/apps/web/app/(use-page-wrapper)/settings/(admin-layout)/admin/playground/layout.tsx | include | authenticated admin route |
| cal.diy/apps/web/app/(use-page-wrapper)/settings/(admin-layout)/admin/playground/page.tsx | include | authenticated admin route |
| cal.diy/apps/web/app/(use-page-wrapper)/settings/(admin-layout)/admin/users/[id]/edit/page.tsx | include | authenticated admin route |
| cal.diy/apps/web/app/(use-page-wrapper)/settings/(admin-layout)/admin/users/add/page.tsx | include | authenticated admin route |
| cal.diy/apps/web/app/(use-page-wrapper)/settings/(admin-layout)/admin/users/page.tsx | include | authenticated admin route |
| cal.diy/apps/web/app/(use-page-wrapper)/settings/(admin-layout)/layout.tsx | include | authenticated admin route |
| cal.diy/apps/web/app/(use-page-wrapper)/settings/(settings-layout)/developer/api-keys/page.tsx | include | authenticated admin route |
| cal.diy/apps/web/app/(use-page-wrapper)/settings/(settings-layout)/developer/oauth/page.tsx | include | authenticated admin route |
| cal.diy/apps/web/app/(use-page-wrapper)/settings/(settings-layout)/developer/webhooks/(with-loader)/page.tsx | include | authenticated admin route |
| cal.diy/apps/web/app/(use-page-wrapper)/settings/(settings-layout)/developer/webhooks/[id]/page.tsx | include | authenticated admin route |
| cal.diy/apps/web/app/(use-page-wrapper)/settings/(settings-layout)/developer/webhooks/new/page.tsx | include | authenticated admin route |
| cal.diy/apps/web/app/(use-page-wrapper)/settings/(settings-layout)/layout.tsx | include | authenticated admin route |
| cal.diy/apps/web/app/(use-page-wrapper)/settings/(settings-layout)/my-account/appearance/page.tsx | include | authenticated admin route |
| cal.diy/apps/web/app/(use-page-wrapper)/settings/(settings-layout)/my-account/calendars/page.tsx | include | authenticated admin route |
| cal.diy/apps/web/app/(use-page-wrapper)/settings/(settings-layout)/my-account/conferencing/page.tsx | include | authenticated admin route |
| cal.diy/apps/web/app/(use-page-wrapper)/settings/(settings-layout)/my-account/general/page.tsx | include | authenticated admin route |
| cal.diy/apps/web/app/(use-page-wrapper)/settings/(settings-layout)/my-account/out-of-office/page.tsx | include | authenticated admin route |
| cal.diy/apps/web/app/(use-page-wrapper)/settings/(settings-layout)/my-account/profile/page.tsx | include | authenticated admin route |
| cal.diy/apps/web/app/(use-page-wrapper)/settings/(settings-layout)/my-account/push-notifications/page.tsx | include | authenticated admin route |
| cal.diy/apps/web/app/(use-page-wrapper)/settings/(settings-layout)/security/password/page.tsx | include | authenticated admin route |
| cal.diy/apps/web/app/(use-page-wrapper)/settings/(settings-layout)/security/two-factor-auth/page.tsx | include | authenticated admin route |
| cal.diy/apps/web/app/(use-page-wrapper)/signup/page.tsx | include | authenticated admin route |
| cal.diy/apps/web/app/(use-page-wrapper)/upgrade/page.tsx | include | authenticated admin route |
| cal.diy/apps/web/app/(use-page-wrapper)/video/[uid]/page.tsx | include | authenticated admin route |
| cal.diy/apps/web/app/(use-page-wrapper)/video/meeting-ended/[uid]/page.tsx | include | authenticated admin route |
| cal.diy/apps/web/app/(use-page-wrapper)/video/meeting-not-started/[uid]/page.tsx | include | authenticated admin route |
| cal.diy/apps/web/app/(use-page-wrapper)/video/no-meeting-found/page.tsx | include | authenticated admin route |
| cal.diy/apps/web/app/api/auth/forgot-password/route.ts | adapt | application/main/Api endpoint |
| cal.diy/apps/web/app/api/auth/oauth/me/route.ts | adapt | application/main/Api endpoint |
| cal.diy/apps/web/app/api/auth/oauth/refreshToken/route.ts | adapt | application/main/Api endpoint |
| cal.diy/apps/web/app/api/auth/oauth/token/route.ts | adapt | application/main/Api endpoint |
| cal.diy/apps/web/app/api/auth/reset-password/route.ts | adapt | application/main/Api endpoint |
| cal.diy/apps/web/app/api/auth/setup/route.ts | adapt | application/main/Api endpoint |
| cal.diy/apps/web/app/api/auth/signup/route.ts | adapt | application/main/Api endpoint |
| cal.diy/apps/web/app/api/auth/two-factor/totp/disable/route.ts | adapt | application/main/Api endpoint |
| cal.diy/apps/web/app/api/auth/two-factor/totp/enable/route.ts | adapt | application/main/Api endpoint |
| cal.diy/apps/web/app/api/auth/two-factor/totp/setup/route.ts | adapt | application/main/Api endpoint |
| cal.diy/apps/web/app/api/availability/calendar/route.ts | adapt | application/main/Api endpoint |
| cal.diy/apps/web/app/api/avatar/[uuid]/route.ts | adapt | application/main/Api endpoint |
| cal.diy/apps/web/app/api/cancel/route.ts | adapt | application/main/Api endpoint |
| cal.diy/apps/web/app/api/cron/bookingReminder/route.ts | adapt | application/main/Api endpoint |
| cal.diy/apps/web/app/api/cron/calendar-subscriptions-cleanup/route.ts | adapt | application/main/Api endpoint |
| cal.diy/apps/web/app/api/cron/calendar-subscriptions/route.ts | adapt | application/main/Api endpoint |
| cal.diy/apps/web/app/api/cron/changeTimeZone/route.ts | adapt | application/main/Api endpoint |
| cal.diy/apps/web/app/api/cron/selected-calendars/route.ts | adapt | application/main/Api endpoint |
| cal.diy/apps/web/app/api/cron/syncAppMeta/route.ts | adapt | application/main/Api endpoint |
| cal.diy/apps/web/app/api/cron/webhookTriggers/route.ts | adapt | application/main/Api endpoint |
| cal.diy/apps/web/app/api/csrf/route.ts | adapt | application/main/Api endpoint |
| cal.diy/apps/web/app/api/email/route.ts | adapt | application/main/Api endpoint |
| cal.diy/apps/web/app/api/geolocation/route.ts | adapt | application/main/Api endpoint |
| cal.diy/apps/web/app/api/ip/route.ts | adapt | application/main/Api endpoint |
| cal.diy/apps/web/app/api/link/route.ts | adapt | application/main/Api endpoint |
| cal.diy/apps/web/app/api/logo/route.ts | adapt | application/main/Api endpoint |
| cal.diy/apps/web/app/api/me/route.ts | adapt | application/main/Api endpoint |
| cal.diy/apps/web/app/api/recorded-daily-video/route.ts | adapt | application/main/Api endpoint |
| cal.diy/apps/web/app/api/sync/helpscout/route.ts | adapt | application/main/Api endpoint |
| cal.diy/apps/web/app/api/tasks/cleanup/route.ts | adapt | application/main/Api endpoint |
| cal.diy/apps/web/app/api/tasks/cron/route.ts | adapt | application/main/Api endpoint |
| cal.diy/apps/web/app/api/user/referrals-token/route.ts | adapt | application/main/Api endpoint |
| cal.diy/apps/web/app/api/username/route.ts | adapt | application/main/Api endpoint |
| cal.diy/apps/web/app/api/verify-booking-token/route.ts | adapt | application/main/Api endpoint |
| cal.diy/apps/web/app/api/version/route.ts | adapt | application/main/Api endpoint |
| cal.diy/apps/web/app/api/video/guest-session/route.ts | adapt | application/main/Api endpoint |
| cal.diy/apps/web/app/api/video/recording/route.ts | adapt | application/main/Api endpoint |
| cal.diy/apps/web/app/api/webhook/app-credential/route.ts | adapt | application/main/Api endpoint |
| cal.diy/apps/web/app/api/webhooks/calendar-subscription/[provider]/route.ts | adapt | application/main/Api endpoint |
| cal.diy/apps/web/app/e2e/session-warmup/page.tsx | adapt | application/main/WebApp or API equivalent |
| cal.diy/apps/web/app/icons/page.tsx | adapt | application/main/WebApp or API equivalent |
| cal.diy/apps/web/app/layout.tsx | adapt | application/main/WebApp or API equivalent |
| cal.diy/apps/web/app/page.tsx | adapt | application/main/WebApp or API equivalent |
| cal.diy/apps/web/app/reschedule/[uid]/embed/page.tsx | replace | WhatsApp Flow behavior reference; no public web page |
| cal.diy/apps/web/app/reschedule/[uid]/page.tsx | replace | WhatsApp Flow behavior reference; no public web page |
| cal.diy/apps/web/pages/_app.tsx | adapt | application/main/WebApp or API equivalent |
| cal.diy/apps/web/pages/_document.tsx | adapt | application/main/WebApp or API equivalent |
| cal.diy/apps/web/pages/_error.tsx | adapt | application/main/WebApp or API equivalent |
| cal.diy/apps/web/pages/api/auth/[...nextauth].ts | adapt | application/main/Api endpoint |
| cal.diy/apps/web/pages/api/auth/verify-email.ts | adapt | application/main/Api endpoint |
| cal.diy/apps/web/pages/api/book/event.ts | adapt | application/main/Api endpoint |
| cal.diy/apps/web/pages/api/book/recurring-event.test.ts | adapt | application/main/Api endpoint |
| cal.diy/apps/web/pages/api/book/recurring-event.ts | adapt | application/main/Api endpoint |
| cal.diy/apps/web/pages/api/integrations/[...args].ts | adapt | application/main/Api endpoint |
| cal.diy/apps/web/pages/api/integrations/alby/webhook.ts | adapt | application/main/Api endpoint |
| cal.diy/apps/web/pages/api/integrations/btcpayserver/webhook.ts | adapt | application/main/Api endpoint |
| cal.diy/apps/web/pages/api/integrations/paypal/webhook.ts | adapt | application/main/Api endpoint |
| cal.diy/apps/web/pages/api/integrations/stripepayment/webhook.ts | adapt | application/main/Api endpoint |
| cal.diy/apps/web/pages/api/stripe/webhook.ts | adapt | application/main/Api endpoint |
| cal.diy/apps/web/pages/api/trpc/admin/[trpc].ts | adapt | application/main/Api endpoint |
| cal.diy/apps/web/pages/api/trpc/apps/[trpc].ts | adapt | application/main/Api endpoint |
| cal.diy/apps/web/pages/api/trpc/appsRouter/[trpc].ts | adapt | application/main/Api endpoint |
| cal.diy/apps/web/pages/api/trpc/auth/[trpc].ts | adapt | application/main/Api endpoint |
| cal.diy/apps/web/pages/api/trpc/availability/[trpc].ts | adapt | application/main/Api endpoint |
| cal.diy/apps/web/pages/api/trpc/bookings/[trpc].ts | adapt | application/main/Api endpoint |
| cal.diy/apps/web/pages/api/trpc/calendars/[trpc].ts | adapt | application/main/Api endpoint |
| cal.diy/apps/web/pages/api/trpc/calVideo/[trpc].ts | adapt | application/main/Api endpoint |
| cal.diy/apps/web/pages/api/trpc/credentials/[trpc].ts | adapt | application/main/Api endpoint |
| cal.diy/apps/web/pages/api/trpc/deploymentSetup/[trpc].ts | adapt | application/main/Api endpoint |
| cal.diy/apps/web/pages/api/trpc/eventTypes/[trpc].ts | adapt | application/main/Api endpoint |
| cal.diy/apps/web/pages/api/trpc/eventTypesHeavy/[trpc].ts | adapt | application/main/Api endpoint |
| cal.diy/apps/web/pages/api/trpc/features/[trpc].ts | adapt | application/main/Api endpoint |
| cal.diy/apps/web/pages/api/trpc/feedback/[trpc].ts | adapt | application/main/Api endpoint |
| cal.diy/apps/web/pages/api/trpc/googleWorkspace/[trpc].ts | adapt | application/main/Api endpoint |
| cal.diy/apps/web/pages/api/trpc/holidays/[trpc].ts | adapt | application/main/Api endpoint |
| cal.diy/apps/web/pages/api/trpc/i18n/[trpc].ts | adapt | application/main/Api endpoint |
| cal.diy/apps/web/pages/api/trpc/loggedInViewerRouter/[trpc].ts | adapt | application/main/Api endpoint |
| cal.diy/apps/web/pages/api/trpc/me/[trpc].ts | adapt | application/main/Api endpoint |
| cal.diy/apps/web/pages/api/trpc/oAuth/[trpc].ts | adapt | application/main/Api endpoint |
| cal.diy/apps/web/pages/api/trpc/ooo/[trpc].ts | adapt | application/main/Api endpoint |
| cal.diy/apps/web/pages/api/trpc/public/[trpc].ts | adapt | application/main/Api endpoint |
| cal.diy/apps/web/pages/api/trpc/slots/[trpc].ts | adapt | application/main/Api endpoint |
| cal.diy/apps/web/pages/api/trpc/timezones/[trpc].ts | adapt | application/main/Api endpoint |
| cal.diy/apps/web/pages/api/trpc/travelSchedules/[trpc].ts | adapt | application/main/Api endpoint |
| cal.diy/apps/web/pages/api/trpc/users/[trpc].ts | adapt | application/main/Api endpoint |
| cal.diy/apps/web/pages/api/trpc/viewer/[trpc].ts | adapt | application/main/Api endpoint |
| cal.diy/apps/web/pages/api/trpc/webhook/[trpc].ts | adapt | application/main/Api endpoint |
| cal.diy/apps/web/pages/router/embed.tsx | replace | WhatsApp Flow behavior reference; no public web page |
| cal.diy/apps/web/pages/router/index.tsx | adapt | application/main/WebApp or API equivalent |

## Public Booking Surface Replaced By WhatsApp Flow

| Path | Class | Target |
| --- | --- | --- |
| cal.diy/apps/web/app/(booking-page-wrapper)/[user]/[type]/embed/page.tsx | replace | WhatsApp Flow behavior reference; no public web page |
| cal.diy/apps/web/app/(booking-page-wrapper)/[user]/[type]/page.tsx | replace | WhatsApp Flow behavior reference; no public web page |
| cal.diy/apps/web/app/(booking-page-wrapper)/[user]/embed/page.tsx | replace | WhatsApp Flow behavior reference; no public web page |
| cal.diy/apps/web/app/(booking-page-wrapper)/[user]/page.tsx | replace | WhatsApp Flow behavior reference; no public web page |
| cal.diy/apps/web/app/(booking-page-wrapper)/booking-successful/[uid]/page.tsx | replace | WhatsApp Flow behavior reference; no public web page |
| cal.diy/apps/web/app/(booking-page-wrapper)/booking/[uid]/embed/page.tsx | replace | WhatsApp Flow behavior reference; no public web page |
| cal.diy/apps/web/app/(booking-page-wrapper)/booking/[uid]/page.tsx | replace | WhatsApp Flow behavior reference; no public web page |
| cal.diy/apps/web/app/(booking-page-wrapper)/booking/dry-run-successful/page.tsx | replace | WhatsApp Flow behavior reference; no public web page |
| cal.diy/apps/web/app/(booking-page-wrapper)/d/[link]/[slug]/page.tsx | replace | WhatsApp Flow behavior reference; no public web page |
| cal.diy/apps/web/app/(booking-page-wrapper)/layout.tsx | replace | WhatsApp Flow behavior reference; no public web page |
| cal.diy/apps/web/app/reschedule/[uid]/embed/page.tsx | replace | WhatsApp Flow behavior reference; no public web page |
| cal.diy/apps/web/app/reschedule/[uid]/page.tsx | replace | WhatsApp Flow behavior reference; no public web page |
| cal.diy/apps/web/pages/router/embed.tsx | replace | WhatsApp Flow behavior reference; no public web page |

## Packages

| Name | Class | Files | Tests | Target |
| --- | --- | --- | --- | --- |
| app-store | include | 1556 | 34 | connector registry/services/UI |
| app-store-cli | include | 28 | 1 | Nerova connector scaffolding workflow |
| config | include | 5 | 0 | application/main or shared infrastructure |
| coss-ui | include | 61 | 0 | UI behavior reference only |
| dayjs | include | 6 | 0 | application/main or shared infrastructure |
| debugging | adapt | 4 | 0 | tooling/support reference |
| emails | include | 150 | 6 | application/main or shared infrastructure |
| embeds | defer | 104 | 21 | public embeds out of Solo v1 |
| features | include | 987 | 192 | application/main or shared infrastructure |
| i18n | include | 50 | 2 | application/main or shared infrastructure |
| kysely | adapt | 5 | 0 | tooling/support reference |
| lib | include | 291 | 44 | application/main or shared infrastructure |
| platform | include | 409 | 9 | atom/API behavior reference |
| prisma | include | 625 | 2 | EF Core/Postgres models and migrations |
| sms | include | 11 | 1 | application/main or shared infrastructure |
| testing | include | 29 | 0 | application/main or shared infrastructure |
| trpc | include | 433 | 28 | OpenAPI endpoints and generated clients |
| tsconfig | adapt | 5 | 0 | tooling/support reference |
| types | include | 32 | 0 | application/main or shared infrastructure |
| ui | include | 227 | 33 | @repo/ui wrappers; shared primitives locked |

## App Store Packages

| Name | Class | Files | Tests | Reason |
| --- | --- | --- | --- | --- |
| _components | include infra | 12 | 1 | registry/generation/shared app-store behavior |
| _lib | include infra | 2 | 0 | registry/generation/shared app-store behavior |
| _pages | include infra | 1 | 0 | registry/generation/shared app-store behavior |
| _utils | include infra | 56 | 7 | registry/generation/shared app-store behavior |
| alby | defer | 26 | 0 | out of Solo v1 connector scope |
| amie | defer | 6 | 0 | out of Solo v1 connector scope |
| applecalendar | defer | 10 | 0 | out of Solo v1 connector scope |
| attio | defer | 15 | 0 | out of Solo v1 connector scope |
| autocheckin | defer | 5 | 0 | out of Solo v1 connector scope |
| baa-for-hipaa | defer | 4 | 0 | out of Solo v1 connector scope |
| basecamp3 | defer | 21 | 0 | out of Solo v1 connector scope |
| bolna | defer | 7 | 0 | out of Solo v1 connector scope |
| btcpayserver | defer | 20 | 0 | out of Solo v1 connector scope |
| caldavcalendar | defer | 10 | 0 | out of Solo v1 connector scope |
| campfire | defer | 16 | 0 | out of Solo v1 connector scope |
| caretta | defer | 5 | 0 | out of Solo v1 connector scope |
| chatbase | defer | 6 | 0 | out of Solo v1 connector scope |
| clic | defer | 7 | 0 | out of Solo v1 connector scope |
| closecom | defer | 21 | 1 | out of Solo v1 connector scope |
| cron | defer | 7 | 0 | out of Solo v1 connector scope |
| dailyvideo | defer | 15 | 0 | out of Solo v1 connector scope |
| databuddy | defer | 11 | 0 | out of Solo v1 connector scope |
| deel | defer | 6 | 0 | out of Solo v1 connector scope |
| demodesk | defer | 13 | 0 | out of Solo v1 connector scope |
| dialpad | defer | 10 | 0 | out of Solo v1 connector scope |
| discord | defer | 11 | 0 | out of Solo v1 connector scope |
| dub | defer | 18 | 0 | out of Solo v1 connector scope |
| eightxeight | defer | 9 | 0 | out of Solo v1 connector scope |
| element-call | defer | 10 | 0 | out of Solo v1 connector scope |
| elevenlabs | defer | 5 | 0 | out of Solo v1 connector scope |
| exchange2013calendar | defer | 10 | 0 | out of Solo v1 connector scope |
| exchange2016calendar | defer | 10 | 0 | out of Solo v1 connector scope |
| exchangecalendar | defer | 12 | 0 | out of Solo v1 connector scope |
| facetime | defer | 10 | 0 | out of Solo v1 connector scope |
| famulor | defer | 14 | 0 | out of Solo v1 connector scope |
| fathom | defer | 11 | 0 | out of Solo v1 connector scope |
| feishucalendar | defer | 20 | 0 | out of Solo v1 connector scope |
| fonio-ai | defer | 8 | 0 | out of Solo v1 connector scope |
| framer | defer | 7 | 0 | out of Solo v1 connector scope |
| ga4 | defer | 16 | 0 | out of Solo v1 connector scope |
| giphy | defer | 19 | 0 | out of Solo v1 connector scope |
| googlecalendar | include/adapt | 24 | 5 | visible Solo connector or WhatsApp source |
| googlevideo | include/adapt | 12 | 0 | visible Solo connector or WhatsApp source |
| granola | defer | 8 | 0 | out of Solo v1 connector scope |
| greetmate-ai | defer | 5 | 0 | out of Solo v1 connector scope |
| gtm | defer | 12 | 0 | out of Solo v1 connector scope |
| hitpay | defer | 29 | 0 | out of Solo v1 connector scope |
| horizon-workrooms | defer | 10 | 0 | out of Solo v1 connector scope |
| hubspot | defer | 15 | 1 | out of Solo v1 connector scope |
| huddle01video | defer | 18 | 0 | out of Solo v1 connector scope |
| ics-feedcalendar | defer | 9 | 0 | out of Solo v1 connector scope |
| insihts | defer | 10 | 0 | out of Solo v1 connector scope |
| intercom | defer | 24 | 0 | out of Solo v1 connector scope |
| jelly | defer | 14 | 0 | out of Solo v1 connector scope |
| jitsivideo | defer | 11 | 0 | out of Solo v1 connector scope |
| larkcalendar | defer | 20 | 0 | out of Solo v1 connector scope |
| lindy | defer | 8 | 0 | out of Solo v1 connector scope |
| linear | defer | 9 | 0 | out of Solo v1 connector scope |
| lyra | defer | 13 | 0 | out of Solo v1 connector scope |
| make | defer | 19 | 0 | out of Solo v1 connector scope |
| matomo | defer | 10 | 0 | out of Solo v1 connector scope |
| metapixel | defer | 13 | 0 | out of Solo v1 connector scope |
| millis-ai | defer | 8 | 0 | out of Solo v1 connector scope |
| mirotalk | defer | 9 | 0 | out of Solo v1 connector scope |
| mock-payment-app | defer | 11 | 0 | out of Solo v1 connector scope |
| monobot | defer | 6 | 0 | out of Solo v1 connector scope |
| n8n | defer | 7 | 0 | out of Solo v1 connector scope |
| nextcloudtalk | defer | 14 | 0 | out of Solo v1 connector scope |
| office365calendar | include/adapt | 17 | 0 | visible Solo connector or WhatsApp source |
| office365video | include/adapt | 21 | 1 | visible Solo connector or WhatsApp source |
| paypal | defer | 22 | 0 | out of Solo v1 connector scope |
| ping | defer | 11 | 0 | out of Solo v1 connector scope |
| pipedream | defer | 8 | 0 | out of Solo v1 connector scope |
| pipedrive-crm | defer | 16 | 0 | out of Solo v1 connector scope |
| plausible | defer | 11 | 0 | out of Solo v1 connector scope |
| posthog | defer | 10 | 0 | out of Solo v1 connector scope |
| qr_code | defer | 10 | 0 | out of Solo v1 connector scope |
| raycast | defer | 7 | 0 | out of Solo v1 connector scope |
| retell-ai | defer | 7 | 0 | out of Solo v1 connector scope |
| riverside | defer | 9 | 0 | out of Solo v1 connector scope |
| roam | defer | 11 | 0 | out of Solo v1 connector scope |
| salesforce | defer | 59 | 8 | out of Solo v1 connector scope |
| salesroom | defer | 13 | 0 | out of Solo v1 connector scope |
| sendgrid | defer | 13 | 0 | out of Solo v1 connector scope |
| shimmervideo | defer | 14 | 0 | out of Solo v1 connector scope |
| signal | defer | 10 | 0 | out of Solo v1 connector scope |
| sirius_video | defer | 11 | 0 | out of Solo v1 connector scope |
| skype | defer | 10 | 0 | out of Solo v1 connector scope |
| stripepayment | defer | 50 | 5 | out of Solo v1 connector scope |
| sylapsvideo | defer | 11 | 0 | out of Solo v1 connector scope |
| synthflow | defer | 6 | 0 | out of Solo v1 connector scope |
| tandemvideo | defer | 17 | 0 | out of Solo v1 connector scope |
| telegram | defer | 11 | 0 | out of Solo v1 connector scope |
| telli | defer | 8 | 0 | out of Solo v1 connector scope |
| templates | include infra | 64 | 0 | registry/generation/shared app-store behavior |
| tests | include infra | 1 | 1 | registry/generation/shared app-store behavior |
| twipla | defer | 10 | 0 | out of Solo v1 connector scope |
| umami | defer | 10 | 0 | out of Solo v1 connector scope |
| vimcal | defer | 8 | 0 | out of Solo v1 connector scope |
| vital | defer | 20 | 0 | out of Solo v1 connector scope |
| weather_in_your_calendar | defer | 10 | 0 | out of Solo v1 connector scope |
| webex | defer | 17 | 0 | out of Solo v1 connector scope |
| whatsapp | include/adapt | 11 | 0 | visible Solo connector or WhatsApp source |
| whereby | defer | 11 | 0 | out of Solo v1 connector scope |
| wipemycalother | defer | 11 | 0 | out of Solo v1 connector scope |
| wordpress | defer | 5 | 0 | out of Solo v1 connector scope |
| zapier | defer | 26 | 0 | out of Solo v1 connector scope |
| zoho-bigin | defer | 15 | 0 | out of Solo v1 connector scope |
| zohocalendar | defer | 17 | 0 | out of Solo v1 connector scope |
| zohocrm | defer | 16 | 0 | out of Solo v1 connector scope |
| zoomvideo | include/adapt | 20 | 1 | visible Solo connector or WhatsApp source |

## Docs App References

| Path | Class | Target |
| --- | --- | --- |
| cal.diy/apps/docs/app/[[...mdxPath]]/page.tsx | reference | connector/deployment implementation intent |
| cal.diy/apps/docs/app/fonts.css | reference | connector/deployment implementation intent |
| cal.diy/apps/docs/app/layout.tsx | reference | connector/deployment implementation intent |
| cal.diy/apps/docs/app/logo.css | reference | connector/deployment implementation intent |
| cal.diy/apps/docs/content/_meta.ts | reference | connector/deployment implementation intent |
| cal.diy/apps/docs/content/apps/_meta.ts | reference | connector/deployment implementation intent |
| cal.diy/apps/docs/content/apps/daily.mdx | reference | connector/deployment implementation intent |
| cal.diy/apps/docs/content/apps/google.mdx | reference | connector/deployment implementation intent |
| cal.diy/apps/docs/content/apps/hubspot.mdx | reference | connector/deployment implementation intent |
| cal.diy/apps/docs/content/apps/microsoft.mdx | reference | connector/deployment implementation intent |
| cal.diy/apps/docs/content/apps/sendgrid.mdx | reference | connector/deployment implementation intent |
| cal.diy/apps/docs/content/apps/stripe.mdx | reference | connector/deployment implementation intent |
| cal.diy/apps/docs/content/apps/twilio.mdx | reference | connector/deployment implementation intent |
| cal.diy/apps/docs/content/apps/zoho.mdx | reference | connector/deployment implementation intent |
| cal.diy/apps/docs/content/apps/zoom.mdx | reference | connector/deployment implementation intent |
| cal.diy/apps/docs/content/database-migrations.mdx | reference | connector/deployment implementation intent |
| cal.diy/apps/docs/content/deployments/_meta.ts | reference | connector/deployment implementation intent |
| cal.diy/apps/docs/content/deployments/aws.mdx | reference | connector/deployment implementation intent |
| cal.diy/apps/docs/content/deployments/azure.mdx | reference | connector/deployment implementation intent |
| cal.diy/apps/docs/content/deployments/elestio.mdx | reference | connector/deployment implementation intent |
| cal.diy/apps/docs/content/deployments/gcp.mdx | reference | connector/deployment implementation intent |
| cal.diy/apps/docs/content/deployments/northflank.mdx | reference | connector/deployment implementation intent |
| cal.diy/apps/docs/content/deployments/railway.mdx | reference | connector/deployment implementation intent |
| cal.diy/apps/docs/content/deployments/render.mdx | reference | connector/deployment implementation intent |
| cal.diy/apps/docs/content/deployments/vercel.mdx | reference | connector/deployment implementation intent |
| cal.diy/apps/docs/content/docker.mdx | reference | connector/deployment implementation intent |
| cal.diy/apps/docs/content/index.mdx | reference | connector/deployment implementation intent |
| cal.diy/apps/docs/content/installation.mdx | reference | connector/deployment implementation intent |
| cal.diy/apps/docs/content/troubleshooting.mdx | reference | connector/deployment implementation intent |
| cal.diy/apps/docs/content/upgrading.mdx | reference | connector/deployment implementation intent |
| cal.diy/apps/docs/fonts/CalSans-Regular.woff2 | reference | connector/deployment implementation intent |
| cal.diy/apps/docs/fonts/CalSansUI-UIBold.woff2 | reference | connector/deployment implementation intent |
| cal.diy/apps/docs/fonts/CalSansUI-UILight.woff2 | reference | connector/deployment implementation intent |
| cal.diy/apps/docs/fonts/CalSansUI-UIMedium.woff2 | reference | connector/deployment implementation intent |
| cal.diy/apps/docs/fonts/CalSansUI-UIRegular.woff2 | reference | connector/deployment implementation intent |
| cal.diy/apps/docs/fonts/CalSansUI-UISemiBold.woff2 | reference | connector/deployment implementation intent |
| cal.diy/apps/docs/mdx-components.ts | reference | connector/deployment implementation intent |
| cal.diy/apps/docs/next-env.d.ts | reference | connector/deployment implementation intent |
| cal.diy/apps/docs/next.config.mjs | reference | connector/deployment implementation intent |
| cal.diy/apps/docs/package.json | reference | connector/deployment implementation intent |
| cal.diy/apps/docs/public/cal-docs-logo-white.svg | reference | connector/deployment implementation intent |
| cal.diy/apps/docs/public/cal-docs-logo.svg | reference | connector/deployment implementation intent |
| cal.diy/apps/docs/tsconfig.json | reference | connector/deployment implementation intent |

## Source Coverage Rule

Any implementation task that touches one of these source areas must cite the row classification, exact Cal.diy paths, target Nerova files, and required tests. If a task discovers an unlisted source area, stop and update this inventory before coding.
