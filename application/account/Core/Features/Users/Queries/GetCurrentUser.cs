using Account.Features.ExternalAuthentication.Domain;
using Account.Features.Users.Domain;
using JetBrains.Annotations;
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
    bool EmailConfirmed,
    string? FirstName,
    string? LastName,
    string? Title,
    string? AvatarUrl,
    ExternalProviderType[] LinkedExternalProviders
);

public sealed class GetUserHandler(IUserRepository userRepository)
    : IRequestHandler<GetUserQuery, Result<CurrentUserResponse>>
{
    public async Task<Result<CurrentUserResponse>> Handle(GetUserQuery query, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetLoggedInUserAsync(cancellationToken);
        return new CurrentUserResponse(
            user.Id,
            user.CreatedAt,
            user.ModifiedAt,
            user.Email,
            user.Role,
            user.EmailConfirmed,
            user.FirstName,
            user.LastName,
            user.Title,
            user.Avatar.Url,
            user.ExternalIdentities.Select(identity => identity.Provider).ToArray()
        );
    }
}
