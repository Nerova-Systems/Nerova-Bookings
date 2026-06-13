using SharedKernel.Domain;

namespace Main.Features.Clients.Domain;

public enum VerticalFieldKind
{
    Text,
    LongText,
    Number,
    Date,
    Boolean,
    Choice,
    MultiChoice
}

public enum VerticalFieldSensitivity
{
    /// <summary>Ordinary preference/context. Plain jsonb storage, normal import mapping.</summary>
    Standard,

    /// <summary>Safety-critical for service delivery. Plain storage but surfaced prominently; agent writes notify the owner.</summary>
    Constraint,

    /// <summary>POPIA special / high-risk identifiers. Encrypted storage, role-gated, audited, never visible to the AI.</summary>
    Sensitive
}

public enum VerticalFieldAgentAccess
{
    None,
    Read,
    ReadWrite
}

/// <summary>
///     A single fixed field definition (docs/vertical-template-fields-spec.md §2). Keys are stable
///     snake_case identifiers that are never reused or renamed once shipped. No vertical field is ever
///     required — empty is a valid state everywhere.
/// </summary>
public sealed record VerticalFieldDefinition(
    string Key,
    string Label,
    VerticalFieldKind Kind,
    VerticalFieldSensitivity Sensitivity,
    VerticalFieldAgentAccess AgentAccess,
    string[] Options,
    string[] ImportSynonyms
);

/// <summary>
///     The code-defined field catalog for every vertical (docs/vertical-template-fields-spec.md §4).
///     The vertical is the schema: there is no field-builder UI, no per-tenant schema, and no admin
///     screen that edits these definitions. Additive changes (new field, synonym, choice option) are
///     safe releases; fields are deprecated, never removed. Architecture tests assert key uniqueness
///     and that Sensitive fields always have <see cref="VerticalFieldAgentAccess.None" />.
/// </summary>
public static class VerticalFieldCatalog
{
    private static readonly VerticalFieldDefinition Birthday = new(
        "birthday", "Birthday", VerticalFieldKind.Date, VerticalFieldSensitivity.Standard, VerticalFieldAgentAccess.Read,
        [], ["birthday", "dob", "date of birth", "geboortedatum", "verjaarsdag"]
    );

    private static readonly VerticalFieldDefinition AllergiesSensitivities = new(
        "allergies_sensitivities", "Allergies & sensitivities", VerticalFieldKind.LongText, VerticalFieldSensitivity.Constraint, VerticalFieldAgentAccess.ReadWrite,
        [], ["allergies", "allergic", "sensitivities", "allergieë"]
    );

    private static readonly VerticalFieldDefinition EmergencyContactName = new(
        "emergency_contact_name", "Emergency contact", VerticalFieldKind.Text, VerticalFieldSensitivity.Standard, VerticalFieldAgentAccess.None,
        [], ["emergency", "ice", "nood kontak", "emergency contact"]
    );

    private static readonly VerticalFieldDefinition EmergencyContactPhone = new(
        "emergency_contact_phone", "Emergency contact phone", VerticalFieldKind.Text, VerticalFieldSensitivity.Standard, VerticalFieldAgentAccess.None,
        [], ["emergency phone", "ice number", "emergency contact phone"]
    );

    private static readonly VerticalFieldDefinition[] Salon =
    [
        new("hair_type", "Hair type", VerticalFieldKind.Choice, VerticalFieldSensitivity.Standard, VerticalFieldAgentAccess.Read,
            ["Straight", "Wavy", "Curly", "Coily"], ["hair type", "hair texture", "haartipe"]
        ),
        new("colour_notes", "Colour notes", VerticalFieldKind.LongText, VerticalFieldSensitivity.Standard, VerticalFieldAgentAccess.Read,
            [], ["colour", "color formula", "tint", "kleur", "kleurnotas"]
        ),
        AllergiesSensitivities,
        new("preferred_staff", "Preferred stylist", VerticalFieldKind.Text, VerticalFieldSensitivity.Standard, VerticalFieldAgentAccess.Read,
            [], ["stylist", "preferred staff", "voorkeur stilis"]
        ),
        Birthday
    ];

