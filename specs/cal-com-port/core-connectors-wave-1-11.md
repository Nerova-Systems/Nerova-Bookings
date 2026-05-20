# Cal.com Core Connectors Wave 1.11 Manifest

## Scope

Wave 1.11 hardens the already-imported Google Calendar/Meet, Office 365 Calendar/video, and Zoom connector slice with deterministic fake-provider proof.

## Runtime Targets

- `application/main/Core/Features/BookingSideEffects/Domain/BookingSideEffectEnqueueHandler.cs`
- `application/main/Core/Features/BookingSideEffects/Workers/BookingSideEffectProcessor.cs`
- `application/main/Core/Features/BookingSideEffects/Shared/BookingSideEffectContracts.cs`
- `application/main/Core/Features/Connectors/Domain/CoreConnectorClient.cs`
- `application/main/Core/Features/Connectors/Commands/EnsureTestCoreConnectorCredentials.cs`
- `application/main/Api/Endpoints/CoreConnectorEndpoints.cs`
- `application/main/WebApp/tests/e2e/event-types-flows.spec.ts`

## Cal.com Source Rows Reviewed

- `packages/app-store/googlecalendar`
- `packages/app-store/office365calendar`
- `packages/app-store/zoomvideo`
- Cal.com selected-calendar, destination-calendar, connected-calendar, booking reference, and conferencing delivery behavior rows remain the source reference for runtime parity.

## Status Notes

- Core fake connector operation semantics are adapted into Main runtime: create, update, delete.
- Deterministic development fixtures are added for browser proof only; they are authenticated, tenant/user scoped, excluded from OpenAPI, and unavailable outside Development.
- Browser proof now covers connector account visibility, selected-calendar busy-window behavior, destination calendar persistence, and default conferencing persistence through the event type editor.
- Booking reference sync semantics remain implemented through fake connector deliveries: create/update upsert references, delete marks matching calendar/conferencing references deleted, and delivery summaries expose connector operation values.
- Real OAuth/provider HTTP remains blocked for the real-provider wave.
- Non-core connectors remain deferred-for-pruning per Wave 1.9 scope.
- External Cal.com API v1/v2 compatibility remains blocked for the later API compatibility wave.

## Tests

- `Main.Tests.Scheduling.CoreConnectorEndpointsTests`
- `Main.Tests.Scheduling.BookingSideEffectsTests`
- `Main.Tests.Scheduling.PublicSchedulingEndpointsTests`
- `application/main/WebApp/tests/e2e/event-types-flows.spec.ts`

## Verification

- `dotnet run --project developer-cli -- test --filter "FullyQualifiedName~Main.Tests.Scheduling.CoreConnectorEndpointsTests|FullyQualifiedName~Main.Tests.Scheduling.CoreConnectorProductionEndpointsTests|FullyQualifiedName~Main.Tests.Scheduling.BookingSideEffectsTests|FullyQualifiedName~Main.Tests.Scheduling.PublicSchedulingEndpointsTests" --no-build --quiet`
- `PUBLIC_URL=https://app.lvh.me:9000 BACK_OFFICE_PUBLIC_URL=https://back-office.lvh.me:9017 dotnet run --project developer-cli -- e2e "should create, edit, persist, duplicate, and delete event types" --no-wait-for-aspire --workers 1 --quiet`
- `PUBLIC_URL=https://app.lvh.me:9000 BACK_OFFICE_PUBLIC_URL=https://back-office.lvh.me:9017 dotnet run --project developer-cli -- e2e "event-types" --no-wait-for-aspire --quiet`
