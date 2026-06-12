# Vertical Template Fields — Specification

Version: 1.0 · Date: 2026-06-12 · Owner: Colin (founder)
Extends: `docs/agentic-system-spec.md` (Data Import agent §5.3), `docs/maf-autonomy-design.md` (Memory §5, receipts §A4), `docs/ux-design-review.md` (§3 vocabulary layer, §4.5 client profile, §4.7 welcome templates).

> **Founder decisions locked (2026-06-12):** all six verticals fully defined at launch including vet and clinic · client-level fields at v1 with visit-level designed for P1 · AI receptionist gets read + structured write with receipts · field sets are **fixed per vertical — no hide, no rename, no builder**. Import-born custom attributes (see §8) remain the only escape hatch.

---

## 0. Ground rules

1. **The vertical is the schema.** Field sets ship as code-defined data (a static catalog, same pattern as `AppListingCatalog`). There is no field-builder UI, no per-tenant schema, no admin screen that edits field definitions. Ever.
2. **Three field tiers, strict precedence.** Core fields (existing `Client`: first/last name, email, phone, avatar, notes) → vertical fields (this spec) → custom attributes (import-born only, §8). Import maps in that order; a column never becomes a custom attribute if a vertical field matches.
3. **Sensitivity is a first-class property**, not an afterthought. Three classes drive storage, access, AI exposure, and import behavior (§3).
4. **Nerova is not an EHR.** Clinic fields are *front-desk administrative* data (medical aid, allergies for triage routing, emergency contact). Clinical documentation — diagnoses, treatment notes, scripts — is explicitly out of scope at every phase. Vet medical fields are convenience records for a booking context, not veterinary clinical records.
5. All spec v1.0 §0 ground rules apply (commands/queries via repo conventions, migrations per rules, dark behind flags).

---

## 1. The vertical

A first-class `NerovaVertical` enum: `Salon · Barber · Nails · Trainer · Tutor · Vet · Clinic · Other`. Chosen explicitly in welcome step 1 (ux-review §4.7); stored on the tenant (account SCS); propagated to the `main` SCS through the existing tenant-context rails so `Clients`, import, and agents can read it without a cross-SCS call.

The existing `VerticalDeriver`/`MetaBusinessVertical` (account SCS) serves Meta's WhatsApp profile taxonomy and **stays as-is**; `NerovaVertical` maps onto it one-way (e.g., `Salon|Barber|Nails → Beauty`, `Clinic → Health`, `Tutor → Education`) so the WhatsApp brand profile derives automatically. `Other` gets core fields + custom attributes only.

Changing vertical after onboarding: allowed once via support (back-office action) at v1 — field data from the old vertical is retained but hidden, never deleted. Not a self-service setting; switching schemas is a rare, support-worthy event.

---

## 2. Field definition model

The catalog is C# data: `VerticalFieldDefinition`:

| Property | Values | Notes |
| --- | --- | --- |
| `Key` | snake_case stable identifier | Never reused or renamed once shipped |
| `Label` | Lingui message | Localized; SA languages follow app translation phases |
| `Kind` | `Text · LongText · Number · Date · Boolean · Choice · MultiChoice` | `Choice`/`MultiChoice` carry a fixed option list in the catalog |
| `Scope` | `Client` (v1) · `Visit` (P1, §9) | |
| `Sensitivity` | `Standard · Constraint · Sensitive` | §3 |
| `AgentAccess` | `Read · ReadWrite · None` | §6; `Sensitive` forces `None` |
| `ImportSynonyms` | per-locale string list | Seed lists in §4 tables; grown from real imports |
| `Group / Order` | display grouping | Fixed render order on the client profile |
| `Required` | always `false` | No vertical field is ever required — empty is a valid state everywhere |

Catalog versioning: additive changes (new field, new synonym, new choice option) are safe releases. Removing a field = `Deprecated` flag — hidden from UI and import, data retained. Choice options are never removed, only deprecated. An architecture test asserts keys are unique and sensitive fields have `AgentAccess.None`.

---

## 3. Sensitivity classes

