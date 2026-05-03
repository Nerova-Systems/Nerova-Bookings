using FluentAssertions;
using SharedKernel.Integrations.Email;
using Xunit;

namespace SharedKernel.Tests.Emails;

public sealed class EmailMessageTests
{
    [Fact]
    public void EmailMessage_WhenConstructedWithHeaders_ShouldExposeThemForPassthrough()
    {
        // Arrange & Act
        var headers = new Dictionary<string, string>
        {
            ["X-PlatformPlatform-Tenant"] = "tenant-123",
            ["List-Unsubscribe"] = "<mailto:unsubscribe@example.com>"
        };
        var message = new EmailMessage("user@example.com", "Hello", "<p>Hi</p>", "Hi", headers);

        // Assert
        message.Recipient.Should().Be("user@example.com");
        message.Subject.Should().Be("Hello");
        message.HtmlBody.Should().Be("<p>Hi</p>");
        message.PlainTextBody.Should().Be("Hi");
        message.Headers.Should().BeEquivalentTo(headers);
    }

    [Fact]
    public void EmailMessage_WhenHeadersOmitted_ShouldDefaultToNull()
    {
        // Arrange & Act
        var message = new EmailMessage("user@example.com", "Hello", "<p>Hi</p>", "Hi");

        // Assert
        message.Headers.Should().BeNull();
    }
}
