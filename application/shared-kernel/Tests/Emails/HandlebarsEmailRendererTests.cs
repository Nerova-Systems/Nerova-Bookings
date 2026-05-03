using FluentAssertions;
using HandlebarsDotNet;
using SharedKernel.Emails;
using Xunit;

namespace SharedKernel.Tests.Emails;

public sealed class HandlebarsEmailRendererTests
{
    private readonly IHandlebars _handlebars = CreateHandlebars();

    private static IHandlebars CreateHandlebars()
    {
        var handlebars = Handlebars.Create();
        EmailHelpers.Register(handlebars);
        return handlebars;
    }

    private HandlebarsEmailRenderer CreateRenderer(string html, string plainText)
    {
        return new HandlebarsEmailRenderer(_handlebars, new InMemoryEmailTemplateLoader(html, plainText));
    }

    [Fact]
    public void RenderEmail_WhenTemplateHasVariables_ShouldSubstituteFromModel()
    {
        // Arrange
        var html = "<html><head><title>Hello {{name}}</title></head><body><p>Welcome, {{name}}!</p></body></html>";
        var plainText = "Welcome, {{name}}!";
        var renderer = CreateRenderer(html, plainText);
        var template = new TestTemplate("welcome", "en-US", new { name = "Alice" });

        // Act
        var result = renderer.RenderEmail(template);

        // Assert
        result.Subject.Should().Be("Hello Alice");
        result.HtmlBody.Should().Contain("Welcome, Alice!");
        result.PlainTextBody.Should().Be("Welcome, Alice!");
    }

    [Fact]
    public void RenderEmail_WhenTemplateHasLoop_ShouldRenderEachItem()
    {
        // Arrange
        var html = "<html><head><title>Order</title></head><body><ul>{{#each items}}<li>{{this}}</li>{{/each}}</ul></body></html>";
        var plainText = "{{#each items}}- {{this}}\n{{/each}}";
        var renderer = CreateRenderer(html, plainText);
        var template = new TestTemplate("order", "en-US", new { items = new[] { "Apple", "Banana", "Cherry" } });

        // Act
        var result = renderer.RenderEmail(template);

        // Assert
        result.HtmlBody.Should().Contain("<li>Apple</li>").And.Contain("<li>Banana</li>").And.Contain("<li>Cherry</li>");
        result.PlainTextBody.Should().Contain("- Apple").And.Contain("- Banana").And.Contain("- Cherry");
    }

    [Fact]
    public void RenderEmail_WhenTemplateHasConditional_ShouldRenderBranch()
    {
        // Arrange
        var html = "<html><head><title>Receipt</title></head><body>{{#if paid}}Thank you!{{else}}Please pay.{{/if}}</body></html>";
        var plainText = "{{#if paid}}Thank you!{{else}}Please pay.{{/if}}";
        var renderer = CreateRenderer(html, plainText);

        // Act
        var paidResult = renderer.RenderEmail(new TestTemplate("receipt", "en-US", new { paid = true }));
        var unpaidResult = renderer.RenderEmail(new TestTemplate("receipt", "en-US", new { paid = false }));

        // Assert
        paidResult.HtmlBody.Should().Contain("Thank you!").And.NotContain("Please pay.");
        paidResult.PlainTextBody.Should().Be("Thank you!");
        unpaidResult.HtmlBody.Should().Contain("Please pay.").And.NotContain("Thank you!");
        unpaidResult.PlainTextBody.Should().Be("Please pay.");
    }

    [Fact]
    public void RenderEmail_WhenFormatCurrencyHelperUsedInEnUs_ShouldFormatWithDollar()
    {
        // Arrange
        var html = "<html><head><title>Invoice</title></head><body>{{formatCurrency amount currency=\"USD\" locale=\"en-US\"}}</body></html>";
        var renderer = CreateRenderer(html, "{{formatCurrency amount currency=\"USD\" locale=\"en-US\"}}");
        var template = new TestTemplate("invoice", "en-US", new { amount = 1234.56m });

        // Act
        var result = renderer.RenderEmail(template);

        // Assert
        result.PlainTextBody.Should().Be("$1,234.56");
    }

    [Fact]
    public void RenderEmail_WhenFormatCurrencyHelperUsedInDaDk_ShouldFormatWithKronerAndDanishGrouping()
    {
        // Arrange
        var html = "<html><head><title>Faktura</title></head><body>{{formatCurrency amount currency=\"DKK\" locale=\"da-DK\"}}</body></html>";
        var renderer = CreateRenderer(html, "{{formatCurrency amount currency=\"DKK\" locale=\"da-DK\"}}");
        var template = new TestTemplate("faktura", "da-DK", new { amount = 1234.56m });

        // Act
        var result = renderer.RenderEmail(template);

        // Assert
        result.PlainTextBody.Should().Contain("1.234,56").And.Contain("kr");
    }

    [Fact]
    public void RenderEmail_WhenFormatDateHelperUsed_ShouldFormatPerLocale()
    {
        // Arrange
        var date = new DateTimeOffset(2026, 5, 3, 10, 0, 0, TimeSpan.Zero);
        var html = "<html><head><title>Date</title></head><body>{{formatDate date locale=\"en-US\"}}</body></html>";
        var plainText = "{{formatDate date locale=\"da-DK\"}}";
        var renderer = CreateRenderer(html, plainText);
        var template = new TestTemplate("date", "en-US", new { date });

        // Act
        var result = renderer.RenderEmail(template);

        // Assert
        result.HtmlBody.Should().Contain("Sunday, May 3, 2026");
        result.PlainTextBody.Should().Contain("3. maj 2026");
    }

