using Main.Database;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Domain;
using SharedKernel.EntityFramework;
using SharedKernel.Persistence;

namespace Main.Features.WhatsAppFlows.Domain;

public interface ITenantFlowConfigRepository : ICrudRepository<TenantFlowConfig, TenantFlowConfigId>
{
    Task<TenantFlowConfig?> GetByTenantIdAsync(TenantId tenantId, CancellationToken cancellationToken);
}

public sealed class TenantFlowConfigRepository(MainDbContext dbContext)
    : RepositoryBase<TenantFlowConfig, TenantFlowConfigId>(dbContext), ITenantFlowConfigRepository
{
    public Task<TenantFlowConfig?> GetByTenantIdAsync(TenantId tenantId, CancellationToken cancellationToken)
    {
        return DbSet.SingleOrDefaultAsync(t => t.TenantId == tenantId, cancellationToken);
    }
}
