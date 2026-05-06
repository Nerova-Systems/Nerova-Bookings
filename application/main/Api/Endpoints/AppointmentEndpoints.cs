using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Main.Database;
using Main.Features.Appointments;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Domain;
using SharedKernel.Endpoints;
using SharedKernel.ExecutionContext;
using SharedKernel.Integrations.Email;

namespace Main.Api.Endpoints;

public sealed class AppointmentEndpoints : IEndpoints
{
    public void MapEndpoints(IEndpointRouteBuilder routes)
    {
        var app = routes.MapGroup("/api/main/app").RequireAuthorization().WithTags("Nerova App");
        app.MapGet("/shell", GetShell);
        app.MapGet("/availability/slots", GetAuthenticatedSlots);
        app.MapPut("/availability/weekly", UpdateWeeklyAvailability);
        app.MapPut("/availability/holidays", UpdateHolidaySettings);
        app.MapPost("/availability/closures", CreateClosure);
        app.MapDelete("/availability/closures/{id}", DeleteClosure);
        app.MapPost("/services", CreateService);
        app.MapPut("/services/{id}", UpdateService);
        app.MapPost("/services/{id}/archive", ArchiveService);
        app.MapPost("/services/{id}/restore", RestoreService);
        app.MapPost("/calendar/blocks", CreateCalendarBlock);
        app.MapDelete("/calendar/blocks/{id}", DeleteCalendarBlock);
        app.MapPost("/appointments/{id}/participants", AddAppointmentParticipant);
        app.MapPut("/appointments/{id}/location", UpdateAppointmentLocation);
        app.MapPost("/appointments/{id}/reschedule-requests", CreateRescheduleRequest);
        app.MapPost("/appointments/{id}/confirm", ConfirmAppointment);
        app.MapPost("/appointments/{id}/status", UpdateAppointmentStatus);
        app.MapPost("/appointments/{id}/payments/terminal-intent", CreateTerminalPaymentIntent);
        app.MapPut("/clients/{id}", UpdateClient);
        app.MapPost("/payments/paystack/initialize", InitializePaystackPayment);
        app.MapGet("/payments/paystack/confirm", ConfirmPaystackPayment);
        app.MapGet("/payments/overview", GetPaymentOverview);
        app.MapGet("/payments/paystack/banks", GetPaystackBanks);
        app.MapPost("/payments/paystack/resolve-account", ResolvePaystackAccount);
        app.MapPost("/payments/paystack/subaccount", SavePaystackSubaccount);
        app.MapGet("/payments/paystack/settlements", GetPaystackSettlements);
        app.MapGet("/integrations", GetIntegrations);

        var booking = routes.MapGroup("/api/main/public-booking").WithTags("Public booking");
        booking.MapGet("/{businessSlug}", GetPublicBookingProfile);
        booking.MapGet("/{businessSlug}/client-prefill", GetPublicClientPrefill);
        booking.MapPost("/{businessSlug}/phone-verifications", StartPublicPhoneVerification).DisableAntiforgery();
        booking.MapPost("/{businessSlug}/phone-verifications/check", CheckPublicPhoneVerification).DisableAntiforgery();
        booking.MapGet("/{businessSlug}/slots", GetPublicSlots);
        booking.MapPost("/{businessSlug}/appointments", CreatePublicAppointment).DisableAntiforgery();
        booking.MapGet("/approvals/{token}", GetRescheduleApproval);
        booking.MapPost("/approvals/{token}/approve", ApproveRescheduleRequest).DisableAntiforgery();
        booking.MapPost("/approvals/{token}/reject", RejectRescheduleRequest).DisableAntiforgery();
        booking.MapGet("/confirmation/{reference}", GetPublicConfirmation);

        routes.MapPost("/api/main/payments/paystack/webhook", HandlePaystackWebhook).WithTags("Paystack").DisableAntiforgery();
        routes.MapGet("/api/main/payments/paystack/confirm", ConfirmPaystackPayment).WithTags("Paystack");
    }

    private static async Task<IResult> GetShell(MainDbContext db, IExecutionContext executionContext, TimeProvider timeProvider, CancellationToken cancellationToken)
    {
        var tenantId = RequireTenant(executionContext);
        await SeedTenantAsync(db, tenantId, timeProvider.GetUtcNow(), executionContext.UserInfo.Id?.ToString(), cancellationToken);
        return Results.Ok(await BuildShellAsync(db, cancellationToken, executionContext.UserInfo.Id?.ToString()));
    }

    private static async Task<IResult> CreateService(CreateServiceRequest request, MainDbContext db, IExecutionContext executionContext, TimeProvider timeProvider, CancellationToken cancellationToken)
    {
        var tenantId = RequireTenant(executionContext);
        await EnsureDefaultCategoryAsync(db, tenantId, cancellationToken);
        var location = await EnsureDefaultLocationAsync(db, tenantId, cancellationToken);
        var category = await db.ServiceCategories.AsTracking().FirstOrDefaultAsync(c => c.Name == request.CategoryName, cancellationToken);
        if (category is null)
        {
            category = new ServiceCategory { TenantId = tenantId, Name = request.CategoryName, SortOrder = 100 };
            db.ServiceCategories.Add(category);
        }
        var service = new BookableService
        {
            TenantId = tenantId,
            LocationId = location.Id,
            CategoryId = category.Id,
            Name = request.Name,
            Description = request.Description ?? string.Empty,
            Mode = request.Mode,
            DurationMinutes = request.DurationMinutes,
            PriceCents = request.PriceCents,
            DepositCents = request.DepositCents,
            PaymentPolicy = NormalizePaymentPolicy(request.PaymentPolicy, request.DepositCents),
            BufferBeforeMinutes = request.BufferBeforeMinutes,
            BufferAfterMinutes = request.BufferAfterMinutes,
            Location = request.Location,
            SortOrder = 100
        };
        db.BookableServices.Add(service);
        db.BookableServiceVersions.Add(CreateServiceVersion(service, 1, timeProvider.GetUtcNow()));
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(await BuildShellAsync(db, cancellationToken));
    }

