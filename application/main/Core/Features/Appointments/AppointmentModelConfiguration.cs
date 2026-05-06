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

public sealed class BusinessLocationConfiguration : IEntityTypeConfiguration<BusinessLocation>
{
    public void Configure(EntityTypeBuilder<BusinessLocation> builder)
    {
        builder.HasKey(x => x.Id);
        builder.MapStronglyTypedLongId<BusinessLocation, TenantId>(x => x.TenantId);
        builder.Property(x => x.Name).HasMaxLength(160);
        builder.Property(x => x.TimeZone).HasMaxLength(80);
        builder.Property(x => x.Address).HasMaxLength(240);
        builder.HasIndex(x => new { x.TenantId, x.IsDefault });
    }
}

public sealed class BookableServiceConfiguration : IEntityTypeConfiguration<BookableService>
{
    public void Configure(EntityTypeBuilder<BookableService> builder)
    {
        builder.HasKey(x => x.Id);
        builder.MapStronglyTypedLongId<BookableService, TenantId>(x => x.TenantId);
        builder.Property(x => x.LocationId).HasMaxLength(64);
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
        builder.Property(x => x.LocationId).HasMaxLength(64);
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
        builder.Property(x => x.LocationId).HasMaxLength(64);
        builder.Property(x => x.UserId).HasMaxLength(32);
        builder.Property(x => x.Name).HasMaxLength(160);
    }
}

public sealed class SchedulingResourceConfiguration : IEntityTypeConfiguration<SchedulingResource>
{
    public void Configure(EntityTypeBuilder<SchedulingResource> builder)
    {
        builder.HasKey(x => x.Id);
        builder.MapStronglyTypedLongId<SchedulingResource, TenantId>(x => x.TenantId);
        builder.Property(x => x.LocationId).HasMaxLength(64);
        builder.Property(x => x.Name).HasMaxLength(160);
        builder.Property(x => x.Type).HasMaxLength(80);
    }
}

public sealed class BookableServiceResourceConfiguration : IEntityTypeConfiguration<BookableServiceResource>
{
    public void Configure(EntityTypeBuilder<BookableServiceResource> builder)
    {
        builder.HasKey(x => x.Id);
        builder.MapStronglyTypedLongId<BookableServiceResource, TenantId>(x => x.TenantId);
        builder.Property(x => x.ServiceId).HasMaxLength(64);
        builder.Property(x => x.ResourceId).HasMaxLength(64);
        builder.HasIndex(x => new { x.TenantId, x.ServiceId, x.ResourceId }).IsUnique();
    }
}

