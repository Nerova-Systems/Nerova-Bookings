using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharedKernel.Domain;
using SharedKernel.EntityFramework;

namespace Main.Features.Clients.Domain;

public sealed class ClientConfiguration : IEntityTypeConfiguration<Client>
{
    public void Configure(EntityTypeBuilder<Client> builder)
    {
        builder.MapStronglyTypedUuid<Client, ClientId>(client => client.Id);
        builder.MapStronglyTypedLongId<Client, TenantId>(client => client.TenantId);

        builder.Property(client => client.FirstName).HasMaxLength(30);
        builder.Property(client => client.LastName).HasMaxLength(30);
        builder.Property(client => client.Email).HasMaxLength(100);
        builder.Property(client => client.PhoneNumber).HasMaxLength(30);
        builder.Property(client => client.AvatarUrl).HasMaxLength(500);

        builder.Ignore(client => client.NeedsAttention);

        builder.HasIndex(client => new { client.TenantId, client.CreatedAt });
        builder.HasIndex(client => new { client.TenantId, client.Email });
        builder.HasIndex(client => new { client.TenantId, client.PhoneNumber });
    }
}
