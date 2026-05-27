using Account.Features.WhatsApp.Commands;
using Account.Features.WhatsApp.Queries;
using JetBrains.Annotations;
using SharedKernel.ApiResults;
using SharedKernel.Cqrs;
using SharedKernel.Domain;
using SharedKernel.Endpoints;
using SharedKernel.ExecutionContext;
using SharedKernel.OpenApi;

namespace Account.Api.Endpoints;

public sealed class WhatsAppEndpoints : IEndpoints
{
    private const string RoutesPrefix = "/api/whatsapp";

    public void MapEndpoints(IEndpointRouteBuilder routes)
    {
        var group = routes
            .MapGroup(RoutesPrefix)
            .WithTags("WhatsApp")
            .WithGroupName(OpenApiDocumentNames.Account)
            .RequireAuthorization()
            .ProducesValidationProblem();

        group.MapPost("/link-waba", async Task<ApiResult> (
            [PublicAPI] LinkWabaAccountRequest request,
            IMediator mediator,
            IExecutionContext executionContext
        ) =>
        {
            var tenantId = executionContext.TenantId!;
            return await mediator.Send(new LinkWabaAccountCommand(tenantId, request.WabaId, request.PhoneNumberId, request.DisplayPhoneNumber));
        });

        group.MapPost("/generate-key-pair", async Task<ApiResult<GenerateWabaKeyPairResponse>> (
            IMediator mediator,
            IExecutionContext executionContext
        ) =>
        {
            var tenantId = executionContext.TenantId!;
            return await mediator.Send(new GenerateWabaKeyPairCommand(tenantId));
        }).Produces<GenerateWabaKeyPairResponse>();

        group.MapPost("/connect-paystack", async Task<ApiResult<ConnectPaystackSubaccountResponse>> (
            [PublicAPI] ConnectPaystackRequest request,
            IMediator mediator,
            IExecutionContext executionContext
        ) =>
        {
            var tenantId = executionContext.TenantId!;
            return await mediator.Send(new ConnectPaystackSubaccountCommand(tenantId, request.BusinessName, request.BankCode, request.AccountNumber, request.PercentageFee));
        }).Produces<ConnectPaystackSubaccountResponse>();

        group.MapGet("/onboarding-status", async Task<ApiResult<WabaOnboardingStatusResponse?>> (
            IMediator mediator,
            IExecutionContext executionContext
        ) =>
        {
            var tenantId = executionContext.TenantId!;
            var result = await mediator.Send(new GetWabaOnboardingStatusQuery(tenantId));
            return result is null
                ? Result<WabaOnboardingStatusResponse?>.NotFound("WhatsApp configuration not found for this tenant.")
                : Result<WabaOnboardingStatusResponse?>.Success(result);
        }).Produces<WabaOnboardingStatusResponse?>();

        group.MapPost("/display-name", async Task<ApiResult> (
            [PublicAPI] RequestWabaDisplayNameChangeRequest request,
            IMediator mediator
        ) => await mediator.Send(new RequestWabaDisplayNameChangeCommand(request.RequestedDisplayName)));

        group.MapGet("/display-name", async Task<ApiResult<WabaDisplayNameStatusResponse?>> (
            IMediator mediator,
            IExecutionContext executionContext
        ) =>
        {
            var tenantId = executionContext.TenantId!;
            var result = await mediator.Send(new GetWabaDisplayNameStatusQuery(tenantId));
            return result is null
                ? Result<WabaDisplayNameStatusResponse?>.NotFound("WhatsApp configuration not found for this tenant.")
                : Result<WabaDisplayNameStatusResponse?>.Success(result);
        }).Produces<WabaDisplayNameStatusResponse?>();
    }
}

[PublicAPI]
public sealed record LinkWabaAccountRequest(string WabaId, string PhoneNumberId, string DisplayPhoneNumber);

[PublicAPI]
public sealed record ConnectPaystackRequest(string BusinessName, string BankCode, string AccountNumber, decimal PercentageFee);

[PublicAPI]
public sealed record RequestWabaDisplayNameChangeRequest(string RequestedDisplayName);