    private static readonly VerticalFieldDefinition[] Barber =
    [
        new("usual_cut", "Usual cut", VerticalFieldKind.Text, VerticalFieldSensitivity.Standard, VerticalFieldAgentAccess.Read,
            [], ["cut", "style", "usual", "haarstyl"]
        ),
        new("clipper_guard", "Clipper guard", VerticalFieldKind.Text, VerticalFieldSensitivity.Standard, VerticalFieldAgentAccess.Read,
            [], ["guard", "clipper", "number"]
        ),
        new("beard_style", "Beard style", VerticalFieldKind.Text, VerticalFieldSensitivity.Standard, VerticalFieldAgentAccess.Read,
            [], ["beard", "baard"]
        ),
        AllergiesSensitivities,
        Birthday
    ];

    private static readonly VerticalFieldDefinition[] Nails =
    [
        new("nail_shape", "Preferred shape", VerticalFieldKind.Choice, VerticalFieldSensitivity.Standard, VerticalFieldAgentAccess.Read,
            ["Square", "Round", "Almond", "Coffin", "Stiletto"], ["shape", "nail shape", "vorm"]
        ),
        new("service_preference", "Gel or acrylic", VerticalFieldKind.Choice, VerticalFieldSensitivity.Standard, VerticalFieldAgentAccess.Read,
            ["Gel", "Acrylic", "Dip", "Natural"], ["gel", "acrylic", "akriel"]
        ),
        new("colour_preferences", "Colour preferences", VerticalFieldKind.Text, VerticalFieldSensitivity.Standard, VerticalFieldAgentAccess.Read,
            [], ["colour", "color", "polish", "kleur"]
        ),
        new("allergies_sensitivities", "Allergies & sensitivities", VerticalFieldKind.LongText, VerticalFieldSensitivity.Constraint, VerticalFieldAgentAccess.ReadWrite,
            [], ["allergies", "acrylic allergy", "allergieë"]
        ),
        new("nail_condition_notes", "Nail condition notes", VerticalFieldKind.LongText, VerticalFieldSensitivity.Standard, VerticalFieldAgentAccess.Read,
            [], ["condition", "nail health"]
        )
    ];

    private static readonly VerticalFieldDefinition[] Trainer =
    [
        new("fitness_goals", "Goals", VerticalFieldKind.LongText, VerticalFieldSensitivity.Standard, VerticalFieldAgentAccess.ReadWrite,
            [], ["goals", "objective", "doelwitte"]
        ),
        new("injuries_limitations", "Injuries & limitations", VerticalFieldKind.LongText, VerticalFieldSensitivity.Constraint, VerticalFieldAgentAccess.ReadWrite,
            [], ["injuries", "limitations", "conditions", "beserings"]
        ),
        new("fitness_level", "Fitness level", VerticalFieldKind.Choice, VerticalFieldSensitivity.Standard, VerticalFieldAgentAccess.Read,
            ["Beginner", "Intermediate", "Advanced"], ["level", "experience", "vlak"]
        ),
        EmergencyContactName,
        EmergencyContactPhone,
        Birthday
    ];

