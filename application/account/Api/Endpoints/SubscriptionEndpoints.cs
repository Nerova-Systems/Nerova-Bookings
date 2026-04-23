using Account.Features.Subscriptions.Commands;
using Account.Features.Subscriptions.Queries;
using SharedKernel.ApiResults;
using SharedKernel.Endpoints;

namespace Account.Api.Endpoints;

public sealed class SubscriptionEndpoints : IEndpoints
{
    private const string RoutesPrefix = "/api/account/subscriptions";

    public void MapEndpoints(IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup(RoutesPrefix).WithTags("Subscriptions").RequireAuthorization().ProducesValidationProblem();

        group.MapGet("/pricing-catalog", async Task<ApiResult<PricingCatalogResponse>> ([AsParameters] GetPricingCatalogQuery query, IMediator mediator)
            => await mediator.Send(query)
        ).Produces<PricingCatalogResponse>();

        group.MapGet("/current", async Task<ApiResult<SubscriptionResponse>> ([AsParameters] GetCurrentSubscriptionQuery query, IMediator mediator)
            => await mediator.Send(query)
        ).Produces<SubscriptionResponse>();

        group.MapGet("/subscribe-preview", async Task<ApiResult<SubscribePreviewResponse>> ([AsParameters] GetSubscribePreviewQuery query, IMediator mediator)
            => await mediator.Send(query)
        ).Produces<SubscribePreviewResponse>();

        group.MapGet("/upgrade-preview", async Task<ApiResult<UpgradePreviewResponse>> ([AsParameters] GetUpgradePreviewQuery query, IMediator mediator)
            => await mediator.Send(query)
        ).Produces<UpgradePreviewResponse>();

        group.MapGet("/update-card-url", async Task<ApiResult<UpdateCardUrlResponse>> ([AsParameters] GetUpdateCardUrlQuery query, IMediator mediator)
            => await mediator.Send(query)
        ).Produces<UpdateCardUrlResponse>();

        group.MapPost("/initiate", async Task<ApiResult<InitiateSubscriptionResponse>> (InitiateSubscriptionCommand command, IMediator mediator)
            => await mediator.Send(command)
        ).Produces<InitiateSubscriptionResponse>();

        group.MapPost("/upgrade", async Task<ApiResult> (UpgradeSubscriptionCommand command, IMediator mediator)
            => await mediator.Send(command)
        );

        group.MapPost("/schedule-downgrade", async Task<ApiResult> (ScheduleDowngradeCommand command, IMediator mediator)
            => await mediator.Send(command)
        );

        group.MapPost("/cancel-scheduled-downgrade", async Task<ApiResult> (IMediator mediator)
            => await mediator.Send(new CancelScheduledDowngradeCommand())
        );

        group.MapPost("/cancel", async Task<ApiResult> (CancelSubscriptionCommand command, IMediator mediator)
            => await mediator.Send(command)
        );

        group.MapPost("/reactivate", async Task<ApiResult<ReactivateSubscriptionResponse>> (IMediator mediator)
            => await mediator.Send(new ReactivateSubscriptionCommand())
        ).Produces<ReactivateSubscriptionResponse>();

        group.MapPost("/retry-charge", async Task<ApiResult> (IMediator mediator)
            => await mediator.Send(new RetryFailedChargeCommand())
        );

        // PayFast ITN webhook — no auth, receives form-encoded POST from PayFast servers
        group.MapPost("/payfast/itn", async Task<ApiResult> (IFormCollection form, IMediator mediator)
            => await mediator.Send(new HandlePayFastItnCommand(form.ToDictionary(k => k.Key, v => v.Value.ToString())))
        ).AllowAnonymous();
    }
}
