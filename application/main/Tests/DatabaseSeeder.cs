using Bogus;
using Main.Database;
using SharedKernel.Authentication;
using SharedKernel.Domain;
using FeatureFlagRegistry = SharedKernel.FeatureFlags.FeatureFlags;

namespace Main.Tests;

public sealed class DatabaseSeeder
{
    public readonly UserInfo Tenant1Member;
    public readonly UserInfo Tenant1Owner;
    public readonly TenantId TenantId;
    private readonly Faker _faker = new();

    public DatabaseSeeder(MainDbContext mainDbContext)
    {
        TenantId = TenantId.NewId();

        Tenant1Owner = new UserInfo
        {
            Email = "owner@tenant-1.com",
            FirstName = _faker.Person.FirstName,
            LastName = _faker.Person.LastName,
            Id = UserId.NewId(),
            IsAuthenticated = true,
            Locale = "en-US",
            Role = "Owner",
            TenantId = TenantId,
            FeatureFlags = new HashSet<string>
            {
                FeatureFlagRegistry.TierTeams.Key,
                FeatureFlagRegistry.TierOrganizations.Key,
                FeatureFlagRegistry.TierEnterprise.Key,
                FeatureFlagRegistry.CapManagedEventTypes.Key,
                FeatureFlagRegistry.CapRoundRobin.Key,
                FeatureFlagRegistry.CapCollective.Key,
                FeatureFlagRegistry.CapAttributes.Key,
                FeatureFlagRegistry.CapCustomSmtp.Key,
                FeatureFlagRegistry.CapOrgBilling.Key,
                FeatureFlagRegistry.CapDelegationCredentials.Key,
                FeatureFlagRegistry.CapSsoMicrosoft.Key,
                FeatureFlagRegistry.CapSsoGoogle.Key,
                FeatureFlagRegistry.CapIntegrationAttributeSync.Key,
                FeatureFlagRegistry.CapAuditLog.Key,
                FeatureFlagRegistry.CapWorkflows.Key,
                FeatureFlagRegistry.CapApiKeys.Key,
                FeatureFlagRegistry.CapImpersonation.Key,
                FeatureFlagRegistry.CapInsights.Key
            }
        };

        Tenant1Member = new UserInfo
        {
            Email = "member1@tenant-1.com",
            FirstName = _faker.Person.FirstName,
            LastName = _faker.Person.LastName,
            Id = UserId.NewId(),
            IsAuthenticated = true,
            Locale = "en-US",
            Role = "Member",
            TenantId = TenantId,
            FeatureFlags = new HashSet<string>
            {
                FeatureFlagRegistry.TierTeams.Key,
                FeatureFlagRegistry.TierOrganizations.Key,
                FeatureFlagRegistry.TierEnterprise.Key,
                FeatureFlagRegistry.CapManagedEventTypes.Key,
                FeatureFlagRegistry.CapRoundRobin.Key,
                FeatureFlagRegistry.CapCollective.Key,
                FeatureFlagRegistry.CapAttributes.Key,
                FeatureFlagRegistry.CapCustomSmtp.Key,
                FeatureFlagRegistry.CapOrgBilling.Key,
                FeatureFlagRegistry.CapDelegationCredentials.Key,
                FeatureFlagRegistry.CapSsoMicrosoft.Key,
                FeatureFlagRegistry.CapSsoGoogle.Key,
                FeatureFlagRegistry.CapIntegrationAttributeSync.Key,
                FeatureFlagRegistry.CapAuditLog.Key,
                FeatureFlagRegistry.CapWorkflows.Key,
                FeatureFlagRegistry.CapApiKeys.Key,
                FeatureFlagRegistry.CapImpersonation.Key,
                FeatureFlagRegistry.CapInsights.Key
            }
        };

        mainDbContext.SaveChanges();
    }
}
