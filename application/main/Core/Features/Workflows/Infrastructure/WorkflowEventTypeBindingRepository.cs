using Main.Database;
using Main.Features.EventTypes.Domain;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Domain;
using SharedKernel.Persistence;

namespace Main.Features.Workflows.Domain;

public interface IWorkflowEventTypeBindingRepository : ICrudRepository<WorkflowEventTypeBinding, WorkflowEventTypeBindingId>
{
    Task<WorkflowEventTypeBinding[]> GetByWorkflowIdAsync(WorkflowId workflowId, CancellationToken cancellationToken);

    Task<WorkflowEventTypeBinding?> GetByWorkflowAndEventTypeAsync(WorkflowId workflowId, EventTypeId eventTypeId, CancellationToken cancellationToken);

    Task<bool> ExistsForEventTypeAsync(EventTypeId eventTypeId, CancellationToken cancellationToken);
}

public sealed class WorkflowEventTypeBindingRepository(MainDbContext context)
    : RepositoryBase<WorkflowEventTypeBinding, WorkflowEventTypeBindingId>(context), IWorkflowEventTypeBindingRepository
{
    public async Task<WorkflowEventTypeBinding[]> GetByWorkflowIdAsync(WorkflowId workflowId, CancellationToken cancellationToken)
    {
        return await DbSet
            .Where(b => b.WorkflowId == workflowId)
            .ToArrayAsync(cancellationToken);
    }

    public async Task<WorkflowEventTypeBinding?> GetByWorkflowAndEventTypeAsync(WorkflowId workflowId, EventTypeId eventTypeId, CancellationToken cancellationToken)
    {
        return await DbSet
            .Where(b => b.WorkflowId == workflowId && b.EventTypeId == eventTypeId)
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> ExistsForEventTypeAsync(EventTypeId eventTypeId, CancellationToken cancellationToken)
    {
        return await DbSet
            .Where(b => b.EventTypeId == eventTypeId)
            .AnyAsync(cancellationToken);
    }
}
