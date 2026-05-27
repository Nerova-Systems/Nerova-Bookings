using Account.Features.WhatsApp.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;

namespace Account.Tests.WhatsApp;

/// <summary>
///     Subclass of <see cref="AccountWebApplicationFactory" /> that replaces
///     <see cref="IPaystackSubaccountService" /> with an NSubstitute mock so WhatsApp endpoint
///     tests can control Paystack responses without live HTTP calls.
/// </summary>
public sealed class WhatsAppWebApplicationFactory : AccountWebApplicationFactory
{
    /// <summary>
    ///     The NSubstitute double for <see cref="IPaystackSubaccountService" />.
    ///     Configure return values in each test before calling the endpoint.
    /// </summary>
    public IPaystackSubaccountService MockSubaccountService { get; } =
        Substitute.For<IPaystackSubaccountService>();

    protected override void ConfigureAdditionalTestServices(IServiceCollection services)
    {
        services.RemoveAll<IPaystackSubaccountService>();
        services.AddScoped<IPaystackSubaccountService>(_ => MockSubaccountService);
    }
}