| Class | Meaning | Storage | UI | AI | Import |
| --- | --- | --- | --- | --- | --- |
| **Standard** | Ordinary preference/context | `clients.vertical_fields` (jsonb, plain) | Details card | Per catalog (`Read`/`ReadWrite`) | Normal mapping |
| **Constraint** | Safety-critical for service delivery (allergies at a salon, injuries at a trainer, temperament at a vet) | jsonb, plain | Prominent chip on client profile **and** booking sheet | `Read` always; `ReadWrite` allowed — writes notify the owner | Normal mapping, flagged in review |
| **Sensitive** | POPIA special / high-risk identifiers (health data, ID numbers, medical aid) | `clients.sensitive_fields` — encrypted payload via a `FieldProtector` (same pattern as `Apps/CredentialProtector`) | Separate visually-distinct section; visible only to Owner/Admin (role-gated); every read audit-logged | **`None` — no agent tool can ever read or write sensitive fields** | Never auto-approved (R24 instant path excluded); explicit per-column confirmation at review; values masked in the review UI |

Sensitive fields additionally: excluded from telemetry, logs, exports-by-default, and AI distillation input. Clinic onboarding adds a consent posture: a system-managed `consent_on_file` boolean and a consent line in the tenant's client-facing booking confirmation (copy in welcome template). Tutor note: students are typically minors — the *client* is the parent/guardian; student fields are standard but excluded from AI smalltalk distillation (memory `kind` whitelist already excludes minors' personal context).

---

## 4. The six field sets

Synonym lists below are seeds (English + Afrikaans starters); they live in the catalog and grow from real import corpora. All fields optional, always.

### 4.1 Salon

| Key | Label | Kind | Sens. | Agent | Synonyms (seed) |
| --- | --- | --- | --- | --- | --- |
| `hair_type` | Hair type | Choice: Straight/Wavy/Curly/Coily | Std | Read | hair type, hair texture, haartipe |
| `colour_notes` | Colour notes | LongText | Std | Read | colour, color formula, tint, kleur, kleurnotas |
| `allergies_sensitivities` | Allergies & sensitivities | LongText | **Constraint** | ReadWrite | allergies, allergic, sensitivities, allergieë |
| `preferred_staff` | Preferred stylist | Text | Std | Read | stylist, preferred staff, voorkeur stilis |
| `birthday` | Birthday | Date | Std | Read | birthday, dob, date of birth, geboortedatum, verjaarsdag |

### 4.2 Barber

| Key | Label | Kind | Sens. | Agent | Synonyms (seed) |
| --- | --- | --- | --- | --- | --- |
| `usual_cut` | Usual cut | Text | Std | Read | cut, style, usual, haarstyl |
| `clipper_guard` | Clipper guard | Text | Std | Read | guard, clipper, number |
| `beard_style` | Beard style | Text | Std | Read | beard, baard |
| `allergies_sensitivities` | Allergies & sensitivities | LongText | **Constraint** | ReadWrite | (as salon) |
| `birthday` | Birthday | Date | Std | Read | (as salon) |

### 4.3 Nails

| Key | Label | Kind | Sens. | Agent | Synonyms (seed) |
| --- | --- | --- | --- | --- | --- |
| `nail_shape` | Preferred shape | Choice: Square/Round/Almond/Coffin/Stiletto | Std | Read | shape, nail shape, vorm |
| `service_preference` | Gel or acrylic | Choice: Gel/Acrylic/Dip/Natural | Std | Read | gel, acrylic, akriel |
| `colour_preferences` | Colour preferences | Text | Std | Read | colour, color, polish, kleur |
| `allergies_sensitivities` | Allergies & sensitivities | LongText | **Constraint** | ReadWrite | allergies, acrylic allergy, allergieë |
| `nail_condition_notes` | Nail condition notes | LongText | Std | Read | condition, nail health |

### 4.4 Trainer

