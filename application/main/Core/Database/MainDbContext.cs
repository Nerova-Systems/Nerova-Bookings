using Microsoft.EntityFrameworkCore;
using Main.Features.Appointments;
using SharedKernel.EntityFramework;
using SharedKernel.ExecutionContext;

namespace Main.Database;

public sealed class MainDbContext(DbContextOptions<MainDbContext> options, IExecutionContext executionContext, TimeProvider timeProvider)
    : SharedKernelDbContext<MainDbContext>(options, executionContext, timeProvider)
{
    public DbSet<BusinessProfile> BusinessProfiles => Set<BusinessProfile>();
    public DbSet<BusinessLocation> BusinessLocations => Set<BusinessLocation>();
    public DbSet<ServiceCategory> ServiceCategories => Set<ServiceCategory>();
    public DbSet<BookableService> BookableServices => Set<BookableService>();
    public DbSet<BookableServiceVersion> BookableServiceVersions => Set<BookableServiceVersion>();
    public DbSet<StaffMember> StaffMembers => Set<StaffMember>();
    public DbSet<SchedulingResource> SchedulingResources => Set<SchedulingResource>();
    public DbSet<BookableServiceResource> BookableServiceResources => Set<BookableServiceResource>();
    public DbSet<ResourceReservation> ResourceReservations => Set<ResourceReservation>();
    public DbSet<AvailabilityRule> AvailabilityRules => Set<AvailabilityRule>();
    public DbSet<BusinessClosure> BusinessClosures => Set<BusinessClosure>();
    public DbSet<ExternalBusyBlock> ExternalBusyBlocks => Set<ExternalBusyBlock>();
    public DbSet<ManualCalendarBlock> ManualCalendarBlocks => Set<ManualCalendarBlock>();
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<PublicPhoneVerification> PublicPhoneVerifications => Set<PublicPhoneVerification>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<AppointmentParticipant> AppointmentParticipants => Set<AppointmentParticipant>();
    public DbSet<AppointmentRescheduleRequest> AppointmentRescheduleRequests => Set<AppointmentRescheduleRequest>();
    public DbSet<AppointmentExternalCalendarEvent> AppointmentExternalCalendarEvents => Set<AppointmentExternalCalendarEvent>();
    public DbSet<AppointmentPaymentIntent> AppointmentPaymentIntents => Set<AppointmentPaymentIntent>();
    public DbSet<PaystackSubaccount> PaystackSubaccounts => Set<PaystackSubaccount>();
    public DbSet<AppointmentFlowEvent> AppointmentFlowEvents => Set<AppointmentFlowEvent>();
    public DbSet<IntegrationConnection> IntegrationConnections => Set<IntegrationConnection>();
    public DbSet<IntegrationCalendar> IntegrationCalendars => Set<IntegrationCalendar>();
}