    [Fact]
    public void RenderEmail_WhenFormatDateHelperWithCustomFormat_ShouldHonorFormat()
    {
        // Arrange
        var date = new DateTimeOffset(2026, 5, 3, 10, 0, 0, TimeSpan.Zero);
        var html = "<html><head><title>Date</title></head><body>{{formatDate date locale=\"en-US\" format=\"yyyy-MM-dd\"}}</body></html>";
        var renderer = CreateRenderer(html, "{{formatDate date locale=\"en-US\" format=\"yyyy-MM-dd\"}}");
        var template = new TestTemplate("date", "en-US", new { date });

        // Act
        var result = renderer.RenderEmail(template);

        // Assert
        result.PlainTextBody.Should().Be("2026-05-03");
    }

    [Theory]
    [InlineData(0, "items")]
    [InlineData(1, "item")]
    [InlineData(2, "items")]
    [InlineData(7, "items")]
    public void RenderEmail_WhenPluralizeHelperUsed_ShouldPickCorrectForm(int count, string expected)
    {
        // Arrange
        var html = "<html><head><title>Cart</title></head><body>{{pluralize count \"item\"}}</body></html>";
        var renderer = CreateRenderer(html, "{{pluralize count \"item\"}}");
        var template = new TestTemplate("cart", "en-US", new { count });

        // Act
        var result = renderer.RenderEmail(template);

        // Assert
        result.PlainTextBody.Should().Be(expected);
    }

    [Fact]
    public void RenderEmail_WhenPluralizeHelperWithExplicitPlural_ShouldUseProvidedPlural()
    {
        // Arrange
        var html = "<html><head><title>Cart</title></head><body>{{pluralize count \"child\" \"children\"}}</body></html>";
        var renderer = CreateRenderer(html, "{{pluralize count \"child\" \"children\"}}");

        // Act
        var single = renderer.RenderEmail(new TestTemplate("cart", "en-US", new { count = 1 }));
        var many = renderer.RenderEmail(new TestTemplate("cart", "en-US", new { count = 3 }));

        // Assert
        single.PlainTextBody.Should().Be("child");
        many.PlainTextBody.Should().Be("children");
    }

    [Fact]
    public void RenderEmail_WhenHtmlAndPlainTextShareModel_ShouldProduceConsistentValues()
    {
        // Arrange: a finance-style template using all three helpers in both formats.
        var html = "<html><head><title>Receipt for {{customer}}</title></head><body><p>{{customer}}, you owe {{formatCurrency amount currency=\"USD\" locale=\"en-US\"}} due on {{formatDate dueDate locale=\"en-US\" format=\"yyyy-MM-dd\"}} ({{pluralize lineItemCount \"item\"}}).</p></body></html>";
        var plainText = "{{customer}}, you owe {{formatCurrency amount currency=\"USD\" locale=\"en-US\"}} due on {{formatDate dueDate locale=\"en-US\" format=\"yyyy-MM-dd\"}} ({{pluralize lineItemCount \"item\"}}).";
        var renderer = CreateRenderer(html, plainText);
        var template = new TestTemplate("receipt", "en-US", new
            {
                customer = "Bob",
                amount = 99.95m,
                dueDate = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero),
                lineItemCount = 3
            }
        );

        // Act
        var result = renderer.RenderEmail(template);

        // Assert: both rendered bodies must contain the same dynamic values so plaintext stays in lockstep with HTML.
        const string expectedFragment = "Bob, you owe $99.95 due on 2026-06-01 (items).";
        result.HtmlBody.Should().Contain(expectedFragment);
        result.PlainTextBody.Should().Be(expectedFragment);
    }

    [Fact]
    public void RenderEmail_WhenTitleMissing_ShouldThrow()
    {
        // Arrange
        var renderer = CreateRenderer("<html><body>No title here</body></html>", "No title");

        // Act
        var act = () => renderer.RenderEmail(new TestTemplate("broken", "en-US", new { }));

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*broken*missing*<title>*");
    }

    [Fact]
    public void RenderEmail_WhenTemplateMissing_ShouldThrowFileNotFound()
    {
        // Arrange: real loader pointed at a temp directory with no template files.
        using var tempDir = new TemporaryDirectory();
        var handlebars = CreateHandlebars();
        var loader = new FileSystemEmailTemplateLoader(tempDir.Path, false);
        var renderer = new HandlebarsEmailRenderer(handlebars, loader);

        // Act
        var act = () => renderer.RenderEmail(new TestTemplate("missing", "en-US", new { }));

        // Assert
        act.Should().Throw<FileNotFoundException>().WithMessage("*missing.en-US.html*");
    }

    [Fact]
    public void RenderEmail_WhenSubjectHasInternalWhitespace_ShouldCollapseToSingleSpaces()
    {
        // Arrange: title with newlines/tabs inside should produce a clean subject.
        var html = "<html><head><title>\n  Hello\t  {{name}}  \n</title></head><body></body></html>";
        var renderer = CreateRenderer(html, "");
        var template = new TestTemplate("hello", "en-US", new { name = "Alice" });

        // Act
        var result = renderer.RenderEmail(template);

        // Assert
        result.Subject.Should().Be("Hello Alice");
    }

    private sealed record TestTemplate(string Name, string Locale, object Model) : EmailTemplateBase(Name, Locale, Model);

    private sealed class InMemoryEmailTemplateLoader(string html, string plainText) : IEmailTemplateLoader
    {
        public string LoadHtml(string name, string locale)
        {
            return html;
        }

        public string LoadPlainText(string name, string locale)
        {
            return plainText;
        }
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Directory.CreateDirectory(Path);
        }

        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"EmailRendererTests-{Guid.NewGuid():N}");

        public void Dispose()
        {
            if (Directory.Exists(Path)) Directory.Delete(Path, true);
        }
    }
}
