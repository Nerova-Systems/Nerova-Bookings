namespace SharedKernel.Domain;

/// <summary>
///     The business vertical a tenant operates in (docs/vertical-template-fields-spec.md §1). The vertical
///     is the schema: it selects the fixed client field catalog, welcome defaults, and vocabulary. Chosen
///     explicitly in welcome step 1, stored on the tenant (account SCS), and propagated to the main SCS
///     (scheduling profile) so clients, import, and agents read it without a cross-SCS call. Shared here
///     because both self-contained systems speak it.
/// </summary>
public enum NerovaVertical
{
    Other,
    Salon,
    Barber,
    Nails,
    Trainer,
    Tutor,
    Vet,
    Clinic
}
