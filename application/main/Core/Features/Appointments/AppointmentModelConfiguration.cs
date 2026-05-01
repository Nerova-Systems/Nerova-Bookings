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
        builder.Property(x => x.LogoUrl).HasMaxLength(512);
        builder.Property(x => x.HolidayCountryCode).HasMaxLength(8);
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
        builder.Property(x => x.PaymentPolicy).HasConversion<string>().HasMaxLength(40);
    }
}

public sealed class BookableServiceVersionConfiguration : IEntityTypeConfiguration<BookableServiceVersion>
{
    public void Configure(EntityTypeBuilder<BookableServiceVersion> builder)
    {
        builder.HasKey(x => x.Id);
        builder.MapStronglyTypedLongId<BookableServiceVersion, TenantId>(x => x.TenantId);
        builder.HasIndex(x => new { x.ServiceId, x.VersionNumber }).IsUnique();
        builder.Property(x => x.Name).HasMaxLength(160);
        builder.Property(x => x.Mode).HasMaxLength(32);
        builder.Property(x => x.PaymentPolicy).HasConversion<string>().HasMaxLength(40);
        builder.Property(x => x.Location).HasMaxLength(240);
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

public sealed class BusinessClosureConfiguration : IEntityTypeConfiguration<BusinessClosure>
{
    public void Configure(EntityTypeBuilder<BusinessClosure> builder)
    {
        builder.HasKey(x => x.Id);
        builder.MapStronglyTypedLongId<BusinessClosure, TenantId>(x => x.TenantId);
        builder.HasIndex(x => new { x.TenantId, x.StartDate, x.EndDate });
        builder.Property(x => x.Label).HasMaxLength(160);
        builder.Property(x => x.Type).HasMaxLength(32);
    }
}

public sealed class ExternalBusyBlockConfiguration : IEntityTypeConfiguration<ExternalBusyBlock>
{
    public void Configure(EntityTypeBuilder<ExternalBusyBlock> builder)
    {
        builder.HasKey(x => x.Id);
        builder.MapStronglyTypedLongId<ExternalBusyBlock, TenantId>(x => x.TenantId);
        builder.Property(x => x.Provider).HasMaxLength(80);
        builder.Property(x => x.Label).HasMaxLength(160);
    }
}

public sealed class ManualCalendarBlockConfiguration : IEntityTypeConfiguration<ManualCalendarBlock>
{
    public void Configure(EntityTypeBuilder<ManualCalendarBlock> builder)
    {
        builder.HasKey(x => x.Id);
        builder.MapStronglyTypedLongId<ManualCalendarBlock, TenantId>(x => x.TenantId);
        builder.Property(x => x.StaffMemberId).HasMaxLength(64);
        builder.Property(x => x.Title).HasMaxLength(160);
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

public sealed class PublicPhoneVerificationConfiguration : IEntityTypeConfiguration<PublicPhoneVerification>
{
    public void Configure(EntityTypeBuilder<PublicPhoneVerification> builder)
    {
        builder.HasKey(x => x.Id);
        builder.MapStronglyTypedLongId<PublicPhoneVerification, TenantId>(x => x.TenantId);
        builder.HasIndex(x => new { x.TenantId, x.Phone, x.Status });
        builder.Property(x => x.Phone).HasMaxLength(32);
        builder.Property(x => x.MaskedPhone).HasMaxLength(32);
        builder.Property(x => x.Provider).HasMaxLength(40);
        builder.Property(x => x.ProviderSid).HasMaxLength(80);
        builder.Property(x => x.Status).HasMaxLength(32);
        builder.Property(x => x.VerificationTokenHash).HasMaxLength(128);
    }
}

public sealed class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
{
    public void Configure(EntityTypeBuilder<Appointment> builder)
    {
        builder.HasKey(x => x.Id);
        builder.MapStronglyTypedLongId<Appointment, TenantId>(x => x.TenantId);
        builder.HasIndex(x => x.PublicReference).IsUnique();
        builder.Property(x => x.ServiceVersionId).HasMaxLength(64);
    }
}

public sealed class AppointmentPaymentIntentConfiguration : IEntityTypeConfiguration<AppointmentPaymentIntent>
{
    public void Configure(EntityTypeBuilder<AppointmentPaymentIntent> builder)
    {
        builder.HasKey(x => x.Id);
        builder.MapStronglyTypedLongId<AppointmentPaymentIntent, TenantId>(x => x.TenantId);
        builder.HasIndex(x => x.Reference).IsUnique();
        builder.Property(x => x.Channel).HasConversion<string>().HasMaxLength(40);
        builder.Property(x => x.VirtualTerminalCode).HasMaxLength(80);
    }
}

public sealed class PaystackSubaccountConfiguration : IEntityTypeConfiguration<PaystackSubaccount>
{
    public void Configure(EntityTypeBuilder<PaystackSubaccount> builder)
    {
        builder.HasKey(x => x.Id);
        builder.MapStronglyTypedLongId<PaystackSubaccount, TenantId>(x => x.TenantId);
        builder.HasIndex(x => x.TenantId).IsUnique();
        builder.HasIndex(x => x.SubaccountCode).IsUnique();
        builder.Property(x => x.SubaccountCode).HasMaxLength(80);
        builder.Property(x => x.SplitCode).HasMaxLength(80);
        builder.Property(x => x.VirtualTerminalCode).HasMaxLength(80);
        builder.Property(x => x.BusinessName).HasMaxLength(160);
        builder.Property(x => x.SettlementBankName).HasMaxLength(160);
        builder.Property(x => x.SettlementBankCode).HasMaxLength(32);
        builder.Property(x => x.AccountName).HasMaxLength(160);
        builder.Property(x => x.MaskedAccountNumber).HasMaxLength(32);
        builder.Property(x => x.Currency).HasMaxLength(8);
        builder.Property(x => x.PrimaryContactName).HasMaxLength(160);
        builder.Property(x => x.PrimaryContactEmail).HasMaxLength(320);
        builder.Property(x => x.PrimaryContactPhone).HasMaxLength(64);
        builder.Property(x => x.SettlementSchedule).HasMaxLength(32);
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