| Key | Label | Kind | Sens. | Agent | Synonyms (seed) |
| --- | --- | --- | --- | --- | --- |
| `fitness_goals` | Goals | LongText | Std | ReadWrite | goals, objective, doelwitte |
| `injuries_limitations` | Injuries & limitations | LongText | **Constraint** | ReadWrite | injuries, limitations, conditions, beserings |
| `fitness_level` | Fitness level | Choice: Beginner/Intermediate/Advanced | Std | Read | level, experience, vlak |
| `emergency_contact_name` | Emergency contact | Text | Std | None | emergency, ice, nood kontak |
| `emergency_contact_phone` | Emergency contact phone | Text | Std | None | emergency phone, ice number |
| `birthday` | Birthday | Date | Std | Read | (as salon) |

### 4.5 Tutor

| Key | Label | Kind | Sens. | Agent | Synonyms (seed) |
| --- | --- | --- | --- | --- | --- |
| `student_name` | Student name | Text | Std | Read | student, learner, child, leerder, kind |
| `grade_level` | Grade | Choice: Gr 1–12 / Tertiary / Adult | Std | Read | grade, year, standerd, graad |
| `subjects` | Subjects | MultiChoice: curated list | Std | Read | subject, vakke |
| `school` | School | Text | Std | Read | school, skool |
| `curriculum` | Curriculum | Choice: CAPS/IEB/Cambridge/Other | Std | Read | curriculum, syllabus, board |
| `guardian_name` | Parent / guardian | Text | Std | Read | parent, guardian, ouer, voog |
| `guardian_phone` | Guardian phone | Text | Std | Read | parent phone, guardian contact |
| `learning_notes` | Learning notes | LongText | Std | Read | notes, needs, leernotas |

*Modeling note:* the client is the **payer** (usually the guardian); the student is the subject of service. v1 holds student fields on the client record (one student assumed). The `ClientSubject` entity (§9) lifts this to N students when it ships — same forces as vet's pets.

### 4.6 Vet

| Key | Label | Kind | Sens. | Agent | Synonyms (seed) |
| --- | --- | --- | --- | --- | --- |
| `pet_name` | Pet name | Text | Std | Read | pet, animal, patient, troeteldier, naam |
| `species` | Species | Choice: Dog/Cat/Bird/Reptile/Small mammal/Horse/Other | Std | Read | species, animal type, spesie |
| `breed` | Breed | Text | Std | Read | breed, ras |
| `pet_birthday` | Pet date of birth | Date | Std | Read | dob, age, geboortedatum |
| `sex_sterilised` | Sex / sterilised | Choice: M/F + neutered variants | Std | Read | sex, neutered, spayed, gesteriliseer |
| `weight_kg` | Weight (kg) | Number | Std | Read | weight, kg, gewig |
| `microchip_number` | Microchip number | Text | Std | None | microchip, chip |
| `vaccinations_current` | Vaccinations up to date | Boolean | Std | Read | vaccinated, shots, inentings |
| `last_vaccination_date` | Last vaccination | Date | Std | Read | vaccination date |
| `medical_conditions` | Medical conditions | LongText | **Constraint** | Read | conditions, illness, siektes |
| `medications` | Medications | LongText | **Constraint** | Read | meds, medication, medikasie |
| `temperament_notes` | Temperament & handling | LongText | **Constraint** | ReadWrite | temperament, aggressive, nervous, hantering |
| `insurance_provider` | Pet insurance | Text | Std | None | insurance, medical aid, versekering |

*Modeling note:* pet data is **not POPIA special-category** (it describes an animal, not a person) — hence Constraint, not Sensitive. The client is the owner; multiple pets per client is the norm. v1 single-pet on the client record is an accepted limitation; the vet vertical **should not be marketed** until `ClientSubject` (§9) ships. Defined now so import mapping, agents, and UI are schema-ready.

### 4.7 Clinic

