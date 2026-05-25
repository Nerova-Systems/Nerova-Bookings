using Account.Features.Users.Domain;
using JetBrains.Annotations;
using Mapster;
using SharedKernel.Cqrs;
using SharedKernel.Domain;

namespace Account.Features.Users.Queries;

[PublicAPI]
public sealed record GetUserQuery : IRequest<Result<CurrentUserResponse>>;

[PublicAPI]
public sealed record CurrentUserResponse(
    UserId Id,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ModifiedAt,
    string Email,
    UserRole Role,
    string FirstName,
    string LastName,
    string Title,
    string? AvatarUrl,
    UserPreferencesResponse Preferences
);

public sealed class GetUserHandler(IUserRepository userRepository, IUserPreferencesRepository preferencesRepository)
    : IRequestHandler<GetUserQuery, Result<CurrentUserResponse>>
{
    public async Task<Result<CurrentUserResponse>> Handle(GetUserQuery query, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetLoggedInUserAsync(cancellationToken);
        var preferences = await preferencesRepository.GetByUserIdAsync(user.Id, cancellationToken);
        var response = user.Adapt<CurrentUserResponse>();
        return response with { Preferences = UserPreferencesResponse.FromAggregateOrDefault(preferences) };
    }
}
