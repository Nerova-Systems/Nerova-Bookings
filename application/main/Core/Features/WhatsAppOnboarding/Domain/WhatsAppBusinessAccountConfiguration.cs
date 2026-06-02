using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharedKernel.Domain;
using SharedKernel.EntityFramework;

namespace Main.Features.WhatsAppOnboarding.Domain;

public sealed class WhatsAppBusinessAccountConfiguration : IEntityTypeConfiguration<WhatsAppBusinessAccount>
{
    public void Configure(EntityTypeBuilder<WhatsAppBusinessAccount> builder)
    {
        builder.MapStronglyTypedUuid<WhatsAppBusinessAccount, WhatsAppBusinessAccountId>(a => a.Id);
        builder.MapStronglyTypedLongId<WhatsAppBusinessAccount, TenantId>(a => a.TenantId);
        builder.OwnsOne(a => a.PhoneNumber, b => b.ToJson());
    }
}