    private static readonly VerticalFieldDefinition[] Tutor =
    [
        new("student_name", "Student name", VerticalFieldKind.Text, VerticalFieldSensitivity.Standard, VerticalFieldAgentAccess.Read,
            [], ["student", "learner", "child", "leerder", "kind"]
        ),
        new("grade_level", "Grade", VerticalFieldKind.Choice, VerticalFieldSensitivity.Standard, VerticalFieldAgentAccess.Read,
            ["Gr 1", "Gr 2", "Gr 3", "Gr 4", "Gr 5", "Gr 6", "Gr 7", "Gr 8", "Gr 9", "Gr 10", "Gr 11", "Gr 12", "Tertiary", "Adult"],
            ["grade", "year", "standerd", "graad"]
        ),
        new("subjects", "Subjects", VerticalFieldKind.MultiChoice, VerticalFieldSensitivity.Standard, VerticalFieldAgentAccess.Read,
            ["Mathematics", "Mathematical Literacy", "Physical Sciences", "Life Sciences", "English", "Afrikaans", "isiZulu", "isiXhosa", "Accounting", "Economics", "Business Studies", "Geography", "History", "Computer Applications", "Other"],
            ["subject", "vakke"]
        ),
        new("school", "School", VerticalFieldKind.Text, VerticalFieldSensitivity.Standard, VerticalFieldAgentAccess.Read,
            [], ["school", "skool"]
        ),
        new("curriculum", "Curriculum", VerticalFieldKind.Choice, VerticalFieldSensitivity.Standard, VerticalFieldAgentAccess.Read,
            ["CAPS", "IEB", "Cambridge", "Other"], ["curriculum", "syllabus", "board"]
        ),
        new("guardian_name", "Parent / guardian", VerticalFieldKind.Text, VerticalFieldSensitivity.Standard, VerticalFieldAgentAccess.Read,
            [], ["parent", "guardian", "ouer", "voog"]
        ),
        new("guardian_phone", "Guardian phone", VerticalFieldKind.Text, VerticalFieldSensitivity.Standard, VerticalFieldAgentAccess.Read,
            [], ["parent phone", "guardian contact"]
        ),
        new("learning_notes", "Learning notes", VerticalFieldKind.LongText, VerticalFieldSensitivity.Standard, VerticalFieldAgentAccess.Read,
            [], ["notes", "needs", "leernotas"]
        )
    ];

    private static readonly VerticalFieldDefinition[] Vet =
    [
        new("pet_name", "Pet name", VerticalFieldKind.Text, VerticalFieldSensitivity.Standard, VerticalFieldAgentAccess.Read,
            [], ["pet", "animal", "patient", "troeteldier", "naam"]
        ),
        new("species", "Species", VerticalFieldKind.Choice, VerticalFieldSensitivity.Standard, VerticalFieldAgentAccess.Read,
            ["Dog", "Cat", "Bird", "Reptile", "Small mammal", "Horse", "Other"], ["species", "animal type", "spesie"]
        ),
        new("breed", "Breed", VerticalFieldKind.Text, VerticalFieldSensitivity.Standard, VerticalFieldAgentAccess.Read,
            [], ["breed", "ras"]
        ),
        new("pet_birthday", "Pet date of birth", VerticalFieldKind.Date, VerticalFieldSensitivity.Standard, VerticalFieldAgentAccess.Read,
            [], ["dob", "age", "geboortedatum"]
        ),
        new("sex_sterilised", "Sex / sterilised", VerticalFieldKind.Choice, VerticalFieldSensitivity.Standard, VerticalFieldAgentAccess.Read,
            ["Male", "Male (neutered)", "Female", "Female (spayed)"], ["sex", "neutered", "spayed", "gesteriliseer"]
        ),
        new("weight_kg", "Weight (kg)", VerticalFieldKind.Number, VerticalFieldSensitivity.Standard, VerticalFieldAgentAccess.Read,
            [], ["weight", "kg", "gewig"]
        ),
        new("microchip_number", "Microchip number", VerticalFieldKind.Text, VerticalFieldSensitivity.Standard, VerticalFieldAgentAccess.None,
            [], ["microchip", "chip"]
        ),
        new("vaccinations_current", "Vaccinations up to date", VerticalFieldKind.Boolean, VerticalFieldSensitivity.Standard, VerticalFieldAgentAccess.Read,
            [], ["vaccinated", "shots", "inentings"]
        ),
        new("last_vaccination_date", "Last vaccination", VerticalFieldKind.Date, VerticalFieldSensitivity.Standard, VerticalFieldAgentAccess.Read,
            [], ["vaccination date"]
        ),
        new("medical_conditions", "Medical conditions", VerticalFieldKind.LongText, VerticalFieldSensitivity.Constraint, VerticalFieldAgentAccess.Read,
            [], ["conditions", "illness", "siektes"]
        ),
        new("medications", "Medications", VerticalFieldKind.LongText, VerticalFieldSensitivity.Constraint, VerticalFieldAgentAccess.Read,
            [], ["meds", "medication", "medikasie"]
        ),
        new("temperament_notes", "Temperament & handling", VerticalFieldKind.LongText, VerticalFieldSensitivity.Constraint, VerticalFieldAgentAccess.ReadWrite,
            [], ["temperament", "aggressive", "nervous", "hantering"]
        ),
        new("insurance_provider", "Pet insurance", VerticalFieldKind.Text, VerticalFieldSensitivity.Standard, VerticalFieldAgentAccess.None,
            [], ["insurance", "medical aid", "versekering"]
        )
    ];

