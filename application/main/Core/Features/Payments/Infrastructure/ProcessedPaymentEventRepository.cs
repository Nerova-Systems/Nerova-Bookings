using Main.Database;
using Main.Features.Payments.Domain;
using Microsoft.EntityFrameworkCore;

namespace Main.Features.Payments.Infrastructure;

public interface IProcessedPaymentEventRepository
{
    Task<bool> IsProcessedAsync(string eventId, CancellationToken cancellationToken);

    Task AddAsync(ProcessedPaymentEvent entry, CancellationToken cancellationToken);
}

public sealed class ProcessedPaymentEventRepository(MainDbContext dbContext) : IProcessedPaymentEventRepository
{
    public Task<bool> IsProcessedAsync(string eventId, CancellationToken cancellationToken)
        => dbContext.Set<ProcessedPaymentEvent>().AsNoTracking().AnyAsync(e => e.EventId == eventId, cancellationToken);

    public async Task AddAsync(ProcessedPaymentEvent entry, CancellationToken cancellationToken)
    {
        await dbContext.Set<ProcessedPaymentEvent>().AddAsync(entry, cancellationToken);
    }
}
