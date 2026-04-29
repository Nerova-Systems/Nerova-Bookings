using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharedKernel.Domain;
using SharedKernel.EntityFramework;

namespace Main.Features.Appointments;

public sealed class BusinessProfileConfiguration : IEntityTypeConfiguration<BusinessProfile>
{
    public void Configure(EntityTypeBuilder<BusinessProfile> builder)
    {
        builder.HasKey(x => x.Id);
        builder.MapStronglyTypedLongId<BusinessProfile, TenantId>(x => x.TenantId);
        builder.HasIndex(x => x.Slug).IsUnique();
        builder.Property(x => x.Name).HasMaxLength(160);
        builder.Property(x => x.Slug).HasMaxLength(120);
    }
}

public sealed class ServiceCategoryConfiguration : IEntityTypeConfiguration<ServiceCategory>
{
    public void Configure(EntityTypeBuilder<ServiceCategory> builder)
    {
        builder.HasKey(x => x.Id);
        builder.MapStronglyTypedLongId<ServiceCategory, TenantId>(x => x.TenantId);
        builder.Property(x => x.Name).HasMaxLength(120);
    }
}

public sealed class BookableServiceConfiguration : IEntityTypeConfiguration<BookableService>
{
    public void Configure(EntityTypeBuilder<BookableService> builder)
    {
        builder.HasKey(x => x.Id);
        builder.MapStronglyTypedLongId<BookableService, TenantId>(x => x.TenantId);
        builder.Property(x => x.Name).HasMaxLength(160);
        builder.Property(x => x.Mode).HasMaxLength(32);
    }
}

public sealed class StaffMemberConfiguration : IEntityTypeConfiguration<StaffMember>
{
    public void Configure(EntityTypeBuilder<StaffMember> builder)
    {
        builder.HasKey(x => x.Id);
        builder.MapStronglyTypedLongId<StaffMember, TenantId>(x => x.TenantId);
        builder.Property(x => x.Name).HasMaxLength(160);
    }
}

public sealed class AvailabilityRuleConfiguration : IEntityTypeConfiguration<AvailabilityRule>
{
    public void Configure(EntityTypeBuilder<AvailabilityRule> builder)
    {
        builder.HasKey(x => x.Id);
        builder.MapStronglyTypedLongId<AvailabilityRule, TenantId>(x => x.TenantId);
    }
}

public sealed class ExternalBusyBlockConfiguration : IEntityTypeConfiguration<ExternalBusyBlock>
{
    public void Configure(EntityTypeBuilder<ExternalBusyBlock> builder)
    {
        builder.HasKey(x => x.Id);
        builder.MapStronglyTypedLongId<ExternalBusyBlock, TenantId>(x => x.TenantId);
    }
}

public sealed class ClientConfiguration : IEntityTypeConfiguration<Client>
{
    public void Configure(EntityTypeBuilder<Client> builder)
    {
        builder.HasKey(x => x.Id);
        builder.MapStronglyTypedLongId<Client, TenantId>(x => x.TenantId);
        builder.Property(x => x.Name).HasMaxLength(160);
        builder.Property(x => x.Phone).HasMaxLength(64);
    }
}

public sealed class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
{
    public void Configure(EntityTypeBuilder<Appointment> builder)
    {
        builder.HasKey(x => x.Id);
        builder.MapStronglyTypedLongId<Appointment, TenantId>(x => x.TenantId);
        builder.HasIndex(x => x.PublicReference).IsUnique();
    }
}

public sealed class AppointmentPaymentIntentConfiguration : IEntityTypeConfiguration<AppointmentPaymentIntent>
{
    public void Configure(EntityTypeBuilder<AppointmentPaymentIntent> builder)
    {
        builder.HasKey(x => x.Id);
        builder.MapStronglyTypedLongId<AppointmentPaymentIntent, TenantId>(x => x.TenantId);
        builder.HasIndex(x => x.Reference).IsUnique();
    }
}

public sealed class AppointmentFlowEventConfiguration : IEntityTypeConfiguration<AppointmentFlowEvent>
{
    public void Configure(EntityTypeBuilder<AppointmentFlowEvent> builder)
    {
        builder.HasKey(x => x.Id);
        builder.MapStronglyTypedLongId<AppointmentFlowEvent, TenantId>(x => x.TenantId);
    }
}

public sealed class IntegrationConnectionConfiguration : IEntityTypeConfiguration<IntegrationConnection>
{
    public void Configure(EntityTypeBuilder<IntegrationConnection> builder)
    {
        builder.HasKey(x => x.Id);
        builder.MapStronglyTypedLongId<IntegrationConnection, TenantId>(x => x.TenantId);
    }
}
