import { t } from "@lingui/core/macro";

import { NerovaVertical, type components } from "@/shared/lib/api/client";

export type ClientDetails = components["schemas"]["ClientDetails"];
export type ClientVerticalField = components["schemas"]["ClientVerticalFieldResponse"];
export type FieldKind = "Text" | "LongText" | "Number" | "Date" | "Boolean" | "Choice" | "MultiChoice";

export type EditableField = Omit<ClientVerticalField, "kind"> & { kind: FieldKind };

export const EMPTY_SELECT_VALUE = "__empty";

export function toEditableField(field: ClientVerticalField): EditableField | null {
  if (!isFieldKind(field.kind)) return null;
  return { ...field, kind: field.kind };
}

export function isFieldKind(kind: string): kind is FieldKind {
  return ["Text", "LongText", "Number", "Date", "Boolean", "Choice", "MultiChoice"].includes(kind);
}

export function normalizeFieldValue(field: EditableField, value: string) {
  const trimmed = value.trim();
  if (trimmed.length === 0) return null;
  if (field.kind === "MultiChoice") return parseMultiChoice(trimmed).join(", ");
  return trimmed;
}

export function parseMultiChoice(value: string) {
  return value
    .split(",")
    .map((part) => part.trim())
    .filter((part) => part.length > 0);
}

export function getSensitiveDefinitions(
  vertical: components["schemas"]["NerovaVertical"] | null | undefined,
  keysWithValues: string[]
): EditableField[] {
  const clinicFields: EditableField[] =
    vertical === NerovaVertical.Clinic
      ? [
          sensitiveField("id_passport_number", t`ID / passport number`, "Text"),
          sensitiveField("medical_aid_scheme", t`Medical aid scheme`, "Text"),
          sensitiveField("medical_aid_number", t`Medical aid number`, "Text"),
          sensitiveField("allergies", t`Allergies`, "LongText"),
          sensitiveField("chronic_conditions", t`Chronic conditions`, "LongText"),
          sensitiveField("current_medications", t`Current medications`, "LongText")
        ]
      : [];

  const knownKeys = new Set(clinicFields.map((field) => field.key));
  return [
    ...clinicFields,
    ...keysWithValues
      .filter((key) => !knownKeys.has(key))
      .map((key) => sensitiveField(key, humanizeFieldKey(key), "Text"))
  ];
}

export function sensitiveField(key: string, label: string, kind: FieldKind): EditableField {
  return { key, label, kind, sensitivity: "Sensitive", options: [], value: null };
}

export function humanizeFieldKey(key: string) {
  return key
    .split("_")
    .map((part) => part.charAt(0).toUpperCase() + part.slice(1))
    .join(" ");
}
