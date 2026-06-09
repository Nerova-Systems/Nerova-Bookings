using FluentValidation;
using JetBrains.Annotations;
using Main.Features.Clients.Domain;
using Mapster;
using SharedKernel.Cqrs;
using SharedKernel.Persistence;

namespace Main.Features.Clients.Queries;

[PublicAPI]
public sealed record GetClientsQuery(
    string? Search = null,
    DateTimeOffset? StartDate = null,
    DateTimeOffset? EndDate = null,
    SortableClientProperties OrderBy = SortableClientProperties.Name,
    SortOrder SortOrder = SortOrder.Ascending,
    int? PageOffset = null,
    int PageSize = 25
) : IRequest<Result<ClientsResponse>>;

[PublicAPI]
public sealed record ClientsResponse(int TotalCount, int PageSize, int TotalPages, int CurrentPageOffset, ClientDetails[] Clients);

[PublicAPI]
public sealed record ClientDetails(
    ClientId Id,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ModifiedAt,
    DateTimeOffset? LastVisitAt,
    string FirstName,
    string LastName,
    string? Email,
    string? PhoneNumber,
    bool NeedsAttention,
    string? AvatarUrl
);

public sealed class GetClientsQueryValidator : AbstractValidator<GetClientsQuery>
{
    public GetClientsQueryValidator()
    {
        RuleFor(x => x.Search).MaximumLength(100).WithMessage("Search must be no longer than 100 characters.");
        RuleFor(x => x.PageSize).InclusiveBetween(0, 1000).WithMessage("Page size must be between 0 and 1000.");
        RuleFor(x => x.PageOffset).GreaterThanOrEqualTo(0).WithMessage("Page offset must be greater than or equal to 0.");
    }
}

public sealed class GetClientsHandler(IClientRepository clientRepository)
    : IRequestHandler<GetClientsQuery, Result<ClientsResponse>>
{
    public async Task<Result<ClientsResponse>> Handle(GetClientsQuery query, CancellationToken cancellationToken)
    {
        var (clients, count, totalPages) = await clientRepository.Search(
            query.Search,
            query.StartDate,
            query.EndDate,
            query.OrderBy,
            query.SortOrder,
            query.PageOffset,
            query.PageSize,
            cancellationToken
        );

        if (query.PageOffset.HasValue && totalPages > 0 && query.PageOffset.Value >= totalPages)
        {
            return Result<ClientsResponse>.BadRequest($"The page offset {query.PageOffset.Value} is greater than the total number of pages.");
        }

        var clientResponses = clients.Adapt<ClientDetails[]>();
        return new ClientsResponse(count, query.PageSize, totalPages, query.PageOffset ?? 0, clientResponses);
    }
}
