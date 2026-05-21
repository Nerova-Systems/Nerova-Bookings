using Account.Features.Permissions.Domain;
using Account.Features.Permissions.Pipeline;
using Account.Features.Smtp.Infrastructure;
using FluentValidation;
using JetBrains.Annotations;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;
using System.Net;
using System.Net.Mail;
using FeatureFlagDefinitions = SharedKernel.FeatureFlags.FeatureFlags;

namespace Account.Features.Smtp.Commands;

/// <summary>
///     Sends a test email using the provided SMTP credentials to verify connectivity
///     before persisting the configuration.
///     Requires <c>Smtp.Manage</c> permission at organization scope.
/// </summary>
[PublicAPI]
[RequirePermission(PermissionResource.Smtp, PermissionAction.Manage, PermissionScope.Organization)]
public sealed record TestOrgSmtpCommand : ICommand, IRequest<Result<TestOrgSmtpResult>>
{
    public required string Host { get; init; }
    public required int Port { get; init; }
    public required bool UseSsl { get; init; }
    public required string Username { get; init; }
    public required string Password { get; init; }
    public required string FromEmail { get; init; }
    public string? FromName { get; init; }

    /// <summary>Email address that will receive the test message.</summary>
    public required string RecipientEmail { get; init; }
}

[PublicAPI]
public sealed record TestOrgSmtpResult(bool Success, string? ErrorMessage);

public sealed class TestOrgSmtpValidator : AbstractValidator<TestOrgSmtpCommand>
{
    public TestOrgSmtpValidator()
    {
        RuleFor(x => x.Host)
            .NotEmpty()
            .MaximumLength(253)
            .WithMessage("Host must be between 1 and 253 characters.");

        RuleFor(x => x.Port)
            .InclusiveBetween(1, 65535)
            .WithMessage("Port must be between 1 and 65535.");

        RuleFor(x => x.Username)
            .NotEmpty()
            .MaximumLength(256)
            .WithMessage("Username must be between 1 and 256 characters.");

        RuleFor(x => x.Password)
            .NotEmpty()
            .WithMessage("Password is required.");

        RuleFor(x => x.FromEmail)
            .NotEmpty()
            .EmailAddress()
            .WithMessage("A valid From email address is required.");

        RuleFor(x => x.RecipientEmail)
            .NotEmpty()
            .EmailAddress()
            .WithMessage("A valid recipient email address is required.");
    }
}

public sealed class TestOrgSmtpHandler(IExecutionContext executionContext) : IRequestHandler<TestOrgSmtpCommand, Result<TestOrgSmtpResult>>
{
    public async Task<Result<TestOrgSmtpResult>> Handle(TestOrgSmtpCommand command, CancellationToken cancellationToken)
    {
        if (!executionContext.UserInfo.IsFeatureFlagEnabled(FeatureFlagDefinitions.CapCustomSmtp.Key))
            return Result<TestOrgSmtpResult>.Forbidden("The custom SMTP feature is not enabled for this organization.");

        try
        {
#pragma warning disable SYSLIB0006 // SmtpClient is deprecated but no alternative ships with .NET BCL; revisit when project adopts MailKit.
            using var smtpClient = new SmtpClient(command.Host, command.Port)
            {
                EnableSsl = command.UseSsl,
                Credentials = new NetworkCredential(command.Username, command.Password),
                DeliveryMethod = SmtpDeliveryMethod.Network
            };
#pragma warning restore SYSLIB0006

            using var mailMessage = new MailMessage
            {
                From = new MailAddress(command.FromEmail, command.FromName ?? string.Empty),
                Subject = "Nerova Bookings — SMTP configuration test",
                Body = "This is an automated test message from Nerova Bookings to verify your custom SMTP configuration.",
                IsBodyHtml = false
            };
            mailMessage.To.Add(command.RecipientEmail);

            await smtpClient.SendMailAsync(mailMessage, cancellationToken);
            return new TestOrgSmtpResult(Success: true, ErrorMessage: null);
        }
        catch (Exception ex)
        {
            return new TestOrgSmtpResult(Success: false, ErrorMessage: ex.Message);
        }
    }
}
