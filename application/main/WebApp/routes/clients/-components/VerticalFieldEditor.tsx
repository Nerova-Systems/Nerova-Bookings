import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Badge } from "@repo/ui/components/Badge";
import { TextAreaField } from "@repo/ui/components/TextAreaField";
import { TextField } from "@repo/ui/components/TextField";
import { AlertTriangleIcon } from "lucide-react";

import type { components } from "@/shared/lib/api/client";

import type { EditableField } from "./verticalFieldHelpers";

import { BooleanFieldInput, ChoiceFieldInput, MultiChoiceFieldInput } from "./VerticalFieldInputs";
import { localizeFieldLabel } from "./verticalFieldLabelLocalization";

export function VerticalFieldEditor({
  field,
  value,
  isPending,
  onChange,
  onSave
}: Readonly<{
  field: EditableField;
  value: string;
  isPending: boolean;
  onChange: (value: string) => void;
  onSave: (value: string) => void;
}>) {
  const label = localizeFieldLabel(field.label);
  const placeholder = t`Add ${label}`;

  switch (field.kind) {
    case "LongText":
      return (
        <FieldShell field={field}>
          <TextAreaField
            name={field.key}
            aria-label={label}
            placeholder={placeholder}
            value={value}
            disabled={isPending}
            lines={3}
            onChange={onChange}
            onBlur={() => onSave(value)}
          />
        </FieldShell>
      );
    case "Number":
      return (
        <FieldShell field={field}>
          <TextField
            name={field.key}
            aria-label={label}
            type="number"
            inputMode="decimal"
            placeholder={placeholder}
            value={value}
            disabled={isPending}
            onChange={onChange}
            onBlur={() => onSave(value)}
          />
        </FieldShell>
      );
    case "Date":
      return (
        <FieldShell field={field}>
          <TextField
            name={field.key}
            aria-label={label}
            type="date"
            value={value}
            disabled={isPending}
            onChange={onChange}
            onBlur={() => onSave(value)}
          />
        </FieldShell>
      );
    case "Boolean":
      return (
        <FieldShell field={field}>
          <BooleanFieldInput
            field={field}
            value={value}
            isPending={isPending}
            label={label}
            onChange={onChange}
            onSave={onSave}
          />
        </FieldShell>
      );
    case "Choice":
      return (
        <FieldShell field={field}>
          <ChoiceFieldInput
            field={field}
            value={value}
            isPending={isPending}
            placeholder={placeholder}
            onChange={onChange}
            onSave={onSave}
          />
        </FieldShell>
      );
    case "MultiChoice":
      return (
        <FieldShell field={field}>
          <MultiChoiceFieldInput
            field={field}
            value={value}
            isPending={isPending}
            onChange={onChange}
            onSave={onSave}
          />
        </FieldShell>
      );
    case "Text":
    default:
      return (
        <FieldShell field={field}>
          <TextField
            name={field.key}
            aria-label={label}
            placeholder={placeholder}
            value={value}
            disabled={isPending}
            onChange={onChange}
            onBlur={() => onSave(value)}
          />
        </FieldShell>
      );
  }
}

function FieldShell({ field, children }: Readonly<{ field: EditableField; children: React.ReactNode }>) {
  const hasValue = (field.value ?? "").trim().length > 0;
  const label = localizeFieldLabel(field.label);

  return (
    <div className="grid gap-2 rounded-md border bg-background p-3">
      <div className="flex items-center justify-between gap-2">
        <div className="flex min-w-0 items-center gap-2">
          <span className="truncate text-sm font-medium">{label}</span>
          {field.sensitivity === "Constraint" && (
            <Badge variant="warning" className="shrink-0 gap-1">
              <AlertTriangleIcon className="size-3" />
              <Trans>Constraint</Trans>
            </Badge>
          )}
        </div>
        {!hasValue && (
          <span className="shrink-0 text-xs text-muted-foreground">
            <Trans>Add</Trans>
          </span>
        )}
      </div>
      {children}
    </div>
  );
}

export function FieldErrorSummary({
  error
}: Readonly<{ error: components["schemas"]["HttpValidationProblemDetails"] | null | undefined }>) {
  const messages = [error?.detail, ...Object.values(error?.errors ?? {}).flat()].filter(
    (message): message is string => typeof message === "string" && message.length > 0
  );

  if (messages.length === 0) return null;

  return (
    <div className="mb-4 rounded-md border border-destructive/30 bg-destructive/10 p-3 text-sm text-destructive">
      {messages.map((message) => (
        <div key={message}>{message}</div>
      ))}
    </div>
  );
}
