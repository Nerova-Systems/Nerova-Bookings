using System.Text.RegularExpressions;
using JetBrains.Annotations;
using SharedKernel.Domain;
using SharedKernel.StronglyTypedIds;

namespace Main.Features.Scheduling.Domain;

[IdPrefix("sprof")]
[JsonConverter(typeof(StronglyTypedIdJsonConverter<string, SchedulingProfileId>))]
public sealed record SchedulingProfileId(string Value) : StronglyTypedUlid<SchedulingProfileId>(Value)
{
    public override string ToString()
    {
        return Value;
    }
}

public sealed class SchedulingProfile : SoftDeletableAggregateRoot<SchedulingProfileId>, ITenantScopedEntity
{
    [UsedImplicitly]
    private SchedulingProfile() : base(SchedulingProfileId.NewId())
    {
        OwnerUserId = new UserId(string.Empty);
        Handle = string.Empty;
        DisplayName = string.Empty;
    }

    private SchedulingProfile(TenantId tenantId, UserId ownerUserId, string handle, string displayName, string? avatarUrl)
        : base(SchedulingProfileId.NewId())
    {
        TenantId = tenantId;
        OwnerUserId = ownerUserId;
        Update(handle, displayName, avatarUrl);
    }

    public UserId OwnerUserId { get; private set; }

    public string Handle { get; private set; } = string.Empty;

    public string DisplayName { get; private set; } = string.Empty;

    public string? AvatarUrl { get; private set; }

    /// <summary>
    ///     Paystack subaccount code that routes booking deposits and payments to the tenant's own bank
    ///     account (Paystack splits the charge automatically). Null until the tenant connects payouts.
    /// </summary>
    public string? PaystackSubaccountCode { get; private set; }

    /// <summary>
    ///     When non-null, references a Tenant of TenantKind.Team. When null, the aggregate is owned by the existing
    ///     user/solo scope.
    /// </summary>
    public TenantId? TeamId { get; private set; }

    public TenantId TenantId { get; } = new(0);

    /// <summary>
    ///     Assigns this scheduling profile to a team.
    /// </summary>
    /// <remarks>
    ///     The command layer is responsible for verifying that <paramref name="teamId" /> references a Tenant of
    ///     TenantKind.Team. This aggregate cannot verify TenantKind itself.
    /// </remarks>
    public void AssignToTeam(TenantId teamId)
    {
        // Command layer must ensure teamId refers to a TenantKind.Team tenant.
        TeamId = teamId;
    }

    /// <summary>
    ///     Removes the team association, reverting the scheduling profile to user/solo scope.
    /// </summary>
    public void RemoveFromTeam()
    {
        TeamId = null;
    }

    public static SchedulingProfile Create(TenantId tenantId, UserId ownerUserId, string handle, string displayName, string? avatarUrl, TenantId? teamId = null)
    {
        var profile = new SchedulingProfile(tenantId, ownerUserId, handle, displayName, avatarUrl);
        if (teamId is not null) profile.AssignToTeam(teamId);
        return profile;
    }

    public void Update(string handle, string displayName, string? avatarUrl)
    {
        Handle = NormalizeHandle(handle);
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? Handle : displayName.Trim();
        AvatarUrl = string.IsNullOrWhiteSpace(avatarUrl) ? null : avatarUrl.Trim();
    }

    public void SetPaystackSubaccount(string? subaccountCode)
    {
        PaystackSubaccountCode = string.IsNullOrWhiteSpace(subaccountCode) ? null : subaccountCode.Trim();
    }

    public static string NormalizeHandle(string value)
    {
        var normalized = Regex.Replace(value.Trim().ToLowerInvariant(), "[^a-z0-9]+", "-", RegexOptions.None, TimeSpan.FromSeconds(1));
        return normalized.Trim('-');
    }
}
