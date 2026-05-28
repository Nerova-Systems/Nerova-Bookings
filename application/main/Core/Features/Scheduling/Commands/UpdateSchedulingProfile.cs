using FluentValidation;
using JetBrains.Annotations;
using Main.Features.Permissions.Domain;
using Main.Features.Permissions.Pipeline;
using Main.Features.Scheduling.Domain;
using Main.Features.Scheduling.Shared;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;

namespace Main.Features.Scheduling.Commands;

[PublicAPI]
[RequirePermission(PermissionResource.EventType, PermissionAction.Update)]
public sealed record UpdateSchedulingProfileCommand(string Handle, string DisplayName, string? AvatarUrl) : ICommand, IRequest<Result<SchedulingProfileResponse>>;

public sealed class UpdateSchedulingProfileValidator : AbstractValidator<UpdateSchedulingProfileCommand>
{
    public UpdateSchedulingProfileValidator()
    {
        RuleFor(command => command.Handle)
            .NotEmpty()
            .MaximumLength(80)
            .Matches("^[a-zA-Z0-9][a-zA-Z0-9_-]*[a-zA-Z0-9]$|^[a-zA-Z0-9]$")
            .WithMessage("Handle must contain letters, numbers, underscores, or hyphens.");
        RuleFor(command => command.DisplayName).NotEmpty().MaximumLength(160);
        RuleFor(command => command.AvatarUrl).MaximumLength(500);
    }
}

public sealed class UpdateSchedulingProfileHandler(ISchedulingProfileRepository schedulingProfileRepository, IExecutionContext executionContext)
    : IRequestHandler<UpdateSchedulingProfileCommand, Result<SchedulingProfileResponse>>
{
    public async Task<Result<SchedulingProfileResponse>> Handle(UpdateSchedulingProfileCommand command, CancellationToken cancellationToken)
    {
        var tenantId = executionContext.TenantId;
        var ownerUserId = executionContext.UserInfo.Id;
        if (tenantId is null || ownerUserId is null)
        {
            return Result<SchedulingProfileResponse>.Unauthorized("Authentication is required.");
        }

        var handle = SchedulingProfileHandle.Normalize(command.Handle);
        if (SchedulingProfileHandle.IsReserved(handle))
        {
            return Result<SchedulingProfileResponse>.BadRequest($"Scheduling handle '{handle}' is reserved.");
        }

        if (await schedulingProfileRepository.HandleExistsAsync(handle, ownerUserId, cancellationToken))
        {
            return Result<SchedulingProfileResponse>.BadRequest($"Scheduling handle '{handle}' is already taken.");
        }

        var profile = await schedulingProfileRepository.GetForOwnerAsync(ownerUserId, executionContext.ActiveTeamId, cancellationToken);
        if (profile is null)
        {
            profile = SchedulingProfile.Create(tenantId, ownerUserId, handle, command.DisplayName, command.AvatarUrl, executionContext.ActiveTeamId);
            await schedulingProfileRepository.AddAsync(profile, cancellationToken);
        }
        else
        {
            profile.Update(handle, command.DisplayName, command.AvatarUrl);
            schedulingProfileRepository.Update(profile);
        }

        return SchedulingProfileResponse.From(profile);
    }
}
