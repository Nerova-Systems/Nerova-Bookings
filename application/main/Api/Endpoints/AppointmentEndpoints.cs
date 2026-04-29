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

namespace Main.Api.Endpoints;

public sealed class AppointmentEndpoints : IEndpoints
{
    public void MapEndpoints(IEndpointRouteBuilder routes)
    {
        var app = routes.MapGroup("/api/main/app").RequireAuthorization().WithTags("Nerova App");
        app.MapGet("/shell", GetShell);
        app.MapGet("/availability/slots", GetAuthenticatedSlots);
        app.MapPost("/services", CreateService);
        app.MapPost("/services/{id}/archive", ArchiveService);
        app.MapPost("/appointments/{id}/confirm", ConfirmAppointment);
        app.MapPost("/payments/paystack/initialize", InitializePaystackPayment);
        app.MapGet("/payments/paystack/confirm", ConfirmPaystackPayment);
        app.MapGet("/integrations", GetIntegrations);

        var booking = routes.MapGroup("/api/main/public-booking").WithTags("Public booking");
        booking.MapGet("/{businessSlug}", GetPublicBookingProfile);
        booking.MapGet("/{businessSlug}/slots", GetPublicSlots);
        booking.MapPost("/{businessSlug}/appointments", CreatePublicAppointment).DisableAntiforgery();
        booking.MapGet("/confirmation/{reference}", GetPublicConfirmation);

        routes.MapPost("/api/main/payments/paystack/webhook", HandlePaystackWebhook).WithTags("Paystack").DisableAntiforgery();
        routes.MapGet("/api/main/payments/paystack/confirm", ConfirmPaystackPayment).WithTags("Paystack");
    }

    private static async Task<IResult> GetShell(MainDbContext db, IExecutionContext executionContext, TimeProvider timeProvider, CancellationToken cancellationToken)
    {
        var tenantId = RequireTenant(executionContext);
        await SeedTenantAsync(db, tenantId, timeProvider.GetUtcNow(), cancellationToken);
        return Results.Ok(await BuildShellAsync(db, cancellationToken));
    }

    private static async Task<IResult> CreateService(CreateServiceRequest request, MainDbContext db, IExecutionContext executionContext, CancellationToken cancellationToken)
    {
        var tenantId = RequireTenant(executionContext);
        await EnsureDefaultCategoryAsync(db, tenantId, cancellationToken);
        var category = await db.ServiceCategories.AsTracking().FirstOrDefaultAsync(c => c.Name == request.CategoryName, cancellationToken);
        if (category is null)
        {
            category = new ServiceCategory { TenantId = tenantId, Name = request.CategoryName, SortOrder = 100 };
            db.ServiceCategories.Add(category);
        }
        var service = new BookableService
        {
            TenantId = tenantId,
            CategoryId = category.Id,
            Name = request.Name,
            Description = request.Description ?? string.Empty,
            Mode = request.Mode,
            DurationMinutes = request.DurationMinutes,
            PriceCents = request.PriceCents,
            DepositCents = request.DepositCents,
            BufferBeforeMinutes = request.BufferBeforeMinutes,
            BufferAfterMinutes = request.BufferAfterMinutes,
            Location = request.Location,
            SortOrder = 100
        };
        db.BookableServices.Add(service);
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(await BuildShellAsync(db, cancellationToken));
    }

    private static async Task<IResult> ArchiveService(string id, MainDbContext db, CancellationToken cancellationToken)
    {
        var service = await db.BookableServices.AsTracking().FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (service is null) return Results.NotFound();
        service.IsActive = false;
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok();
    }

    private static async Task<IResult> ConfirmAppointment(string id, MainDbContext db, CancellationToken cancellationToken)
    {
        var appointment = await db.Appointments.AsTracking().FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (appointment is null) return Results.NotFound();
        appointment.Status = AppointmentStatus.Confirmed;
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
        return Results.Ok(new PublicBookingProfileResponse(profile.Name, profile.Slug, profile.TimeZone, profile.Address, services.Select(ToServiceDto)));
    }

