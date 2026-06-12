using System.Collections.Immutable;
using System.Text.Json;
using Main.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharedKernel.Domain;
using SharedKernel.EntityFramework;
using SharedKernel.Persistence;

namespace Main.Features.DataImport.Domain;

public sealed class ImportJobConfiguration : IEntityTypeConfiguration<ImportJob>
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = JsonSerializerOptions.Default;

    public void Configure(EntityTypeBuilder<ImportJob> builder)
    {
        builder.MapStronglyTypedUuid<ImportJob, ImportJobId>(job => job.Id);
        builder.MapStronglyTypedLongId<ImportJob, TenantId>(job => job.TenantId);
        builder.MapStronglyTypedNullableId<ImportJob, UserId, string>(job => job.ApprovedByUserId);

        builder.Property(job => job.ColumnMapping)
            .HasColumnType("jsonb")
            .HasConversion(
                value => value == null ? null : JsonSerializer.Serialize(value, JsonSerializerOptions),
                value => value == null ? null : JsonSerializer.Deserialize<ImportColumnMapping>(value, JsonSerializerOptions)
            );

        builder.Property(job => job.Rows)
            .HasColumnType("jsonb")
            .HasConversion(
                value => JsonSerializer.Serialize(value.ToArray(), JsonSerializerOptions),
                value => JsonSerializer.Deserialize<ImmutableArray<ImportRowResult>>(value, JsonSerializerOptions)
            );
    }
}

public interface IImportJobRepository : IAppendRepository<ImportJob, ImportJobId>
{
    /// <summary>Returns the tenant's import jobs, newest first.</summary>
    Task<ImportJob[]> GetByTenantAsync(CancellationToken cancellationToken);

    void Update(ImportJob importJob);
}

public sealed class ImportJobRepository(MainDbContext mainDbContext)
    : RepositoryBase<ImportJob, ImportJobId>(mainDbContext), IImportJobRepository
{
    public async Task<ImportJob[]> GetByTenantAsync(CancellationToken cancellationToken)
    {
        var jobs = await DbSet.ToListAsync(cancellationToken);
        return [.. jobs.OrderByDescending(job => job.CreatedAt)];
    }

    public new void Update(ImportJob importJob)
    {
        base.Update(importJob);
    }
}
