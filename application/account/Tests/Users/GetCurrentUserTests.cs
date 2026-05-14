using Account.Database;
using Account.Features.ExternalAuthentication.Domain;
using FluentAssertions;
using NJsonSchema;
using SharedKernel.Tests;
using SharedKernel.Tests.Persistence;
using Xunit;

namespace Account.Tests.Users;

public sealed class GetCurrentUserTests : EndpointBaseTest<AccountDbContext>
{
    [Fact]
    public async Task GetLoggedInUser_WhenUserExists_ShouldReturnUserWithValidContract()
    {
        // Act
        var response = await AuthenticatedOwnerHttpClient.GetAsync("/api/account/users/me");

        // Assert
        response.ShouldBeSuccessfulGetRequest();

        var schema = await JsonSchema.FromJsonAsync(
            """
            {
                'type': 'object',
                'properties': {
                    'id': {'type': 'string', 'pattern': '^usr_[A-Z0-9]{26}$'},
                    'createdAt': {'type': 'string', 'format': 'date-time'},
                    'modifiedAt': {'type': ['null', 'string'], 'format': 'date-time'},
                    'email': {'type': 'string', 'maxLength': 100},
                    'firstName': {'type': ['null', 'string'], 'maxLength': 30},
                    'lastName': {'type': ['null', 'string'], 'maxLength': 30},
                    'title': {'type': ['null', 'string'], 'maxLength': 50},
                    'role': {'type': 'string', 'minLength': 1, 'maxLength': 20},
                    'emailConfirmed': {'type': 'boolean'},
                    'avatarUrl': {'type': ['null', 'string'], 'maxLength': 100},
                    'linkedExternalProviders': {
                        'type': 'array',
                        'items': {'type': 'string', 'minLength': 1, 'maxLength': 20}
                    }
                },
                'required': ['id', 'createdAt', 'modifiedAt', 'email', 'role', 'linkedExternalProviders'],
                'additionalProperties': false
            }
            """
        );

        var responseBody = await response.Content.ReadAsStringAsync();
        schema.Validate(responseBody).Should().BeEmpty();
    }

    [Fact]
    public async Task GetLoggedInUser_WhenExternalIdentitiesExist_ShouldReturnLinkedProviders()
    {
        // Arrange
        Connection.Update("users", "id", DatabaseSeeder.Tenant1Owner.Id.ToString(), [
                ("external_identities", $$"""
                                          [
                                            {"Provider":"{{nameof(ExternalProviderType.Google)}}","ProviderUserId":"google-id"},
                                            {"Provider":"{{nameof(ExternalProviderType.Facebook)}}","ProviderUserId":"facebook-id"}
                                          ]
                                          """)
            ]
        );

        // Act
        var response = await AuthenticatedOwnerHttpClient.GetAsync("/api/account/users/me");

        // Assert
        response.ShouldBeSuccessfulGetRequest();
        var responseBody = await response.Content.ReadAsStringAsync();
        responseBody.Should().Contain(nameof(ExternalProviderType.Google));
        responseBody.Should().Contain(nameof(ExternalProviderType.Facebook));
    }
}
