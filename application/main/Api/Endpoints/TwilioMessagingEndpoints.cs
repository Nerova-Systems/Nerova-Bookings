using Main.Database;
using Main.Features.Appointments;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Domain;
using SharedKernel.Endpoints;
using SharedKernel.ExecutionContext;

namespace Main.Api.Endpoints;

public sealed class TwilioMessagingEndpoints : IEndpoints
{
    private const string RoutesPrefix = "/api/main/messaging/whatsapp";

    public void MapEndpoints(IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup(RoutesPrefix).WithTags("Messaging").RequireAuthorization();
        group.MapGet("/status", GetStatus);
        group.MapPost("/provision-subaccount", ProvisionSubaccount);
        group.MapPost("/claim-number", ClaimNumber);
    }

    private static async Task<IResult> GetStatus(MainDbContext db, IExecutionContext executionContext, CancellationToken cancellationToken)
    {
        var tenantId = RequireTenant(executionContext);
        var profile = await db.TenantMessagingProfiles.AsNoTracking().FirstOrDefaultAsync(profile => profile.AppSlug == "whatsapp", cancellationToken);
        var number = profile is null
            ? null
            : await db.TenantPhoneNumberAssignments.AsNoTracking()
                .Where(number => number.MessagingProfileId == profile.Id && number.AssignmentStatus == "Assigned")
                .FirstOrDefaultAsync(cancellationToken);
        var templateCount = profile is null
            ? 0
            : await db.TenantMessageTemplates.AsNoTracking().CountAsync(template => template.MessagingProfileId == profile.Id, cancellationToken);

        return Results.Ok(BuildStatus(tenantId, profile, number, templateCount));
    }

    private static async Task<IResult> ProvisionSubaccount(MainDbContext db, IExecutionContext executionContext, ITwilioMessagingProvisioningClient provisioningClient, TimeProvider timeProvider, CancellationToken cancellationToken)
    {
        var tenantId = RequireTenant(executionContext);
        var profile = await db.TenantMessagingProfiles.AsTracking().FirstOrDefaultAsync(profile => profile.AppSlug == "whatsapp", cancellationToken);
        if (profile is not null && !string.IsNullOrWhiteSpace(profile.TwilioSubaccountSid))
        {
            return Results.Ok(BuildStatus(tenantId, profile, await GetAssignedNumberAsync(db, profile.Id, cancellationToken), await CountTemplatesAsync(db, profile.Id, cancellationToken)));
        }

        var business = await db.BusinessProfiles.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();
        var result = await provisioningClient.CreateSubaccountAsync($"{business?.Name ?? "Nerova"} WhatsApp tenant {tenantId.Value}", cancellationToken);
        if (profile is null)
        {
            profile = new TenantMessagingProfile
            {
                TenantId = tenantId,
                OwnerType = MessagingOwnerType.Tenant,
                OwnerId = tenantId.ToString(),
                DisplayName = business?.Name ?? string.Empty,
                Address = business?.Address ?? string.Empty,
                CreatedAt = now
            };
            db.TenantMessagingProfiles.Add(profile);
        }

        profile.TwilioSubaccountSid = result.AccountSid;
        profile.TwilioSubaccountStatus = result.Status;
        profile.ProvisioningStatus = "SubaccountProvisioned";
        profile.WhatsAppApprovalStatus = "NotSubmitted";
        profile.CountryCode = "ZA";
        profile.LastSyncedAt = now;
        EnsureLifecycleTemplates(db, profile, now);
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(BuildStatus(tenantId, profile, null, LifecycleTemplates.Length));
    }

    private static async Task<IResult> ClaimNumber(MainDbContext db, IExecutionContext executionContext, ITwilioMessagingProvisioningClient provisioningClient, TimeProvider timeProvider, CancellationToken cancellationToken)
    {
        var tenantId = RequireTenant(executionContext);
        var profile = await db.TenantMessagingProfiles.AsTracking().FirstOrDefaultAsync(profile => profile.AppSlug == "whatsapp", cancellationToken);
        if (profile is null || string.IsNullOrWhiteSpace(profile.TwilioSubaccountSid))
        {
            return Results.BadRequest("Provision a Twilio tenant subaccount before claiming a number.");
        }

        var existing = await GetAssignedNumberAsync(db, profile.Id, cancellationToken);
        if (existing is not null)
        {
            return Results.Ok(BuildStatus(tenantId, profile, existing, await CountTemplatesAsync(db, profile.Id, cancellationToken)));
        }

        var now = timeProvider.GetUtcNow();
        var result = await provisioningClient.ClaimSouthAfricanNumberAsync(profile.TwilioSubaccountSid, cancellationToken);
        var assignment = new TenantPhoneNumberAssignment
        {
            TenantId = tenantId,
            MessagingProfileId = profile.Id,
            PhoneNumber = result.PhoneNumber,
            TwilioPhoneNumberSid = result.PhoneNumberSid,
            CountryCode = "ZA",
            SmsCapable = result.SmsCapable,
            WhatsAppCapable = result.WhatsAppCapable,
            WebhookUrl = result.WebhookUrl,
            AssignmentStatus = "Assigned",
            CreatedAt = now,
            LastSyncedAt = now
        };
        db.TenantPhoneNumberAssignments.Add(assignment);
        profile.ProvisioningStatus = "NumberAssigned";
        profile.LastSyncedAt = now;
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(BuildStatus(tenantId, profile, assignment, await CountTemplatesAsync(db, profile.Id, cancellationToken)));
    }

