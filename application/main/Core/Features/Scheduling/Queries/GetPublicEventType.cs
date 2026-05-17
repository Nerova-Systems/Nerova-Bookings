using JetBrains.Annotations;
using Main.Features.Scheduling.Shared;
using SharedKernel.Cqrs;

namespace Main.Features.Scheduling.Queries;

[PublicAPI]
public sealed record GetPublicEventTypeQuery(string Handle, string Slug, string? PrivateLink = null) : IRequest<Result<PublicEventTypeResponse>>
{
    public string Handle { get; } = Handle.Trim().ToLowerInvariant();

    public string Slug { get; } = Slug.Trim().ToLowerInvariant();

    public string? PrivateLink { get; } = string.IsNullOrWhiteSpace(PrivateLink) ? null : PrivateLink.Trim();
}

public sealed class GetPublicEventTypeHandler(PublicSchedulingResolver publicSchedulingResolver)
    : IRequestHandler<GetPublicEventTypeQuery, Result<PublicEventTypeResponse>>
{
    public async Task<Result<PublicEventTypeResponse>> Handle(GetPublicEventTypeQuery query, CancellationToken cancellationToken)
    {
        var contextResult = await publicSchedulingResolver.ResolveAsync(query.Handle, query.Slug, query.PrivateLink, cancellationToken);
        if (!contextResult.IsSuccess)
        {
            return Result<PublicEventTypeResponse>.From(contextResult);
        }

        var context = contextResult.Value!;
        return PublicEventTypeResponse.From(context.Profile, context.EventType);
    }
}