| Key | Label | Kind | Sens. | Agent | Synonyms (seed) |
| --- | --- | --- | --- | --- | --- |
| `date_of_birth` | Date of birth | Date | Std | Read | dob, birth date, geboortedatum |
| `id_passport_number` | ID / passport number | Text | **Sensitive** | None | id number, identity, passport, id nommer |
| `medical_aid_scheme` | Medical aid scheme | Text | **Sensitive** | None | medical aid, scheme, mediese fonds |
| `medical_aid_number` | Medical aid number | Text | **Sensitive** | None | member number, lidnommer |
| `allergies` | Allergies | LongText | **Sensitive** | None | allergies, allergieë |
| `chronic_conditions` | Chronic conditions | LongText | **Sensitive** | None | chronic, conditions, kroniese |
| `current_medications` | Current medications | LongText | **Sensitive** | None | medication, meds, medikasie |
| `referring_practitioner` | Referring practitioner | Text | Std | None | referred by, referring doctor, verwys deur |
| `emergency_contact_name` | Emergency contact | Text | Std | None | (as trainer) |
| `emergency_contact_phone` | Emergency contact phone | Text | Std | None | (as trainer) |

*Posture:* every clinic field the AI cannot touch; the receptionist agent books clinic appointments using core fields only. Clinic allergies are Sensitive (human health data, POPIA s32) unlike salon allergies (service-delivery constraint) — same word, different class, by design. Consent capture (§3) gates the clinic template's activation in welcome.

---

## 5. Storage and validation

- `clients.vertical_fields jsonb null` — `{key: value}` for Standard + Constraint fields. EF `HasColumnType("jsonb")` + `HasConversion` per migration rules.
- `clients.sensitive_fields text null` — encrypted JSON payload via `FieldProtector` (ASP.NET Data Protection, `CredentialProtector` pattern). Decrypted only in role-gated query paths; access collected as telemetry-safe audit events (field keys, never values).
- Generic FluentValidation: value validated against catalog `Kind` (date parse, number range, choice membership, text length ≤ 500 / longtext ≤ 4000). Unknown keys rejected. One validator, catalog-driven — no per-field validator classes.
- `UpdateClientVerticalFields` command (and a role-gated `UpdateClientSensitiveFields`); `Client` aggregate gains typed accessor methods, not public dictionaries.

## 6. AI receptionist integration

- `GetClientDetails` tool (identified sessions only): returns Standard+Constraint fields where `AgentAccess ≠ None`, rendered as compact labeled text. Constraint fields prefixed so the model treats them as service-affecting facts.
- `UpdateClientDetail(field_key, value)` tool: allowlist = catalog entries with `ReadWrite` for the tenant's vertical; schema-validated server-side (same generic validator); rejected values return as recoverable tool errors. Every write produces a **receipt** ("Noted Naledi's acrylic allergy") in the activity feed; **Constraint writes additionally notify the owner** (suggestion-inbox entry, auto-resolved L2-style — visible, not blocking).
- Persona injection: vertical fields render inside the server-composed "Known about this client" block alongside memory facts (spec §6.5.5 — data, never instructions).
- Memory interplay (autonomy doc §5): when distillation produces a fact that matches a `ReadWrite` vertical field with high confidence, it *proposes* a field write as an L1 suggestion rather than keeping a duplicate loose fact. Fields are structure; memory is everything that doesn't fit.

## 7. Import integration (extends spec R18)

