using Account.Database;
using FluentAssertions;
using NJsonSchema;
using SharedKernel.Tests;
using Xunit;

namespace Account.Tests.Users;

public sealed class GetCurrentUserTests(AccountWebApplicationFactory factory) : EndpointBaseTest<AccountDbContext>(factory), IClassFixture<AccountWebApplicationFactory>
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
                    'tenantId': {'type': 'string', 'pattern': '^[0-9]+$'},
                    'createdAt': {'type': 'string', 'format': 'date-time'},
                    'modifiedAt': {'type': ['null', 'string'], 'format': 'date-time'},
                    'email': {'type': 'string', 'maxLength': 100},
                    'firstName': {'type': ['null', 'string'], 'maxLength': 30},
                    'lastName': {'type': ['null', 'string'], 'maxLength': 30},
                    'title': {'type': ['null', 'string'], 'maxLength': 50},
                    'role': {'type': 'string', 'minLength': 1, 'maxLength': 20},
                    'emailConfirmed': {'type': 'boolean'},
                    'avatarUrl': {'type': ['null', 'string'], 'maxLength': 100},
                    'preferences': {
                        'type': 'object',
                        'properties': {
                            'timeFormat': {'type': 'string', 'enum': ['TwelveHour', 'TwentyFourHour']},
                            'weekStart': {'type': 'string'},
                            'language': {'type': 'string'},
                            'timeZone': {'type': 'string'}
                        },
                        'required': ['timeFormat', 'weekStart', 'language', 'timeZone'],
                        'additionalProperties': false
                    }
                },
                'required': ['id', 'tenantId', 'createdAt', 'modifiedAt', 'email', 'role', 'preferences'],
                'additionalProperties': false
            }
            """
        );

        var responseBody = await response.Content.ReadAsStringAsync();
        schema.Validate(responseBody).Should().BeEmpty();
    }
}
