import { Trans } from "@lingui/react/macro";
import { Button } from "@repo/ui/components/Button";
import { ShieldIcon } from "lucide-react";

import type { EditableField } from "./verticalFieldHelpers";

import { VerticalFieldEditor } from "./VerticalFieldEditor";

export function SensitiveFieldsSection({
  fields,
  values,
  keysWithValues,
  isExpanded,
  isLoading,
  isReady,
  isPending,
  onExpand,
  onChange,
  onSave
}: Readonly<{
  fields: EditableField[];
  values: Record<string, string>;
  keysWithValues: string[];
  isExpanded: boolean;
  isLoading: boolean;
  isReady: boolean;
  isPending: boolean;
  onExpand: () => void;
  onChange: (key: string, value: string) => void;
  onSave: (field: EditableField, value: string) => void;
}>) {
  if (!isExpanded || !isReady) {
    return (
      <div className="rounded-md border p-3">
        <div className="flex items-start justify-between gap-3">
          <div className="min-w-0">
            <p className="mt-1 text-xs text-muted-foreground">
              {keysWithValues.length > 0 ? (
                <Trans>Sensitive details are saved. Open protected details to view them.</Trans>
              ) : (
                <Trans>Open protected details to add sensitive medical or identity details.</Trans>
              )}
            </p>
          </div>
          <Button type="button" variant="outline" size="sm" onClick={onExpand} isPending={isLoading}>
            <Trans>Open</Trans>
          </Button>
        </div>
      </div>
    );
  }

  return (
    <div className="rounded-md border border-primary/25 bg-primary/5 p-3">
      <div className="mb-3 flex items-center gap-2">
        <ShieldIcon className="size-4 text-primary" />
        <h4 className="text-sm font-medium">
          <Trans>Medical & identity</Trans>
        </h4>
      </div>
      <div className="space-y-3">
        {fields.map((field) => (
          <VerticalFieldEditor
            key={field.key}
            field={field}
            value={values[field.key] ?? ""}
            isPending={isPending}
            onChange={(value) => onChange(field.key, value)}
            onSave={(value) => onSave(field, value)}
          />
        ))}
      </div>
      <p className="mt-3 text-xs text-muted-foreground">
        <Trans>Access to this section is recorded for audit purposes.</Trans>
      </p>
    </div>
  );
}
