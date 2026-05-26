using FluentAssertions;
using Main.Features.Webhooks.Infrastructure;
using Xunit;

namespace Main.Tests.Webhooks;

public sealed class WebhookBackoffTests
{
    [Theory]
    [InlineData(1, 1)] //  1m after attempt 1
    [InlineData(2, 5)] //  5m after attempt 2
    [InlineData(3, 30)] // 30m after attempt 3
    [InlineData(4, 60)] //  1h after attempt 4
    [InlineData(5, 360)] //  6h after attempt 5
    public void GetDelayAfterAttempt_ShouldReturnExpectedDelay(int attemptCount, int expectedMinutes)
    {
        var delay = WebhookBackoff.GetDelayAfterAttempt(attemptCount);

        delay.Should().NotBeNull();
        delay.Value.Should().Be(TimeSpan.FromMinutes(expectedMinutes));
    }

    [Fact]
    public void GetDelayAfterAttempt_AtMaxAttempts_ShouldReturnNullToSignalDeadLetter()
    {
        // Six attempts have been made → no further retry; caller must dead-letter.
        WebhookBackoff.GetDelayAfterAttempt(WebhookBackoff.MaxAttempts).Should().BeNull();
    }

    [Fact]
    public void GetDelayAfterAttempt_WhenAttemptCountBelowOne_ShouldThrow()
    {
        var act = () => WebhookBackoff.GetDelayAfterAttempt(0);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void MaxAttempts_ShouldBeSix()
    {
        WebhookBackoff.MaxAttempts.Should().Be(6);
    }
}
