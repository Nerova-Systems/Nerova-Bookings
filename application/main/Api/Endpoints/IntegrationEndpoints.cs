using Main.Database;
using Main.Features.Appointments;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Domain;
using SharedKernel.Endpoints;
using SharedKernel.ExecutionContext;

namespace Main.Api.Endpoints;

public sealed class IntegrationEndpoints : IEndpoints
{
    private const string RoutesPrefix = "/api/main/integrations";
    private static readonly IntegrationAppDefinition GoogleCalendar = new("google-calendar", "google-calendar", "Google", "Calendar", ConnectorOwnerType.StaffMember);

    public void MapEndpoints(IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup(RoutesPrefix).WithTags("Integrations").RequireAuthorization();
        group.MapPost("/connect-session", CreateConnectSession);
        group.MapPost("/sync-connections", SyncConnections);
    }

    private static async Task<IResult> CreateConnectSession(IntegrationAppRequest request, MainDbContext db, INangoClient nangoClient, IExecutionContext executionContext, CancellationToken cancellationToken)
    {
        var definition = ResolveApp(request.AppSlug);
        if (definition is null) return Results.BadRequest("Unsupported integration app.");
        var owner = await ResolveOwnerAsync(definition, db, executionContext, cancellationToken);
        if (owner is null) return Results.BadRequest("Integration owner could not be resolved.");

        try
        {
            var session = await nangoClient.CreateConnectSessionAsync(
                new NangoConnectSessionRequest(
                    definition.IntegrationKey,
                    [definition.IntegrationKey],
                    BuildTags(definition, owner, executionContext)
                ),
                cancellationToken
            );
            return Results.Ok(new ConnectSessionResponse(session.ConnectLink, session.ExpiresAt, definition.IntegrationKey));
        }
        catch (NangoConfigurationException exception)
        {
            return Results.Problem(exception.Message, statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        catch (InvalidOperationException exception)
        {
            return Results.Problem(exception.Message, statusCode: StatusCodes.Status502BadGateway);
        }
    }

    private static async Task<IResult> SyncConnections(IntegrationAppRequest request, MainDbContext db, INangoClient nangoClient, IExecutionContext executionContext, TimeProvider timeProvider, CancellationToken cancellationToken)
    {
        var definition = ResolveApp(request.AppSlug);
        if (definition is null) return Results.BadRequest("Unsupported integration app.");
        var owner = await ResolveOwnerAsync(definition, db, executionContext, cancellationToken);
        if (owner is null) return Results.BadRequest("Integration owner could not be resolved.");

        try
        {
            var tags = BuildTags(definition, owner, executionContext);
            var nangoConnections = await nangoClient.ListConnectionsAsync(
                definition.IntegrationKey,
                new Dictionary<string, string>
                {
                    ["end_user_id"] = tags["end_user_id"],
                    ["organization_id"] = tags["organization_id"]
                },
                cancellationToken
            );
            var nangoConnection = nangoConnections.FirstOrDefault();
            if (nangoConnection is null)
            {
                return Results.Ok(Array.Empty<IntegrationConnectionDto>());
            }

            var connection = await db.IntegrationConnections.AsTracking().FirstOrDefaultAsync(
                item => item.Provider == definition.Provider &&
                        item.Capability == definition.Capability &&
                        item.OwnerType == definition.OwnerType &&
                        item.OwnerId == owner.OwnerId,
                cancellationToken
            );
            if (connection is null)
            {
                connection = new IntegrationConnection
                {
                    TenantId = owner.TenantId,
                    Provider = definition.Provider,
                    Capability = definition.Capability,
                    OwnerType = definition.OwnerType,
                    OwnerId = owner.OwnerId
                };
                db.IntegrationConnections.Add(connection);
            }

            connection.ExternalConnectionId = nangoConnection.ConnectionId;
            connection.Status = "Connected";
            connection.LastSyncedAt = nangoConnection.LastSyncedAt ?? timeProvider.GetUtcNow();
            var calendars = await nangoClient.ListCalendarsAsync(definition.IntegrationKey, connection.ExternalConnectionId, cancellationToken);
            foreach (var calendar in calendars)
            {
                var existingCalendar = await db.IntegrationCalendars.AsTracking().FirstOrDefaultAsync(
                    item => item.IntegrationConnectionId == connection.Id && item.ExternalCalendarId == calendar.Id,
                    cancellationToken
                );
                if (existingCalendar is null)
                {
                    existingCalendar = new IntegrationCalendar
                    {
                        TenantId = owner.TenantId,
                        IntegrationConnectionId = connection.Id,
                        ExternalCalendarId = calendar.Id,
                        AddEventsToCalendar = calendar.IsPrimary,
                        CheckForConflicts = calendar.IsPrimary
                    };
                    db.IntegrationCalendars.Add(existingCalendar);
                }

                existingCalendar.Name = calendar.Name;
                existingCalendar.IsPrimary = calendar.IsPrimary;
                existingCalendar.CanWrite = calendar.CanWrite;
                existingCalendar.LastSyncedAt = timeProvider.GetUtcNow();
            }
            await db.SaveChangesAsync(cancellationToken);
            return Results.Ok(new[]
            {
                new IntegrationConnectionDto(connection.Provider, connection.Capability, connection.Status, connection.LastSyncedAt, connection.OwnerType.ToString(), connection.OwnerId, connection.ExternalConnectionId)
            });
        }
        catch (NangoConfigurationException exception)
        {
            return Results.Problem(exception.Message, statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        catch (InvalidOperationException exception)
        {
            return Results.Problem(exception.Message, statusCode: StatusCodes.Status502BadGateway);
        }
    }

    private static IntegrationAppDefinition? ResolveApp(string? appSlug)
    {
        return string.Equals(appSlug, GoogleCalendar.AppSlug, StringComparison.OrdinalIgnoreCase) ? GoogleCalendar : null;
    }

    private static async Task<IntegrationOwner?> ResolveOwnerAsync(IntegrationAppDefinition definition, MainDbContext db, IExecutionContext executionContext, CancellationToken cancellationToken)
    {
        var tenantId = RequireTenant(executionContext);
        if (definition.OwnerType != ConnectorOwnerType.StaffMember)
        {
            return new IntegrationOwner(tenantId, definition.OwnerType, tenantId.ToString());
        }

        var staff = await db.StaffMembers.AsTracking().OrderByDescending(staff => staff.UserId == executionContext.UserInfo.Id!.ToString()).ThenBy(staff => staff.Name).FirstOrDefaultAsync(cancellationToken);
        if (staff is not null)
        {
            return new IntegrationOwner(tenantId, ConnectorOwnerType.StaffMember, staff.Id);
        }

        var profile = await db.BusinessProfiles.AsTracking().FirstOrDefaultAsync(cancellationToken);
        if (profile is null) return null;
        var location = await db.BusinessLocations.AsTracking().FirstOrDefaultAsync(location => location.IsDefault, cancellationToken);
        if (location is null)
        {
            location = new BusinessLocation
            {
                TenantId = tenantId,
                Name = profile.Name,
                TimeZone = profile.TimeZone,
                Address = profile.Address,
                IsDefault = true,
                IsActive = true
            };
            db.BusinessLocations.Add(location);
        }

        staff = new StaffMember
        {
            TenantId = tenantId,
            LocationId = location.Id,
            UserId = executionContext.UserInfo.Id?.ToString(),
            Name = profile.Name,
            Email = string.Empty,
            IsActive = true
        };
        db.StaffMembers.Add(staff);
        await db.SaveChangesAsync(cancellationToken);
        return new IntegrationOwner(tenantId, ConnectorOwnerType.StaffMember, staff.Id);
    }

    private static IReadOnlyDictionary<string, string> BuildTags(IntegrationAppDefinition definition, IntegrationOwner owner, IExecutionContext executionContext)
    {
        return new Dictionary<string, string>
        {
            ["tenant_id"] = owner.TenantId.ToString(),
            ["owner_type"] = owner.OwnerType.ToString(),
            ["owner_id"] = owner.OwnerId,
            ["provider"] = definition.Provider,
            ["capability"] = definition.Capability,
            ["app_slug"] = definition.AppSlug,
            ["end_user_id"] = owner.OwnerId,
            ["end_user_email"] = string.Empty,
            ["organization_id"] = owner.TenantId.ToString(),
            ["user_id"] = executionContext.UserInfo.Id?.ToString() ?? string.Empty
        };
    }

    private static TenantId RequireTenant(IExecutionContext executionContext)
    {
        return executionContext.TenantId ?? throw new InvalidOperationException("Tenant context is required.");
    }
}

public sealed record IntegrationAppRequest(string? AppSlug);
public sealed record ConnectSessionResponse(string ConnectLink, DateTimeOffset ExpiresAt, string IntegrationKey);

sealed record IntegrationAppDefinition(string AppSlug, string IntegrationKey, string Provider, string Capability, ConnectorOwnerType OwnerType);
sealed record IntegrationOwner(TenantId TenantId, ConnectorOwnerType OwnerType, string OwnerId);
