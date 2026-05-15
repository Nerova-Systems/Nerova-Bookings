# Cal.diy Port Spec Pack

Date: 2026-05-15

Status: source-backed spec pack for future implementation. This document set changes no application behavior.

Local source: `cal.diy`

Related prior map: `docs/superpowers/specs/2026-05-11-cal-diy-scheduling-behavior-map.md`

## Purpose

This spec pack defines how NerovaBookings will port Cal.diy into Nerova's architecture. Cal.diy is the behavioral and layout source of truth, but the production runtime must be rewritten in Nerova standards: .NET backend, Postgres persistence, React/Rsbuild frontend, generated OpenAPI clients, Lingui translations, TanStack Query, and `@repo/ui`.

The port is not a Next.js, Prisma, tRPC, or Yarn runtime embed. Those sources are reference material for behavior, data shape, UI layout, and tests.

## Locked Decisions

- Add a first-class `Solo` subscription tier and isolate this port behind Solo eligibility.
- Keep Cal.diy behavior as the default answer unless this spec explicitly replaces it.
- Rewrite into Nerova's `main` self-contained system and existing account/subscription infrastructure.
- Replace Solo public web booking with a WhatsApp Business Flow booking surface.
- Keep the Solo app-store catalog to scheduling-critical workforce apps: `google-calendar`, `google-meet`, `office365-calendar`, `msteams`, `zoom`, and `whatsapp`.
- Fold Cal.diy's minimal static WhatsApp `wa.me` location behavior into the native WhatsApp Business Flow connector; do not expose a second static WhatsApp tile.
- Keep Cal.diy docs as supporting implementation intent, but treat code and tests as the primary source of truth.
- Retain MIT license attribution for copied, translated, or substantially derived Cal.diy code.

## Source Coverage

The source inventory was checked against the local workspace on 2026-05-15.

| Source area | Files found | Spec treatment |
| --- | ---: | --- |
| `cal.diy/apps` | 2,084 | Classified by API, web, docs, tests, and runtime support role. |
| `cal.diy/packages` | 4,981 | Classified by package, app-store connector, reusable library, UI package, test support, or deferred product area. |
| `cal.diy/apps/api/v2` | 635 | Communication contract and API behavior reference. |
| `cal.diy/apps/web/app` | 211 | Route, page, public booking, admin/dashboard, auth, settings, and app-store layout reference. |
| `cal.diy/apps/web/modules` | 446 | Admin workflow and page-module implementation reference. |
| `cal.diy/apps/web/components` | 111 | Shared web component reference. |
| `cal.diy/apps/docs/content` | 26 | Implementation guide and product-intent reference. |

## Pack Documents

- `01-source-inventory.md`: Source tree classification, apps/API/web/docs/packages inventory, app-store connector decisions, and package-level responsibilities.
- `02-target-nerova-mapping.md`: Target backend, database, API, frontend, and account/subscription mapping.
- `03-business-logic-and-algorithms.md`: Business rules, algorithms, redundancy, side effects, and source seams that must be preserved.
- `04-ui-atoms-and-web.md`: Web app route/module/component inventory, Cal.diy UI atoms, Nerova `@repo/ui` mapping, and layout fidelity rules.
- `05-connectors-and-whatsapp.md`: Workforce connector scope, OAuth/provider behavior, app-store tile behavior, and WhatsApp Flow booking replacement.
- `06-test-and-traceability.md`: Test inventory, required Nerova test equivalents, acceptance gates, and re-inventory commands.
- `07-coverage-scan-results.md`: Mechanical coverage scan results for source directories, Prisma names, route names, docs files, and test roots.

## Completion Contract

Implementation must not start from memory. After the latest upstream pull, the first implementation task is to re-run the inventory commands from `06-test-and-traceability.md` and update this spec pack if counts, modules, app-store entries, routes, or tests changed.

Every Cal.diy source area has one of these statuses:

- `Port`: behavior is required in the Solo port.
- `Replace`: behavior is required, but implemented through Nerova architecture rather than the Cal.diy runtime pattern.
- `Reference`: use as design, algorithm, test, or layout reference but do not expose the source feature as-is.
- `Defer`: intentionally out of v1, with a recorded reason.
- `Reject`: incompatible with the Solo/Nerova product direction.

No implementation task is accepted unless its source traceability row names the Cal.diy source area and target Nerova behavior.
