using SharedKernel.StronglyTypedIds;

namespace Main.Features.Clients.Domain;

[IdPrefix("cli")]
[JsonConverter(typeof(StronglyTypedIdJsonConverter<string, ClientId>))]
public sealed record ClientId(string Value) : StronglyTypedUlid<ClientId>(Value)
{
    public override string ToString()
    {
        return Value;
    }
}
