using Microsoft.EntityFrameworkCore;
using Account.Features.FeatureFlags.Domain;
using Account.Features.Teams.Domain;
using SharedKernel.EntityFramework;
using SharedKernel.ExecutionContext;

namespace Account.Database;

public sealed class AccountDbContext(DbContextOptions<AccountDbContext> options, IExecutionContext executionContext, TimeProvider timeProvider)
    : SharedKernelDbContext<AccountDbContext>(options, executionContext, timeProvider)
{
    public DbSet<FeatureFlag> FeatureFlags => Set<FeatureFlag>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<TeamMember> TeamMembers => Set<TeamMember>();
}
