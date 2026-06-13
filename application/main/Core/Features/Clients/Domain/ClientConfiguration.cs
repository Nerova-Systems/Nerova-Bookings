using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharedKernel.Domain;
using SharedKernel.EntityFramework;

namespace Main.Features.Clients.Domain;

public sealed class ClientConfiguration : IEntityTypeConfiguration<Client>
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = JsonSerializerOptions.Default;

    private static readonly ValueComparer<Dictionary<string, string>> DictionaryComparer = new(
        (left, right) => left != null && right != null && left.Count == right.Count && left.All(pair => right.ContainsKey(pair.Key) && right[pair.Key] == pair.Value),
        value => value.Aggregate(0, (hash, pair) => HashCode.Combine(hash, pair.Key.GetHashCode(StringComparison.Ordinal), pair.Value.GetHashCode(StringComparison.Ordinal))),
        value => new Dictionary<string, string>(value)
    );

    public void Configure(EntityTypeBuilder<Client> builder)
    {
        builder.MapStronglyTypedUuid<Client, ClientId>(client => client.Id);
        builder.MapStronglyTypedLongId<Client, TenantId>(client => client.TenantId);

        builder.Property(client => client.FirstName).HasMaxLength(30);
        builder.Property(client => client.LastName).HasMaxLength(30);
        builder.Property(client => client.Email).HasMaxLength(100);
        builder.Property(client => client.PhoneNumber).HasMaxLength(30);
        builder.Property(client => client.AvatarUrl).HasMaxLength(500);

        builder.Property<Dictionary<string, string>>("VerticalFields")
            .HasColumnType("jsonb")
            .HasConversion(
                fields => JsonSerializer.Serialize(fields, JsonSerializerOptions),
                value => JsonSerializer.Deserialize<Dictionary<string, string>>(value, JsonSerializerOptions) ?? new Dictionary<string, string>()
            )
            // DEFAULT '{}' so EnsureCreated() (SQLite tests) accepts the existing raw-SQL client inserts
            // that omit the column — mirrors the TenantConfiguration HasDefaultValue convention.
            .HasDefaultValueSql("'{}'")
            .Metadata.SetValueComparer(DictionaryComparer);

        builder.Ignore(client => client.NeedsAttention);

        builder.HasIndex(client => new { client.TenantId, client.CreatedAt });
        builder.HasIndex(client => new { client.TenantId, client.Email });
        builder.HasIndex(client => new { client.TenantId, client.PhoneNumber });
    }
}
