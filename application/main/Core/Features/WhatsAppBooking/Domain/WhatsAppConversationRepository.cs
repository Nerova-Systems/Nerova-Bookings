using Main.Database;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Domain;
using SharedKernel.EntityFramework;
using SharedKernel.Persistence;

namespace Main.Features.WhatsAppBooking.Domain;

public interface IWhatsAppConversationRepository : IAppendRepository<WhatsAppConversation, WhatsAppConversationId>
{
    /// <summary>
    ///     Looks up the conversation for a tenant + customer phone number, bypassing the tenant query filter.
    ///     Used during webhook processing when no tenant context is available.
    /// </summary>
    Task<WhatsAppConversation?> GetByTenantAndPhoneUnfilteredAsync(TenantId tenantId, string customerPhoneNumber, CancellationToken cancellationToken);

    /// <summary>Returns all conversations for the current tenant, most recently active first.</summary>
    Task<WhatsAppConversation[]> GetByTenantAsync(CancellationToken cancellationToken);

    void Update(WhatsAppConversation conversation);
}

public sealed class WhatsAppConversationRepository(MainDbContext mainDbContext)
    : RepositoryBase<WhatsAppConversation, WhatsAppConversationId>(mainDbContext), IWhatsAppConversationRepository
{
    public async Task<WhatsAppConversation?> GetByTenantAndPhoneUnfilteredAsync(TenantId tenantId, string customerPhoneNumber, CancellationToken cancellationToken)
    {
        return await DbSet
            .IgnoreQueryFilters([QueryFilterNames.Tenant])
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.CustomerPhoneNumber == customerPhoneNumber, cancellationToken);
    }

    public async Task<WhatsAppConversation[]> GetByTenantAsync(CancellationToken cancellationToken)
    {
        var conversations = await DbSet.ToListAsync(cancellationToken);
        return [.. conversations.OrderByDescending(c => c.LastInboundAt)];
    }

    public new void Update(WhatsAppConversation conversation)
    {
        base.Update(conversation);
    }
}