    private static readonly VerticalFieldDefinition[] Clinic =
    [
        new("date_of_birth", "Date of birth", VerticalFieldKind.Date, VerticalFieldSensitivity.Standard, VerticalFieldAgentAccess.Read,
            [], ["dob", "birth date", "geboortedatum"]
        ),
        new("id_passport_number", "ID / passport number", VerticalFieldKind.Text, VerticalFieldSensitivity.Sensitive, VerticalFieldAgentAccess.None,
            [], ["id number", "identity", "passport", "id nommer"]
        ),
        new("medical_aid_scheme", "Medical aid scheme", VerticalFieldKind.Text, VerticalFieldSensitivity.Sensitive, VerticalFieldAgentAccess.None,
            [], ["medical aid", "scheme", "mediese fonds"]
        ),
        new("medical_aid_number", "Medical aid number", VerticalFieldKind.Text, VerticalFieldSensitivity.Sensitive, VerticalFieldAgentAccess.None,
            [], ["member number", "lidnommer"]
        ),
        new("allergies", "Allergies", VerticalFieldKind.LongText, VerticalFieldSensitivity.Sensitive, VerticalFieldAgentAccess.None,
            [], ["allergies", "allergieë"]
        ),
        new("chronic_conditions", "Chronic conditions", VerticalFieldKind.LongText, VerticalFieldSensitivity.Sensitive, VerticalFieldAgentAccess.None,
            [], ["chronic", "conditions", "kroniese"]
        ),
        new("current_medications", "Current medications", VerticalFieldKind.LongText, VerticalFieldSensitivity.Sensitive, VerticalFieldAgentAccess.None,
            [], ["medication", "meds", "medikasie"]
        ),
        new("referring_practitioner", "Referring practitioner", VerticalFieldKind.Text, VerticalFieldSensitivity.Standard, VerticalFieldAgentAccess.None,
            [], ["referred by", "referring doctor", "verwys deur"]
        ),
        EmergencyContactName,
        EmergencyContactPhone
    ];

    public static IReadOnlyList<VerticalFieldDefinition> For(NerovaVertical vertical)
    {
        return vertical switch
        {
            NerovaVertical.Salon => Salon,
            NerovaVertical.Barber => Barber,
            NerovaVertical.Nails => Nails,
            NerovaVertical.Trainer => Trainer,
            NerovaVertical.Tutor => Tutor,
            NerovaVertical.Vet => Vet,
            NerovaVertical.Clinic => Clinic,
            _ => []
        };
    }

    public static VerticalFieldDefinition? Find(NerovaVertical vertical, string key)
    {
        return For(vertical).FirstOrDefault(definition => definition.Key == key);
    }

    public static IEnumerable<NerovaVertical> AllVerticals => Enum.GetValues<NerovaVertical>();
}