    private static async Task<IResult> UpdateService(string id, CreateServiceRequest request, MainDbContext db, IExecutionContext executionContext, TimeProvider timeProvider, CancellationToken cancellationToken)
    {
        var tenantId = RequireTenant(executionContext);
        var service = await db.BookableServices.AsTracking().FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (service is null) return Results.NotFound();
        if (string.IsNullOrWhiteSpace(service.LocationId))
        {
            service.LocationId = (await EnsureDefaultLocationAsync(db, tenantId, cancellationToken)).Id;
        }
        var category = await db.ServiceCategories.AsTracking().FirstOrDefaultAsync(c => c.Name == request.CategoryName, cancellationToken);
        if (category is null)
        {
            category = new ServiceCategory { TenantId = tenantId, Name = request.CategoryName, SortOrder = 100 };
            db.ServiceCategories.Add(category);
        }
        service.CategoryId = category.Id;
        service.Name = request.Name;
        service.Description = request.Description ?? string.Empty;
        service.Mode = request.Mode;
        service.DurationMinutes = request.DurationMinutes;
        service.PriceCents = request.PriceCents;
        service.DepositCents = request.DepositCents;
        service.PaymentPolicy = NormalizePaymentPolicy(request.PaymentPolicy, request.DepositCents);
        service.BufferBeforeMinutes = request.BufferBeforeMinutes;
        service.BufferAfterMinutes = request.BufferAfterMinutes;
        service.Location = request.Location;
        await AddNextServiceVersionAsync(db, service, timeProvider.GetUtcNow(), cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(await BuildShellAsync(db, cancellationToken));
    }

    private static async Task<IResult> ArchiveService(string id, MainDbContext db, TimeProvider timeProvider, CancellationToken cancellationToken)
    {
        var service = await db.BookableServices.AsTracking().FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (service is null) return Results.NotFound();
        service.IsActive = false;
        await AddNextServiceVersionAsync(db, service, timeProvider.GetUtcNow(), cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(await BuildShellAsync(db, cancellationToken));
    }

    private static async Task<IResult> RestoreService(string id, MainDbContext db, TimeProvider timeProvider, CancellationToken cancellationToken)
    {
        var service = await db.BookableServices.AsTracking().FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (service is null) return Results.NotFound();
        service.IsActive = true;
        await AddNextServiceVersionAsync(db, service, timeProvider.GetUtcNow(), cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(await BuildShellAsync(db, cancellationToken));
    }

    private static async Task<IResult> CreateCalendarBlock(CreateCalendarBlockRequest request, MainDbContext db, IExecutionContext executionContext, TimeProvider timeProvider, CancellationToken cancellationToken)
    {
        var tenantId = RequireTenant(executionContext);
        if (request.EndAt <= request.StartAt) return Results.BadRequest("Block end time must be after start time.");
        var title = string.IsNullOrWhiteSpace(request.Title) ? "Blocked time" : request.Title.Trim();
        db.ManualCalendarBlocks.Add(new ManualCalendarBlock
        {
            TenantId = tenantId,
            StaffMemberId = string.IsNullOrWhiteSpace(request.StaffMemberId) ? null : request.StaffMemberId,
            Title = title,
            StartAt = request.StartAt.ToUniversalTime(),
            EndAt = request.EndAt.ToUniversalTime(),
            CreatedAt = timeProvider.GetUtcNow()
        });
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(await BuildShellAsync(db, cancellationToken));
    }

    private static async Task<IResult> DeleteCalendarBlock(string id, MainDbContext db, IExecutionContext executionContext, CancellationToken cancellationToken)
    {
        var tenantId = RequireTenant(executionContext);
        var block = await db.ManualCalendarBlocks.AsTracking().FirstOrDefaultAsync(item => item.Id == id && item.TenantId == tenantId, cancellationToken);
        if (block is null) return Results.NotFound();
        db.ManualCalendarBlocks.Remove(block);
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(await BuildShellAsync(db, cancellationToken));
    }

    private static async Task<IResult> AddAppointmentParticipant(string id, AppointmentParticipantRequest request, MainDbContext db, IExecutionContext executionContext, TimeProvider timeProvider, INangoClient nangoClient, CancellationToken cancellationToken)
    {
        var tenantId = RequireTenant(executionContext);
        var appointment = await db.Appointments.AsTracking().FirstOrDefaultAsync(appointment => appointment.Id == id && appointment.TenantId == tenantId, cancellationToken);
        if (appointment is null) return Results.NotFound();
        if (string.IsNullOrWhiteSpace(request.Name)) return Results.BadRequest("Guest name is required.");
        if (string.IsNullOrWhiteSpace(request.Phone) && string.IsNullOrWhiteSpace(request.Email)) return Results.BadRequest("Guest phone or email is required.");

        var normalizedPhone = NormalizePhone(request.Phone ?? string.Empty);
        var email = (request.Email ?? string.Empty).Trim().ToLowerInvariant();
        var candidates = await db.Clients.AsTracking().Where(client => client.TenantId == tenantId).ToListAsync(cancellationToken);
        var client = candidates.FirstOrDefault(client =>
            (!string.IsNullOrWhiteSpace(email) && string.Equals(client.Email, email, StringComparison.OrdinalIgnoreCase)) ||
            (!string.IsNullOrWhiteSpace(normalizedPhone) && NormalizePhone(client.Phone) == normalizedPhone));
        if (client is null)
        {
            client = new Client
            {
                TenantId = tenantId,
                Name = request.Name.Trim(),
                Phone = normalizedPhone,
                Email = email,
                Status = "Active"
            };
            db.Clients.Add(client);
        }

        if (!await db.AppointmentParticipants.AnyAsync(participant => participant.AppointmentId == id && participant.ClientId == client.Id, cancellationToken))
        {
            db.AppointmentParticipants.Add(new AppointmentParticipant
            {
                TenantId = tenantId,
                AppointmentId = id,
                ClientId = client.Id,
                CreatedAt = timeProvider.GetUtcNow()
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        await SyncGoogleCalendarEventIfConnectedAsync(db, appointment, nangoClient, timeProvider.GetUtcNow(), cancellationToken);
        return Results.Ok(await BuildShellAsync(db, cancellationToken));
    }

    private static async Task<IResult> UpdateAppointmentLocation(string id, UpdateAppointmentLocationRequest request, MainDbContext db, IExecutionContext executionContext, TimeProvider timeProvider, INangoClient nangoClient, IEmailClient emailClient, CancellationToken cancellationToken)
    {
        var tenantId = RequireTenant(executionContext);
        var appointment = await db.Appointments.AsTracking().FirstOrDefaultAsync(appointment => appointment.Id == id && appointment.TenantId == tenantId, cancellationToken);
        if (appointment is null) return Results.NotFound();
        if (string.IsNullOrWhiteSpace(request.Location)) return Results.BadRequest("Location is required.");

        var serviceVersion = await GetAppointmentServiceVersionAsync(db, appointment, cancellationToken);
        serviceVersion.Location = request.Location.Trim();
        await db.SaveChangesAsync(cancellationToken);
        await SyncGoogleCalendarEventIfConnectedAsync(db, appointment, nangoClient, timeProvider.GetUtcNow(), cancellationToken);
        await NotifyClientAsync(db, appointment, "Booking location updated", $"Your booking location is now: {serviceVersion.Location}", emailClient, cancellationToken);
        return Results.Ok(await BuildShellAsync(db, cancellationToken));
    }

    private static async Task<IResult> CreateRescheduleRequest(string id, CreateRescheduleRequest request, MainDbContext db, IExecutionContext executionContext, TimeProvider timeProvider, ITwilioWhatsAppClient whatsAppClient, IEmailClient emailClient, CancellationToken cancellationToken)
    {
        var tenantId = RequireTenant(executionContext);
        var appointment = await db.Appointments.AsTracking().FirstOrDefaultAsync(appointment => appointment.Id == id && appointment.TenantId == tenantId, cancellationToken);
        if (appointment is null) return Results.NotFound();
        if (appointment.Status == AppointmentStatus.Cancelled) return Results.BadRequest("Cancelled bookings cannot be rescheduled.");
        var serviceVersion = await GetAppointmentServiceVersionAsync(db, appointment, cancellationToken);
        var proposedStart = request.ProposedStartAt.ToUniversalTime();
        var proposedEnd = proposedStart.AddMinutes(serviceVersion.DurationMinutes);
        if (proposedStart <= timeProvider.GetUtcNow()) return Results.BadRequest("Choose a future time.");
        if (await HasAppointmentConflictAsync(db, appointment.TenantId, appointment.Id, proposedStart, proposedEnd, cancellationToken))
        {
            return Results.Conflict("Selected slot is no longer available.");
        }

        var now = timeProvider.GetUtcNow();
        var token = CreateApprovalToken();
        db.AppointmentRescheduleRequests.Add(new AppointmentRescheduleRequest
        {
            TenantId = appointment.TenantId,
            AppointmentId = appointment.Id,
            TokenHash = HashApprovalToken(token),
            ProposedStartAt = proposedStart,
            ProposedEndAt = proposedEnd,
            Note = request.Note?.Trim() ?? string.Empty,
            Status = "Pending",
            ExpiresAt = now.AddDays(7),
            CreatedAt = now
        });
        db.AppointmentFlowEvents.Add(new AppointmentFlowEvent
        {
            TenantId = appointment.TenantId,
            AppointmentId = appointment.Id,
            Type = "RescheduleRequested",
            Status = "Pending",
            ScheduledFor = now,
            PayloadJson = JsonSerializer.Serialize(new { proposedStartAt = proposedStart, proposedEndAt = proposedEnd })
        });
        await db.SaveChangesAsync(cancellationToken);

        var approvalUrl = BuildPublicUrl($"/book/approval/{token}");
        var message = $"Please approve or reject your booking reschedule request: {approvalUrl}";
        var client = await db.Clients.IgnoreQueryFilters().FirstAsync(client => client.Id == appointment.ClientId, cancellationToken);
        try
        {
            await whatsAppClient.SendAsync(appointment.TenantId, client.Phone, message, cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            return Results.Problem($"Reschedule request was saved, but WhatsApp could not be sent: {exception.Message}", statusCode: StatusCodes.Status502BadGateway);
        }
        await SendEmailIfAvailableAsync(client.Email, "Booking reschedule approval", message, emailClient, cancellationToken);
        return Results.Ok(new RescheduleRequestResponse(approvalUrl, token, "Pending"));
    }

    private static async Task<IResult> UpdateWeeklyAvailability(WeeklyAvailabilityRequest request, MainDbContext db, IExecutionContext executionContext, CancellationToken cancellationToken)
    {
        var tenantId = RequireTenant(executionContext);
        var rules = new List<AvailabilityRule>();
        var windowsByDay = new Dictionary<DayOfWeek, List<(TimeOnly Start, TimeOnly End)>>();

        foreach (var day in request.Days ?? [])
        {
            if (!TryParseDayOfWeek(day.DayOfWeek, out var dayOfWeek))
            {
                return Results.BadRequest("Unsupported day of week.");
            }

            var dayWindows = windowsByDay.GetValueOrDefault(dayOfWeek) ?? [];
            foreach (var window in day.Windows ?? [])
            {
                if (!TimeOnly.TryParse(window.StartTime, out var startTime) || !TimeOnly.TryParse(window.EndTime, out var endTime))
                {
                    return Results.BadRequest("Availability windows must use HH:mm times.");
                }
                if (endTime <= startTime)
                {
                    return Results.BadRequest("Availability window end time must be after start time.");
                }
                dayWindows.Add((startTime, endTime));
            }
            windowsByDay[dayOfWeek] = dayWindows;
        }

        foreach (var entry in windowsByDay)
        {
            var ordered = entry.Value.OrderBy(window => window.Start).ToList();
            for (var index = 1; index < ordered.Count; index++)
            {
                if (ordered[index].Start < ordered[index - 1].End)
                {
                    return Results.BadRequest("Availability windows cannot overlap.");
                }
            }

            rules.AddRange(ordered.Select(window => new AvailabilityRule
            {
                TenantId = tenantId,
                DayOfWeek = entry.Key,
                StartTime = window.Start,
                EndTime = window.End
            }));
        }

        var existing = await db.AvailabilityRules.AsTracking().Where(rule => rule.TenantId == tenantId).ToListAsync(cancellationToken);
        db.AvailabilityRules.RemoveRange(existing);
        db.AvailabilityRules.AddRange(rules);
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(await BuildShellAsync(db, cancellationToken));
    }

    private static async Task<IResult> UpdateHolidaySettings(HolidaySettingsRequest request, MainDbContext db, IExecutionContext executionContext, CancellationToken cancellationToken)
    {
        var tenantId = RequireTenant(executionContext);
        var profile = await db.BusinessProfiles.AsTracking().FirstAsync(profile => profile.TenantId == tenantId, cancellationToken);
        var countryCode = NormalizeHolidayCountryCode(request.CountryCode);
        var supportedHolidayIds = BuildPublicHolidays(countryCode, DateTimeOffset.UtcNow.Year - 1, DateTimeOffset.UtcNow.Year + 2)
            .Select(holiday => holiday.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var openHolidayIds = (request.OpenHolidayIds ?? [])
            .Where(id => supportedHolidayIds.Contains(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(id => id)
            .ToArray();
        profile.HolidayCountryCode = countryCode;
        profile.OpenPublicHolidayIdsJson = JsonSerializer.Serialize(openHolidayIds);
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(await BuildShellAsync(db, cancellationToken));
    }

    private static async Task<IResult> CreateClosure(CreateClosureRequest request, MainDbContext db, IExecutionContext executionContext, TimeProvider timeProvider, CancellationToken cancellationToken)
    {
        var tenantId = RequireTenant(executionContext);
        if (request.EndDate < request.StartDate) return Results.BadRequest("Closure end date must be on or after start date.");
        var label = string.IsNullOrWhiteSpace(request.Label) ? "Closed" : request.Label.Trim();
        db.BusinessClosures.Add(new BusinessClosure
        {
            TenantId = tenantId,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            Label = label,
            Type = "manual",
            CreatedAt = timeProvider.GetUtcNow()
        });
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(await BuildShellAsync(db, cancellationToken));
    }

    private static async Task<IResult> DeleteClosure(string id, MainDbContext db, IExecutionContext executionContext, CancellationToken cancellationToken)
    {
        var tenantId = RequireTenant(executionContext);
        var closure = await db.BusinessClosures.AsTracking().FirstOrDefaultAsync(item => item.Id == id && item.TenantId == tenantId, cancellationToken);
        if (closure is null) return Results.NotFound();
        db.BusinessClosures.Remove(closure);
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(await BuildShellAsync(db, cancellationToken));
    }

    private static async Task<IResult> ConfirmAppointment(string id, MainDbContext db, TimeProvider timeProvider, INangoClient nangoClient, IEmailClient emailClient, CancellationToken cancellationToken)
    {
        var appointment = await db.Appointments.AsTracking().FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (appointment is null) return Results.NotFound();
        appointment.Status = AppointmentStatus.Confirmed;
        await db.SaveChangesAsync(cancellationToken);
        await SyncGoogleCalendarEventIfConnectedAsync(db, appointment, nangoClient, timeProvider.GetUtcNow(), cancellationToken);
        await NotifyClientAsync(db, appointment, "Booking confirmed", "Your booking has been confirmed.", emailClient, cancellationToken);
        return Results.Ok(await BuildShellAsync(db, cancellationToken));
    }

    private static async Task<IResult> UpdateAppointmentStatus(string id, UpdateAppointmentStatusRequest request, MainDbContext db, TimeProvider timeProvider, INangoClient nangoClient, IEmailClient emailClient, CancellationToken cancellationToken)
    {
        var appointment = await db.Appointments.AsTracking().FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (appointment is null) return Results.NotFound();
        if (!Enum.TryParse<AppointmentStatus>(request.Status, true, out var status))
        {
            return Results.BadRequest("Unsupported appointment status.");
        }
        appointment.Status = status;
        if (!string.IsNullOrWhiteSpace(request.PaymentStatus)) return Results.BadRequest("Payment status is controlled by verified Paystack events.");
        db.AppointmentFlowEvents.Add(new AppointmentFlowEvent
        {
            TenantId = appointment.TenantId,
            AppointmentId = appointment.Id,
            Type = $"Status:{status}",
            Status = "Completed",
            ScheduledFor = timeProvider.GetUtcNow(),
            PayloadJson = "{}"
        });
        await db.SaveChangesAsync(cancellationToken);
        if (status == AppointmentStatus.Cancelled)
        {
            await DeleteGoogleCalendarEventIfConnectedAsync(db, appointment, nangoClient, cancellationToken);
            await NotifyClientAsync(db, appointment, "Booking cancelled", "Your booking has been cancelled.", emailClient, cancellationToken);
        }
        else if (status is AppointmentStatus.Completed or AppointmentStatus.NoShow or AppointmentStatus.Confirmed)
        {
            await SyncGoogleCalendarEventIfConnectedAsync(db, appointment, nangoClient, timeProvider.GetUtcNow(), cancellationToken);
        }
        return Results.Ok(await BuildShellAsync(db, cancellationToken));
    }

    private static async Task<IResult> UpdateClient(string id, UpdateClientRequest request, MainDbContext db, CancellationToken cancellationToken)
    {
        var client = await db.Clients.AsTracking().FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (client is null) return Results.NotFound();
        client.Name = request.Name;
        client.Phone = request.Phone;
        client.Email = request.Email;
        client.Status = request.Status;
        client.Alert = string.IsNullOrWhiteSpace(request.Alert) ? null : request.Alert;
        client.InternalNote = string.IsNullOrWhiteSpace(request.InternalNote) ? null : request.InternalNote;
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(await BuildShellAsync(db, cancellationToken));
    }

    private static async Task<IResult> GetAuthenticatedSlots(string serviceId, DateOnly date, MainDbContext db, CancellationToken cancellationToken)
    {
        var slots = await BuildSlotsAsync(db, serviceId, date, cancellationToken);
        return Results.Ok(slots);
    }

    private static async Task<IResult> GetPublicBookingProfile(string businessSlug, MainDbContext db, TimeProvider timeProvider, CancellationToken cancellationToken)
    {
        await SeedPublicDemoTenantAsync(db, timeProvider.GetUtcNow(), cancellationToken);
        var profile = await db.BusinessProfiles.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Slug == businessSlug && p.PublicBookingEnabled, cancellationToken);
        if (profile is null) return Results.NotFound();
        var services = await db.BookableServices.IgnoreQueryFilters()
            .Where(s => s.TenantId == profile.TenantId && s.IsActive)
            .OrderBy(s => s.SortOrder)
            .ToListAsync(cancellationToken);
        var serviceVersions = await db.BookableServiceVersions.IgnoreQueryFilters()
            .Where(v => v.TenantId == profile.TenantId)
            .ToListAsync(cancellationToken);
        return Results.Ok(new PublicBookingProfileResponse(profile.Name, profile.Slug, profile.TimeZone, profile.Address, profile.LogoUrl, services.Select(service => ToServiceDto(service, serviceVersions))));
    }

    private static async Task<IResult> GetPublicClientPrefill(string businessSlug, string phone, MainDbContext db, TimeProvider timeProvider, CancellationToken cancellationToken)
    {
        await SeedPublicDemoTenantAsync(db, timeProvider.GetUtcNow(), cancellationToken);
        var profile = await db.BusinessProfiles.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Slug == businessSlug && p.PublicBookingEnabled, cancellationToken);
        if (profile is null) return Results.NotFound();

        return Results.BadRequest("Verify your phone number before requesting saved booking details.");
    }

    private static async Task<IResult> StartPublicPhoneVerification(string businessSlug, StartPhoneVerificationRequest request, MainDbContext db, TimeProvider timeProvider, ITwilioVerifyClient twilioVerifyClient, CancellationToken cancellationToken)
    {
        await SeedPublicDemoTenantAsync(db, timeProvider.GetUtcNow(), cancellationToken);
        var profile = await db.BusinessProfiles.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Slug == businessSlug && p.PublicBookingEnabled, cancellationToken);
        if (profile is null) return Results.NotFound();

        var normalizedPhone = NormalizePhone(request.Phone);
        if (normalizedPhone.Length == 0) return Results.BadRequest("Enter a valid phone number.");

        var now = timeProvider.GetUtcNow();
        TwilioVerificationStarted started;
        try
        {
            started = await twilioVerifyClient.StartVerificationAsync(normalizedPhone, cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            return Results.BadRequest(ToPublicTwilioError(exception));
        }
        var verification = new PublicPhoneVerification
        {
            TenantId = profile.TenantId,
            Phone = normalizedPhone,
            MaskedPhone = MaskPhone(normalizedPhone),
            ProviderSid = started.Sid,
            Status = "Pending",
            CreatedAt = now,
            ExpiresAt = now.AddMinutes(10)
        };
        db.PublicPhoneVerifications.Add(verification);
        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(new StartPhoneVerificationResponse(verification.MaskedPhone, verification.ExpiresAt, 60));
    }

    private static async Task<IResult> CheckPublicPhoneVerification(string businessSlug, CheckPhoneVerificationRequest request, MainDbContext db, TimeProvider timeProvider, ITwilioVerifyClient twilioVerifyClient, CancellationToken cancellationToken)
    {
        await SeedPublicDemoTenantAsync(db, timeProvider.GetUtcNow(), cancellationToken);
        var profile = await db.BusinessProfiles.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Slug == businessSlug && p.PublicBookingEnabled, cancellationToken);
        if (profile is null) return Results.NotFound();

        var normalizedPhone = NormalizePhone(request.Phone);
        if (normalizedPhone.Length == 0) return Results.BadRequest("Enter a valid phone number.");

        var now = timeProvider.GetUtcNow();
        var pendingVerifications = await db.PublicPhoneVerifications.IgnoreQueryFilters().AsTracking()
            .Where(v => v.Phone == normalizedPhone && v.Status == "Pending")
            .ToListAsync(cancellationToken);
        var verification = pendingVerifications
            .Where(v => v.TenantId == profile.TenantId && v.ExpiresAt > now)
            .OrderByDescending(v => v.CreatedAt)
            .FirstOrDefault();
        if (verification is null) return Results.BadRequest("Start phone verification before checking a code.");

        bool approved;
        try
        {
            approved = await twilioVerifyClient.CheckVerificationAsync(normalizedPhone, request.Code, cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            return Results.BadRequest(ToPublicTwilioError(exception));
        }
        if (!approved) return Results.BadRequest("Invalid verification code.");

        var token = CreatePhoneVerificationToken();
        verification.Status = "Verified";
        verification.VerifiedAt = now;
        verification.ExpiresAt = now.AddMinutes(15);
        verification.VerificationTokenHash = HashPhoneVerificationToken(token);

        var tenantClients = await db.Clients.IgnoreQueryFilters()
            .Where(c => c.TenantId == profile.TenantId)
            .ToListAsync(cancellationToken);
        var client = tenantClients.FirstOrDefault(c => NormalizePhone(c.Phone) == normalizedPhone);
        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(new CheckPhoneVerificationResponse(token, verification.MaskedPhone, client?.Name ?? string.Empty, client?.Email ?? string.Empty));
    }

    private static async Task<IResult> GetPublicSlots(string businessSlug, string serviceId, DateOnly date, MainDbContext db, TimeProvider timeProvider, CancellationToken cancellationToken)
    {
        await SeedPublicDemoTenantAsync(db, timeProvider.GetUtcNow(), cancellationToken);
        var profile = await db.BusinessProfiles.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Slug == businessSlug && p.PublicBookingEnabled, cancellationToken);
        if (profile is null) return Results.NotFound();
        var slots = await BuildSlotsAsync(db, serviceId, date, cancellationToken, profile.TenantId);
        return Results.Ok(slots);
    }

    private static async Task<IResult> CreatePublicAppointment(string businessSlug, PublicBookingRequest request, MainDbContext db, TimeProvider timeProvider, IPaystackClient paystackClient, CancellationToken cancellationToken)
    {
        await SeedPublicDemoTenantAsync(db, timeProvider.GetUtcNow(), cancellationToken);
        var profile = await db.BusinessProfiles.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Slug == businessSlug && p.PublicBookingEnabled, cancellationToken);
        if (profile is null) return Results.NotFound();
        var service = await db.BookableServices.IgnoreQueryFilters().FirstOrDefaultAsync(s => s.Id == request.ServiceId && s.TenantId == profile.TenantId && s.IsActive, cancellationToken);
        if (service is null) return Results.BadRequest("Service is not bookable.");

        var serviceVersion = await GetLatestServiceVersionAsync(db, service, timeProvider.GetUtcNow(), cancellationToken);
        var startAt = request.StartAt.ToUniversalTime();
        var endAt = startAt.AddMinutes(serviceVersion.DurationMinutes);
        var requestedDate = DateOnly.FromDateTime(request.StartAt.ToOffset(TimeSpan.FromHours(2)).DateTime);
        var availableSlots = await BuildSlotsAsync(db, service.Id, requestedDate, cancellationToken, profile.TenantId);
        if (!availableSlots.Any(slot => slot.StartAt.ToUniversalTime() == request.StartAt.ToUniversalTime()))
        {
            return Results.Conflict("Selected slot is no longer available.");
        }
        var existingAppointments = await db.Appointments.IgnoreQueryFilters()
            .Where(a => a.TenantId == profile.TenantId && a.Status != AppointmentStatus.Cancelled)
            .ToListAsync(cancellationToken);
        var overlaps = existingAppointments.Any(a => a.StartAt < endAt && a.EndAt > startAt);
        if (overlaps) return Results.Conflict("Selected slot is no longer available.");

        var normalizedPhone = NormalizePhone(request.Phone);
        var verification = await ConsumePhoneVerificationAsync(db, profile.TenantId, normalizedPhone, request.PhoneVerificationToken, timeProvider.GetUtcNow(), cancellationToken);
        if (verification is null) return Results.BadRequest("Verify your phone number before booking.");

        var tenantClients = await db.Clients.IgnoreQueryFilters().AsTracking()
            .Where(c => c.TenantId == profile.TenantId)
            .ToListAsync(cancellationToken);
        var client = tenantClients.FirstOrDefault(c => NormalizePhone(c.Phone) == normalizedPhone);
        if (client is null)
        {
            client = new Client { TenantId = profile.TenantId, Name = request.Name, Phone = verification.Phone, Email = request.Email, Status = "New" };
            db.Clients.Add(client);
        }
        else
        {
            client.Phone = verification.Phone;
        }

        var staff = await db.StaffMembers.IgnoreQueryFilters().FirstAsync(s => s.TenantId == profile.TenantId && s.IsActive, cancellationToken);
        var paymentPolicy = NormalizePaymentPolicy(serviceVersion.PaymentPolicy.ToString(), serviceVersion.DepositCents);
        var requiresHostedPayment = paymentPolicy is ServicePaymentPolicy.DepositBeforeBooking or ServicePaymentPolicy.FullPaymentBeforeBooking;
        var appointment = new Appointment
        {
            TenantId = profile.TenantId,
            ClientId = client.Id,
            LocationId = string.IsNullOrWhiteSpace(serviceVersion.LocationId) ? service.LocationId : serviceVersion.LocationId,
            ServiceId = service.Id,
            ServiceVersionId = serviceVersion.Id,
            StaffMemberId = staff.Id,
            StartAt = startAt,
            EndAt = endAt,
            Status = requiresHostedPayment ? AppointmentStatus.Pending : AppointmentStatus.Confirmed,
            PaymentStatus = paymentPolicy == ServicePaymentPolicy.NoPaymentRequired ? AppointmentPaymentStatus.NotRequired : AppointmentPaymentStatus.Pending,
            Source = AppointmentSource.PublicBookingPage,
            AnswersJson = JsonSerializer.Serialize(request.Answers),
            CreatedAt = timeProvider.GetUtcNow()
        };
        db.Appointments.Add(appointment);
        AddFlowEvents(db, appointment, timeProvider.GetUtcNow());

        string? paymentUrl = null;
        if (requiresHostedPayment)
        {
            var subaccountCode = await GetActivePaystackSubaccountCodeAsync(db, profile.TenantId, true, cancellationToken);
            if (subaccountCode is null) return Results.BadRequest("Connect Paystack payouts before accepting appointment payments.");
            var intent = CreatePaymentIntent(profile.TenantId, appointment.Id, GetPaymentAmountCents(serviceVersion), AppointmentPaymentChannel.HostedCheckout, timeProvider.GetUtcNow());
            intent.AuthorizationUrl = await TryInitializePaystackTransactionAsync(intent, request.Email, subaccountCode, paystackClient, cancellationToken);
            paymentUrl = intent.AuthorizationUrl;
            db.AppointmentPaymentIntents.Add(intent);
        }

        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(new PublicBookingCreatedResponse(appointment.PublicReference, requiresHostedPayment, paymentUrl));
    }

    private static async Task<IResult> GetPublicConfirmation(string reference, MainDbContext db, CancellationToken cancellationToken)
    {
        var appointment = await db.Appointments.IgnoreQueryFilters().FirstOrDefaultAsync(a => a.PublicReference == reference, cancellationToken);
        if (appointment is null) return Results.NotFound();
        return Results.Ok(await ToAppointmentDetailAsync(db, appointment, cancellationToken));
    }

    private static async Task<IResult> GetRescheduleApproval(string token, MainDbContext db, TimeProvider timeProvider, CancellationToken cancellationToken)
    {
        var request = await FindPendingApprovalAsync(db, token, timeProvider.GetUtcNow(), cancellationToken);
        if (request is null) return Results.NotFound();
        var appointment = await db.Appointments.IgnoreQueryFilters().FirstAsync(appointment => appointment.Id == request.AppointmentId, cancellationToken);
        var detail = await ToAppointmentDetailAsync(db, appointment, cancellationToken);
        return Results.Ok(new RescheduleApprovalResponse(detail, request.ProposedStartAt, request.ProposedEndAt, request.Note, request.Status));
    }

    private static async Task<IResult> ApproveRescheduleRequest(string token, MainDbContext db, TimeProvider timeProvider, INangoClient nangoClient, IEmailClient emailClient, CancellationToken cancellationToken)
    {
        var request = await FindPendingApprovalAsync(db, token, timeProvider.GetUtcNow(), cancellationToken);
        if (request is null) return Results.NotFound();
        var appointment = await db.Appointments.IgnoreQueryFilters().AsTracking().FirstAsync(appointment => appointment.Id == request.AppointmentId, cancellationToken);
        if (await HasAppointmentConflictAsync(db, appointment.TenantId, appointment.Id, request.ProposedStartAt, request.ProposedEndAt, cancellationToken))
        {
            return Results.Conflict("Selected slot is no longer available.");
        }

        appointment.StartAt = request.ProposedStartAt;
        appointment.EndAt = request.ProposedEndAt;
        appointment.Status = AppointmentStatus.Confirmed;
        request.Status = "Approved";
        request.RespondedAt = timeProvider.GetUtcNow();
        db.AppointmentFlowEvents.Add(new AppointmentFlowEvent
        {
            TenantId = appointment.TenantId,
            AppointmentId = appointment.Id,
            Type = "RescheduleApproved",
            Status = "Completed",
            ScheduledFor = timeProvider.GetUtcNow(),
            PayloadJson = "{}"
        });
        await db.SaveChangesAsync(cancellationToken);
        await SyncGoogleCalendarEventIfConnectedAsync(db, appointment, nangoClient, timeProvider.GetUtcNow(), cancellationToken);
        await NotifyClientAsync(db, appointment, "Booking rescheduled", "Your booking reschedule has been approved.", emailClient, cancellationToken);
        return Results.Ok(new PublicApprovalResultResponse(appointment.PublicReference, "Approved"));
    }

    private static async Task<IResult> RejectRescheduleRequest(string token, MainDbContext db, TimeProvider timeProvider, IEmailClient emailClient, CancellationToken cancellationToken)
    {
        var request = await FindPendingApprovalAsync(db, token, timeProvider.GetUtcNow(), cancellationToken);
        if (request is null) return Results.NotFound();
        var appointment = await db.Appointments.IgnoreQueryFilters().FirstAsync(appointment => appointment.Id == request.AppointmentId, cancellationToken);
        request.Status = "Rejected";
        request.RespondedAt = timeProvider.GetUtcNow();
        db.AppointmentFlowEvents.Add(new AppointmentFlowEvent
        {
            TenantId = appointment.TenantId,
            AppointmentId = appointment.Id,
            Type = "RescheduleRejected",
            Status = "Completed",
            ScheduledFor = timeProvider.GetUtcNow(),
            PayloadJson = "{}"
        });
        await db.SaveChangesAsync(cancellationToken);
        await NotifyClientAsync(db, appointment, "Booking reschedule rejected", "Your original booking time is unchanged.", emailClient, cancellationToken);
        return Results.Ok(new PublicApprovalResultResponse(appointment.PublicReference, "Rejected"));
    }

    private static async Task<IResult> InitializePaystackPayment(PaystackInitializeRequest request, MainDbContext db, TimeProvider timeProvider, IPaystackClient paystackClient, CancellationToken cancellationToken)
    {
        var appointment = await db.Appointments.AsTracking().FirstOrDefaultAsync(a => a.Id == request.AppointmentId, cancellationToken);
        if (appointment is null) return Results.NotFound();
        var subaccountCode = await GetActivePaystackSubaccountCodeAsync(db, appointment.TenantId, false, cancellationToken);
        if (subaccountCode is null) return Results.BadRequest("Connect Paystack payouts before accepting appointment payments.");
        var serviceVersion = await GetAppointmentServiceVersionAsync(db, appointment, cancellationToken);
        var paymentPolicy = NormalizePaymentPolicy(serviceVersion.PaymentPolicy.ToString(), serviceVersion.DepositCents);
        if (paymentPolicy == ServicePaymentPolicy.CollectAfterAppointment)
        {
            return Results.BadRequest("Use the virtual terminal to collect after-appointment payments.");
        }
        var intent = CreatePaymentIntent(appointment.TenantId, appointment.Id, GetPaymentAmountCents(serviceVersion), AppointmentPaymentChannel.HostedCheckout, timeProvider.GetUtcNow());
        var client = await db.Clients.FirstAsync(c => c.Id == appointment.ClientId, cancellationToken);
        intent.AuthorizationUrl = await TryInitializePaystackTransactionAsync(intent, client.Email, subaccountCode, paystackClient, cancellationToken);
        db.AppointmentPaymentIntents.Add(intent);
        appointment.PaymentStatus = AppointmentPaymentStatus.Pending;
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(new PaystackInitializeResponse(intent.Reference, intent.AuthorizationUrl, intent.AmountCents));
    }

    private static async Task<IResult> CreateTerminalPaymentIntent(string id, MainDbContext db, TimeProvider timeProvider, IPaystackClient paystackClient, CancellationToken cancellationToken)
    {
        var appointment = await db.Appointments.AsTracking().FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (appointment is null) return Results.NotFound();
        var serviceVersion = await GetAppointmentServiceVersionAsync(db, appointment, cancellationToken);
        if (NormalizePaymentPolicy(serviceVersion.PaymentPolicy.ToString(), serviceVersion.DepositCents) != ServicePaymentPolicy.CollectAfterAppointment)
        {
            return Results.BadRequest("This service is not configured for virtual terminal collection.");
        }
        if (appointment.PaymentStatus is AppointmentPaymentStatus.Paid or AppointmentPaymentStatus.DepositPaid or AppointmentPaymentStatus.NotRequired)
        {
            return Results.BadRequest("This appointment does not have an outstanding payment.");
        }
        PaystackSubaccount? subaccount;
        try
        {
            subaccount = await EnsurePaystackTerminalAsync(db, appointment.TenantId, paystackClient, timeProvider.GetUtcNow(), cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            return Results.Problem(exception.Message, statusCode: StatusCodes.Status502BadGateway);
        }
        if (subaccount is null) return Results.BadRequest("Connect Paystack payouts before collecting appointment payments.");

        var pendingTerminalIntents = await db.AppointmentPaymentIntents.AsTracking()
            .Where(i => i.TenantId == appointment.TenantId && i.Channel == AppointmentPaymentChannel.VirtualTerminal && i.Status == "Pending")
            .ToListAsync(cancellationToken);
        foreach (var pendingIntent in pendingTerminalIntents)
        {
            pendingIntent.Status = "Superseded";
        }

        var intent = CreatePaymentIntent(appointment.TenantId, appointment.Id, serviceVersion.PriceCents, AppointmentPaymentChannel.VirtualTerminal, timeProvider.GetUtcNow());
        intent.VirtualTerminalCode = subaccount.VirtualTerminalCode;
        db.AppointmentPaymentIntents.Add(intent);
        appointment.PaymentStatus = AppointmentPaymentStatus.Pending;
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(new TerminalPaymentIntentResponse(intent.Reference, intent.AmountCents, intent.Status, subaccount.VirtualTerminalCode!, BuildVirtualTerminalUrl(subaccount.VirtualTerminalCode!)));
    }

    private static async Task<IResult> ConfirmPaystackPayment(string reference, MainDbContext db, TimeProvider timeProvider, IPaystackClient paystackClient, CancellationToken cancellationToken)
    {
        var intent = await db.AppointmentPaymentIntents.AsTracking().FirstOrDefaultAsync(p => p.Reference == reference, cancellationToken);
        if (intent is null) return Results.NotFound();
        var appointment = await db.Appointments.IgnoreQueryFilters().AsTracking().FirstAsync(a => a.Id == intent.AppointmentId, cancellationToken);
        if (intent.Status == "Confirmed")
        {
            return Results.Ok(new PaymentConfirmationResponse(reference, intent.Status, appointment.PublicReference));
        }
        if (!await paystackClient.IsTransactionSuccessfulAsync(reference, intent.AmountCents, cancellationToken))
        {
            return Results.Problem("Paystack has not verified this transaction as successful yet.", statusCode: StatusCodes.Status409Conflict);
        }
        intent.Status = "Confirmed";
        intent.ConfirmedAt = timeProvider.GetUtcNow();
        var serviceVersion = await GetAppointmentServiceVersionAsync(db, appointment, cancellationToken);
        appointment.PaymentStatus = NormalizePaymentPolicy(serviceVersion.PaymentPolicy.ToString(), serviceVersion.DepositCents) == ServicePaymentPolicy.DepositBeforeBooking
            ? AppointmentPaymentStatus.DepositPaid
            : AppointmentPaymentStatus.Paid;
        appointment.Status = AppointmentStatus.Confirmed;
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(new PaymentConfirmationResponse(reference, intent.Status, appointment.PublicReference));
    }

    private static async Task<IResult> HandlePaystackWebhook(HttpRequest request, MainDbContext db, TimeProvider timeProvider, IPaystackClient paystackClient, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(request.Body);
        var body = await reader.ReadToEndAsync(cancellationToken);
        var secret = Environment.GetEnvironmentVariable("PAYSTACK_SECRET_KEY");
        if (!IsConfiguredPaystackSecret(secret))
        {
            return Results.Unauthorized();
        }
        var signature = request.Headers["x-paystack-signature"].ToString();
        var expected = Convert.ToHexString(HMACSHA512.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(body))).ToLowerInvariant();
        if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(signature), Encoding.UTF8.GetBytes(expected)))
        {
            return Results.Unauthorized();
        }

        var json = JsonDocument.Parse(body);
        if (json.RootElement.GetProperty("event").GetString() != "charge.success") return Results.Ok();
        var data = json.RootElement.GetProperty("data");
        var reference = data.GetProperty("reference").GetString();
        if (!string.IsNullOrWhiteSpace(reference))
        {
            var intent = await db.AppointmentPaymentIntents.IgnoreQueryFilters().FirstOrDefaultAsync(i => i.Reference == reference, cancellationToken);
            if (intent is not null)
            {
                await ConfirmPaystackPayment(reference, db, timeProvider, paystackClient, cancellationToken);
                return Results.Ok();
            }
        }

        var terminalCode = TryReadVirtualTerminalCode(data);
        if (!string.IsNullOrWhiteSpace(terminalCode))
        {
            await ConfirmVirtualTerminalPaymentAsync(db, terminalCode, data, timeProvider.GetUtcNow(), cancellationToken);
        }
        return Results.Ok();
    }

    private static async Task<IResult> GetPaymentOverview(MainDbContext db, IExecutionContext executionContext, TimeProvider timeProvider, CancellationToken cancellationToken)
    {
        var tenantId = RequireTenant(executionContext);
        await SeedTenantAsync(db, tenantId, timeProvider.GetUtcNow(), executionContext.UserInfo.Id?.ToString(), cancellationToken);
        var subaccount = await db.PaystackSubaccounts.FirstOrDefaultAsync(cancellationToken);
        var appointments = (await db.Appointments.ToListAsync(cancellationToken)).OrderByDescending(a => a.StartAt).ToList();
        var clients = await db.Clients.ToListAsync(cancellationToken);
        var services = await db.BookableServices.ToListAsync(cancellationToken);
        var serviceVersions = await db.BookableServiceVersions.ToListAsync(cancellationToken);
        var intents = (await db.AppointmentPaymentIntents.ToListAsync(cancellationToken)).OrderByDescending(i => i.CreatedAt).Take(25).ToList();
        return Results.Ok(new PaymentOverviewResponse(
            BuildPaymentStats(appointments, services, serviceVersions),
            subaccount is null ? null : ToPaystackSubaccountDto(subaccount),
            intents.Select(intent => ToPaymentIntentDto(intent, appointments, clients, services, serviceVersions))
        ));
    }

    private static async Task<IResult> GetPaystackBanks(IPaystackClient paystackClient, CancellationToken cancellationToken)
    {
        try
        {
            return Results.Ok(await paystackClient.ListBanksAsync(cancellationToken));
        }
        catch (InvalidOperationException exception)
        {
            return Results.Problem(exception.Message, statusCode: StatusCodes.Status502BadGateway);
        }
    }

    private static async Task<IResult> ResolvePaystackAccount(ResolvePaystackAccountRequest request, IPaystackClient paystackClient, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.BankCode) || string.IsNullOrWhiteSpace(request.AccountNumber))
        {
            return Results.BadRequest("Bank and account number are required.");
        }
        try
        {
            var resolved = await paystackClient.ResolveAccountAsync(request.BankCode, request.AccountNumber, cancellationToken);
            return Results.Ok(new ResolvePaystackAccountResponse(request.BankCode, MaskAccountNumber(resolved.AccountNumber), resolved.AccountName));
        }
        catch (InvalidOperationException exception)
        {
            return Results.Problem(exception.Message, statusCode: StatusCodes.Status502BadGateway);
        }
    }

    private static async Task<IResult> SavePaystackSubaccount(SavePaystackSubaccountRequest request, MainDbContext db, IExecutionContext executionContext, TimeProvider timeProvider, IPaystackClient paystackClient, CancellationToken cancellationToken)
    {
        var tenantId = RequireTenant(executionContext);
        if (string.IsNullOrWhiteSpace(request.BankCode) || string.IsNullOrWhiteSpace(request.AccountNumber))
        {
            return Results.BadRequest("Bank and account number are required.");
        }

        var businessName = executionContext.UserInfo.TenantName;
        if (string.IsNullOrWhiteSpace(businessName))
        {
            var profile = await db.BusinessProfiles.FirstOrDefaultAsync(cancellationToken);
            businessName = profile?.Name;
        }
        if (string.IsNullOrWhiteSpace(businessName))
        {
            return Results.BadRequest("Tenant name must be set before setting up Paystack payouts.");
        }

        var existing = await db.PaystackSubaccounts.AsTracking().FirstOrDefaultAsync(cancellationToken);
        var paystackRequest = new PaystackSubaccountRequest(
            businessName,
            request.BankName,
            request.BankCode,
            request.AccountNumber,
            request.AccountName,
            $"{businessName} appointment payments",
            request.PrimaryContactName,
            request.PrimaryContactEmail,
            request.PrimaryContactPhone
        );

        PaystackSubaccountResult result;
        try
        {
            result = existing is null
                ? await paystackClient.CreateSubaccountAsync(paystackRequest, cancellationToken)
                : await paystackClient.UpdateSubaccountAsync(existing.SubaccountCode, paystackRequest, cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            return Results.Problem(exception.Message, statusCode: StatusCodes.Status502BadGateway);
        }

        var subaccount = existing ?? new PaystackSubaccount { TenantId = tenantId };
        ApplyPaystackSubaccountResult(subaccount, result, timeProvider.GetUtcNow());
        subaccount.PrimaryContactName = request.PrimaryContactName;
        subaccount.PrimaryContactEmail = request.PrimaryContactEmail;
        subaccount.PrimaryContactPhone = request.PrimaryContactPhone;
        if (existing is null) db.PaystackSubaccounts.Add(subaccount);
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(ToPaystackSubaccountDto(subaccount));
    }

    private static async Task<IResult> GetPaystackSettlements(MainDbContext db, IPaystackClient paystackClient, CancellationToken cancellationToken)
    {
        var subaccount = await db.PaystackSubaccounts.FirstOrDefaultAsync(cancellationToken);
        if (subaccount is null || !subaccount.IsActive) return Results.Ok(new PaystackSettlementsResponse([]));
        try
        {
            var settlements = await paystackClient.ListSettlementsAsync(subaccount.SubaccountCode, cancellationToken);
            return Results.Ok(new PaystackSettlementsResponse(settlements));
        }
        catch (InvalidOperationException exception)
        {
            return Results.Problem(exception.Message, statusCode: StatusCodes.Status502BadGateway);
        }
    }

    private static async Task<IResult> GetIntegrations(MainDbContext db, IExecutionContext executionContext, CancellationToken cancellationToken)
    {
        var tenantId = RequireTenant(executionContext);
        await EnsureIntegrationRowsAsync(db, tenantId, cancellationToken);
        return Results.Ok(await db.IntegrationConnections.OrderBy(i => i.Provider).ThenBy(i => i.Capability).ToListAsync(cancellationToken));
    }

    private static TenantId RequireTenant(IExecutionContext executionContext)
    {
        return executionContext.TenantId ?? throw new InvalidOperationException("A tenant context is required.");
    }

    private static async Task AddNextServiceVersionAsync(MainDbContext db, BookableService service, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var existingVersions = await db.BookableServiceVersions
            .Where(version => version.ServiceId == service.Id)
            .Select(version => version.VersionNumber)
            .ToListAsync(cancellationToken);
        var nextVersion = existingVersions.Count == 0 ? 1 : existingVersions.Max() + 1;
        db.BookableServiceVersions.Add(CreateServiceVersion(service, nextVersion, now));
    }

    private static BookableServiceVersion CreateServiceVersion(BookableService service, int versionNumber, DateTimeOffset now)
    {
        return new BookableServiceVersion
        {
            TenantId = service.TenantId,
            LocationId = service.LocationId,
            ServiceId = service.Id,
            VersionNumber = versionNumber,
            CategoryId = service.CategoryId,
            Name = service.Name,
            Description = service.Description,
            Mode = service.Mode,
            DurationMinutes = service.DurationMinutes,
            PriceCents = service.PriceCents,
            DepositCents = service.DepositCents,
            PaymentPolicy = service.PaymentPolicy,
            BufferBeforeMinutes = service.BufferBeforeMinutes,
            BufferAfterMinutes = service.BufferAfterMinutes,
            Location = service.Location,
            IsActive = service.IsActive,
            CreatedAt = now
        };
    }

    private static async Task<BookableServiceVersion> GetLatestServiceVersionAsync(MainDbContext db, BookableService service, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var version = await db.BookableServiceVersions.IgnoreQueryFilters()
            .Where(v => v.TenantId == service.TenantId && v.ServiceId == service.Id)
            .OrderByDescending(v => v.VersionNumber)
            .FirstOrDefaultAsync(cancellationToken);
        if (version is not null) return version;

        version = CreateServiceVersion(service, 1, now);
        db.BookableServiceVersions.Add(version);
        return version;
    }

    private static async Task<BookableServiceVersion> GetAppointmentServiceVersionAsync(MainDbContext db, Appointment appointment, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(appointment.ServiceVersionId))
        {
            var version = await db.BookableServiceVersions.IgnoreQueryFilters().FirstOrDefaultAsync(v => v.Id == appointment.ServiceVersionId, cancellationToken);
            if (version is not null) return version;
        }

        return await db.BookableServiceVersions.IgnoreQueryFilters()
            .Where(v => v.TenantId == appointment.TenantId && v.ServiceId == appointment.ServiceId)
            .OrderByDescending(v => v.VersionNumber)
            .FirstAsync(cancellationToken);
    }

    private static async Task EnsureServiceVersionBackfillAsync(MainDbContext db, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var services = await db.BookableServices.ToListAsync(cancellationToken);
        var versions = await db.BookableServiceVersions.ToListAsync(cancellationToken);
        foreach (var service in services.Where(service => versions.All(version => version.ServiceId != service.Id)))
        {
            var version = CreateServiceVersion(service, 1, now);
            db.BookableServiceVersions.Add(version);
            versions.Add(version);
        }

        var appointments = await db.Appointments.AsTracking().ToListAsync(cancellationToken);
        foreach (var appointment in appointments.Where(appointment => string.IsNullOrWhiteSpace(appointment.ServiceVersionId)))
        {
            appointment.ServiceVersionId = versions
                .Where(version => version.ServiceId == appointment.ServiceId)
                .OrderByDescending(version => version.VersionNumber)
                .First()
                .Id;
        }

        if (db.ChangeTracker.HasChanges())
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private static async Task<AppShellResponse> BuildShellAsync(MainDbContext db, CancellationToken cancellationToken, string? ownerUserId = null)
    {
        await EnsureSchedulingFoundationBackfillAsync(db, ownerUserId, cancellationToken);
        await EnsureServiceVersionBackfillAsync(db, DateTimeOffset.UtcNow, cancellationToken);
        var appointments = (await db.Appointments.ToListAsync(cancellationToken)).OrderBy(a => a.StartAt).ToList();
        var clients = await db.Clients.ToListAsync(cancellationToken);
        var services = await db.BookableServices.OrderBy(s => s.SortOrder).ToListAsync(cancellationToken);
        var serviceVersions = await db.BookableServiceVersions.ToListAsync(cancellationToken);
        var categories = await db.ServiceCategories.OrderBy(c => c.SortOrder).ToListAsync(cancellationToken);
        var profile = await db.BusinessProfiles.FirstAsync(cancellationToken);
        var integrations = await db.IntegrationConnections.OrderBy(i => i.Provider).ThenBy(i => i.Capability).ToListAsync(cancellationToken);
        var externalEvents = await db.AppointmentExternalCalendarEvents.ToListAsync(cancellationToken);
        var availabilityRules = await db.AvailabilityRules.OrderBy(rule => rule.DayOfWeek).ThenBy(rule => rule.StartTime).ToListAsync(cancellationToken);
        var manualClosures = await db.BusinessClosures.OrderBy(closure => closure.StartDate).ToListAsync(cancellationToken);
        var manualBlocks = (await db.ManualCalendarBlocks.ToListAsync(cancellationToken)).OrderBy(b => b.StartAt).ToList();
        var externalBlocks = (await db.ExternalBusyBlocks.ToListAsync(cancellationToken)).OrderBy(b => b.StartAt).ToList();
        var holidaySettings = BuildHolidaySettings(profile, DateTimeOffset.UtcNow.Year - 1, DateTimeOffset.UtcNow.Year + 2);
        return new AppShellResponse(
            new BusinessProfileDto(profile.Name, profile.Slug, profile.TimeZone, profile.Address, profile.PublicBookingEnabled),
            appointments.Select(a => ToAppointmentDto(a, clients, services, serviceVersions, externalEvents)),
            services.Select(service => ToServiceDto(service, serviceVersions)),
            categories.Select(c => new ServiceCategoryDto(c.Id, c.Name)),
            clients.Select(c => ToClientDto(c, appointments, services, serviceVersions)),
            BuildAnalytics(appointments, services, serviceVersions, clients),
            integrations.Select(i => new IntegrationConnectionDto(i.Provider, i.Capability, i.Status, i.LastSyncedAt, i.OwnerType.ToString(), i.OwnerId, i.ExternalConnectionId)),
            availabilityRules.Select(ToAvailabilityRuleDto),
            holidaySettings,
            manualClosures.Select(ToClosureDto).Concat(holidaySettings.Holidays.Where(holiday => !holiday.IsOpen).Select(ToHolidayClosureDto)),
            manualBlocks.Select(ToManualCalendarBlockDto)
                .Concat(externalBlocks.Select(ToExternalCalendarBlockDto))
        );
    }

    private static AppointmentDto ToAppointmentDto(Appointment appointment, List<Client> clients, List<BookableService> services, List<BookableServiceVersion> serviceVersions, List<AppointmentExternalCalendarEvent>? externalEvents = null)
    {
        var client = clients.First(c => c.Id == appointment.ClientId);
        var service = services.First(s => s.Id == appointment.ServiceId);
        var version = GetServiceVersionForAppointment(appointment, serviceVersions) ?? CreateFallbackVersion(service, appointment.CreatedAt);
        return new AppointmentDto(
            appointment.Id,
            appointment.PublicReference,
            client.Id,
            service.Id,
            version.Id,
            version.VersionNumber,
            appointment.StartAt,
            appointment.EndAt,
            client.Name,
            client.Phone,
            client.Email,
            version.Name,
            version.DurationMinutes,
            version.PriceCents,
            version.DepositCents,
            version.PaymentPolicy.ToString(),
            appointment.Status.ToString(),
            appointment.PaymentStatus.ToString(),
            appointment.Source.ToString(),
            version.Location,
            appointment.AnswersJson,
            client.Status,
            client.Alert,
            client.InternalNote,
            externalEvents?.FirstOrDefault(item => item.AppointmentId == appointment.Id)?.MeetUrl
        );
    }

    private static async Task<AppointmentDto> ToAppointmentDetailAsync(MainDbContext db, Appointment appointment, CancellationToken cancellationToken)
    {
        var client = await db.Clients.IgnoreQueryFilters().FirstAsync(c => c.Id == appointment.ClientId, cancellationToken);
        var service = await db.BookableServices.IgnoreQueryFilters().FirstAsync(s => s.Id == appointment.ServiceId, cancellationToken);
        var serviceVersion = await GetAppointmentServiceVersionAsync(db, appointment, cancellationToken);
        var externalEvents = await db.AppointmentExternalCalendarEvents.IgnoreQueryFilters().Where(item => item.AppointmentId == appointment.Id).ToListAsync(cancellationToken);
        return ToAppointmentDto(appointment, [client], [service], [serviceVersion], externalEvents);
    }

    private static ServiceDto ToServiceDto(BookableService service, List<BookableServiceVersion> serviceVersions)
    {
        var latestVersionNumber = serviceVersions
            .Where(version => version.ServiceId == service.Id)
            .Select(version => version.VersionNumber)
            .DefaultIfEmpty(1)
            .Max();
        return new ServiceDto(service.Id, service.CategoryId, service.Name, service.Mode, service.DurationMinutes, service.PriceCents, service.DepositCents, service.PaymentPolicy.ToString(), service.Location, service.IsActive, latestVersionNumber);
    }

    private static CalendarBlockDto ToManualCalendarBlockDto(ManualCalendarBlock block)
    {
        return new CalendarBlockDto(block.Id, block.Title, block.StartAt, block.EndAt, "manual");
    }

    private static CalendarBlockDto ToExternalCalendarBlockDto(ExternalBusyBlock block)
    {
        return new CalendarBlockDto(block.Id, $"{block.Provider} · {block.Label}", block.StartAt, block.EndAt, "external");
    }

    private static AvailabilityRuleDto ToAvailabilityRuleDto(AvailabilityRule rule)
    {
        return new AvailabilityRuleDto(rule.Id, rule.DayOfWeek.ToString(), rule.StartTime.ToString("HH:mm"), rule.EndTime.ToString("HH:mm"));
    }

    private static ClosureDto ToClosureDto(BusinessClosure closure)
    {
        return new ClosureDto(closure.Id, closure.StartDate, closure.EndDate, closure.Label, closure.Type);
    }

    private static ClosureDto ToHolidayClosureDto(PublicHolidayDto holiday)
    {
        return new ClosureDto(holiday.Id, holiday.Date, holiday.Date, holiday.Label, "publicHoliday");
    }

    private static ClientAppointmentHistoryDto ToClientAppointmentHistoryDto(Appointment appointment, List<BookableService> services, List<BookableServiceVersion> serviceVersions)
    {
        var service = services.First(s => s.Id == appointment.ServiceId);
        var version = GetServiceVersionForAppointment(appointment, serviceVersions) ?? CreateFallbackVersion(service, appointment.CreatedAt);
        return new ClientAppointmentHistoryDto(
            appointment.Id,
            appointment.PublicReference,
            appointment.StartAt,
            appointment.EndAt,
            version.Name,
            version.PriceCents,
            version.DepositCents,
            version.PaymentPolicy.ToString(),
            appointment.Status.ToString(),
            appointment.PaymentStatus.ToString(),
            appointment.Source.ToString(),
            version.Location
        );
    }

    private static BookableServiceVersion? GetServiceVersionForAppointment(Appointment appointment, List<BookableServiceVersion> serviceVersions)
    {
        return serviceVersions.FirstOrDefault(version => version.Id == appointment.ServiceVersionId) ??
               serviceVersions
                   .Where(version => version.ServiceId == appointment.ServiceId)
                   .OrderByDescending(version => version.VersionNumber)
                   .FirstOrDefault();
    }

    private static BookableServiceVersion CreateFallbackVersion(BookableService service, DateTimeOffset createdAt)
    {
        return new BookableServiceVersion
        {
            Id = $"{service.Id}_fallback",
            TenantId = service.TenantId,
            LocationId = service.LocationId,
            ServiceId = service.Id,
            VersionNumber = 1,
            CategoryId = service.CategoryId,
            Name = service.Name,
            Description = service.Description,
            Mode = service.Mode,
            DurationMinutes = service.DurationMinutes,
            PriceCents = service.PriceCents,
            DepositCents = service.DepositCents,
            PaymentPolicy = service.PaymentPolicy,
            BufferBeforeMinutes = service.BufferBeforeMinutes,
            BufferAfterMinutes = service.BufferAfterMinutes,
            Location = service.Location,
            IsActive = service.IsActive,
            CreatedAt = createdAt
        };
    }

    private static ClientDto ToClientDto(Client client, List<Appointment> appointments, List<BookableService> services, List<BookableServiceVersion> serviceVersions)
    {
        var clientAppointments = appointments.Where(a => a.ClientId == client.Id).OrderByDescending(a => a.StartAt).ToList();
        var lifetime = clientAppointments.Sum(a => GetServiceVersionForAppointment(a, serviceVersions)?.PriceCents ?? services.First(s => s.Id == a.ServiceId).PriceCents);
        var noShows = clientAppointments.Count(a => a.Status == AppointmentStatus.NoShow);
        var history = clientAppointments.Select(a => ToClientAppointmentHistoryDto(a, services, serviceVersions)).ToList();
        return new ClientDto(client.Id, client.Name, client.Phone, client.Email, client.Status, client.Alert, client.InternalNote, clientAppointments.Count, lifetime, noShows, clientAppointments.FirstOrDefault()?.StartAt, history);
    }

    private static AnalyticsDto BuildAnalytics(List<Appointment> appointments, List<BookableService> services, List<BookableServiceVersion> serviceVersions, List<Client> clients)
    {
        var revenue = appointments.Where(a => a.PaymentStatus is AppointmentPaymentStatus.Paid or AppointmentPaymentStatus.DepositPaid or AppointmentPaymentStatus.NotRequired)
            .Sum(a => GetServiceVersionForAppointment(a, serviceVersions)?.PriceCents ?? services.First(s => s.Id == a.ServiceId).PriceCents);
        var noShows = appointments.Count(a => a.Status == AppointmentStatus.NoShow);
        return new AnalyticsDto(appointments.Count, revenue, clients.Count, appointments.Count == 0 ? 0 : revenue / appointments.Count, appointments.Count == 0 ? 0 : Math.Round(noShows * 100m / appointments.Count, 1));
    }

    private static PaymentStatsDto BuildPaymentStats(List<Appointment> appointments, List<BookableService> services, List<BookableServiceVersion> serviceVersions)
    {
        var paid = appointments.Where(a => a.PaymentStatus is AppointmentPaymentStatus.Paid or AppointmentPaymentStatus.DepositPaid or AppointmentPaymentStatus.NotRequired).ToList();
        var pending = appointments.Where(a => a.PaymentStatus is AppointmentPaymentStatus.Pending or AppointmentPaymentStatus.Failed).ToList();
        return new PaymentStatsDto(
            appointments.Count,
            paid.Count,
            appointments.Count(a => a.PaymentStatus != AppointmentPaymentStatus.Paid && a.PaymentStatus != AppointmentPaymentStatus.DepositPaid && a.PaymentStatus != AppointmentPaymentStatus.NotRequired),
            appointments.Count(a => a.PaymentStatus == AppointmentPaymentStatus.Failed),
            pending.Sum(a => GetPaymentAmountCents(GetServiceVersionForAppointment(a, serviceVersions) ?? CreateFallbackVersion(services.First(s => s.Id == a.ServiceId), a.CreatedAt))),
            paid.Sum(a => GetServiceVersionForAppointment(a, serviceVersions)?.PriceCents ?? services.First(s => s.Id == a.ServiceId).PriceCents)
        );
    }

    private static PaymentIntentDto ToPaymentIntentDto(AppointmentPaymentIntent intent, List<Appointment> appointments, List<Client> clients, List<BookableService> services, List<BookableServiceVersion> serviceVersions)
    {
        var appointment = appointments.FirstOrDefault(a => a.Id == intent.AppointmentId);
        var client = appointment is null ? null : clients.FirstOrDefault(c => c.Id == appointment.ClientId);
        var service = appointment is null ? null : GetServiceVersionForAppointment(appointment, serviceVersions);
        var currentService = appointment is null ? null : services.FirstOrDefault(s => s.Id == appointment.ServiceId);
        return new PaymentIntentDto(
            intent.Reference,
            intent.AmountCents,
            intent.Status,
            intent.AuthorizationUrl,
            intent.CreatedAt,
            intent.ConfirmedAt,
            appointment?.PublicReference ?? string.Empty,
            client?.Name ?? "Unknown client",
            service?.Name ?? currentService?.Name ?? "Unknown service"
        );
    }

    private static PaystackSubaccountDto ToPaystackSubaccountDto(PaystackSubaccount subaccount)
    {
        return new PaystackSubaccountDto(
            subaccount.SubaccountCode,
            subaccount.SplitCode,
            subaccount.VirtualTerminalCode,
            subaccount.BusinessName,
            subaccount.SettlementBankName,
            subaccount.SettlementBankCode,
            subaccount.AccountName,
            subaccount.MaskedAccountNumber,
            subaccount.Currency,
            subaccount.PrimaryContactName,
            subaccount.PrimaryContactEmail,
            subaccount.PrimaryContactPhone,
            subaccount.IsActive,
            subaccount.IsVerified,
            subaccount.SettlementSchedule,
            subaccount.LastSyncedAt
        );
    }

    private static void ApplyPaystackSubaccountResult(PaystackSubaccount subaccount, PaystackSubaccountResult result, DateTimeOffset now)
    {
        subaccount.SubaccountCode = result.SubaccountCode;
        subaccount.SubaccountId = result.SubaccountId;
        subaccount.BusinessName = result.BusinessName;
        subaccount.SettlementBankName = result.BankName;
        subaccount.SettlementBankCode = result.BankCode;
        subaccount.AccountName = result.AccountName;
        subaccount.MaskedAccountNumber = MaskAccountNumber(result.AccountNumber);
        subaccount.Currency = result.Currency;
        subaccount.IsActive = result.Active;
        subaccount.IsVerified = result.IsVerified;
        subaccount.SettlementSchedule = result.SettlementSchedule;
        subaccount.LastSyncedAt = now;
    }

    private static string MaskAccountNumber(string accountNumber)
    {
        var digits = new string(accountNumber.Where(char.IsDigit).ToArray());
        return digits.Length <= 4 ? "****" : $"**** {digits[^4..]}";
    }

    private static async Task<string?> GetActivePaystackSubaccountCodeAsync(MainDbContext db, TenantId tenantId, bool ignoreQueryFilters, CancellationToken cancellationToken)
    {
        var query = ignoreQueryFilters ? db.PaystackSubaccounts.IgnoreQueryFilters().Where(s => s.TenantId == tenantId) : db.PaystackSubaccounts;
        var subaccount = await query.FirstOrDefaultAsync(s => s.IsActive && s.SubaccountCode != string.Empty, cancellationToken);
        return subaccount?.SubaccountCode;
    }

    private static async Task<PaystackSubaccount?> EnsurePaystackTerminalAsync(MainDbContext db, TenantId tenantId, IPaystackClient paystackClient, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var subaccount = await db.PaystackSubaccounts.AsTracking().FirstOrDefaultAsync(s => s.TenantId == tenantId && s.IsActive && s.SubaccountCode != string.Empty, cancellationToken);
        if (subaccount is null) return null;
        if (string.IsNullOrWhiteSpace(subaccount.SplitCode))
        {
            var split = await paystackClient.CreateSplitAsync(
                new PaystackSplitRequest($"{subaccount.BusinessName} appointment terminal", subaccount.SubaccountCode, subaccount.Currency),
                cancellationToken
            );
            subaccount.SplitCode = split.SplitCode;
        }
        if (string.IsNullOrWhiteSpace(subaccount.VirtualTerminalCode))
        {
            if (string.IsNullOrWhiteSpace(subaccount.PrimaryContactPhone))
            {
                throw new InvalidOperationException("Paystack virtual terminal setup requires a WhatsApp-enabled contact phone on the payout account.");
            }
            var terminal = await paystackClient.CreateVirtualTerminalAsync(
                new PaystackVirtualTerminalRequest(
                    $"{subaccount.BusinessName} appointment terminal",
                    subaccount.PrimaryContactPhone,
                    string.IsNullOrWhiteSpace(subaccount.PrimaryContactName) ? subaccount.BusinessName : subaccount.PrimaryContactName,
                    subaccount.Currency,
                    tenantId.Value.ToString()
                ),
                cancellationToken
            );
            subaccount.VirtualTerminalCode = terminal.Code;
        }
        await paystackClient.AssignSplitToVirtualTerminalAsync(subaccount.VirtualTerminalCode!, subaccount.SplitCode!, cancellationToken);
        subaccount.LastSyncedAt = now;
        return subaccount;
    }

    private static async Task<IReadOnlyList<SlotDto>> BuildSlotsAsync(MainDbContext db, string serviceId, DateOnly date, CancellationToken cancellationToken, TenantId? publicTenantId = null)
    {
        var serviceQuery = publicTenantId is null ? db.BookableServices : db.BookableServices.IgnoreQueryFilters().Where(s => s.TenantId == publicTenantId);
        var service = await serviceQuery.FirstAsync(s => s.Id == serviceId, cancellationToken);
        var tenantId = publicTenantId ?? service.TenantId;
        var rules = await db.AvailabilityRules.IgnoreQueryFilters().Where(r => r.TenantId == tenantId && r.DayOfWeek == date.DayOfWeek).ToListAsync(cancellationToken);
        var dayStart = new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(2));
        var dayEnd = dayStart.AddDays(1);
        if (await IsClosedAsync(db, tenantId, date, cancellationToken))
        {
            return [];
        }
        var tenantAppointments = await db.Appointments.IgnoreQueryFilters()
            .Where(a => a.TenantId == tenantId && a.Status != AppointmentStatus.Cancelled)
            .ToListAsync(cancellationToken);
        var existing = tenantAppointments.Where(a => a.StartAt < dayEnd && a.EndAt > dayStart).ToList();
        var tenantManualBlocks = await db.ManualCalendarBlocks.IgnoreQueryFilters()
            .Where(b => b.TenantId == tenantId)
            .ToListAsync(cancellationToken);
        var manualBlocks = tenantManualBlocks.Where(b => b.StartAt < dayEnd && b.EndAt > dayStart).ToList();
        var tenantExternalBlocks = await db.ExternalBusyBlocks.IgnoreQueryFilters()
            .Where(b => b.TenantId == tenantId)
            .ToListAsync(cancellationToken);
        var externalBlocks = tenantExternalBlocks.Where(b => b.StartAt < dayEnd && b.EndAt > dayStart).ToList();
        var requiredResourceIds = await db.BookableServiceResources.IgnoreQueryFilters()
            .Where(resource => resource.TenantId == tenantId && resource.ServiceId == serviceId)
            .Select(resource => resource.ResourceId)
            .ToListAsync(cancellationToken);
        var resourceReservations = requiredResourceIds.Count == 0
            ? []
            : await db.ResourceReservations.IgnoreQueryFilters()
                .Where(reservation => reservation.TenantId == tenantId &&
                                      requiredResourceIds.Contains(reservation.ResourceId) &&
                                      reservation.StartAt < dayEnd &&
                                      reservation.EndAt > dayStart)
                .ToListAsync(cancellationToken);
        var slots = new List<SlotDto>();
        foreach (var rule in rules)
        {
            var cursor = date.ToDateTime(rule.StartTime);
            var end = date.ToDateTime(rule.EndTime);
            while (cursor.AddMinutes(service.DurationMinutes) <= end)
            {
                var start = new DateTimeOffset(cursor, TimeSpan.FromHours(2));
                var slotEnd = start.AddMinutes(service.DurationMinutes);
                var hasConflict = existing.Any(a => a.StartAt < slotEnd && a.EndAt > start) ||
                                  manualBlocks.Any(b => b.StartAt <= slotEnd && b.EndAt > start) ||
                                  externalBlocks.Any(b => b.StartAt <= slotEnd && b.EndAt > start) ||
                                  resourceReservations.Any(reservation => reservation.StartAt < slotEnd && reservation.EndAt > start);
                if (!hasConflict)
                {
                    slots.Add(new SlotDto(start, slotEnd));
                }
                cursor = cursor.AddMinutes(30);
            }
        }
        return slots;
    }

    private static async Task<bool> IsClosedAsync(MainDbContext db, TenantId tenantId, DateOnly date, CancellationToken cancellationToken)
    {
        var manualClosures = await db.BusinessClosures.IgnoreQueryFilters()
            .Where(closure => closure.TenantId == tenantId)
            .ToListAsync(cancellationToken);
        var profile = await db.BusinessProfiles.IgnoreQueryFilters().FirstAsync(profile => profile.TenantId == tenantId, cancellationToken);
        var openHolidayIds = ReadOpenHolidayIds(profile);
        var holidayCountryCode = NormalizeHolidayCountryCode(profile.HolidayCountryCode);
        return manualClosures.Any(closure => closure.StartDate <= date && closure.EndDate >= date) ||
               BuildPublicHolidays(holidayCountryCode, date.Year, date.Year)
                   .Any(holiday => holiday.Date == date && !openHolidayIds.Contains(holiday.Id));
    }

    private static bool TryParseDayOfWeek(string dayOfWeek, out DayOfWeek parsed)
    {
        if (Enum.TryParse(dayOfWeek, true, out parsed))
        {
            return true;
        }
        if (int.TryParse(dayOfWeek, out var numeric) && numeric >= 0 && numeric <= 6)
        {
            parsed = (DayOfWeek)numeric;
            return true;
        }
        return false;
    }

    private static HolidaySettingsDto BuildHolidaySettings(BusinessProfile profile, int startYear, int endYear)
    {
        var countryCode = NormalizeHolidayCountryCode(profile.HolidayCountryCode);
        var openHolidayIds = ReadOpenHolidayIds(profile);
        var holidays = BuildPublicHolidays(countryCode, startYear, endYear)
            .Select(holiday => holiday with { IsOpen = openHolidayIds.Contains(holiday.Id) })
            .ToList();
        return new HolidaySettingsDto(countryCode, SupportedHolidayCountries(), holidays);
    }

    private static IReadOnlyList<PublicHolidayDto> BuildPublicHolidays(string countryCode, int startYear, int endYear)
    {
        return Enumerable.Range(startYear, endYear - startYear + 1)
            .SelectMany(year => countryCode == "US" ? BuildUnitedStatesPublicHolidays(year) : BuildSouthAfricanPublicHolidays(year))
            .OrderBy(holiday => holiday.Date)
            .ToList();
    }

    private static IReadOnlyList<PublicHolidayDto> BuildSouthAfricanPublicHolidays(int year)
    {
        var easter = CalculateEasterSunday(year);
        var holidays = new List<(DateOnly Date, string Label)>
        {
            (new DateOnly(year, 1, 1), "New Year's Day"),
            (new DateOnly(year, 3, 21), "Human Rights Day"),
            (easter.AddDays(-2), "Good Friday"),
            (easter.AddDays(1), "Family Day"),
            (new DateOnly(year, 4, 27), "Freedom Day"),
            (new DateOnly(year, 5, 1), "Workers' Day"),
            (new DateOnly(year, 6, 16), "Youth Day"),
            (new DateOnly(year, 8, 9), "National Women's Day"),
            (new DateOnly(year, 9, 24), "Heritage Day"),
            (new DateOnly(year, 12, 16), "Day of Reconciliation"),
            (new DateOnly(year, 12, 25), "Christmas Day"),
            (new DateOnly(year, 12, 26), "Day of Goodwill")
        };

        var observed = holidays
            .Where(holiday => holiday.Date.DayOfWeek == DayOfWeek.Sunday)
            .Select(holiday => (Date: holiday.Date.AddDays(1), Label: $"{holiday.Label} observed"));

        return holidays.Concat(observed)
            .OrderBy(holiday => holiday.Date)
            .Select(holiday => new PublicHolidayDto($"ZA-{holiday.Date:yyyy-MM-dd}", "ZA", holiday.Date, holiday.Label, false))
            .ToList();
    }

    private static IReadOnlyList<PublicHolidayDto> BuildUnitedStatesPublicHolidays(int year)
    {
        var holidays = new List<(DateOnly Date, string Label)>
        {
            (ObservedFixedHoliday(year, 1, 1), "New Year's Day"),
            (NthWeekday(year, 1, DayOfWeek.Monday, 3), "Martin Luther King Jr. Day"),
            (NthWeekday(year, 2, DayOfWeek.Monday, 3), "Washington's Birthday"),
            (LastWeekday(year, 5, DayOfWeek.Monday), "Memorial Day"),
            (ObservedFixedHoliday(year, 6, 19), "Juneteenth"),
            (ObservedFixedHoliday(year, 7, 4), "Independence Day"),
            (NthWeekday(year, 9, DayOfWeek.Monday, 1), "Labor Day"),
            (NthWeekday(year, 10, DayOfWeek.Monday, 2), "Columbus Day"),
            (ObservedFixedHoliday(year, 11, 11), "Veterans Day"),
            (NthWeekday(year, 11, DayOfWeek.Thursday, 4), "Thanksgiving Day"),
            (ObservedFixedHoliday(year, 12, 25), "Christmas Day")
        };
        return holidays
            .OrderBy(holiday => holiday.Date)
            .Select(holiday => new PublicHolidayDto($"US-{holiday.Date:yyyy-MM-dd}", "US", holiday.Date, holiday.Label, false))
            .ToList();
    }

    private static string NormalizeHolidayCountryCode(string? countryCode)
    {
        var normalized = string.IsNullOrWhiteSpace(countryCode) ? "ZA" : countryCode.Trim().ToUpperInvariant();
        return normalized == "US" ? "US" : "ZA";
    }

    private static IReadOnlySet<string> ReadOpenHolidayIds(BusinessProfile profile)
    {
        try
        {
            return JsonSerializer.Deserialize<string[]>(profile.OpenPublicHolidayIdsJson)?.ToHashSet(StringComparer.OrdinalIgnoreCase) ??
                   new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static IReadOnlyList<HolidayCountryDto> SupportedHolidayCountries()
    {
        return
        [
            new HolidayCountryDto("ZA", "South Africa"),
            new HolidayCountryDto("US", "United States")
        ];
    }

    private static DateOnly ObservedFixedHoliday(int year, int month, int day)
    {
        var date = new DateOnly(year, month, day);
        return date.DayOfWeek switch
        {
            DayOfWeek.Saturday => date.AddDays(-1),
            DayOfWeek.Sunday => date.AddDays(1),
            _ => date
        };
    }

    private static DateOnly NthWeekday(int year, int month, DayOfWeek dayOfWeek, int occurrence)
    {
        var date = new DateOnly(year, month, 1);
        while (date.DayOfWeek != dayOfWeek)
        {
            date = date.AddDays(1);
        }
        return date.AddDays((occurrence - 1) * 7);
    }

    private static DateOnly LastWeekday(int year, int month, DayOfWeek dayOfWeek)
    {
        var date = new DateOnly(year, month, DateTime.DaysInMonth(year, month));
        while (date.DayOfWeek != dayOfWeek)
        {
            date = date.AddDays(-1);
        }
        return date;
    }

    private static DateOnly CalculateEasterSunday(int year)
    {
        var a = year % 19;
        var b = year / 100;
        var c = year % 100;
        var d = b / 4;
        var e = b % 4;
        var f = (b + 8) / 25;
        var g = (b - f + 1) / 3;
        var h = (19 * a + b - d - g + 15) % 30;
        var i = c / 4;
        var k = c % 4;
        var l = (32 + 2 * e + 2 * i - h - k) % 7;
        var m = (a + 11 * h + 22 * l) / 451;
        var month = (h + l - 7 * m + 114) / 31;
        var day = ((h + l - 7 * m + 114) % 31) + 1;
        return new DateOnly(year, month, day);
    }

    private static AppointmentPaymentIntent CreatePaymentIntent(TenantId tenantId, string appointmentId, int amountCents, AppointmentPaymentChannel channel, DateTimeOffset now)
    {
        return new AppointmentPaymentIntent
        {
            TenantId = tenantId,
            AppointmentId = appointmentId,
            Channel = channel,
            Reference = $"ps_{Guid.NewGuid():N}",
            AmountCents = amountCents,
            CreatedAt = now
        };
    }

    private static int GetPaymentAmountCents(BookableService service)
    {
        return NormalizePaymentPolicy(service.PaymentPolicy.ToString(), service.DepositCents) == ServicePaymentPolicy.DepositBeforeBooking
            ? service.DepositCents
            : service.PriceCents;
    }

    private static int GetPaymentAmountCents(BookableServiceVersion serviceVersion)
    {
        return NormalizePaymentPolicy(serviceVersion.PaymentPolicy.ToString(), serviceVersion.DepositCents) == ServicePaymentPolicy.DepositBeforeBooking
            ? serviceVersion.DepositCents
            : serviceVersion.PriceCents;
    }

    private static ServicePaymentPolicy NormalizePaymentPolicy(string? paymentPolicy, int depositCents)
    {
        if (Enum.TryParse<ServicePaymentPolicy>(paymentPolicy, true, out var parsed))
        {
            return parsed;
        }
        return depositCents > 0 ? ServicePaymentPolicy.DepositBeforeBooking : ServicePaymentPolicy.NoPaymentRequired;
    }

    private static string BuildVirtualTerminalUrl(string terminalCode)
    {
        return $"https://paystack.shop/pay/{terminalCode.ToLowerInvariant()}";
    }

    private static string? TryReadVirtualTerminalCode(JsonElement data)
    {
        if (data.TryGetProperty("metadata", out var metadata) &&
            metadata.ValueKind == JsonValueKind.Object &&
            metadata.TryGetProperty("virtual_terminal", out var terminal) &&
            terminal.ValueKind == JsonValueKind.Object &&
            terminal.TryGetProperty("code", out var code))
        {
            return code.GetString();
        }
        return null;
    }

    private static async Task ConfirmVirtualTerminalPaymentAsync(MainDbContext db, string terminalCode, JsonElement data, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var intent = await db.AppointmentPaymentIntents.IgnoreQueryFilters().AsTracking()
            .Where(i => i.Channel == AppointmentPaymentChannel.VirtualTerminal && i.VirtualTerminalCode == terminalCode && i.Status == "Pending")
            .OrderByDescending(i => i.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (intent is null) return;
        var amount = data.TryGetProperty("amount", out var amountProperty) ? amountProperty.GetInt32() : 0;
        if (amount != intent.AmountCents) return;
        var appointment = await db.Appointments.IgnoreQueryFilters().AsTracking().FirstAsync(a => a.Id == intent.AppointmentId, cancellationToken);
        intent.Status = "Confirmed";
        intent.ConfirmedAt = now;
        appointment.PaymentStatus = AppointmentPaymentStatus.Paid;
        await db.SaveChangesAsync(cancellationToken);
    }

    private static Task<string?> TryInitializePaystackTransactionAsync(AppointmentPaymentIntent intent, string email, string subaccountCode, IPaystackClient paystackClient, CancellationToken cancellationToken)
    {
        return paystackClient.InitializeTransactionAsync(
            new PaystackTransactionRequest(intent.Reference, email, intent.AmountCents, BuildPaystackCallbackUrl(intent.Reference), subaccountCode),
            cancellationToken
        );
    }

    private static string BuildPaystackCallbackUrl(string reference)
    {
        var callbackUrl = Environment.GetEnvironmentVariable("PAYSTACK_CALLBACK_URL");
        if (string.IsNullOrWhiteSpace(callbackUrl) || callbackUrl == "not-configured")
        {
            return $"https://localhost:9000/book/payment/callback?reference={reference}";
        }
        var separator = callbackUrl.Contains('?') ? '&' : '?';
        return $"{callbackUrl}{separator}reference={reference}";
    }

    private static bool IsConfiguredPaystackSecret(string? secret)
    {
        return !string.IsNullOrWhiteSpace(secret) && secret.StartsWith("sk_", StringComparison.Ordinal);
    }

    private static async Task<PublicPhoneVerification?> ConsumePhoneVerificationAsync(MainDbContext db, TenantId tenantId, string normalizedPhone, string? token, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(normalizedPhone) || string.IsNullOrWhiteSpace(token)) return null;

        var tokenHash = HashPhoneVerificationToken(token);
        var verifiedCandidates = await db.PublicPhoneVerifications.IgnoreQueryFilters().AsTracking()
            .Where(v => v.Phone == normalizedPhone && v.Status == "Verified" && v.VerificationTokenHash == tokenHash)
            .ToListAsync(cancellationToken);
        var verification = verifiedCandidates
            .Where(v => v.TenantId == tenantId && v.ConsumedAt == null && v.ExpiresAt > now)
            .OrderByDescending(v => v.VerifiedAt)
            .FirstOrDefault();
        if (verification is null) return null;

        verification.Status = "Consumed";
        verification.ConsumedAt = now;
        return verification;
    }

    private static string CreatePhoneVerificationToken()
    {
        return $"pv_{Base64UrlEncode(RandomNumberGenerator.GetBytes(32))}";
    }

    private static string HashPhoneVerificationToken(string token)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }

    private static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static string NormalizePhone(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return string.Empty;
        var trimmed = phone.Trim();
        var builder = new StringBuilder(trimmed.Length);
        var hasLeadingPlus = trimmed.StartsWith('+');
        foreach (var character in phone.Trim())
        {
            if (char.IsDigit(character))
            {
                builder.Append(character);
            }
        }
        var digits = builder.ToString();
        if (digits.Length == 0) return string.Empty;
        if (hasLeadingPlus) return $"+{digits}";
        if (digits.StartsWith("00", StringComparison.Ordinal)) return $"+{digits[2..]}";
        if (digits.StartsWith("27", StringComparison.Ordinal)) return $"+{digits}";
        if (digits.StartsWith('0') && digits.Length == 10) return $"+27{digits[1..]}";
        if (digits.Length == 9) return $"+27{digits}";
        return digits.Length >= 8 ? $"+{digits}" : string.Empty;
    }

    private static string MaskPhone(string phone)
    {
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (digits.Length <= 4) return "****";
        var tail = digits[^4..];
        var prefix = phone.StartsWith("+27", StringComparison.Ordinal) ? "+27 " : string.Empty;
        return $"{prefix}*** *** {tail}";
    }

    private static string ToPublicTwilioError(Exception exception)
    {
        if (exception.Message.Contains("20003", StringComparison.Ordinal) || exception.Message.Contains("Authenticate", StringComparison.OrdinalIgnoreCase))
        {
            return "SMS verification is not authenticated. Check that TWILIO_ACCOUNT_SID and TWILIO_AUTH_TOKEN belong to the account that owns TWILIO_VERIFY_SERVICE_SID.";
        }
        return "SMS verification is temporarily unavailable. Please try again later.";
    }

    private static void AddFlowEvents(MainDbContext db, Appointment appointment, DateTimeOffset now)
    {
        foreach (var type in new[] { "BookingCreated", "Confirmation", "Reminder", "PaymentPrompt", "FollowUp" })
        {
            db.AppointmentFlowEvents.Add(new AppointmentFlowEvent { TenantId = appointment.TenantId, AppointmentId = appointment.Id, Type = type, ScheduledFor = now });
        }
    }

    private static async Task<bool> HasAppointmentConflictAsync(MainDbContext db, TenantId tenantId, string appointmentIdToIgnore, DateTimeOffset startAt, DateTimeOffset endAt, CancellationToken cancellationToken)
    {
        var appointments = await db.Appointments.IgnoreQueryFilters()
            .Where(appointment => appointment.TenantId == tenantId && appointment.Id != appointmentIdToIgnore && appointment.Status != AppointmentStatus.Cancelled)
            .ToListAsync(cancellationToken);
        if (appointments.Any(appointment => appointment.StartAt < endAt && appointment.EndAt > startAt))
        {
            return true;
        }

        var manualBlocks = await db.ManualCalendarBlocks.IgnoreQueryFilters()
            .Where(block => block.TenantId == tenantId)
            .ToListAsync(cancellationToken);
        if (manualBlocks.Any(block => block.StartAt < endAt && block.EndAt > startAt))
        {
            return true;
        }

        var externalBlocks = await db.ExternalBusyBlocks.IgnoreQueryFilters()
            .Where(block => block.TenantId == tenantId)
            .ToListAsync(cancellationToken);
        return externalBlocks.Any(block => block.StartAt < endAt && block.EndAt > startAt);
    }

    private static async Task SyncGoogleCalendarEventIfConnectedAsync(MainDbContext db, Appointment appointment, INangoClient nangoClient, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var connection = await ResolveGoogleCalendarConnectionAsync(db, appointment, cancellationToken);
        if (connection is null || string.IsNullOrWhiteSpace(connection.ExternalConnectionId)) return;

        var calendarId = await ResolveAddToCalendarIdAsync(db, connection, cancellationToken);
        var request = await BuildCalendarEventRequestAsync(db, appointment, calendarId, cancellationToken);
        var existing = await db.AppointmentExternalCalendarEvents.IgnoreQueryFilters().AsTracking()
            .FirstOrDefaultAsync(item => item.AppointmentId == appointment.Id && item.Provider == "Google", cancellationToken);

        NangoCalendarEvent result;
        if (existing is null || string.IsNullOrWhiteSpace(existing.ExternalEventId))
        {
            result = await nangoClient.CreateCalendarEventAsync("google-calendar", connection.ExternalConnectionId, request, cancellationToken);
            existing ??= new AppointmentExternalCalendarEvent
            {
                TenantId = appointment.TenantId,
                AppointmentId = appointment.Id,
                Provider = "Google",
                CalendarId = calendarId
            };
            db.AppointmentExternalCalendarEvents.Add(existing);
        }
        else
        {
            result = await nangoClient.UpdateCalendarEventAsync("google-calendar", connection.ExternalConnectionId, existing.CalendarId, existing.ExternalEventId, request, cancellationToken);
        }

        existing.CalendarId = calendarId;
        existing.ExternalEventId = string.IsNullOrWhiteSpace(result.EventId) ? existing.ExternalEventId : result.EventId;
        existing.MeetUrl = result.MeetUrl ?? existing.MeetUrl;
        existing.LastSyncedAt = now;
        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task DeleteGoogleCalendarEventIfConnectedAsync(MainDbContext db, Appointment appointment, INangoClient nangoClient, CancellationToken cancellationToken)
    {
        var external = await db.AppointmentExternalCalendarEvents.IgnoreQueryFilters().AsTracking()
            .FirstOrDefaultAsync(item => item.AppointmentId == appointment.Id && item.Provider == "Google", cancellationToken);
        if (external is null || string.IsNullOrWhiteSpace(external.ExternalEventId)) return;

        var connection = await ResolveGoogleCalendarConnectionAsync(db, appointment, cancellationToken);
        if (connection is not null && !string.IsNullOrWhiteSpace(connection.ExternalConnectionId))
        {
            await nangoClient.DeleteCalendarEventAsync("google-calendar", connection.ExternalConnectionId, external.CalendarId, external.ExternalEventId, cancellationToken);
        }

        db.AppointmentExternalCalendarEvents.Remove(external);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task<IntegrationConnection?> ResolveGoogleCalendarConnectionAsync(MainDbContext db, Appointment appointment, CancellationToken cancellationToken)
    {
        return await db.IntegrationConnections.IgnoreQueryFilters().AsTracking()
                   .Where(connection => connection.TenantId == appointment.TenantId &&
                                        connection.Provider == "Google" &&
                                        connection.Capability == "Calendar" &&
                                        connection.OwnerType == ConnectorOwnerType.StaffMember &&
                                        connection.OwnerId == appointment.StaffMemberId &&
                                        connection.Status == "Connected")
                   .FirstOrDefaultAsync(cancellationToken)
               ?? await db.IntegrationConnections.IgnoreQueryFilters().AsTracking()
                   .Where(connection => connection.TenantId == appointment.TenantId &&
                                        connection.Provider == "Google" &&
                                        connection.Capability == "Calendar" &&
                                        connection.Status == "Connected")
                   .FirstOrDefaultAsync(cancellationToken);
    }

    private static async Task<string> ResolveAddToCalendarIdAsync(MainDbContext db, IntegrationConnection connection, CancellationToken cancellationToken)
    {
        var calendars = await db.IntegrationCalendars.IgnoreQueryFilters()
            .Where(calendar => calendar.IntegrationConnectionId == connection.Id)
            .ToListAsync(cancellationToken);
        return calendars.FirstOrDefault(calendar => calendar.AddEventsToCalendar)?.ExternalCalendarId
               ?? calendars.FirstOrDefault(calendar => calendar.IsPrimary)?.ExternalCalendarId
               ?? "primary";
    }

    private static async Task<NangoCalendarEventRequest> BuildCalendarEventRequestAsync(MainDbContext db, Appointment appointment, string calendarId, CancellationToken cancellationToken)
    {
        var client = await db.Clients.IgnoreQueryFilters().FirstAsync(item => item.Id == appointment.ClientId, cancellationToken);
        var serviceVersion = await GetAppointmentServiceVersionAsync(db, appointment, cancellationToken);
        var profile = await db.BusinessProfiles.IgnoreQueryFilters().FirstAsync(profile => profile.TenantId == appointment.TenantId, cancellationToken);
        var participantClients = await db.AppointmentParticipants.IgnoreQueryFilters()
            .Where(participant => participant.AppointmentId == appointment.Id)
            .Join(db.Clients.IgnoreQueryFilters(), participant => participant.ClientId, participantClient => participantClient.Id, (_, participantClient) => participantClient)
            .ToListAsync(cancellationToken);
        var attendees = new[] { client }
            .Concat(participantClients)
            .Where(item => !string.IsNullOrWhiteSpace(item.Email))
            .GroupBy(item => item.Email.Trim().ToLowerInvariant())
            .Select(group => new NangoCalendarAttendee(group.First().Name, group.First().Email))
            .ToList();

        return new NangoCalendarEventRequest(
            appointment.Id,
            calendarId,
            $"{serviceVersion.DurationMinutes} min meeting between {client.Name} and {serviceVersion.Name}",
            $"Nerova booking reference: {appointment.PublicReference}",
            serviceVersion.Location,
            appointment.StartAt,
            appointment.EndAt,
            profile.TimeZone,
            $"nerova-{appointment.Id}",
            attendees
        );
    }

    private static async Task NotifyClientAsync(MainDbContext db, Appointment appointment, string subject, string body, IEmailClient emailClient, CancellationToken cancellationToken)
    {
        var client = await db.Clients.IgnoreQueryFilters().FirstAsync(client => client.Id == appointment.ClientId, cancellationToken);
        await SendEmailIfAvailableAsync(client.Email, subject, body, emailClient, cancellationToken);
    }

    private static async Task SendEmailIfAvailableAsync(string? email, string subject, string body, IEmailClient emailClient, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(email)) return;
        await emailClient.SendAsync(email.Trim(), subject, $"<p>{System.Net.WebUtility.HtmlEncode(body)}</p>", cancellationToken);
    }

    private static async Task<AppointmentRescheduleRequest?> FindPendingApprovalAsync(MainDbContext db, string token, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var tokenHash = HashApprovalToken(token);
        var candidates = await db.AppointmentRescheduleRequests.IgnoreQueryFilters().AsTracking()
            .Where(request => request.TokenHash == tokenHash && request.Status == "Pending")
            .ToListAsync(cancellationToken);
        return candidates.FirstOrDefault(request => request.ExpiresAt > now);
    }

    private static string CreateApprovalToken()
    {
        return $"ar_{Base64UrlEncode(RandomNumberGenerator.GetBytes(32))}";
    }

    private static string HashApprovalToken(string token)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }

    private static string BuildPublicUrl(string path)
    {
        var publicUrl = Environment.GetEnvironmentVariable(SharedKernel.SinglePageApp.SinglePageAppConfiguration.PublicUrlKey);
        if (string.IsNullOrWhiteSpace(publicUrl) || publicUrl == "not-configured")
        {
            publicUrl = "https://localhost:9000";
        }
        return $"{publicUrl.TrimEnd('/')}/{path.TrimStart('/')}";
    }

    private static async Task EnsureDefaultCategoryAsync(MainDbContext db, TenantId tenantId, CancellationToken cancellationToken)
    {
        if (!await db.ServiceCategories.AnyAsync(cancellationToken))
        {
            db.ServiceCategories.Add(new ServiceCategory { TenantId = tenantId, Name = "Consultations", SortOrder = 1 });
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private static async Task<BusinessLocation> EnsureDefaultLocationAsync(MainDbContext db, TenantId tenantId, CancellationToken cancellationToken)
    {
        var location = await db.BusinessLocations.AsTracking().FirstOrDefaultAsync(location => location.IsDefault, cancellationToken);
        if (location is not null) return location;

        var profile = await db.BusinessProfiles.FirstOrDefaultAsync(cancellationToken);
        location = new BusinessLocation
        {
            TenantId = tenantId,
            Name = "Nerova Studio",
            TimeZone = profile?.TimeZone ?? "Africa/Johannesburg",
            Address = profile?.Address ?? string.Empty,
            IsDefault = true,
            IsActive = true
        };
        db.BusinessLocations.Add(location);
        await db.SaveChangesAsync(cancellationToken);
        return location;
    }

    private static async Task EnsureSchedulingFoundationBackfillAsync(MainDbContext db, string? ownerUserId, CancellationToken cancellationToken)
    {
        var profiles = await db.BusinessProfiles.AsTracking().ToListAsync(cancellationToken);
        foreach (var profile in profiles)
        {
            var location = await db.BusinessLocations.AsTracking().FirstOrDefaultAsync(location => location.TenantId == profile.TenantId && location.IsDefault, cancellationToken);
            if (location is null)
            {
                location = new BusinessLocation
                {
                    TenantId = profile.TenantId,
                    Name = "Nerova Studio",
                    TimeZone = profile.TimeZone,
                    Address = profile.Address,
                    IsDefault = true,
                    IsActive = true
                };
                db.BusinessLocations.Add(location);
            }

            var staffMembers = await db.StaffMembers.AsTracking().Where(staff => staff.TenantId == profile.TenantId).ToListAsync(cancellationToken);
            foreach (var staff in staffMembers)
            {
                if (string.IsNullOrWhiteSpace(staff.LocationId))
                {
                    staff.LocationId = location.Id;
                }

                if (string.IsNullOrWhiteSpace(staff.UserId))
                {
                    staff.UserId = ownerUserId ?? $"tenant-{profile.TenantId}";
                }
            }

            var services = await db.BookableServices.AsTracking().Where(service => service.TenantId == profile.TenantId).ToListAsync(cancellationToken);
            foreach (var service in services.Where(service => string.IsNullOrWhiteSpace(service.LocationId)))
            {
                service.LocationId = location.Id;
            }

            var versions = await db.BookableServiceVersions.AsTracking().Where(version => version.TenantId == profile.TenantId).ToListAsync(cancellationToken);
            foreach (var version in versions.Where(version => string.IsNullOrWhiteSpace(version.LocationId)))
            {
                version.LocationId = services.FirstOrDefault(service => service.Id == version.ServiceId)?.LocationId ?? location.Id;
            }

            var appointments = await db.Appointments.AsTracking().Where(appointment => appointment.TenantId == profile.TenantId).ToListAsync(cancellationToken);
            foreach (var appointment in appointments.Where(appointment => string.IsNullOrWhiteSpace(appointment.LocationId)))
            {
                appointment.LocationId = services.FirstOrDefault(service => service.Id == appointment.ServiceId)?.LocationId ?? location.Id;
            }

            var tenantIntegrations = await db.IntegrationConnections.AsTracking().Where(connection => connection.TenantId == profile.TenantId).ToListAsync(cancellationToken);
            foreach (var integration in tenantIntegrations.Where(connection => string.IsNullOrWhiteSpace(connection.OwnerId)))
            {
                integration.OwnerType = ConnectorOwnerType.Tenant;
                integration.OwnerId = profile.TenantId.ToString();
            }
        }

        if (db.ChangeTracker.HasChanges())
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private static async Task EnsureIntegrationRowsAsync(MainDbContext db, TenantId tenantId, CancellationToken cancellationToken)
    {
        if (await db.IntegrationConnections.AnyAsync(cancellationToken)) return;
        foreach (var provider in new[] { "Google", "Microsoft" })
        foreach (var capability in new[] { "Calendar", "Contacts", "Email" })
        {
            db.IntegrationConnections.Add(new IntegrationConnection
            {
                TenantId = tenantId,
                Provider = provider,
                Capability = capability,
                OwnerType = ConnectorOwnerType.Tenant,
                OwnerId = tenantId.ToString(),
                Status = "PriorityOne"
            });
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedTenantAsync(MainDbContext db, TenantId tenantId, DateTimeOffset now, string? ownerUserId, CancellationToken cancellationToken)
    {
        if (await db.BusinessProfiles.AnyAsync(cancellationToken)) return;
        var profileSlug = await ResolveSeedProfileSlugAsync(db, tenantId, cancellationToken);
        var profile = new BusinessProfile { TenantId = tenantId, Name = "Sea Point studio", Slug = profileSlug, LogoUrl = "/logos/sea-point-studio.svg" };
        var location = new BusinessLocation { TenantId = tenantId, Name = "Nerova Studio", TimeZone = profile.TimeZone, Address = profile.Address, IsDefault = true, IsActive = true };
        var category = new ServiceCategory { TenantId = tenantId, Name = "Consultations", SortOrder = 1 };
        var group = new ServiceCategory { TenantId = tenantId, Name = "Group sessions", SortOrder = 2 };
        var staff = new StaffMember { TenantId = tenantId, LocationId = location.Id, UserId = ownerUserId ?? $"tenant-{tenantId}", Name = "Sarah", Email = "sarah@nerovasystems.com", Phone = "+27 82 000 0000" };
        var full = new BookableService { TenantId = tenantId, LocationId = location.Id, CategoryId = category.Id, Name = "Full consultation", Mode = "physical", DurationMinutes = 60, PriceCents = 45000, DepositCents = 15000, PaymentPolicy = ServicePaymentPolicy.DepositBeforeBooking, Location = "Sea Point studio", SortOrder = 1 };
        var express = new BookableService { TenantId = tenantId, LocationId = location.Id, CategoryId = category.Id, Name = "Express session", Mode = "physical", DurationMinutes = 30, PriceCents = 22000, DepositCents = 0, PaymentPolicy = ServicePaymentPolicy.NoPaymentRequired, Location = "Sea Point studio", SortOrder = 2 };
        var follow = new BookableService { TenantId = tenantId, LocationId = location.Id, CategoryId = category.Id, Name = "Follow-up visit", Mode = "virtual", DurationMinutes = 20, PriceCents = 15000, DepositCents = 0, PaymentPolicy = ServicePaymentPolicy.NoPaymentRequired, Location = "Manual link per booking", SortOrder = 3 };
        var workshop = new BookableService { TenantId = tenantId, LocationId = location.Id, CategoryId = group.Id, Name = "Group workshop", Mode = "physical", DurationMinutes = 90, PriceCents = 85000, DepositCents = 25000, PaymentPolicy = ServicePaymentPolicy.DepositBeforeBooking, Location = "Sea Point studio", SortOrder = 4 };
        db.AddRange(profile, location, category, group, staff, full, express, follow, workshop);
        var serviceVersions = new[] { full, express, follow, workshop }.Select(service => CreateServiceVersion(service, 1, now)).ToArray();
        db.BookableServiceVersions.AddRange(serviceVersions);
        foreach (var day in new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday })
        {
            db.AvailabilityRules.Add(new AvailabilityRule { TenantId = tenantId, StaffMemberId = staff.Id, DayOfWeek = day, StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(17, 0) });
        }
        await SeedAppointmentsAsync(db, tenantId, staff, [full, express, follow, workshop], serviceVersions, now);
        await EnsureIntegrationRowsAsync(db, tenantId, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task<string> ResolveSeedProfileSlugAsync(MainDbContext db, TenantId tenantId, CancellationToken cancellationToken)
    {
        const string baseSlug = "sea-point-studio";
        if (!await db.BusinessProfiles.IgnoreQueryFilters().AnyAsync(profile => profile.Slug == baseSlug, cancellationToken))
        {
            return baseSlug;
        }

        var tenantSlug = $"{baseSlug}-{tenantId.Value}";
        if (!await db.BusinessProfiles.IgnoreQueryFilters().AnyAsync(profile => profile.Slug == tenantSlug, cancellationToken))
        {
            return tenantSlug;
        }

        var suffix = 2;
        while (await db.BusinessProfiles.IgnoreQueryFilters().AnyAsync(profile => profile.Slug == $"{tenantSlug}-{suffix}", cancellationToken))
        {
            suffix++;
        }

        return $"{tenantSlug}-{suffix}";
    }

    private static async Task SeedPublicDemoTenantAsync(MainDbContext db, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var profile = await db.BusinessProfiles.IgnoreQueryFilters().AsTracking().FirstOrDefaultAsync(p => p.Slug == "sea-point-studio", cancellationToken);
        if (profile is not null)
        {
            await EnsurePublicDemoHolidayFixtureAsync(db, profile, cancellationToken);
            return;
        }

        await SeedTenantAsync(db, new TenantId(1), now, null, cancellationToken);
        profile = await db.BusinessProfiles.IgnoreQueryFilters().AsTracking().FirstAsync(p => p.Slug == "sea-point-studio", cancellationToken);
        await EnsurePublicDemoHolidayFixtureAsync(db, profile, cancellationToken);
    }

    private static async Task EnsurePublicDemoHolidayFixtureAsync(MainDbContext db, BusinessProfile profile, CancellationToken cancellationToken)
    {
        var openHolidayIds = ReadOpenHolidayIds(profile).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!openHolidayIds.Add("ZA-2026-04-27")) return;
        profile.OpenPublicHolidayIdsJson = JsonSerializer.Serialize(openHolidayIds.OrderBy(id => id));
        await db.SaveChangesAsync(cancellationToken);
    }

    private static Task SeedAppointmentsAsync(MainDbContext db, TenantId tenantId, StaffMember staff, BookableService[] services, BookableServiceVersion[] serviceVersions, DateTimeOffset now)
    {
        var clients = new[]
        {
            new Client { TenantId = tenantId, Name = "Liam Botha", Phone = "+27 82 341 7890", Email = "liam@example.com", Status = "VIP", Alert = "Sensitive shoulder - prefers firm pressure", InternalNote = "Switched to firm pressure on right side - much better feedback. Continue." },
            new Client { TenantId = tenantId, Name = "Thandi Khoza", Phone = "+27 73 210 5544", Email = "thandi@example.com", Status = "Active" },
            new Client { TenantId = tenantId, Name = "Pieter de Wet", Phone = "+27 84 908 2211", Email = "pieter@example.com", Status = "Active" },
            new Client { TenantId = tenantId, Name = "Refilwe Mthembu", Phone = "+27 79 443 0012", Email = "refilwe@example.com", Status = "New" },
            new Client { TenantId = tenantId, Name = "Aisha Patel", Phone = "+27 83 553 7741", Email = "aisha@example.com", Status = "Active" }
        };
        db.Clients.AddRange(clients);
        var baseDate = new DateTimeOffset(2026, 4, 22, 0, 0, 0, TimeSpan.Zero);
        var data = new[]
        {
            (clients[0], services[0], baseDate.AddHours(9), AppointmentStatus.Pending, AppointmentPaymentStatus.Pending, AppointmentSource.PublicBookingPage),
            (clients[1], services[1], baseDate.AddHours(10.5), AppointmentStatus.Pending, AppointmentPaymentStatus.Pending, AppointmentSource.PublicBookingPage),
            (clients[2], services[2], baseDate.AddHours(13), AppointmentStatus.Confirmed, AppointmentPaymentStatus.Paid, AppointmentSource.PublicBookingPage),
            (clients[3], services[0], baseDate.AddDays(1).AddHours(9.5), AppointmentStatus.Pending, AppointmentPaymentStatus.Pending, AppointmentSource.PublicBookingPage),
            (clients[4], services[0], baseDate.AddDays(2).AddHours(14), AppointmentStatus.Pending, AppointmentPaymentStatus.Failed, AppointmentSource.PublicBookingPage)
        };
        foreach (var row in data)
        {
            var serviceVersion = serviceVersions.Single(version => version.ServiceId == row.Item2.Id);
            var appointment = new Appointment { TenantId = tenantId, ClientId = row.Item1.Id, LocationId = row.Item2.LocationId, ServiceId = row.Item2.Id, ServiceVersionId = serviceVersion.Id, StaffMemberId = staff.Id, StartAt = row.Item3, EndAt = row.Item3.AddMinutes(serviceVersion.DurationMinutes), Status = row.Item4, PaymentStatus = row.Item5, Source = row.Item6, CreatedAt = now };
            db.Appointments.Add(appointment);
            AddFlowEvents(db, appointment, now);
        }
        return Task.CompletedTask;
    }
}

public sealed record AppShellResponse(BusinessProfileDto Profile, IEnumerable<AppointmentDto> Appointments, IEnumerable<ServiceDto> Services, IEnumerable<ServiceCategoryDto> Categories, IEnumerable<ClientDto> Clients, AnalyticsDto Analytics, IEnumerable<IntegrationConnectionDto> Integrations, IEnumerable<AvailabilityRuleDto> AvailabilityRules, HolidaySettingsDto HolidaySettings, IEnumerable<ClosureDto> Closures, IEnumerable<CalendarBlockDto> CalendarBlocks);
public sealed record BusinessProfileDto(string Name, string Slug, string TimeZone, string Address, bool PublicBookingEnabled);
public sealed record AppointmentDto(string Id, string PublicReference, string ClientId, string ServiceId, string ServiceVersionId, int ServiceVersionNumber, DateTimeOffset StartAt, DateTimeOffset EndAt, string ClientName, string ClientPhone, string ClientEmail, string ServiceName, int DurationMinutes, int PriceCents, int DepositCents, string PaymentPolicy, string Status, string PaymentStatus, string Source, string Location, string AnswersJson, string ClientStatus, string? ClientAlert, string? ClientInternalNote, string? MeetUrl);
public sealed record ServiceDto(string Id, string CategoryId, string Name, string Mode, int DurationMinutes, int PriceCents, int DepositCents, string PaymentPolicy, string Location, bool IsActive, int LatestVersionNumber);
public sealed record ServiceCategoryDto(string Id, string Name);
public sealed record ClientDto(string Id, string Name, string Phone, string Email, string Status, string? Alert, string? InternalNote, int VisitCount, int LifetimeSpendCents, int NoShowCount, DateTimeOffset? LastVisitAt, IEnumerable<ClientAppointmentHistoryDto> AppointmentHistory);
public sealed record ClientAppointmentHistoryDto(string Id, string PublicReference, DateTimeOffset StartAt, DateTimeOffset EndAt, string ServiceName, int PriceCents, int DepositCents, string PaymentPolicy, string Status, string PaymentStatus, string Source, string Location);
public sealed record AnalyticsDto(int Bookings, int RevenueCents, int ClientsServed, int AverageBookingValueCents, decimal NoShowRate);
public sealed record IntegrationConnectionDto(string Provider, string Capability, string Status, DateTimeOffset? LastSyncedAt, string OwnerType, string OwnerId, string? ExternalConnectionId);
public sealed record AvailabilityRuleDto(string Id, string DayOfWeek, string StartTime, string EndTime);
public sealed record HolidaySettingsDto(string CountryCode, IEnumerable<HolidayCountryDto> Countries, IEnumerable<PublicHolidayDto> Holidays);
public sealed record HolidayCountryDto(string Code, string Name);
public sealed record PublicHolidayDto(string Id, string CountryCode, DateOnly Date, string Label, bool IsOpen);
public sealed record ClosureDto(string Id, DateOnly StartDate, DateOnly EndDate, string Label, string Type);
public sealed record CalendarBlockDto(string Id, string Title, DateTimeOffset StartAt, DateTimeOffset EndAt, string Type);
public sealed record SlotDto(DateTimeOffset StartAt, DateTimeOffset EndAt);
public sealed record PublicBookingProfileResponse(string Name, string Slug, string TimeZone, string Address, string? LogoUrl, IEnumerable<ServiceDto> Services);
public sealed record PublicClientPrefillResponse(string Name, string Email);
public sealed record StartPhoneVerificationRequest(string Phone);
public sealed record StartPhoneVerificationResponse(string MaskedPhone, DateTimeOffset ExpiresAt, int ResendAfterSeconds);
public sealed record CheckPhoneVerificationRequest(string Phone, string Code);
public sealed record CheckPhoneVerificationResponse(string PhoneVerificationToken, string MaskedPhone, string Name, string Email);
public sealed record PublicBookingRequest(string ServiceId, DateTimeOffset StartAt, string Name, string Phone, string Email, string? PhoneVerificationToken, Dictionary<string, string> Answers);
public sealed record PublicBookingCreatedResponse(string Reference, bool PaymentRequired, string? PaymentUrl);
public sealed record CreateServiceRequest(string Name, string CategoryName, string? Description, string Mode, int DurationMinutes, int PriceCents, int DepositCents, string? PaymentPolicy, int BufferBeforeMinutes, int BufferAfterMinutes, string Location);
public sealed record CreateCalendarBlockRequest(string Title, DateTimeOffset StartAt, DateTimeOffset EndAt, string? StaffMemberId);
public sealed record WeeklyAvailabilityRequest(IEnumerable<AvailabilityDayRequest>? Days);
public sealed record AvailabilityDayRequest(string DayOfWeek, IEnumerable<AvailabilityWindowRequest>? Windows);
public sealed record AvailabilityWindowRequest(string StartTime, string EndTime);
public sealed record HolidaySettingsRequest(string CountryCode, IEnumerable<string>? OpenHolidayIds);
public sealed record CreateClosureRequest(DateOnly StartDate, DateOnly EndDate, string Label);
public sealed record AppointmentParticipantRequest(string Name, string? Phone, string? Email);
public sealed record UpdateAppointmentLocationRequest(string Location);
public sealed record CreateRescheduleRequest(DateTimeOffset ProposedStartAt, string? Note);
public sealed record RescheduleRequestResponse(string ApprovalUrl, string ApprovalToken, string Status);
public sealed record RescheduleApprovalResponse(AppointmentDto Appointment, DateTimeOffset ProposedStartAt, DateTimeOffset ProposedEndAt, string Note, string Status);
public sealed record PublicApprovalResultResponse(string AppointmentReference, string Status);
public sealed record UpdateAppointmentStatusRequest(string Status, string? PaymentStatus);
public sealed record UpdateClientRequest(string Name, string Phone, string Email, string Status, string? Alert, string? InternalNote);
public sealed record PaystackInitializeRequest(string AppointmentId);
public sealed record PaystackInitializeResponse(string Reference, string? AuthorizationUrl, int AmountCents);
public sealed record TerminalPaymentIntentResponse(string Reference, int AmountCents, string Status, string VirtualTerminalCode, string TerminalUrl);
public sealed record PaymentConfirmationResponse(string Reference, string Status, string AppointmentReference);
public sealed record PaymentOverviewResponse(PaymentStatsDto Stats, PaystackSubaccountDto? Subaccount, IEnumerable<PaymentIntentDto> RecentPayments);
public sealed record PaymentStatsDto(int TotalTracked, int PaidOrConfirmed, int NeedsAction, int Overdue, int AmountPendingCents, int AmountPaidCents);
public sealed record PaymentIntentDto(string Reference, int AmountCents, string Status, string? AuthorizationUrl, DateTimeOffset CreatedAt, DateTimeOffset? ConfirmedAt, string AppointmentReference, string ClientName, string ServiceName);
public sealed record PaystackSubaccountDto(string SubaccountCode, string? SplitCode, string? VirtualTerminalCode, string BusinessName, string SettlementBankName, string SettlementBankCode, string AccountName, string MaskedAccountNumber, string Currency, string? PrimaryContactName, string? PrimaryContactEmail, string? PrimaryContactPhone, bool IsActive, bool IsVerified, string SettlementSchedule, DateTimeOffset LastSyncedAt);
public sealed record ResolvePaystackAccountRequest(string BankCode, string AccountNumber);
public sealed record ResolvePaystackAccountResponse(string BankCode, string MaskedAccountNumber, string AccountName);
public sealed record SavePaystackSubaccountRequest(string BankName, string BankCode, string AccountNumber, string AccountName, string? PrimaryContactName, string? PrimaryContactEmail, string? PrimaryContactPhone);
public sealed record PaystackSettlementsResponse(IEnumerable<PaystackSettlementResult> Settlements);
