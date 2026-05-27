using System.Text.Json;
using FluentAssertions;
using Main.Features.WhatsAppFlows.Domain;
using Main.Features.WhatsAppFlows.Infrastructure;
using Microsoft.Extensions.Configuration;
using SharedKernel.Domain;
using Xunit;

namespace Main.Tests.WhatsAppFlows;

public sealed class FlowTemplateEngineTests
{
    private static readonly TenantId TenantId = new(7);

    private static FlowTemplateEngine BuildEngine()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["WhatsApp:EndpointBaseUrl"] = "https://api.test.example.com"
                }
            )
            .Build();
        return new FlowTemplateEngine(config);
    }

    [Fact]
    public void GenerateFlowJson_ProducesValidJsonWithExpectedTopLevelFields()
    {
        var engine = BuildEngine();
        var config = TenantFlowConfig.Create(TenantId, BusinessVertical.HairSalon);

        var json = engine.GenerateFlowJson(config, "Acme Salon");

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        root.GetProperty("data_api_version").GetString().Should().Be("3.0");
        root.GetProperty("version").GetString().Should().Be("7.0");
        root.GetProperty("screens").ValueKind.Should().Be(JsonValueKind.Array);
        root.GetProperty("routing_model").ValueKind.Should().Be(JsonValueKind.Object);
    }

    [Fact]
    public void GenerateFlowJson_SpecificStaff_IncludesSelectStaffScreen()
    {
        var engine = BuildEngine();
        // HairSalon defaults give StaffAssignment.SpecificStaff
        var config = TenantFlowConfig.Create(TenantId, BusinessVertical.HairSalon);

        var json = engine.GenerateFlowJson(config, "Acme Salon");

        json.Should().Contain("SELECT_STAFF");
    }

    [Fact]
    public void GenerateFlowJson_AutoAssign_OmitsSelectStaffScreen()
    {
        var engine = BuildEngine();
        // PersonalTrainer defaults give StaffAssignment.AutoAssign
        var config = TenantFlowConfig.Create(TenantId, BusinessVertical.PersonalTrainer);

        var json = engine.GenerateFlowJson(config, "Acme Training");

        json.Should().NotContain("SELECT_STAFF");
    }

    [Fact]
    public void GenerateFlowJson_NoCustomQuestions_OmitsCustomQuestionsScreen()
    {
        var engine = BuildEngine();
        var config = TenantFlowConfig.Create(TenantId, BusinessVertical.Other);

        var json = engine.GenerateFlowJson(config, "Acme");

        json.Should().NotContain("CUSTOM_QUESTIONS");
    }

    [Fact]
    public void GenerateFlowJson_WithCustomQuestions_IncludesCustomQuestionsScreen()
    {
        var engine = BuildEngine();
        var config = TenantFlowConfig.Create(TenantId, BusinessVertical.Other);
        config.AddCustomQuestion("Any allergies?", CustomQuestionType.Text, true, null);

        var json = engine.GenerateFlowJson(config, "Acme");

        json.Should().Contain("CUSTOM_QUESTIONS");
        json.Should().Contain("Any allergies?");
    }

    [Fact]
    public void GenerateFlowJson_EmbedsEndpointBaseUrl()
    {
        var engine = BuildEngine();
        var config = TenantFlowConfig.Create(TenantId, BusinessVertical.HairSalon);

        var json = engine.GenerateFlowJson(config, "Acme Salon");

        json.Should().Contain("https://api.test.example.com/api/whatsapp/flows/v1");
    }
}