    private static TenantMessagingStatusResponse BuildStatus(TenantId tenantId, TenantMessagingProfile? profile, TenantPhoneNumberAssignment? number, int templateCount)
    {
        return new TenantMessagingStatusResponse(
            "whatsapp",
            "WhatsApp",
            "Twilio",
            "ZA",
            profile?.ProvisioningStatus ?? "NotProvisioned",
            profile?.WhatsAppApprovalStatus ?? "NotSubmitted",
            profile?.TwilioSubaccountSid,
            number?.PhoneNumber,
            templateCount,
            profile?.WhatsAppApprovalStatus == "Approved" && number is not null,
            [
                new MessagingReadinessItem("subaccount", "Twilio tenant subaccount", !string.IsNullOrWhiteSpace(profile?.TwilioSubaccountSid)),
                new MessagingReadinessItem("number", "South African sender number", number is not null),
                new MessagingReadinessItem("templates", "Lifecycle templates", templateCount >= LifecycleTemplates.Length),
                new MessagingReadinessItem("whatsapp_approval", "WhatsApp Business approval", profile?.WhatsAppApprovalStatus == "Approved")
            ]
        );
    }

    private static async Task<TenantPhoneNumberAssignment?> GetAssignedNumberAsync(MainDbContext db, string profileId, CancellationToken cancellationToken)
    {
        return await db.TenantPhoneNumberAssignments.AsTracking()
            .Where(number => number.MessagingProfileId == profileId && number.AssignmentStatus == "Assigned")
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static Task<int> CountTemplatesAsync(MainDbContext db, string profileId, CancellationToken cancellationToken)
    {
        return db.TenantMessageTemplates.CountAsync(template => template.MessagingProfileId == profileId, cancellationToken);
    }

    private static void EnsureLifecycleTemplates(MainDbContext db, TenantMessagingProfile profile, DateTimeOffset now)
    {
        foreach (var template in LifecycleTemplates)
        {
            db.TenantMessageTemplates.Add(new TenantMessageTemplate
            {
                TenantId = profile.TenantId,
                MessagingProfileId = profile.Id,
                TemplateKey = template.Key,
                DisplayName = template.Name,
                Category = template.Category,
                Language = "en",
                ApprovalStatus = "NotSubmitted",
                CreatedAt = now,
                LastSyncedAt = now
            });
        }
    }

    private static TenantId RequireTenant(IExecutionContext executionContext)
    {
        return executionContext.TenantId ?? throw new InvalidOperationException("Tenant context is required.");
    }

    private static readonly (string Key, string Name, string Category)[] LifecycleTemplates =
    [
        ("booking_confirmation", "Booking confirmation", "Utility"),
        ("booking_reminder", "Booking reminder", "Utility"),
        ("reschedule_approval", "Reschedule approval", "Utility"),
        ("booking_cancellation", "Booking cancellation", "Utility"),
        ("payment_link", "Payment link", "Utility"),
        ("no_show", "No-show follow-up", "Utility"),
        ("completion_follow_up", "Completion follow-up", "Utility"),
        ("review_request", "Review request", "Marketing"),
        ("waitlist", "Waitlist update", "Utility"),
        ("loyalty_marketing", "Loyalty and marketing", "Marketing")
    ];
}

public sealed record TenantMessagingStatusResponse(string AppSlug, string AppName, string Provider, string CountryCode, string Status, string WhatsAppApprovalStatus, string? TwilioSubaccountSid, string? PhoneNumber, int TemplateCount, bool CanSendMessages, IEnumerable<MessagingReadinessItem> Readiness);
public sealed record MessagingReadinessItem(string Key, string Label, bool IsReady);
