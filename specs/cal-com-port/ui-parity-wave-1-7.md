# Cal.com UI Pixel Parity Wave 1.7 Manifest

Baseline commit: `cf2a55c42363ab79982eef11610e1de8151b45ce`

Scope: screenshot-driven public booker reset first, then event type editor shell/tabs after the public booker harness is stable. Live `app.cal.com` is not a reference.

Copy-first destination: `application/main/WebApp/routes/-scheduling/calcom-port-source/ui-parity/wave-1-7/`

| Source area | Copied files | Runtime target | Screenshot evidence | Status |
| --- | --- | --- | --- | --- |
| Cal.com public booker layout | `public-booker/*.source` | `application/main/WebApp/routes/$handle/-booker/*` | `application/main/WebApp/tests/e2e/cal-com-ui-parity.spec.ts` | copied |
| Cal.com booker sizing/section helpers | `public-booker/Booker.config.ts.source`, `public-booker/Section.tsx.source` | `application/main/WebApp/routes/$handle/-booker/PublicBooker.tsx` | `application/main/WebApp/tests/e2e/cal-com-ui-parity.spec.ts` | copied |
| Cal.com event type editor shell/tabs | `event-type-editor/*.source` | `application/main/WebApp/routes/event-types/$eventTypeId.tsx`, `application/main/WebApp/routes/-scheduling/event-types-shell/*` | `application/main/WebApp/tests/e2e/cal-com-ui-parity.spec.ts` | copied |

Pixel-parity default: change shared UI primitives when a primitive prevents Cal.com-equivalent behavior; avoid route-local CSS workarounds unless the difference is specific to the Cal.com product surface.

