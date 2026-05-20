using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using JetBrains.Annotations;
using SharedKernel.Domain;

namespace Main.Features.Connectors.Domain;

public sealed class ConnectorCredential : AggregateRoot<string>, ITenantScopedEntity
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerOptions.Default)
    {
        PropertyNameCaseInsensitive = true
    };

    [UsedImplicitly]
    private ConnectorCredential() : base(string.Empty)
    {
        OwnerUserId = new UserId(string.Empty);
        Integration = string.Empty;
        ExternalAccountId = string.Empty;
        AccountEmail = string.Empty;
        DisplayName = string.Empty;
        Status = string.Empty;
        SecretReference = string.Empty;
        CalendarsJson = "[]";
    }

    private ConnectorCredential(
        TenantId tenantId,
        string id,
        UserId ownerUserId,
        string integration,
        string externalAccountId,
        string accountEmail,
        string displayName,
        string status,
        string secretReference,
        CoreConnectorCalendar[] calendars
    ) : base(id)
    {
        TenantId = tenantId;
        OwnerUserId = ownerUserId;
        Integration = integration.Trim();
        ExternalAccountId = externalAccountId.Trim();
        AccountEmail = accountEmail.Trim().ToLowerInvariant();
        DisplayName = displayName.Trim();
        Status = status.Trim();
        SecretReference = secretReference.Trim();
        CalendarsJson = JsonSerializer.Serialize(calendars, JsonSerializerOptions);
    }

    public UserId OwnerUserId { get; private set; }

    public string Integration { get; private set; }

    public string ExternalAccountId { get; private set; }

    public string AccountEmail { get; private set; }

    public string DisplayName { get; private set; }

    public string Status { get; private set; }

    public string SecretReference { get; private set; }

    public string CalendarsJson { get; private set; }

    [NotMapped]
    public CoreConnectorCalendar[] Calendars => JsonSerializer.Deserialize<CoreConnectorCalendar[]>(CalendarsJson, JsonSerializerOptions) ?? [];

    public TenantId TenantId { get; } = new(0);

    public static ConnectorCredential Create(
        TenantId tenantId,
        string id,
        UserId ownerUserId,
        string integration,
        string externalAccountId,
        string accountEmail,
        string displayName,
        string status,
        string secretReference,
        CoreConnectorCalendar[] calendars
    )
    {
        return new ConnectorCredential(tenantId, id, ownerUserId, integration, externalAccountId, accountEmail, displayName, status, secretReference, calendars);
    }

    public void UpdateAccount(string accountEmail, string displayName, string status, string secretReference, CoreConnectorCalendar[] calendars)
    {
        AccountEmail = accountEmail.Trim().ToLowerInvariant();
        DisplayName = displayName.Trim();
        Status = status.Trim();
        SecretReference = secretReference.Trim();
        CalendarsJson = JsonSerializer.Serialize(calendars, JsonSerializerOptions);
    }
}

public sealed record CoreConnectorCalendar(string ExternalId, string Name, bool Primary);