Mapping priority per column: **core → vertical (tenant's vertical only) → custom attribute proposal**. Stage 1 deterministic: exact/normalized synonym match per locale. Stage 2 agent inference (existing `InferColumnMapping`) constrained to the tenant's catalog keys + per-column confidence. Review UI shows vertical-field mappings with the same confidence treatment as core fields.

Sensitive columns (clinic): never on the R24 auto-approve path; masked sample values in review; explicit per-column confirmation checkbox; rows land encrypted on commit. Constraint columns flagged with a callout in review ("142 clients have allergy notes — these will show on every booking").

## 8. Custom attributes (boundary restated)

Unmapped columns with real data → import proposes a custom attribute (name + inferred kind), owner approves per column at review. Text/Number/Date/Choice only, ≤ 15 per tenant, stored in `clients.custom_attributes jsonb`, rendered in a generic "More details" group after vertical fields, `AgentAccess = Read` uniformly, never Sensitive (a column that *looks* sensitive — ID numbers in a salon CSV — is refused as a custom attribute with an explanation, not silently imported). No creation path exists outside import. This is the escape hatch that keeps the fixed-set decision livable.

## 9. Designed-not-built (P1): Visit fields and ClientSubject

**Visit fields** (`Scope = Visit`): per-appointment values on the `Booking` aggregate (`visit_fields jsonb`), e.g. salon `formula_used`, trainer `session_notes`, vet `treatment_summary`. Editable from the booking details sheet post-appointment; surfaced as a visit timeline on the client profile. Clinic visit fields: **none, ever** (ground rule 4 — that would be an EHR).

**ClientSubject**: child entity of `Client` (`client_subjects`: `tenant_id`, `id`, `client_id` FK, `created_at`, `modified_at`, `display_name text`, `subject_fields jsonb`) for verticals where the service subject ≠ the payer: vet pets (N per client), tutor students. Catalog gains `Scope = Subject`; vet/tutor field sets migrate from client-level to subject-level when this ships; the migration copies existing single-subject data into one subject row. Booking gains an optional `client_subject_id` ("which pet is this appointment for?" — the receptionist asks naturally when a client has more than one). **Vet vertical launches publicly only after ClientSubject ships.**

## 10. UI surfaces (per ux-review patterns)

- Client profile (§4.5): "Details" card, catalog order, grouped; Constraint values as warning-tinted chips also pinned on the booking details sheet; Sensitive section ("Medical & identity") rendered only for permitted roles, with an "access is recorded" footnote. Empty fields show as quiet add-affordances; an all-empty card collapses to a single "Add details" row (P4).
- Booking sheet (§4.4): constraint chips visible at booking level ("⚠ Acrylic allergy").
- Import review (§4.7): vertical mappings + sensitive confirmations as above.
- No settings surface exists for fields anywhere — the absence is the feature.

## 11. Requirements summary

**P0** — VF1 `NerovaVertical` on tenant, set in welcome, propagated to main · VF2 catalog with all §4 definitions + architecture tests · VF3 jsonb + encrypted storage, generic validator, update commands · VF4 import mapping priority + sensitive review path · VF5 client profile Details card + constraint chips · VF6 agent `GetClientDetails`/`UpdateClientDetail` with receipts + constraint notifications (gated on spec Phase 2 receptionist).
**P1** — VF7 visit fields · VF8 `ClientSubject` + vet/tutor migration · VF9 synonym growth pipeline from import corpora · VF10 Afrikaans/locale labels.
**P2** — VF11 vertical change self-service with guided remap · VF12 cross-vertical analytics on field fill-rates (which fields earn their place — deprecation input).

Telemetry: `VerticalFieldsUpdated(source: import|agent|owner, field_count)`, `SensitiveFieldAccessed(field_key, role)`, `CustomAttributeCreated(kind)`, `ConstraintFieldFlagged(field_key)`.

## 12. Risks

| Risk | Mitigation |
| --- | --- |
| Clinic data turns Nerova into a regulated health-records system | Ground rule 4 boundary; Sensitive class (encryption, role-gating, no AI, audit); no clinical documentation at any phase; consent gate on template activation |
| Vet ships before multi-pet works → broken promise | Hard gate: vet template not publicly selectable until ClientSubject ships |
| Fixed sets annoy owners ("I don't use clipper guard") | Empty fields are quiet affordances, not nagging blanks; fill-rate telemetry (VF12) drives catalog pruning; custom attributes absorb genuine gaps |
| Synonym misses make import feel dumb | Deterministic pass is only stage 1 — agent inference backstops; every reviewed correction feeds the synonym corpus (VF9) |
| Sensitivity misclassification (salon ID-number column) | Custom-attribute refusal rule (§8); import review is the human gate |

## 13. Open questions (founder)

| # | Question | Blocking? |
| --- | --- | --- |
| 1 | Welcome consent copy for clinic template — legal review needed before clinic launch? | Clinic launch |
| 2 | Which roles beyond Owner/Admin may view Sensitive fields (custom-role permission entry)? | VF3 design |
| 3 | Trainer `injuries_limitations`: Constraint (agent-readable, recommended for service safety) confirmed, or escalate to Sensitive (no AI) for caution? | VF2 |
| 4 | Subjects naming in UI per vertical ("Pets" / "Students") — term map entries (ux-review §3) | VF8 |
