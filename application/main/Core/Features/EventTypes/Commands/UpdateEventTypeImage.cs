using System.Security.Cryptography;
using FluentValidation;
using JetBrains.Annotations;
using Main.Features.EventTypes.Domain;
using Main.Features.Permissions.Domain;
using Main.Features.Permissions.Pipeline;
using Main.Features.Scheduling.Shared;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;
using SharedKernel.Integrations.BlobStorage;
using SharedKernel.Telemetry;

namespace Main.Features.EventTypes.Commands;

[PublicAPI]
[RequirePermission(PermissionResource.EventType, PermissionAction.Update)]
public sealed record UpdateEventTypeImageCommand(Stream FileStream, string ContentType) : ICommand, IRequest<Result>
{
    public EventTypeId Id { get; init; } = null!;
}

public sealed class UpdateEventTypeImageValidator : AbstractValidator<UpdateEventTypeImageCommand>
{
    public UpdateEventTypeImageValidator()
    {
        RuleFor(command => command.ContentType)
            .Must(contentType => contentType is "image/jpeg" or "image/png" or "image/webp")
            .WithMessage("Image must be of type JPEG, PNG, or WebP.");
        RuleFor(command => command.FileStream.Length)
            .LessThanOrEqualTo(2 * 1024 * 1024)
            .WithName("FileStream")
            .WithMessage("Image must be smaller than 2 MB.");
    }
}

public sealed class UpdateEventTypeImageHandler(
    IEventTypeRepository eventTypeRepository,
    [FromKeyedServices("main-storage")] IBlobStorageClient blobStorageClient,
    IExecutionContext executionContext,
    ITelemetryEventsCollector events
) : IRequestHandler<UpdateEventTypeImageCommand, Result>
{
    private const string ContainerName = "service-images";

    public async Task<Result> Handle(UpdateEventTypeImageCommand command, CancellationToken cancellationToken)
    {
        if (!SchedulingAuthorization.CanManageSchedulingSetup(executionContext.UserInfo))
        {
            return Result.Forbidden(SchedulingAuthorization.ManageEventTypesForbiddenMessage);
        }

        var ownerUserId = executionContext.UserInfo.Id;
        if (ownerUserId is null)
        {
            return Result.Unauthorized("Authentication is required.");
        }

        var eventType = await eventTypeRepository.GetByIdAsync(command.Id, cancellationToken);
        if (eventType is null || eventType.OwnerUserId != ownerUserId)
        {
            return Result.NotFound($"Event type '{command.Id}' was not found.");
        }

        var fileHash = await GetFileHash(command.FileStream, cancellationToken);
        var fileExtension = command.ContentType.Split('/')[1];
        var blobName = $"{eventType.TenantId}/{eventType.Id}/{fileHash}.{fileExtension}";

        await blobStorageClient.UploadAsync(ContainerName, blobName, command.ContentType, command.FileStream, cancellationToken);

        eventType.SetImage($"/{ContainerName}/{blobName}");
        eventTypeRepository.Update(eventType);

        events.CollectEvent(new EventTypeImageUpdated(eventType.Id, command.ContentType, command.FileStream.Length));
        return Result.Success();
    }

    private static async Task<string> GetFileHash(Stream fileStream, CancellationToken cancellationToken)
    {
        using var sha1 = SHA1.Create();
        var hashBytes = await sha1.ComputeHashAsync(fileStream, cancellationToken);
        fileStream.Position = 0;
        // Cache-busting uniqueness per event type; 16 chars is plenty for a single image slot.
        return Convert.ToHexString(hashBytes)[..16].ToUpperInvariant();
    }
}

[PublicAPI]
[RequirePermission(PermissionResource.EventType, PermissionAction.Update)]
public sealed record RemoveEventTypeImageCommand(EventTypeId Id) : ICommand, IRequest<Result>;

public sealed class RemoveEventTypeImageHandler(
    IEventTypeRepository eventTypeRepository,
    IExecutionContext executionContext,
    ITelemetryEventsCollector events
) : IRequestHandler<RemoveEventTypeImageCommand, Result>
{
    public async Task<Result> Handle(RemoveEventTypeImageCommand command, CancellationToken cancellationToken)
    {
        if (!SchedulingAuthorization.CanManageSchedulingSetup(executionContext.UserInfo))
        {
            return Result.Forbidden(SchedulingAuthorization.ManageEventTypesForbiddenMessage);
        }

        var ownerUserId = executionContext.UserInfo.Id;
        if (ownerUserId is null)
        {
            return Result.Unauthorized("Authentication is required.");
        }

        var eventType = await eventTypeRepository.GetByIdAsync(command.Id, cancellationToken);
        if (eventType is null || eventType.OwnerUserId != ownerUserId)
        {
            return Result.NotFound($"Event type '{command.Id}' was not found.");
        }

        // The blob is intentionally left in place: the immutable hash-named URL may still be cached
        // by clients, and orphaned blobs are reaped by storage lifecycle policy, not request paths.
        eventType.SetImage(null);
        eventTypeRepository.Update(eventType);

        events.CollectEvent(new EventTypeImageRemoved(eventType.Id));
        return Result.Success();
    }
}
