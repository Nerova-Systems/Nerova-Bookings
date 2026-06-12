using System.Collections.Immutable;
using System.Text.Json;
using Main.Features.Clients.Domain;
using Main.Features.WhatsAppBooking.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharedKernel.Domain;
using SharedKernel.EntityFramework;

namespace Main.Features.Receptionist.Domain;

public sealed class ReceptionistSessionConfiguration : IEntityTypeConfiguration<ReceptionistSession>
{
    public void Configure(EntityTypeBuilder<ReceptionistSession> builder)
    {
        builder.MapStronglyTypedUuid<ReceptionistSession, ReceptionistSessionId>(s => s.Id);
        builder.MapStronglyTypedLongId<ReceptionistSession, TenantId>(s => s.TenantId);
        builder.MapStronglyTypedId<ReceptionistSession, WhatsAppConversationId, string>(s => s.WhatsAppConversationId);
        builder.Property(s => s.AgentThread).HasColumnType("jsonb");
    }
}

public sealed class EscalationConfiguration : IEntityTypeConfiguration<Escalation>
{
    public void Configure(EntityTypeBuilder<Escalation> builder)
    {
        builder.MapStronglyTypedUuid<Escalation, EscalationId>(e => e.Id);
        builder.MapStronglyTypedLongId<Escalation, TenantId>(e => e.TenantId);
        builder.MapStronglyTypedId<Escalation, WhatsAppConversationId, string>(e => e.WhatsAppConversationId);
        builder.MapStronglyTypedNullableId<Escalation, ClientId, string>(e => e.ClientId);
        builder.MapStronglyTypedNullableId<Escalation, UserId, string>(e => e.ResolvedByUserId);
    }
}

public sealed class ReceptionistSettingsConfiguration : IEntityTypeConfiguration<ReceptionistSettings>
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = JsonSerializerOptions.Default;

    public void Configure(EntityTypeBuilder<ReceptionistSettings> builder)
    {
        builder.MapStronglyTypedUuid<ReceptionistSettings, ReceptionistSettingsId>(s => s.Id);
        builder.MapStronglyTypedLongId<ReceptionistSettings, TenantId>(s => s.TenantId);

        builder.Property(s => s.Languages)
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v.ToArray(), JsonSerializerOptions),
                v => JsonSerializer.Deserialize<ImmutableArray<string>>(v, JsonSerializerOptions)
            );
    }
}
