using Main.Integrations.Meta;
using FluentAssertions;
using Xunit;

namespace Main.Tests.Integrations.Meta;

public sealed class MockMetaGraphClientTests
{
    private readonly MockMetaGraphClient _client = new();

    [Fact]
    public async Task ExchangeCodeForTokenAsync_ShouldReturnDeterministicToken()
    {
        var token = await _client.ExchangeCodeForTokenAsync("any-code", CancellationToken.None);

        token.Should().Be(MockMetaGraphClient.MockAccessToken);
    }

    [Fact]
    public async Task GetWabaAsync_ShouldEchoWabaIdWithMockBusinessName()
    {
        var waba = await _client.GetWabaAsync("waba-123", MockMetaGraphClient.MockAccessToken, CancellationToken.None);

        waba.Should().NotBeNull();
        waba!.Id.Should().Be("waba-123");
        waba.Name.Should().Be(MockMetaGraphClient.MockBusinessName);
    }

    [Fact]
    public async Task GetPhoneNumbersAsync_ShouldReturnSingleDeterministicPhoneNumber()
    {
        var phoneNumbers = await _client.GetPhoneNumbersAsync("waba-123", MockMetaGraphClient.MockAccessToken, CancellationToken.None);

        phoneNumbers.Should().NotBeNull();
        phoneNumbers!.Should().ContainSingle();
        phoneNumbers[0].DisplayPhoneNumber.Should().Be(MockMetaGraphClient.MockDisplayPhoneNumber);
        phoneNumbers[0].VerifiedName.Should().Be(MockMetaGraphClient.MockVerifiedName);
    }
}
