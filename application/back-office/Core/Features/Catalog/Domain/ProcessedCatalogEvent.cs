namespace BackOffice.Features.Catalog.Domain;

public sealed class ProcessedCatalogEvent
{
    private ProcessedCatalogEvent()
    {
    }

    private ProcessedCatalogEvent(Guid id, DateTimeOffset processedAt)
    {
        Id = id;
        ProcessedAt = processedAt;
    }

    public Guid Id { get; private set; }

    public DateTimeOffset ProcessedAt { get; private set; }

    public static ProcessedCatalogEvent Create(Guid id, DateTimeOffset processedAt)
    {
        return new ProcessedCatalogEvent(id, processedAt);
    }
}