public sealed class ResourceReservationConfiguration : IEntityTypeConfiguration<ResourceReservation>
{
    public void Configure(EntityTypeBuilder<ResourceReservation> builder)
    {
        builder.HasKey(x => x.Id);
        builder.MapStronglyTypedLongId<ResourceReservation, TenantId>(x => x.TenantId);
        builder.Property(x => x.ResourceId).HasMaxLength(64);
        builder.Property(x => x.AppointmentId).HasMaxLength(64);
        builder.Property(x => x.Source).HasMaxLength(80);
        builder.HasIndex(x => new { x.TenantId, x.ResourceId, x.StartAt, x.EndAt });
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

public sealed class TenantMessagingProfileConfiguration : IEntityTypeConfiguration<TenantMessagingProfile>
{
    public void Configure(EntityTypeBuilder<TenantMessagingProfile> builder)
    {
        builder.HasKey(x => x.Id);
        builder.MapStronglyTypedLongId<TenantMessagingProfile, TenantId>(x => x.TenantId);
        builder.Property(x => x.AppSlug).HasMaxLength(40);
        builder.Property(x => x.Provider).HasMaxLength(40);
        builder.Property(x => x.OwnerType).HasConversion<string>().HasMaxLength(40);
        builder.Property(x => x.OwnerId).HasMaxLength(80);
        builder.Property(x => x.CountryCode).HasMaxLength(8);
        builder.Property(x => x.TwilioSubaccountSid).HasMaxLength(80);
        builder.Property(x => x.TwilioSubaccountStatus).HasMaxLength(40);
        builder.Property(x => x.TwilioMessagingServiceSid).HasMaxLength(80);
        builder.Property(x => x.ProvisioningStatus).HasMaxLength(40);
        builder.Property(x => x.WhatsAppApprovalStatus).HasMaxLength(40);
        builder.Property(x => x.DisplayName).HasMaxLength(160);
        builder.Property(x => x.BusinessCategory).HasMaxLength(80);
        builder.Property(x => x.Description).HasMaxLength(500);
        builder.Property(x => x.WebsiteUrl).HasMaxLength(320);
        builder.Property(x => x.SupportEmail).HasMaxLength(320);
        builder.Property(x => x.Address).HasMaxLength(320);
        builder.Property(x => x.LogoUrl).HasMaxLength(512);
        builder.HasIndex(x => new { x.TenantId, x.AppSlug, x.OwnerType, x.OwnerId }).IsUnique();
    }
}

public sealed class TenantPhoneNumberAssignmentConfiguration : IEntityTypeConfiguration<TenantPhoneNumberAssignment>
{
    public void Configure(EntityTypeBuilder<TenantPhoneNumberAssignment> builder)
    {
        builder.HasKey(x => x.Id);
        builder.MapStronglyTypedLongId<TenantPhoneNumberAssignment, TenantId>(x => x.TenantId);
        builder.Property(x => x.MessagingProfileId).HasMaxLength(64);
        builder.Property(x => x.PhoneNumber).HasMaxLength(32);
        builder.Property(x => x.TwilioPhoneNumberSid).HasMaxLength(80);
        builder.Property(x => x.CountryCode).HasMaxLength(8);
        builder.Property(x => x.WebhookUrl).HasMaxLength(512);
        builder.Property(x => x.AssignmentStatus).HasMaxLength(40);
        builder.HasIndex(x => new { x.TenantId, x.MessagingProfileId, x.AssignmentStatus });
        builder.HasIndex(x => x.PhoneNumber).IsUnique();
        builder.HasIndex(x => x.TwilioPhoneNumberSid).IsUnique();
    }
}

public sealed class TenantMessageTemplateConfiguration : IEntityTypeConfiguration<TenantMessageTemplate>
{
    public void Configure(EntityTypeBuilder<TenantMessageTemplate> builder)
    {
        builder.HasKey(x => x.Id);
        builder.MapStronglyTypedLongId<TenantMessageTemplate, TenantId>(x => x.TenantId);
        builder.Property(x => x.MessagingProfileId).HasMaxLength(64);
        builder.Property(x => x.TemplateKey).HasMaxLength(80);
        builder.Property(x => x.DisplayName).HasMaxLength(160);
        builder.Property(x => x.Category).HasMaxLength(40);
        builder.Property(x => x.Language).HasMaxLength(16);
        builder.Property(x => x.ApprovalStatus).HasMaxLength(40);
        builder.Property(x => x.ExternalTemplateId).HasMaxLength(160);
        builder.HasIndex(x => new { x.TenantId, x.MessagingProfileId, x.TemplateKey, x.Language }).IsUnique();
    }
}

public sealed class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
{
    public void Configure(EntityTypeBuilder<Appointment> builder)
    {
        builder.HasKey(x => x.Id);
        builder.MapStronglyTypedLongId<Appointment, TenantId>(x => x.TenantId);
        builder.HasIndex(x => x.PublicReference).IsUnique();
        builder.Property(x => x.LocationId).HasMaxLength(64);
        builder.Property(x => x.ServiceVersionId).HasMaxLength(64);
    }
}

public sealed class AppointmentParticipantConfiguration : IEntityTypeConfiguration<AppointmentParticipant>
{
    public void Configure(EntityTypeBuilder<AppointmentParticipant> builder)
    {
        builder.HasKey(x => x.Id);
        builder.MapStronglyTypedLongId<AppointmentParticipant, TenantId>(x => x.TenantId);
        builder.Property(x => x.AppointmentId).HasMaxLength(64);
        builder.Property(x => x.ClientId).HasMaxLength(64);
        builder.Property(x => x.Role).HasMaxLength(40);
        builder.HasIndex(x => new { x.TenantId, x.AppointmentId, x.ClientId }).IsUnique();
    }
}

public sealed class AppointmentRescheduleRequestConfiguration : IEntityTypeConfiguration<AppointmentRescheduleRequest>
{
    public void Configure(EntityTypeBuilder<AppointmentRescheduleRequest> builder)
    {
        builder.HasKey(x => x.Id);
        builder.MapStronglyTypedLongId<AppointmentRescheduleRequest, TenantId>(x => x.TenantId);
        builder.Property(x => x.AppointmentId).HasMaxLength(64);
        builder.Property(x => x.TokenHash).HasMaxLength(128);
        builder.Property(x => x.Note).HasMaxLength(1000);
        builder.Property(x => x.Status).HasMaxLength(32);
        builder.Property(x => x.NotificationChannel).HasMaxLength(32);
        builder.HasIndex(x => x.TokenHash).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.AppointmentId, x.Status });
    }
}

