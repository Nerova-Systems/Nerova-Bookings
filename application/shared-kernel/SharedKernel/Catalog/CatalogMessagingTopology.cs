namespace SharedKernel.Catalog;

public sealed record CatalogSubscriptionTopology(string TopicName, string BackOfficeSubscriptionName);

public static class CatalogMessagingTopology
{
    public const string TenantCatalogUpsertedTopic = "tenant-catalog-upserted";
    public const string TenantCatalogDeletedTopic = "tenant-catalog-deleted";
    public const string UserCatalogUpsertedTopic = "user-catalog-upserted";
    public const string UserCatalogDeletedTopic = "user-catalog-deleted";

    public const string BackOfficeTenantCatalogUpsertedSubscription = "back-office-tenant-catalog-upserted";
    public const string BackOfficeTenantCatalogDeletedSubscription = "back-office-tenant-catalog-deleted";
    public const string BackOfficeUserCatalogUpsertedSubscription = "back-office-user-catalog-upserted";
    public const string BackOfficeUserCatalogDeletedSubscription = "back-office-user-catalog-deleted";

    public static readonly CatalogSubscriptionTopology[] BackOfficeCatalogSubscriptions =
    [
        new(TenantCatalogUpsertedTopic, BackOfficeTenantCatalogUpsertedSubscription),
        new(TenantCatalogDeletedTopic, BackOfficeTenantCatalogDeletedSubscription),
        new(UserCatalogUpsertedTopic, BackOfficeUserCatalogUpsertedSubscription),
        new(UserCatalogDeletedTopic, BackOfficeUserCatalogDeletedSubscription)
    ];
}
