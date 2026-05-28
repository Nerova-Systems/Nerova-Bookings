using Account.Integrations.Paystack;
using JetBrains.Annotations;
using Microsoft.Extensions.Caching.Memory;
using SharedKernel.Cqrs;

namespace Account.Features.Payments.Queries;

[PublicAPI]
public sealed record GetPaystackBanksQuery(string Country = "ZA") : IRequest<Result<GetPaystackBanksResponse>>;

[PublicAPI]
public sealed record GetPaystackBanksResponse(PaystackBankItem[] Banks);

[PublicAPI]
public sealed record PaystackBankItem(string Code, string Name);

public sealed class GetPaystackBanksHandler(PaystackClientFactory paystackClientFactory, IMemoryCache memoryCache)
    : IRequestHandler<GetPaystackBanksQuery, Result<GetPaystackBanksResponse>>
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(24);

    public async Task<Result<GetPaystackBanksResponse>> Handle(GetPaystackBanksQuery query, CancellationToken cancellationToken)
    {
        var cacheKey = $"paystack-banks-{query.Country.ToUpperInvariant()}";
        if (memoryCache.TryGetValue(cacheKey, out GetPaystackBanksResponse? cached) && cached is not null) return cached;

        var banks = await paystackClientFactory.GetClient().GetBanksAsync(query.Country, cancellationToken);
        var response = new GetPaystackBanksResponse(banks.Select(b => new PaystackBankItem(b.Code, b.Name)).ToArray());
        memoryCache.Set(cacheKey, response, CacheDuration);
        return response;
    }
}
