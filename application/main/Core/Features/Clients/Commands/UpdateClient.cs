using FluentValidation;
using JetBrains.Annotations;
using Main.Features.Clients.Domain;
using SharedKernel.Cqrs;
using SharedKernel.Telemetry;

namespace Main.Features.Clients.Commands;

[PublicAPI]
public sealed record UpdateClientCommand(ClientId Id, string FirstName, string LastName, string? Email, string? PhoneNumber)
    : ICommand, IRequest<Result>
{
    public string FirstName { get; } = FirstName.Trim();

    public string LastName { get; } = LastName.Trim();
}

public sealed class UpdateClientValidator : AbstractValidator<UpdateClientCommand>
{
    public UpdateClientValidator()
    {
        RuleFor(x => x.FirstName).Length(1, 30).WithMessage("First name must be between 1 and 30 characters.");
        RuleFor(x => x.LastName).Length(1, 30).WithMessage("Last name must be between 1 and 30 characters.");
        RuleFor(x => x.Email)
            .EmailAddress()
            .MaximumLength(100)
            .When(x => !string.IsNullOrWhiteSpace(x.Email))
            .WithMessage("Email must be a valid email address no longer than 100 characters.");
        RuleFor(x => x.PhoneNumber)
            .MaximumLength(30)
            .Matches(@"^[+0-9 ()-]+$")
            .When(x => !string.IsNullOrWhiteSpace(x.PhoneNumber))
            .WithMessage("Phone number must be a valid phone number no longer than 30 characters.");
    }
}

public sealed class UpdateClientHandler(IClientRepository clientRepository, ITelemetryEventsCollector events)
    : IRequestHandler<UpdateClientCommand, Result>
{
    public async Task<Result> Handle(UpdateClientCommand command, CancellationToken cancellationToken)
    {
        var client = await clientRepository.GetByIdAsync(command.Id, cancellationToken);
        if (client is null) return Result.NotFound($"Client with id '{command.Id}' not found.");

        client.Update(command.FirstName, command.LastName, command.Email, command.PhoneNumber);
        clientRepository.Update(client);

        events.CollectEvent(new ClientUpdated(client.Id));

        return Result.Success();
    }
}