    private static async Task<IResult> GetPublicSlots(string businessSlug, string serviceId, DateOnly date, MainDbContext db, TimeProvider timeProvider, CancellationToken cancellationToken)
    {
        await SeedPublicDemoTenantAsync(db, timeProvider.GetUtcNow(), cancellationToken);
        var profile = await db.BusinessProfiles.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Slug == businessSlug && p.PublicBookingEnabled, cancellationToken);
        if (profile is null) return Results.NotFound();
        var slots = await BuildSlotsAsync(db, serviceId, date, cancellationToken, profile.TenantId);
        return Results.Ok(slots);
    }

    private static async Task<IResult> CreatePublicAppointment(string businessSlug, PublicBookingRequest request, MainDbContext db, TimeProvider timeProvider, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken)
    {
        await SeedPublicDemoTenantAsync(db, timeProvider.GetUtcNow(), cancellationToken);
        var profile = await db.BusinessProfiles.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Slug == businessSlug && p.PublicBookingEnabled, cancellationToken);
        if (profile is null) return Results.NotFound();
        var service = await db.BookableServices.IgnoreQueryFilters().FirstOrDefaultAsync(s => s.Id == request.ServiceId && s.TenantId == profile.TenantId && s.IsActive, cancellationToken);
        if (service is null) return Results.BadRequest("Service is not bookable.");

        var startAt = request.StartAt.ToUniversalTime();
        var endAt = startAt.AddMinutes(service.DurationMinutes);
        var overlaps = await db.Appointments.IgnoreQueryFilters().AnyAsync(a =>
            a.TenantId == profile.TenantId &&
            a.Status != AppointmentStatus.Cancelled &&
            a.StartAt < endAt &&
            a.EndAt > startAt,
            cancellationToken);
        if (overlaps) return Results.Conflict("Selected slot is no longer available.");

        var client = await db.Clients.IgnoreQueryFilters().AsTracking().FirstOrDefaultAsync(c => c.TenantId == profile.TenantId && c.Phone == request.Phone, cancellationToken);
        if (client is null)
        {
            client = new Client { TenantId = profile.TenantId, Name = request.Name, Phone = request.Phone, Email = request.Email, Status = "New" };
            db.Clients.Add(client);
        }

        var staff = await db.StaffMembers.IgnoreQueryFilters().FirstAsync(s => s.TenantId == profile.TenantId && s.IsActive, cancellationToken);
        var appointment = new Appointment
        {
            TenantId = profile.TenantId,
            ClientId = client.Id,
            ServiceId = service.Id,
            StaffMemberId = staff.Id,
            StartAt = startAt,
            EndAt = endAt,
            Status = service.DepositCents > 0 ? AppointmentStatus.Pending : AppointmentStatus.Confirmed,
            PaymentStatus = service.DepositCents > 0 ? AppointmentPaymentStatus.Pending : AppointmentPaymentStatus.NotRequired,
            Source = AppointmentSource.PublicBookingPage,
            AnswersJson = JsonSerializer.Serialize(request.Answers),
            CreatedAt = timeProvider.GetUtcNow()
        };
        db.Appointments.Add(appointment);
        AddFlowEvents(db, appointment, timeProvider.GetUtcNow());

        string? paymentUrl = null;
        if (service.DepositCents > 0)
        {
            var intent = CreatePaymentIntent(profile.TenantId, appointment.Id, service.DepositCents, timeProvider.GetUtcNow());
            intent.AuthorizationUrl = await TryInitializePaystackTransactionAsync(intent, request.Email, httpClientFactory, cancellationToken);
            paymentUrl = intent.AuthorizationUrl;
            db.AppointmentPaymentIntents.Add(intent);
        }

        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(new PublicBookingCreatedResponse(appointment.PublicReference, service.DepositCents > 0, paymentUrl));
    }

    private static async Task<IResult> GetPublicConfirmation(string reference, MainDbContext db, CancellationToken cancellationToken)
    {
        var appointment = await db.Appointments.IgnoreQueryFilters().FirstOrDefaultAsync(a => a.PublicReference == reference, cancellationToken);
        if (appointment is null) return Results.NotFound();
        return Results.Ok(await ToAppointmentDetailAsync(db, appointment, cancellationToken));
    }

    private static async Task<IResult> InitializePaystackPayment(PaystackInitializeRequest request, MainDbContext db, TimeProvider timeProvider, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken)
    {
        var appointment = await db.Appointments.AsTracking().FirstOrDefaultAsync(a => a.Id == request.AppointmentId, cancellationToken);
        if (appointment is null) return Results.NotFound();
        var service = await db.BookableServices.FirstAsync(s => s.Id == appointment.ServiceId, cancellationToken);
        var intent = CreatePaymentIntent(appointment.TenantId, appointment.Id, service.DepositCents > 0 ? service.DepositCents : service.PriceCents, timeProvider.GetUtcNow());
        var client = await db.Clients.FirstAsync(c => c.Id == appointment.ClientId, cancellationToken);
        intent.AuthorizationUrl = await TryInitializePaystackTransactionAsync(intent, client.Email, httpClientFactory, cancellationToken);
        db.AppointmentPaymentIntents.Add(intent);
        appointment.PaymentStatus = AppointmentPaymentStatus.Pending;
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(new PaystackInitializeResponse(intent.Reference, intent.AuthorizationUrl, intent.AmountCents));
    }

    private static async Task<IResult> ConfirmPaystackPayment(string reference, MainDbContext db, TimeProvider timeProvider, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken)
    {
        var intent = await db.AppointmentPaymentIntents.AsTracking().FirstOrDefaultAsync(p => p.Reference == reference, cancellationToken);
        if (intent is null) return Results.NotFound();
        var appointment = await db.Appointments.IgnoreQueryFilters().AsTracking().FirstAsync(a => a.Id == intent.AppointmentId, cancellationToken);
        if (intent.Status == "Confirmed")
        {
            return Results.Ok(new PaymentConfirmationResponse(reference, intent.Status, appointment.PublicReference));
        }
        if (!await IsPaystackTransactionSuccessfulAsync(reference, intent.AmountCents, httpClientFactory, cancellationToken))
        {
            return Results.Problem("Paystack has not verified this transaction as successful yet.", statusCode: StatusCodes.Status409Conflict);
        }
        intent.Status = "Confirmed";
        intent.ConfirmedAt = timeProvider.GetUtcNow();
        appointment.PaymentStatus = AppointmentPaymentStatus.DepositPaid;
        appointment.Status = AppointmentStatus.Confirmed;
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(new PaymentConfirmationResponse(reference, intent.Status, appointment.PublicReference));
    }

    private static async Task<IResult> HandlePaystackWebhook(HttpRequest request, MainDbContext db, TimeProvider timeProvider, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken)
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
        var reference = json.RootElement.GetProperty("data").GetProperty("reference").GetString();
        if (!string.IsNullOrWhiteSpace(reference))
        {
            await ConfirmPaystackPayment(reference, db, timeProvider, httpClientFactory, cancellationToken);
        }
        return Results.Ok();
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

    private static async Task<AppShellResponse> BuildShellAsync(MainDbContext db, CancellationToken cancellationToken)
    {
        var appointments = await db.Appointments.OrderBy(a => a.StartAt).ToListAsync(cancellationToken);
        var clients = await db.Clients.ToListAsync(cancellationToken);
        var services = await db.BookableServices.OrderBy(s => s.SortOrder).ToListAsync(cancellationToken);
        var categories = await db.ServiceCategories.OrderBy(c => c.SortOrder).ToListAsync(cancellationToken);
        var profile = await db.BusinessProfiles.FirstAsync(cancellationToken);
        var integrations = await db.IntegrationConnections.OrderBy(i => i.Provider).ThenBy(i => i.Capability).ToListAsync(cancellationToken);
        return new AppShellResponse(
            new BusinessProfileDto(profile.Name, profile.Slug, profile.TimeZone, profile.Address, profile.PublicBookingEnabled),
            appointments.Select(a => ToAppointmentDto(a, clients, services)),
            services.Select(ToServiceDto),
            categories.Select(c => new ServiceCategoryDto(c.Id, c.Name)),
            clients.Select(c => ToClientDto(c, appointments, services)),
            BuildAnalytics(appointments, services, clients),
            integrations.Select(i => new IntegrationConnectionDto(i.Provider, i.Capability, i.Status, i.LastSyncedAt))
        );
    }

    private static AppointmentDto ToAppointmentDto(Appointment appointment, List<Client> clients, List<BookableService> services)
    {
        var client = clients.First(c => c.Id == appointment.ClientId);
        var service = services.First(s => s.Id == appointment.ServiceId);
        return new AppointmentDto(
            appointment.Id,
            appointment.PublicReference,
            appointment.StartAt,
            appointment.EndAt,
            client.Name,
            client.Phone,
            client.Email,
            service.Name,
            service.DurationMinutes,
            service.PriceCents,
            service.DepositCents,
            appointment.Status.ToString(),
            appointment.PaymentStatus.ToString(),
            appointment.Source.ToString(),
            service.Location,
            appointment.AnswersJson,
            client.Status,
            client.Alert,
            client.InternalNote
        );
    }

    private static async Task<AppointmentDto> ToAppointmentDetailAsync(MainDbContext db, Appointment appointment, CancellationToken cancellationToken)
    {
        var client = await db.Clients.IgnoreQueryFilters().FirstAsync(c => c.Id == appointment.ClientId, cancellationToken);
        var service = await db.BookableServices.IgnoreQueryFilters().FirstAsync(s => s.Id == appointment.ServiceId, cancellationToken);
        return ToAppointmentDto(appointment, [client], [service]);
    }

    private static ServiceDto ToServiceDto(BookableService service)
    {
        return new ServiceDto(service.Id, service.CategoryId, service.Name, service.Mode, service.DurationMinutes, service.PriceCents, service.DepositCents, service.Location, service.IsActive);
    }

    private static ClientDto ToClientDto(Client client, List<Appointment> appointments, List<BookableService> services)
    {
        var clientAppointments = appointments.Where(a => a.ClientId == client.Id).ToList();
        var lifetime = clientAppointments.Sum(a => services.First(s => s.Id == a.ServiceId).PriceCents);
        return new ClientDto(client.Id, client.Name, client.Phone, client.Email, client.Status, client.Alert, client.InternalNote, clientAppointments.Count, lifetime, clientAppointments.OrderByDescending(a => a.StartAt).FirstOrDefault()?.StartAt);
    }

    private static AnalyticsDto BuildAnalytics(List<Appointment> appointments, List<BookableService> services, List<Client> clients)
    {
        var revenue = appointments.Where(a => a.PaymentStatus is AppointmentPaymentStatus.Paid or AppointmentPaymentStatus.DepositPaid or AppointmentPaymentStatus.NotRequired)
            .Sum(a => services.First(s => s.Id == a.ServiceId).PriceCents);
        var noShows = appointments.Count(a => a.Status == AppointmentStatus.NoShow);
        return new AnalyticsDto(appointments.Count, revenue, clients.Count, appointments.Count == 0 ? 0 : revenue / appointments.Count, appointments.Count == 0 ? 0 : Math.Round(noShows * 100m / appointments.Count, 1));
    }

    private static async Task<IReadOnlyList<SlotDto>> BuildSlotsAsync(MainDbContext db, string serviceId, DateOnly date, CancellationToken cancellationToken, TenantId? publicTenantId = null)
    {
        var serviceQuery = publicTenantId is null ? db.BookableServices : db.BookableServices.IgnoreQueryFilters().Where(s => s.TenantId == publicTenantId);
        var service = await serviceQuery.FirstAsync(s => s.Id == serviceId, cancellationToken);
        var tenantId = publicTenantId ?? service.TenantId;
        var rules = await db.AvailabilityRules.IgnoreQueryFilters().Where(r => r.TenantId == tenantId && r.DayOfWeek == date.DayOfWeek).ToListAsync(cancellationToken);
        var existing = await db.Appointments.IgnoreQueryFilters().Where(a => a.TenantId == tenantId && DateOnly.FromDateTime(a.StartAt.DateTime) == date && a.Status != AppointmentStatus.Cancelled).ToListAsync(cancellationToken);
        var slots = new List<SlotDto>();
        foreach (var rule in rules)
        {
            var cursor = date.ToDateTime(rule.StartTime);
            var end = date.ToDateTime(rule.EndTime);
            while (cursor.AddMinutes(service.DurationMinutes) <= end)
            {
                var start = new DateTimeOffset(cursor, TimeSpan.FromHours(2));
                var slotEnd = start.AddMinutes(service.DurationMinutes);
                if (!existing.Any(a => a.StartAt < slotEnd && a.EndAt > start))
                {
                    slots.Add(new SlotDto(start, slotEnd));
                }
                cursor = cursor.AddMinutes(30);
            }
        }
        return slots;
    }

    private static AppointmentPaymentIntent CreatePaymentIntent(TenantId tenantId, string appointmentId, int amountCents, DateTimeOffset now)
    {
        return new AppointmentPaymentIntent
        {
            TenantId = tenantId,
            AppointmentId = appointmentId,
            Reference = $"ps_{Guid.NewGuid():N}",
            AmountCents = amountCents,
            CreatedAt = now
        };
    }

    private static async Task<string?> TryInitializePaystackTransactionAsync(AppointmentPaymentIntent intent, string email, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken)
    {
        var secret = Environment.GetEnvironmentVariable("PAYSTACK_SECRET_KEY");
        if (!IsConfiguredPaystackSecret(secret)) return null;

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.paystack.co/transaction/initialize")
        {
            Content = JsonContent.Create(new
            {
                email,
                amount = intent.AmountCents,
                currency = "ZAR",
                reference = intent.Reference,
                callback_url = BuildPaystackCallbackUrl(intent.Reference),
                channels = new[] { "card", "bank", "apple_pay", "eft", "capitec_pay" }
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secret);
        var response = await httpClientFactory.CreateClient().SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode) return null;
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        return json.GetProperty("data").GetProperty("authorization_url").GetString();
    }

    private static async Task<bool> IsPaystackTransactionSuccessfulAsync(string reference, int amountCents, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken)
    {
        var secret = Environment.GetEnvironmentVariable("PAYSTACK_SECRET_KEY");
        if (!IsConfiguredPaystackSecret(secret)) return false;

        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.paystack.co/transaction/verify/{reference}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secret);
        var response = await httpClientFactory.CreateClient().SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode) return false;

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var data = json.GetProperty("data");
        return json.GetProperty("status").GetBoolean() &&
               data.GetProperty("status").GetString() == "success" &&
               data.GetProperty("reference").GetString() == reference &&
               data.GetProperty("amount").GetInt32() == amountCents;
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

    private static void AddFlowEvents(MainDbContext db, Appointment appointment, DateTimeOffset now)
    {
        foreach (var type in new[] { "BookingCreated", "Confirmation", "Reminder", "PaymentPrompt", "FollowUp" })
        {
            db.AppointmentFlowEvents.Add(new AppointmentFlowEvent { TenantId = appointment.TenantId, AppointmentId = appointment.Id, Type = type, ScheduledFor = now });
        }
    }

    private static async Task EnsureDefaultCategoryAsync(MainDbContext db, TenantId tenantId, CancellationToken cancellationToken)
    {
        if (!await db.ServiceCategories.AnyAsync(cancellationToken))
        {
            db.ServiceCategories.Add(new ServiceCategory { TenantId = tenantId, Name = "Consultations", SortOrder = 1 });
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private static async Task EnsureIntegrationRowsAsync(MainDbContext db, TenantId tenantId, CancellationToken cancellationToken)
    {
        if (await db.IntegrationConnections.AnyAsync(cancellationToken)) return;
        foreach (var provider in new[] { "Google", "Microsoft" })
        foreach (var capability in new[] { "Calendar", "Contacts", "Email" })
        {
            db.IntegrationConnections.Add(new IntegrationConnection { TenantId = tenantId, Provider = provider, Capability = capability, Status = "PriorityOne" });
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedTenantAsync(MainDbContext db, TenantId tenantId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (await db.BusinessProfiles.AnyAsync(cancellationToken)) return;
        var profile = new BusinessProfile { TenantId = tenantId, Name = "Sea Point studio", Slug = "sea-point-studio" };
        var category = new ServiceCategory { TenantId = tenantId, Name = "Consultations", SortOrder = 1 };
        var group = new ServiceCategory { TenantId = tenantId, Name = "Group sessions", SortOrder = 2 };
        var staff = new StaffMember { TenantId = tenantId, Name = "Sarah", Email = "sarah@nerovasystems.com", Phone = "+27 82 000 0000" };
        var full = new BookableService { TenantId = tenantId, CategoryId = category.Id, Name = "Full consultation", Mode = "physical", DurationMinutes = 60, PriceCents = 45000, DepositCents = 15000, Location = "Sea Point studio", SortOrder = 1 };
        var express = new BookableService { TenantId = tenantId, CategoryId = category.Id, Name = "Express session", Mode = "physical", DurationMinutes = 30, PriceCents = 22000, DepositCents = 0, Location = "Sea Point studio", SortOrder = 2 };
        var follow = new BookableService { TenantId = tenantId, CategoryId = category.Id, Name = "Follow-up visit", Mode = "virtual", DurationMinutes = 20, PriceCents = 15000, DepositCents = 0, Location = "Manual link per booking", SortOrder = 3 };
        var workshop = new BookableService { TenantId = tenantId, CategoryId = group.Id, Name = "Group workshop", Mode = "physical", DurationMinutes = 90, PriceCents = 85000, DepositCents = 25000, Location = "Sea Point studio", SortOrder = 4 };
        db.AddRange(profile, category, group, staff, full, express, follow, workshop);
        foreach (var day in new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday })
        {
            db.AvailabilityRules.Add(new AvailabilityRule { TenantId = tenantId, StaffMemberId = staff.Id, DayOfWeek = day, StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(17, 0) });
        }
        await SeedAppointmentsAsync(db, tenantId, staff, [full, express, follow, workshop], now);
        await EnsureIntegrationRowsAsync(db, tenantId, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedPublicDemoTenantAsync(MainDbContext db, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var hasPublicDemo = await db.BusinessProfiles.IgnoreQueryFilters().AnyAsync(p => p.Slug == "sea-point-studio", cancellationToken);
        if (hasPublicDemo) return;
        await SeedTenantAsync(db, new TenantId(1), now, cancellationToken);
    }

    private static Task SeedAppointmentsAsync(MainDbContext db, TenantId tenantId, StaffMember staff, BookableService[] services, DateTimeOffset now)
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
            var appointment = new Appointment { TenantId = tenantId, ClientId = row.Item1.Id, ServiceId = row.Item2.Id, StaffMemberId = staff.Id, StartAt = row.Item3, EndAt = row.Item3.AddMinutes(row.Item2.DurationMinutes), Status = row.Item4, PaymentStatus = row.Item5, Source = row.Item6, CreatedAt = now };
            db.Appointments.Add(appointment);
            AddFlowEvents(db, appointment, now);
        }
        return Task.CompletedTask;
    }
}

public sealed record AppShellResponse(BusinessProfileDto Profile, IEnumerable<AppointmentDto> Appointments, IEnumerable<ServiceDto> Services, IEnumerable<ServiceCategoryDto> Categories, IEnumerable<ClientDto> Clients, AnalyticsDto Analytics, IEnumerable<IntegrationConnectionDto> Integrations);
public sealed record BusinessProfileDto(string Name, string Slug, string TimeZone, string Address, bool PublicBookingEnabled);
public sealed record AppointmentDto(string Id, string PublicReference, DateTimeOffset StartAt, DateTimeOffset EndAt, string ClientName, string ClientPhone, string ClientEmail, string ServiceName, int DurationMinutes, int PriceCents, int DepositCents, string Status, string PaymentStatus, string Source, string Location, string AnswersJson, string ClientStatus, string? ClientAlert, string? ClientInternalNote);
public sealed record ServiceDto(string Id, string CategoryId, string Name, string Mode, int DurationMinutes, int PriceCents, int DepositCents, string Location, bool IsActive);
public sealed record ServiceCategoryDto(string Id, string Name);
public sealed record ClientDto(string Id, string Name, string Phone, string Email, string Status, string? Alert, string? InternalNote, int VisitCount, int LifetimeSpendCents, DateTimeOffset? LastVisitAt);
public sealed record AnalyticsDto(int Bookings, int RevenueCents, int ClientsServed, int AverageBookingValueCents, decimal NoShowRate);
public sealed record IntegrationConnectionDto(string Provider, string Capability, string Status, DateTimeOffset? LastSyncedAt);
public sealed record SlotDto(DateTimeOffset StartAt, DateTimeOffset EndAt);
public sealed record PublicBookingProfileResponse(string Name, string Slug, string TimeZone, string Address, IEnumerable<ServiceDto> Services);
public sealed record PublicBookingRequest(string ServiceId, DateTimeOffset StartAt, string Name, string Phone, string Email, Dictionary<string, string> Answers);
public sealed record PublicBookingCreatedResponse(string Reference, bool PaymentRequired, string? PaymentUrl);
public sealed record CreateServiceRequest(string Name, string CategoryName, string? Description, string Mode, int DurationMinutes, int PriceCents, int DepositCents, int BufferBeforeMinutes, int BufferAfterMinutes, string Location);
public sealed record PaystackInitializeRequest(string AppointmentId);
public sealed record PaystackInitializeResponse(string Reference, string? AuthorizationUrl, int AmountCents);
public sealed record PaymentConfirmationResponse(string Reference, string Status, string AppointmentReference);
