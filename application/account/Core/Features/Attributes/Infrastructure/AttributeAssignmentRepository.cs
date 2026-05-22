using Account.Database;
using Account.Features.Attributes.Domain;
using Account.Features.Memberships.Domain;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Domain;
using SharedKernel.EntityFramework;
using SharedKernel.Persistence;

namespace Account.Features.Attributes.Infrastructure;

/// <summary>
///     Repository for <see cref="AttributeAssignment" /> aggregates.
/// </summary>
/// <remarks>
///     All methods bypass the global tenant query filter because
///     <see cref="AttributeAssignment.TenantId" /> is an org tenant ID, which differs from the
///     solo tenant ID held in the execution context. Scoping is done manually via predicate.
/// </remarks>
public sealed class AttributeAssignmentRepository(AccountDbContext context)
    : RepositoryBase<AttributeAssignment, AttributeAssignmentId>(context), IAttributeAssignmentRepository
{
    public async Task<IReadOnlyList<AttributeAssignment>> GetByMembershipAsync(
        MembershipId membershipId,
        CancellationToken cancellationToken)
    {
        return await DbSet
            .IgnoreQueryFilters([QueryFilterNames.Tenant])
            .Where(a => a.MembershipId == membershipId)
            .OrderByDescending(a => a.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AttributeAssignment>> GetByAttributeAsync(
        AttributeId attributeId,
        CancellationToken cancellationToken)
    {
        return await DbSet
            .IgnoreQueryFilters([QueryFilterNames.Tenant])
            .Where(a => a.AttributeId == attributeId)
            .OrderByDescending(a => a.Id)
            .ToListAsync(cancellationToken);
    }

    public Task<AttributeAssignment?> GetByMembershipAttributeOptionAsync(
        MembershipId membershipId,
        AttributeId attributeId,
        AttributeOptionId? optionId,
        CancellationToken cancellationToken)
    {
        return DbSet
            .IgnoreQueryFilters([QueryFilterNames.Tenant])
            .SingleOrDefaultAsync(
                a => a.MembershipId == membershipId
                     && a.AttributeId == attributeId
                     && a.AttributeOptionId == optionId,
                cancellationToken);
    }

    public async Task<IReadOnlyList<AttributeAssignment>> GetByOrgAsync(
        TenantId orgTenantId,
        CancellationToken cancellationToken)
    {
        return await DbSet
            .IgnoreQueryFilters([QueryFilterNames.Tenant])
            .Where(a => a.TenantId == orgTenantId)
            .OrderByDescending(a => a.Id)
            .ToListAsync(cancellationToken);
    }
}
