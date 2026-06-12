using Main.Database;
using Main.Features.WhatsAppBooking.Domain;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Domain;
using SharedKernel.EntityFramework;
using SharedKernel.Persistence;

namespace Main.Features.Receptionist.Domain;

public interface IReceptionistSessionRepository : IAppendRepository<ReceptionistSession, ReceptionistSessionId>
{
    /// <summary>
    ///     Looks up the session for a conversation, bypassing the tenant query filter. Used during webhook
    ///     processing when no tenant context is available.
    /// </summary>
    Task<ReceptionistSession?> GetByConversationUnfilteredAsync(WhatsAppConversationId whatsAppConversationId, CancellationToken cancellationToken);

    /// <summary>Sums input + output tokens across all sessions of a tenant since the given time (monthly budget enforcement).</summary>
    Task<long> GetTenantTokensUsedSinceUnfilteredAsync(TenantId tenantId, DateTimeOffset since, CancellationToken cancellationToken);

    void Update(ReceptionistSession session);
}

public sealed class ReceptionistSessionRepository(MainDbContext mainDbContext)
    : RepositoryBase<ReceptionistSession, ReceptionistSessionId>(mainDbContext), IReceptionistSessionRepository
{
    public async Task<ReceptionistSession?> GetByConversationUnfilteredAsync(WhatsAppConversationId whatsAppConversationId, CancellationToken cancellationToken)
    {
        return await DbSet
            .IgnoreQueryFilters([QueryFilterNames.Tenant])
            .FirstOrDefaultAsync(s => s.WhatsAppConversationId == whatsAppConversationId, cancellationToken);
    }

    public async Task<long> GetTenantTokensUsedSinceUnfilteredAsync(TenantId tenantId, DateTimeOffset since, CancellationToken cancellationToken)
    {
        return await DbSet
            .IgnoreQueryFilters([QueryFilterNames.Tenant])
            .Where(s => s.TenantId == tenantId && s.LastTurnAt >= since)
            .SumAsync(s => s.InputTokens + s.OutputTokens, cancellationToken);
    }

    public new void Update(ReceptionistSession session)
    {
        base.Update(session);
    }
}

public interface IEscalationRepository : IAppendRepository<Escalation, EscalationId>
{
    /// <summary>Returns the open escalation for a conversation if one exists, bypassing the tenant query filter.</summary>
    Task<Escalation?> GetOpenByConversationUnfilteredAsync(WhatsAppConversationId whatsAppConversationId, CancellationToken cancellationToken);

    /// <summary>Returns escalations for the current tenant, newest first, optionally filtered to open only.</summary>
    Task<Escalation[]> GetByTenantAsync(bool openOnly, CancellationToken cancellationToken);

    void Update(Escalation escalation);
}

public sealed class EscalationRepository(MainDbContext mainDbContext)
    : RepositoryBase<Escalation, EscalationId>(mainDbContext), IEscalationRepository
{
    public async Task<Escalation?> GetOpenByConversationUnfilteredAsync(WhatsAppConversationId whatsAppConversationId, CancellationToken cancellationToken)
    {
        return await DbSet
            .IgnoreQueryFilters([QueryFilterNames.Tenant])
            .FirstOrDefaultAsync(e => e.WhatsAppConversationId == whatsAppConversationId && e.Status == EscalationStatus.Open, cancellationToken);
    }

    public async Task<Escalation[]> GetByTenantAsync(bool openOnly, CancellationToken cancellationToken)
    {
        var query = DbSet.AsQueryable();
        if (openOnly)
        {
            query = query.Where(e => e.Status == EscalationStatus.Open);
        }

        var escalations = await query.ToListAsync(cancellationToken);
        return [.. escalations.OrderByDescending(e => e.CreatedAt)];
    }

    public new void Update(Escalation escalation)
    {
        base.Update(escalation);
    }
}

public interface IReceptionistSettingsRepository : IAppendRepository<ReceptionistSettings, ReceptionistSettingsId>
{
    /// <summary>Returns the settings row for the current tenant, or null when none has been created yet.</summary>
    Task<ReceptionistSettings?> GetByTenantAsync(CancellationToken cancellationToken);

    /// <summary>Looks up settings for a tenant, bypassing the tenant query filter (webhook routing).</summary>
    Task<ReceptionistSettings?> GetByTenantUnfilteredAsync(TenantId tenantId, CancellationToken cancellationToken);

    void Update(ReceptionistSettings settings);
}

public sealed class ReceptionistSettingsRepository(MainDbContext mainDbContext)
    : RepositoryBase<ReceptionistSettings, ReceptionistSettingsId>(mainDbContext), IReceptionistSettingsRepository
{
    public async Task<ReceptionistSettings?> GetByTenantAsync(CancellationToken cancellationToken)
    {
        return await DbSet.FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<ReceptionistSettings?> GetByTenantUnfilteredAsync(TenantId tenantId, CancellationToken cancellationToken)
    {
        return await DbSet
            .IgnoreQueryFilters([QueryFilterNames.Tenant])
            .FirstOrDefaultAsync(s => s.TenantId == tenantId, cancellationToken);
    }

    public new void Update(ReceptionistSettings settings)
    {
        base.Update(settings);
    }
}