public sealed class AppointmentExternalCalendarEventConfiguration : IEntityTypeConfiguration<AppointmentExternalCalendarEvent>
{
    public void Configure(EntityTypeBuilder<AppointmentExternalCalendarEvent> builder)
    {
        builder.HasKey(x => x.Id);
        builder.MapStronglyTypedLongId<AppointmentExternalCalendarEvent, TenantId>(x => x.TenantId);
        builder.Property(x => x.AppointmentId).HasMaxLength(64);
        builder.Property(x => x.Provider).HasMaxLength(80);
        builder.Property(x => x.CalendarId).HasMaxLength(320);
        builder.Property(x => x.ExternalEventId).HasMaxLength(320);
        builder.Property(x => x.MeetUrl).HasMaxLength(512);
        builder.HasIndex(x => new { x.TenantId, x.AppointmentId, x.Provider }).IsUnique();
    }
}

public sealed class IntegrationCalendarConfiguration : IEntityTypeConfiguration<IntegrationCalendar>
{
    public void Configure(EntityTypeBuilder<IntegrationCalendar> builder)
    {
        builder.HasKey(x => x.Id);
        builder.MapStronglyTypedLongId<IntegrationCalendar, TenantId>(x => x.TenantId);
        builder.Property(x => x.IntegrationConnectionId).HasMaxLength(64);
        builder.Property(x => x.ExternalCalendarId).HasMaxLength(320);
        builder.Property(x => x.Name).HasMaxLength(240);
        builder.HasIndex(x => new { x.TenantId, x.IntegrationConnectionId, x.ExternalCalendarId }).IsUnique();
    }
}

public sealed class AppointmentPaymentIntentConfiguration : IEntityTypeConfiguration<AppointmentPaymentIntent>
{
    public void Configure(EntityTypeBuilder<AppointmentPaymentIntent> builder)
    {
        builder.HasKey(x => x.Id);
        builder.MapStronglyTypedLongId<AppointmentPaymentIntent, TenantId>(x => x.TenantId);
        builder.HasIndex(x => x.Reference).IsUnique();
        builder.HasIndex(x => x.PaymentTokenId);
        builder.Property(x => x.AppointmentId).HasMaxLength(64);
        builder.Property(x => x.PaymentTokenId).HasMaxLength(64);
        builder.Property(x => x.Provider).HasMaxLength(40);
        builder.Property(x => x.Channel).HasConversion<string>().HasMaxLength(40);
        builder.Property(x => x.Status).HasMaxLength(40);
        builder.Property(x => x.ProviderAccessCode).HasMaxLength(160);
        builder.Property(x => x.VirtualTerminalCode).HasMaxLength(80);
    }
}

public sealed class AppointmentPaymentTokenConfiguration : IEntityTypeConfiguration<AppointmentPaymentToken>
{
    public void Configure(EntityTypeBuilder<AppointmentPaymentToken> builder)
    {
        builder.HasKey(x => x.Id);
        builder.MapStronglyTypedLongId<AppointmentPaymentToken, TenantId>(x => x.TenantId);
        builder.HasIndex(x => x.TokenHash).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.AppointmentId, x.Status });
        builder.Property(x => x.AppointmentId).HasMaxLength(64);
        builder.Property(x => x.TokenHash).HasMaxLength(128);
        builder.Property(x => x.PaymentIntentId).HasMaxLength(64);
        builder.Property(x => x.Status).HasMaxLength(40);
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
        builder.Property(x => x.OwnerType).HasConversion<string>().HasMaxLength(40);
        builder.Property(x => x.OwnerId).HasMaxLength(80);
        builder.Property(x => x.ExternalConnectionId).HasMaxLength(160);
    }
}
