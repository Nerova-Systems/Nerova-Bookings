using FluentValidation;
using JetBrains.Annotations;
using Main.Features.WhatsAppFlows.Domain;
using Main.Features.WhatsAppFlows.Infrastructure;
using Main.Features.WhatsAppFlows.Shared;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;

namespace Main.Features.WhatsAppFlows.Commands;

[PublicAPI]
public sealed record AddCustomQuestionCommand(
    string QuestionText,
    CustomQuestionType QuestionType,
    bool IsRequired,
    string[]? Choices
) : ICommand, IRequest<Result<CustomQuestionResponse>>;

public sealed class AddCustomQuestionValidator : AbstractValidator<AddCustomQuestionCommand>
{
    public AddCustomQuestionValidator()
    {
        RuleFor(c => c.QuestionText).NotEmpty().MaximumLength(200);
        RuleFor(c => c.Choices)
            .NotEmpty().When(c => c.QuestionType == CustomQuestionType.MultipleChoice)
            .WithMessage("Choices are required for MultipleChoice questions.");
    }
}

public sealed class AddCustomQuestionHandler(
    ITenantFlowConfigRepository repository,
    ITierService tierService,
    IExecutionContext executionContext
) : IRequestHandler<AddCustomQuestionCommand, Result<CustomQuestionResponse>>
{
    public async Task<Result<CustomQuestionResponse>> Handle(AddCustomQuestionCommand command, CancellationToken cancellationToken)
    {
        var tenantId = executionContext.TenantId;
        if (tenantId is null) return Result<CustomQuestionResponse>.Unauthorized("Authentication is required.");

        var config = await repository.GetByTenantIdAsync(tenantId, cancellationToken);
        if (config is null) return Result<CustomQuestionResponse>.NotFound("Tenant flow configuration has not been created yet.");

        var tier = await tierService.GetTierAsync(tenantId, cancellationToken);
        var limit = TierLimits.MaxCustomPreBookingQuestions(tier);
        if (limit != -1 && config.CustomPreBookingQuestions.Count >= limit)
        {
            return Result<CustomQuestionResponse>.BadRequest($"Tier {tier} allows at most {limit} custom question(s).");
        }

        var question = config.AddCustomQuestion(command.QuestionText, command.QuestionType, command.IsRequired, command.Choices);
        repository.Update(config);
        return new CustomQuestionResponse(question.Order, question.QuestionText, question.IsRequired, question.QuestionType, question.Choices);
    }
}
