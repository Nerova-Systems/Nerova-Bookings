using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharedKernel.Domain;
using SharedKernel.EntityFramework;

namespace Account.Features.Teams.Domain;

public enum TeamMemberRole
{
    Member,
    Admin
}

public sealed class Team : ITenantScopedEntity
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public TenantId TenantId { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class TeamMember : ITenantScopedEntity
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public TenantId TenantId { get; set; } = null!;
    public string TeamId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public TeamMemberRole Role { get; set; } = TeamMemberRole.Member;
}

public sealed class TeamConfiguration : IEntityTypeConfiguration<Team>
{
    public void Configure(EntityTypeBuilder<Team> builder)
    {
        builder.HasKey(team => team.Id);
        builder.MapStronglyTypedLongId<Team, TenantId>(team => team.TenantId);
        builder.Property(team => team.Name).HasMaxLength(160);
        builder.Property(team => team.Description).HasMaxLength(500);
        builder.HasIndex(team => new { team.TenantId, team.Name });
    }
}

public sealed class TeamMemberConfiguration : IEntityTypeConfiguration<TeamMember>
{
    public void Configure(EntityTypeBuilder<TeamMember> builder)
    {
        builder.HasKey(member => member.Id);
        builder.MapStronglyTypedLongId<TeamMember, TenantId>(member => member.TenantId);
        builder.Property(member => member.TeamId).HasMaxLength(64);
        builder.Property(member => member.UserId).HasMaxLength(32);
        builder.Property(member => member.Role).HasConversion<string>().HasMaxLength(32);
        builder.HasIndex(member => new { member.TenantId, member.TeamId, member.UserId }).IsUnique();
    }
}
