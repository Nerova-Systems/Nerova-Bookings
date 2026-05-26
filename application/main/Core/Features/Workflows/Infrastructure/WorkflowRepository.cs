using Main.Database;
using Main.Features.Workflows.Domain;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Domain;
using SharedKernel.Persistence;

namespace Main.Features.Workflows.Infrastructure;

public interface IWorkflowRepository : ICrudRepository<Workflow, WorkflowId>, ISoftDeletableRepository<Workflow, WorkflowId>
{
    Task<Workflow[]> GetForOwnerAsync(UserId ownerUserId, CancellationToken cancellationToken);

    Task<bool> NameExistsForOwnerAsync(UserId ownerUserId, string name, WorkflowId? excludedWorkflowId, CancellationToken cancellationToken);

    /// <summary>
    ///     Loads a workflow by ID including its owned Steps collection.
    ///     Use this whenever steps need to be read or mutated.
    /// </summary>
    Task<Workflow?> GetByIdWithStepsAsync(WorkflowId id, CancellationToken cancellationToken);

    /// <summary>
    ///     Explicitly registers a new owned step with EF's change tracker so it is inserted on SaveChanges.
    ///     Required because EF does not auto-discover new instances added to the OwnsMany backing field.
    /// </summary>
    void TrackNewStep(Workflow workflow, WorkflowStep step);

    /// <summary>
    ///     Explicitly registers a tracked owned step as deleted so it is removed on SaveChanges.
    /// </summary>
    void TrackRemovedStep(WorkflowStep step);
}

public sealed class WorkflowRepository(MainDbContext context)
    : SoftDeletableRepositoryBase<Workflow, WorkflowId>(context), IWorkflowRepository
{
    public async Task<Workflow?> GetByIdWithStepsAsync(WorkflowId id, CancellationToken cancellationToken)
    {
        return await DbSet
            .Include("Steps")
            .SingleOrDefaultAsync(w => w.Id == id, cancellationToken);
    }

    public void TrackNewStep(Workflow workflow, WorkflowStep step)
    {
        var entry = Context.Entry(step);
        entry.State = EntityState.Added;
        entry.Property("workflow_id").CurrentValue = workflow.Id;
    }

    public void TrackRemovedStep(WorkflowStep step)
    {
        Context.Entry(step).State = EntityState.Deleted;
    }

    public async Task<Workflow[]> GetForOwnerAsync(UserId ownerUserId, CancellationToken cancellationToken)
    {
        return await DbSet
            .Include("Steps")
            .Where(w => w.OwnerUserId == ownerUserId)
            .OrderBy(w => w.Name)
            .ThenBy(w => w.Id)
            .ToArrayAsync(cancellationToken);
    }

    public async Task<bool> NameExistsForOwnerAsync(UserId ownerUserId, string name, WorkflowId? excludedWorkflowId, CancellationToken cancellationToken)
    {
        var query = DbSet
            .Where(w => w.OwnerUserId == ownerUserId)
            .Where(w => w.Name == name.Trim());

        if (excludedWorkflowId is not null)
        {
            query = query.Where(w => w.Id != excludedWorkflowId);
        }

        return await query.AnyAsync(cancellationToken);
    }
}
