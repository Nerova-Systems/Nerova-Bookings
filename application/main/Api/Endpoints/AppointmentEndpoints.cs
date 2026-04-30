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
        app.MapPut("/services/{id}", UpdateService);
        app.MapPost("/services/{id}/archive", ArchiveService);
        app.MapPost("/services/{id}/restore", RestoreService);
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
            PaymentPolicy = NormalizePaymentPolicy(request.PaymentPolicy, request.DepositCents),
            BufferBeforeMinutes = request.BufferBeforeMinutes,
            BufferAfterMinutes = request.BufferAfterMinutes,
            Location = request.Location,
            SortOrder = 100
        };
        db.BookableServices.Add(service);
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(await BuildShellAsync(db, cancellationToken));
    }

    private static async Task<IResult> UpdateService(string id, CreateServiceRequest request, MainDbContext db, IExecutionContext executionContext, CancellationToken cancellationToken)
    {
        var tenantId = RequireTenant(executionContext);
        var service = await db.BookableServices.AsTracking().FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (service is null) return Results.NotFound();
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
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(await BuildShellAsync(db, cancellationToken));
    }

    private static async Task<IResult> ArchiveService(string id, MainDbContext db, CancellationToken cancellationToken)
    {
        var service = await db.BookableServices.AsTracking().FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (service is null) return Results.NotFound();
        service.IsActive = false;
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(await BuildShellAsync(db, cancellationToken));
    }

    private static async Task<IResult> RestoreService(string id, MainDbContext db, CancellationToken cancellationToken)
    {
        var service = await db.BookableServices.AsTracking().FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (service is null) return Results.NotFound();
        service.IsActive = true;
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(await BuildShellAsync(db, cancellationToken));
    }

    private static async Task<IResult> ConfirmAppointment(string id, MainDbContext db, CancellationToken cancellationToken)
    {
        var appointment = await db.Appointments.AsTracking().FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (appointment is null) return Results.NotFound();
        appointment.Status = AppointmentStatus.Confirmed;
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(await BuildShellAsync(db, cancellationToken));
    }

    private static async Task<IResult> UpdateAppointmentStatus(string id, UpdateAppointmentStatusRequest request, MainDbContext db, TimeProvider timeProvider, CancellationToken cancellationToken)
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
        return Results.Ok(new PublicBookingProfileResponse(profile.Name, profile.Slug, profile.TimeZone, profile.Address, profile.LogoUrl, services.Select(ToServiceDto)));
    }

    private static async Task<IResult> GetPublicClientPrefill(string businessSlug, string phone, MainDbContext db, TimeProvider timeProvider, CancellationToken cancellationToken)
    {
        await SeedPublicDemoTenantAsync(db, timeProvider.GetUtcNow(), cancellationToken);
        var profile = await db.BusinessProfiles.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Slug == businessSlug && p.PublicBookingEnabled, cancellationToken);
        if (profile is null) return Results.NotFound();

        var normalizedPhone = NormalizePhone(phone);
        if (normalizedPhone.Length == 0) return Results.Ok(new PublicClientPrefillResponse(string.Empty, string.Empty));

        var tenantClients = await db.Clients.IgnoreQueryFilters()
            .Where(c => c.TenantId == profile.TenantId)
            .ToListAsync(cancellationToken);
        var client = tenantClients.FirstOrDefault(c => NormalizePhone(c.Phone) == normalizedPhone);

        return Results.Ok(new PublicClientPrefillResponse(client?.Name ?? string.Empty, client?.Email ?? string.Empty));
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

        var startAt = request.StartAt.ToUniversalTime();
        var endAt = startAt.AddMinutes(service.DurationMinutes);
        var existingAppointments = await db.Appointments.IgnoreQueryFilters()
            .Where(a => a.TenantId == profile.TenantId && a.Status != AppointmentStatus.Cancelled)
            .ToListAsync(cancellationToken);
        var overlaps = existingAppointments.Any(a => a.StartAt < endAt && a.EndAt > startAt);
        if (overlaps) return Results.Conflict("Selected slot is no longer available.");

        var normalizedPhone = NormalizePhone(request.Phone);
        var tenantClients = await db.Clients.IgnoreQueryFilters().AsTracking()
            .Where(c => c.TenantId == profile.TenantId)
            .ToListAsync(cancellationToken);
        var client = tenantClients.FirstOrDefault(c => NormalizePhone(c.Phone) == normalizedPhone);
        if (client is null)
        {
            client = new Client { TenantId = profile.TenantId, Name = request.Name, Phone = request.Phone, Email = request.Email, Status = "New" };
            db.Clients.Add(client);
        }

        var staff = await db.StaffMembers.IgnoreQueryFilters().FirstAsync(s => s.TenantId == profile.TenantId && s.IsActive, cancellationToken);
        var paymentPolicy = NormalizePaymentPolicy(service.PaymentPolicy.ToString(), service.DepositCents);
        var requiresHostedPayment = paymentPolicy is ServicePaymentPolicy.DepositBeforeBooking or ServicePaymentPolicy.FullPaymentBeforeBooking;
        var appointment = new Appointment
        {
            TenantId = profile.TenantId,
            ClientId = client.Id,
            ServiceId = service.Id,
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
            var intent = CreatePaymentIntent(profile.TenantId, appointment.Id, GetPaymentAmountCents(service), AppointmentPaymentChannel.HostedCheckout, timeProvider.GetUtcNow());
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

    private static async Task<IResult> InitializePaystackPayment(PaystackInitializeRequest request, MainDbContext db, TimeProvider timeProvider, IPaystackClient paystackClient, CancellationToken cancellationToken)
    {
        var appointment = await db.Appointments.AsTracking().FirstOrDefaultAsync(a => a.Id == request.AppointmentId, cancellationToken);
        if (appointment is null) return Results.NotFound();
        var subaccountCode = await GetActivePaystackSubaccountCodeAsync(db, appointment.TenantId, false, cancellationToken);
        if (subaccountCode is null) return Results.BadRequest("Connect Paystack payouts before accepting appointment payments.");
        var service = await db.BookableServices.FirstAsync(s => s.Id == appointment.ServiceId, cancellationToken);
        var paymentPolicy = NormalizePaymentPolicy(service.PaymentPolicy.ToString(), service.DepositCents);
        if (paymentPolicy == ServicePaymentPolicy.CollectAfterAppointment)
        {
            return Results.BadRequest("Use the virtual terminal to collect after-appointment payments.");
        }
        var intent = CreatePaymentIntent(appointment.TenantId, appointment.Id, GetPaymentAmountCents(service), AppointmentPaymentChannel.HostedCheckout, timeProvider.GetUtcNow());
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
        var service = await db.BookableServices.FirstAsync(s => s.Id == appointment.ServiceId, cancellationToken);
        if (NormalizePaymentPolicy(service.PaymentPolicy.ToString(), service.DepositCents) != ServicePaymentPolicy.CollectAfterAppointment)
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

        var intent = CreatePaymentIntent(appointment.TenantId, appointment.Id, service.PriceCents, AppointmentPaymentChannel.VirtualTerminal, timeProvider.GetUtcNow());
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
        var service = await db.BookableServices.IgnoreQueryFilters().FirstAsync(s => s.Id == appointment.ServiceId, cancellationToken);
        appointment.PaymentStatus = NormalizePaymentPolicy(service.PaymentPolicy.ToString(), service.DepositCents) == ServicePaymentPolicy.DepositBeforeBooking
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
        await SeedTenantAsync(db, tenantId, timeProvider.GetUtcNow(), cancellationToken);
        var subaccount = await db.PaystackSubaccounts.FirstOrDefaultAsync(cancellationToken);
        var appointments = (await db.Appointments.ToListAsync(cancellationToken)).OrderByDescending(a => a.StartAt).ToList();
        var clients = await db.Clients.ToListAsync(cancellationToken);
        var services = await db.BookableServices.ToListAsync(cancellationToken);
        var intents = (await db.AppointmentPaymentIntents.ToListAsync(cancellationToken)).OrderByDescending(i => i.CreatedAt).Take(25).ToList();
        return Results.Ok(new PaymentOverviewResponse(
            BuildPaymentStats(appointments, services),
            subaccount is null ? null : ToPaystackSubaccountDto(subaccount),
            intents.Select(intent => ToPaymentIntentDto(intent, appointments, clients, services))
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

    private static async Task<AppShellResponse> BuildShellAsync(MainDbContext db, CancellationToken cancellationToken)
    {
        var appointments = (await db.Appointments.ToListAsync(cancellationToken)).OrderBy(a => a.StartAt).ToList();
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
            client.Id,
            service.Id,
            appointment.StartAt,
            appointment.EndAt,
            client.Name,
            client.Phone,
            client.Email,
            service.Name,
            service.DurationMinutes,
            service.PriceCents,
            service.DepositCents,
            service.PaymentPolicy.ToString(),
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
        return new ServiceDto(service.Id, service.CategoryId, service.Name, service.Mode, service.DurationMinutes, service.PriceCents, service.DepositCents, service.PaymentPolicy.ToString(), service.Location, service.IsActive);
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

    private static PaymentStatsDto BuildPaymentStats(List<Appointment> appointments, List<BookableService> services)
    {
        var paid = appointments.Where(a => a.PaymentStatus is AppointmentPaymentStatus.Paid or AppointmentPaymentStatus.DepositPaid or AppointmentPaymentStatus.NotRequired).ToList();
        var pending = appointments.Where(a => a.PaymentStatus is AppointmentPaymentStatus.Pending or AppointmentPaymentStatus.Failed).ToList();
        return new PaymentStatsDto(
            appointments.Count,
            paid.Count,
            appointments.Count(a => a.PaymentStatus != AppointmentPaymentStatus.Paid && a.PaymentStatus != AppointmentPaymentStatus.DepositPaid && a.PaymentStatus != AppointmentPaymentStatus.NotRequired),
            appointments.Count(a => a.PaymentStatus == AppointmentPaymentStatus.Failed),
            pending.Sum(a => services.First(s => s.Id == a.ServiceId).DepositCents > 0 ? services.First(s => s.Id == a.ServiceId).DepositCents : services.First(s => s.Id == a.ServiceId).PriceCents),
            paid.Sum(a => services.First(s => s.Id == a.ServiceId).PriceCents)
        );
    }

    private static PaymentIntentDto ToPaymentIntentDto(AppointmentPaymentIntent intent, List<Appointment> appointments, List<Client> clients, List<BookableService> services)
    {
        var appointment = appointments.FirstOrDefault(a => a.Id == intent.AppointmentId);
        var client = appointment is null ? null : clients.FirstOrDefault(c => c.Id == appointment.ClientId);
        var service = appointment is null ? null : services.FirstOrDefault(s => s.Id == appointment.ServiceId);
        return new PaymentIntentDto(
            intent.Reference,
            intent.AmountCents,
            intent.Status,
            intent.AuthorizationUrl,
            intent.CreatedAt,
            intent.ConfirmedAt,
            appointment?.PublicReference ?? string.Empty,
            client?.Name ?? "Unknown client",
            service?.Name ?? "Unknown service"
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
        var tenantAppointments = await db.Appointments.IgnoreQueryFilters()
            .Where(a => a.TenantId == tenantId && a.Status != AppointmentStatus.Cancelled)
            .ToListAsync(cancellationToken);
        var existing = tenantAppointments.Where(a => a.StartAt < dayEnd && a.EndAt > dayStart).ToList();
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

    private static string NormalizePhone(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return string.Empty;
        var builder = new StringBuilder(phone.Length);
        foreach (var character in phone.Trim())
        {
            if (char.IsDigit(character) || character == '+')
            {
                builder.Append(character);
            }
        }
        return builder.ToString();
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
        var profile = new BusinessProfile { TenantId = tenantId, Name = "Sea Point studio", Slug = "sea-point-studio", LogoUrl = "/logos/sea-point-studio.svg" };
        var category = new ServiceCategory { TenantId = tenantId, Name = "Consultations", SortOrder = 1 };
        var group = new ServiceCategory { TenantId = tenantId, Name = "Group sessions", SortOrder = 2 };
        var staff = new StaffMember { TenantId = tenantId, Name = "Sarah", Email = "sarah@nerovasystems.com", Phone = "+27 82 000 0000" };
        var full = new BookableService { TenantId = tenantId, CategoryId = category.Id, Name = "Full consultation", Mode = "physical", DurationMinutes = 60, PriceCents = 45000, DepositCents = 15000, PaymentPolicy = ServicePaymentPolicy.DepositBeforeBooking, Location = "Sea Point studio", SortOrder = 1 };
        var express = new BookableService { TenantId = tenantId, CategoryId = category.Id, Name = "Express session", Mode = "physical", DurationMinutes = 30, PriceCents = 22000, DepositCents = 0, PaymentPolicy = ServicePaymentPolicy.NoPaymentRequired, Location = "Sea Point studio", SortOrder = 2 };
        var follow = new BookableService { TenantId = tenantId, CategoryId = category.Id, Name = "Follow-up visit", Mode = "virtual", DurationMinutes = 20, PriceCents = 15000, DepositCents = 0, PaymentPolicy = ServicePaymentPolicy.NoPaymentRequired, Location = "Manual link per booking", SortOrder = 3 };
        var workshop = new BookableService { TenantId = tenantId, CategoryId = group.Id, Name = "Group workshop", Mode = "physical", DurationMinutes = 90, PriceCents = 85000, DepositCents = 25000, PaymentPolicy = ServicePaymentPolicy.DepositBeforeBooking, Location = "Sea Point studio", SortOrder = 4 };
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
public sealed record AppointmentDto(string Id, string PublicReference, string ClientId, string ServiceId, DateTimeOffset StartAt, DateTimeOffset EndAt, string ClientName, string ClientPhone, string ClientEmail, string ServiceName, int DurationMinutes, int PriceCents, int DepositCents, string PaymentPolicy, string Status, string PaymentStatus, string Source, string Location, string AnswersJson, string ClientStatus, string? ClientAlert, string? ClientInternalNote);
public sealed record ServiceDto(string Id, string CategoryId, string Name, string Mode, int DurationMinutes, int PriceCents, int DepositCents, string PaymentPolicy, string Location, bool IsActive);
public sealed record ServiceCategoryDto(string Id, string Name);
public sealed record ClientDto(string Id, string Name, string Phone, string Email, string Status, string? Alert, string? InternalNote, int VisitCount, int LifetimeSpendCents, DateTimeOffset? LastVisitAt);
public sealed record AnalyticsDto(int Bookings, int RevenueCents, int ClientsServed, int AverageBookingValueCents, decimal NoShowRate);
public sealed record IntegrationConnectionDto(string Provider, string Capability, string Status, DateTimeOffset? LastSyncedAt);
public sealed record SlotDto(DateTimeOffset StartAt, DateTimeOffset EndAt);
public sealed record PublicBookingProfileResponse(string Name, string Slug, string TimeZone, string Address, string? LogoUrl, IEnumerable<ServiceDto> Services);
public sealed record PublicClientPrefillResponse(string Name, string Email);
public sealed record PublicBookingRequest(string ServiceId, DateTimeOffset StartAt, string Name, string Phone, string Email, Dictionary<string, string> Answers);
public sealed record PublicBookingCreatedResponse(string Reference, bool PaymentRequired, string? PaymentUrl);
public sealed record CreateServiceRequest(string Name, string CategoryName, string? Description, string Mode, int DurationMinutes, int PriceCents, int DepositCents, string? PaymentPolicy, int BufferBeforeMinutes, int BufferAfterMinutes, string Location);
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
