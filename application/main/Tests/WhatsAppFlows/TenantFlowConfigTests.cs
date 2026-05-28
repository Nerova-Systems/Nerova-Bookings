using FluentAssertions;
using Main.Features.WhatsAppFlows.Domain;
using SharedKernel.Domain;
using Xunit;

namespace Main.Tests.WhatsAppFlows;

public sealed class TenantFlowConfigTests
{
    private static readonly TenantId TenantId = new(42);

    [Fact]
    public void Create_HairSalon_AppliesVerticalDefaults()
    {
        var config = TenantFlowConfig.Create(TenantId, BusinessVertical.HairSalon);

        config.TenantId.Should().Be(TenantId);
        config.BusinessVertical.Should().Be(BusinessVertical.HairSalon);
        config.HasMultipleServices.Should().BeTrue();
        config.StaffAssignment.Should().Be(StaffAssignment.SpecificStaff);
        config.DefaultSessionMinutes.Should().Be(45);
        config.BookingWindowDays.Should().Be(30);
        config.PaymentTiming.Should().Be(PaymentTiming.AfterSession);
        config.ConfigVersionHash.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Create_PersonalTrainer_HasNoMultipleServicesAndAutoAssign()
    {
        var config = TenantFlowConfig.Create(TenantId, BusinessVertical.PersonalTrainer);

        config.HasMultipleServices.Should().BeFalse();
        config.StaffAssignment.Should().Be(StaffAssignment.AutoAssign);
        config.DefaultSessionMinutes.Should().Be(60);
    }

    [Fact]
    public void AddCustomQuestion_AppendsAndIncrementsOrder()
    {
        var config = TenantFlowConfig.Create(TenantId, BusinessVertical.Other);

        var q1 = config.AddCustomQuestion("Allergies?", CustomQuestionType.Text, true, null);
        var q2 = config.AddCustomQuestion("Pick one", CustomQuestionType.MultipleChoice, false, ["A", "B"]);

        q1.Order.Should().Be(1);
        q2.Order.Should().Be(2);
        config.CustomPreBookingQuestions.Should().HaveCount(2);
    }

    [Fact]
    public void RemoveCustomQuestion_ByOrder_Removes()
    {
        var config = TenantFlowConfig.Create(TenantId, BusinessVertical.Other);
        config.AddCustomQuestion("Q1", CustomQuestionType.Text, false, null);
        config.AddCustomQuestion("Q2", CustomQuestionType.Text, false, null);

        config.RemoveCustomQuestion(1).Should().BeTrue();
        config.CustomPreBookingQuestions.Should().HaveCount(1);
        config.CustomPreBookingQuestions[0].QuestionText.Should().Be("Q2");
    }

    [Fact]
    public void RemoveCustomQuestion_Unknown_ReturnsFalse()
    {
        var config = TenantFlowConfig.Create(TenantId, BusinessVertical.Other);

        config.RemoveCustomQuestion(99).Should().BeFalse();
    }

    [Fact]
    public void UpdateBusinessProfile_RecomputesVersionHash()
    {
        var config = TenantFlowConfig.Create(TenantId, BusinessVertical.Other);
        var initialHash = config.ConfigVersionHash;

        config.UpdateBusinessProfile(
            BusinessVertical.Clinic,
            StaffAssignment.SpecificStaff,
            PaymentTiming.Deposit,
            depositAmountCents: 5000,
            bookingWindowDays: 14,
            defaultSessionMinutes: 30,
            hasMultipleServices: true,
            allowSameDayBookings: false,
            confirmationMessageTemplate: "Hi {name}",
            cancellationContact: "+15551234567"
        );

        config.ConfigVersionHash.Should().NotBe(initialHash);
        config.DepositAmountCents.Should().Be(5000);
    }

    [Fact]
    public void UpdateBusinessProfile_NonDepositTiming_ClearsDepositAmount()
    {
        var config = TenantFlowConfig.Create(TenantId, BusinessVertical.Other);

        config.UpdateBusinessProfile(
            BusinessVertical.Other,
            StaffAssignment.AutoAssign,
            PaymentTiming.AfterSession,
            depositAmountCents: 12345, // should be ignored / cleared
            bookingWindowDays: 30,
            defaultSessionMinutes: 60,
            hasMultipleServices: false,
            allowSameDayBookings: true,
            confirmationMessageTemplate: "x",
            cancellationContact: "y"
        );

        config.DepositAmountCents.Should().BeNull();
    }
}
