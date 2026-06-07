using Main.Features.Scheduling.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharedKernel.Domain;
using SharedKernel.EntityFramework;

namespace Main.Features.WhatsAppBooking.Domain;

public sealed class WhatsAppConversationConfiguration : IEntityTypeConfiguration<WhatsAppConversation>
{
    public void Configure(EntityTypeBuilder<WhatsAppConversation> builder)
    {
        builder.MapStronglyTypedUuid<WhatsAppConversation, WhatsAppConversationId>(c => c.Id);
        builder.MapStronglyTypedLongId<WhatsAppConversation, TenantId>(c => c.TenantId);
        builder.MapStronglyTypedNullableId<WhatsAppConversation, BookingId, string>(c => c.DraftBookingId);
    }
}
