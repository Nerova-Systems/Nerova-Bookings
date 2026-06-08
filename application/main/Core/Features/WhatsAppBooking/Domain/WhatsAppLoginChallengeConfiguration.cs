using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharedKernel.Domain;
using SharedKernel.EntityFramework;

namespace Main.Features.WhatsAppBooking.Domain;

public sealed class WhatsAppLoginChallengeConfiguration : IEntityTypeConfiguration<WhatsAppLoginChallenge>
{
    public void Configure(EntityTypeBuilder<WhatsAppLoginChallenge> builder)
    {
        builder.MapStronglyTypedUuid<WhatsAppLoginChallenge, WhatsAppLoginChallengeId>(c => c.Id);
        builder.MapStronglyTypedLongId<WhatsAppLoginChallenge, TenantId>(c => c.TenantId);
    }
}
