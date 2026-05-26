using System.Text.Json;
using Account.Features.Attributes.Domain;
using Account.Features.AttributeSync.Domain;
using Account.Features.AuditLog.Domain;
using Account.Features.Memberships.Domain;
using Microsoft.AspNetCore.Http;
using SharedKernel.AuditLog;
using SharedKernel.Domain;
using SharedKernel.ExecutionContext;
using SharedKernel.Telemetry;
using Attribute = Account.Features.Attributes.Domain.Attribute;

namespace Account.Features.AttributeSync.Infrastructure;

/// <summary>
///     Evaluates all enabled <see cref="AttributeSyncRule" />s for an org and idempotently
///     reconciles the member's <see cref="AttributeAssignment" />s against the IdP claims.
///     <para>
///         Called by <c>SsoLoginCompletedAttributeSyncHandler</c> on every SSO login and by the
///         <c>ApplyAttributeSyncCommand</c> for admin-triggered manual syncs.
///     </para>
/// </summary>
public sealed class AttributeSyncService(
    IAttributeSyncRuleRepository ruleRepository,
    IAttributeRepository attributeRepository,
    IAttributeAssignmentRepository assignmentRepository,
    IAuditLogEmitter auditLogEmitter,
    IHttpContextAccessor httpContextAccessor,
    IExecutionContext executionContext,
    ITelemetryEventsCollector events,
    ILogger<AttributeSyncService> logger
)
{
    /// <summary>
    ///     Applies all enabled sync rules for <paramref name="orgTenantId" /> to the given member,
    ///     adding or removing attribute assignments to match the IdP <paramref name="claims" />.
    /// </summary>
    public async Task ApplyAsync(
        MembershipId membershipId,
        TenantId orgTenantId,
        SyncSource source,
        IReadOnlyDictionary<string, JsonElement> claims,
        CancellationToken cancellationToken)
    {
        var rules = await ruleRepository.GetEnabledByOrgUnfilteredAsync(orgTenantId, cancellationToken);
        if (rules.Count == 0) return;

        var httpContext = httpContextAccessor.HttpContext;
        var actorId = executionContext.UserInfo.Id;
        // Fall back to "system" when no authenticated HTTP user is present (e.g. background jobs
        // or service-test contexts). AuditLogEntry.Create rejects null/empty actor emails.
        var actorEmail = string.IsNullOrWhiteSpace(executionContext.UserInfo.Email)
            ? "system"
            : executionContext.UserInfo.Email;
        var ipAddress = httpContext?.Connection.RemoteIpAddress?.ToString();
        var userAgent = httpContext?.Request.Headers.UserAgent.ToString();

        foreach (var rule in rules)
        {
            try
            {
                await ApplyRuleAsync(rule, membershipId, orgTenantId, claims, actorId, actorEmail, ipAddress, userAgent, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "AttributeSync rule {RuleId} failed for membership {MembershipId} (source: {Source})",
                    rule.Id, membershipId, source
                );
                events.CollectEvent(new AttributeSyncFailed(rule.Id, membershipId, orgTenantId, ex.Message));
            }
        }
    }

    private async Task ApplyRuleAsync(
        AttributeSyncRule rule,
        MembershipId membershipId,
        TenantId orgTenantId,
        IReadOnlyDictionary<string, JsonElement> claims,
        UserId? actorId,
        string actorEmail,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken)
    {
        // Strip the [] suffix to get the actual claim type key.
        var claimKey = rule.ClaimPath.EndsWith("[]", StringComparison.Ordinal)
            ? rule.ClaimPath[..^2]
            : rule.ClaimPath;

        if (!claims.TryGetValue(claimKey, out var claimElement))
        {
            events.CollectEvent(new AttributeSyncSkipped(rule.Id, membershipId, orgTenantId, "claim_not_found"));
            return;
        }

        // Load the attribute — needed for option resolution and type checks.
        var attribute = await attributeRepository.GetByIdUnfilteredAsync(rule.AttributeId, cancellationToken);
        if (attribute is null)
        {
            events.CollectEvent(new AttributeSyncSkipped(rule.Id, membershipId, orgTenantId, "attribute_not_found"));
            return;
        }

        switch (rule.Mode)
        {
            case ClaimMappingMode.Direct:
                await ApplyDirectAsync(rule, attribute, membershipId, orgTenantId, claimElement,
                    actorId, actorEmail, ipAddress, userAgent, cancellationToken
                );
                break;

            case ClaimMappingMode.Lookup:
                await ApplyLookupAsync(rule, attribute, membershipId, orgTenantId, claimElement,
                    actorId, actorEmail, ipAddress, userAgent, cancellationToken
                );
                break;

            case ClaimMappingMode.Group:
                await ApplyGroupAsync(rule, attribute, membershipId, orgTenantId, claimElement,
                    actorId, actorEmail, ipAddress, userAgent, cancellationToken
                );
                break;
        }
    }

    // ─── Direct mode ──────────────────────────────────────────────────────────

    private async Task ApplyDirectAsync(
        AttributeSyncRule rule,
        Attribute attribute,
        MembershipId membershipId,
        TenantId orgTenantId,
        JsonElement claimElement,
        UserId? actorId,
        string actorEmail,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken)
    {
        var value = ExtractScalarString(claimElement);
        if (value is null)
        {
            events.CollectEvent(new AttributeSyncSkipped(rule.Id, membershipId, orgTenantId, "claim_not_scalar"));
            return;
        }

        var existing = await assignmentRepository.GetByMembershipAttributeOptionAsync(
            membershipId, attribute.Id, null, cancellationToken
        );

        if (existing is not null)
        {
            if (existing.Value == value) return; // Already in sync — no-op.
            existing.UpdateValue(value, null, null);
            assignmentRepository.Update(existing);
            await EmitAuditAsync(orgTenantId, actorId, actorEmail, AuditAction.Updated, existing.Id.ToString(), ipAddress, userAgent, cancellationToken);
        }
        else
        {
            var assignment = AttributeAssignment.Create(orgTenantId, membershipId, attribute.Id, null, value, null);
            await assignmentRepository.AddAsync(assignment, cancellationToken);
            await EmitAuditAsync(orgTenantId, actorId, actorEmail, AuditAction.Created, assignment.Id.ToString(), ipAddress, userAgent, cancellationToken);
        }

        events.CollectEvent(new AttributeSyncApplied(rule.Id, membershipId, orgTenantId));
    }

    // ─── Lookup mode ──────────────────────────────────────────────────────────

    private async Task ApplyLookupAsync(
        AttributeSyncRule rule,
        Attribute attribute,
        MembershipId membershipId,
        TenantId orgTenantId,
        JsonElement claimElement,
        UserId? actorId,
        string actorEmail,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken)
    {
        var value = ExtractScalarString(claimElement);
        if (value is null)
        {
            events.CollectEvent(new AttributeSyncSkipped(rule.Id, membershipId, orgTenantId, "claim_not_scalar"));
            return;
        }

        var option = ResolveOrCreateOption(rule, attribute, value, membershipId, orgTenantId);
        if (option is null) return; // AutoCreateOptions=false and no match → already emitted Skipped

        // For SingleSelect: clear existing selection first, then assign the desired option.
        var existingAll = await assignmentRepository.GetByMembershipAttributeOptionAsync(
            membershipId, attribute.Id, null, cancellationToken
        );
        if (existingAll is not null && existingAll.AttributeOptionId != option.Id)
        {
            assignmentRepository.Remove(existingAll);
            await EmitAuditAsync(orgTenantId, actorId, actorEmail, AuditAction.Deleted, existingAll.Id.ToString(), ipAddress, userAgent, cancellationToken);
        }

        var desired = await assignmentRepository.GetByMembershipAttributeOptionAsync(
            membershipId, attribute.Id, option.Id, cancellationToken
        );
        if (desired is null)
        {
            var assignment = AttributeAssignment.Create(orgTenantId, membershipId, attribute.Id, option.Id, null, null);
            await assignmentRepository.AddAsync(assignment, cancellationToken);
            await EmitAuditAsync(orgTenantId, actorId, actorEmail, AuditAction.Created, assignment.Id.ToString(), ipAddress, userAgent, cancellationToken);
            events.CollectEvent(new AttributeSyncApplied(rule.Id, membershipId, orgTenantId));
        }
    }

    // ─── Group mode ───────────────────────────────────────────────────────────

    private async Task ApplyGroupAsync(
        AttributeSyncRule rule,
        Attribute attribute,
        MembershipId membershipId,
        TenantId orgTenantId,
        JsonElement claimElement,
        UserId? actorId,
        string actorEmail,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken)
    {
        var claimValues = ExtractStringArray(claimElement);
        if (claimValues.Length == 0)
        {
            events.CollectEvent(new AttributeSyncSkipped(rule.Id, membershipId, orgTenantId, "claim_empty_array"));
            return;
        }

        // Resolve desired option IDs (auto-creating when enabled).
        var desiredOptionIds = new HashSet<AttributeOptionId>();
        foreach (var val in claimValues)
        {
            var option = ResolveOrCreateOption(rule, attribute, val, membershipId, orgTenantId);
            if (option is not null)
            {
                desiredOptionIds.Add(option.Id);
            }
        }

        // Load all existing assignments for (membership, attribute).
        var existing = await GetExistingOptionAssignmentsAsync(membershipId, attribute.Id, cancellationToken);
        var existingOptionIds = existing.Select(a => a.AttributeOptionId!).ToHashSet();

        // Remove stale assignments (options no longer in the desired set).
        foreach (var stale in existing.Where(a => !desiredOptionIds.Contains(a.AttributeOptionId!)))
        {
            assignmentRepository.Remove(stale);
            await EmitAuditAsync(orgTenantId, actorId, actorEmail, AuditAction.Deleted, stale.Id.ToString(), ipAddress, userAgent, cancellationToken);
            events.CollectEvent(new AttributeSyncApplied(rule.Id, membershipId, orgTenantId));
        }

        // Add missing assignments.
        foreach (var optionId in desiredOptionIds.Where(id => !existingOptionIds.Contains(id)))
        {
            var assignment = AttributeAssignment.Create(orgTenantId, membershipId, attribute.Id, optionId, null, null);
            await assignmentRepository.AddAsync(assignment, cancellationToken);
            await EmitAuditAsync(orgTenantId, actorId, actorEmail, AuditAction.Created, assignment.Id.ToString(), ipAddress, userAgent, cancellationToken);
            events.CollectEvent(new AttributeSyncApplied(rule.Id, membershipId, orgTenantId));
        }
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private AttributeOption? ResolveOrCreateOption(
        AttributeSyncRule rule,
        Attribute attribute,
        string value,
        MembershipId membershipId,
        TenantId orgTenantId)
    {
        var slug = Attribute.GenerateSlug(value);
        var existing = attribute.Options.FirstOrDefault(o => o.Slug == slug || string.Equals(o.Value, value, StringComparison.OrdinalIgnoreCase));
        if (existing is not null) return existing;

        if (!rule.AutoCreateOptions)
        {
            events.CollectEvent(new AttributeSyncSkipped(rule.Id, membershipId, orgTenantId, "option_not_found"));
            return null;
        }

        // Auto-create a new option on the attribute.
        // The option is added to the in-memory aggregate; it will be flushed to DB by the
        // outer unit-of-work at the end of the request (EF change tracker handles this).
        var newOption = attribute.AddOption(value);
        attributeRepository.Update(attribute);
        return newOption;
    }

    private async Task<List<AttributeAssignment>> GetExistingOptionAssignmentsAsync(
        MembershipId membershipId,
        AttributeId attributeId,
        CancellationToken cancellationToken)
    {
        var all = await assignmentRepository.GetByMembershipAsync(membershipId, cancellationToken);
        return all.Where(a => a.AttributeId == attributeId && a.AttributeOptionId is not null).ToList();
    }

    private Task EmitAuditAsync(
        TenantId orgTenantId,
        UserId? actorId,
        string actorEmail,
        AuditAction action,
        string resourceId,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken)
    {
        return auditLogEmitter.EmitAsync(new AuditLogEvent(
                orgTenantId,
                actorId,
                actorEmail,
                nameof(AuditResource.Attribute),
                action.ToString(),
                resourceId,
                IpAddress: ipAddress,
                UserAgent: userAgent
            ), cancellationToken
        );
    }

    private static string? ExtractScalarString(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.ToString(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null
        };
    }

    private static string[] ExtractStringArray(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            return element.EnumerateArray()
                .Select(ExtractScalarString)
                .Where(v => v is not null)
                .Select(v => v!)
                .ToArray();
        }

        // Scalar claim treated as a single-element group.
        var scalar = ExtractScalarString(element);
        return scalar is not null ? [scalar] : [];
    }
}
