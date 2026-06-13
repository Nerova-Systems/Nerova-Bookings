import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { useQueryClient } from "@tanstack/react-query";
import { useEffect, useMemo, useState } from "react";
import { toast } from "sonner";

import { api, NerovaVertical } from "@/shared/lib/api/client";

import { FieldGroup } from "./FieldGroup";
import { SensitiveFieldsSection } from "./SensitiveFieldsSection";
import { FieldErrorSummary, VerticalFieldEditor } from "./VerticalFieldEditor";
import {
  getSensitiveDefinitions,
  normalizeFieldValue,
  toEditableField,
  type ClientDetails,
  type EditableField
} from "./verticalFieldHelpers";

export function ClientVerticalFieldsCard({ client }: Readonly<{ client: ClientDetails }>) {
  const queryClient = useQueryClient();
  const [draftValues, setDraftValues] = useState<Record<string, string>>({});
  const [isSensitiveExpanded, setIsSensitiveExpanded] = useState(false);
  const [hideSensitiveSection, setHideSensitiveSection] = useState(false);

  const verticalFieldsQuery = api.useQuery("get", "/api/main/clients/{id}/vertical-fields", {
    params: { path: { id: client.id } }
  });
  const verticalFields = verticalFieldsQuery.data;
  const visibleFields = useMemo(
    () => (verticalFields?.fields ?? []).map(toEditableField).filter((field): field is EditableField => field !== null),
    [verticalFields?.fields]
  );
  const standardFields = visibleFields.filter((field) => field.sensitivity !== "Constraint");
  const constraintFields = visibleFields.filter((field) => field.sensitivity === "Constraint");
  const sensitiveDefinitions = useMemo(
    () => getSensitiveDefinitions(verticalFields?.vertical, verticalFields?.sensitiveFieldKeysWithValues ?? []),
    [verticalFields?.vertical, verticalFields?.sensitiveFieldKeysWithValues]
  );
  const hasSensitiveFields = sensitiveDefinitions.length > 0;

  const sensitiveFieldsQuery = api.useQuery(
    "get",
    "/api/main/clients/{id}/sensitive-fields",
    { params: { path: { id: client.id } } },
    { enabled: isSensitiveExpanded && hasSensitiveFields && !hideSensitiveSection, retry: false }
  );

  const verticalMutation = api.useMutation("put", "/api/main/clients/{id}/vertical-fields", {
    meta: { skipQueryInvalidation: true }
  });
  const sensitiveMutation = api.useMutation("put", "/api/main/clients/{id}/sensitive-fields", {
    meta: { skipQueryInvalidation: true }
  });

  useEffect(() => {
    setDraftValues(Object.fromEntries(visibleFields.map((field) => [field.key, field.value ?? ""])));
  }, [visibleFields]);

  useEffect(() => {
    if (sensitiveFieldsQuery.data) {
      setDraftValues((current) => ({ ...current, ...sensitiveFieldsQuery.data.fields }));
    }
  }, [sensitiveFieldsQuery.data]);

  useEffect(() => {
    setIsSensitiveExpanded(false);
    setHideSensitiveSection(false);
  }, [client.id]);

  useEffect(() => {
    if (sensitiveFieldsQuery.isError) {
      setHideSensitiveSection(true);
      setIsSensitiveExpanded(false);
    }
  }, [sensitiveFieldsQuery.isError]);

  if (verticalFieldsQuery.isLoading) {
    return (
      <section className="mb-4 rounded-md border p-4 text-sm text-muted-foreground">
        <Trans>Loading details...</Trans>
      </section>
    );
  }

  if (!verticalFields || verticalFields.vertical === null || verticalFields.vertical === NerovaVertical.Other) {
    return null;
  }

  const saveVerticalField = (field: EditableField, rawValue: string) => {
    const value = normalizeFieldValue(field, rawValue);
    if (value === (field.value ?? null)) return;

    verticalMutation.mutate(
      { params: { path: { id: client.id } }, body: { id: client.id, fields: { [field.key]: value } } },
      {
        onSuccess: async () => {
          toast.success(t`Details updated`);
          await invalidateClientFieldQueries(queryClient);
        }
      }
    );
  };

  const saveSensitiveField = (field: EditableField, rawValue: string) => {
    const value = normalizeFieldValue(field, rawValue);
    const originalValue = sensitiveFieldsQuery.data?.fields[field.key] ?? null;
    if (value === originalValue) return;

    sensitiveMutation.mutate(
      { params: { path: { id: client.id } }, body: { id: client.id, fields: { [field.key]: value } } },
      {
        onSuccess: async () => {
          toast.success(t`Sensitive details updated`);
          await invalidateClientFieldQueries(queryClient);
        }
      }
    );
  };

  return (
    <section className="mb-4 rounded-md border p-4">
      <div className="mb-4">
        <h3 className="text-sm font-medium">
          <Trans>Details</Trans>
        </h3>
        <p className="mt-1 text-xs text-muted-foreground">
          <Trans>Optional client notes shaped for this business.</Trans>
        </p>
      </div>

      <FieldErrorSummary error={verticalMutation.error ?? sensitiveMutation.error} />

      <div className="space-y-5">
        {constraintFields.length > 0 && (
          <FieldGroup title={t`Service constraints`} tone="warning">
            {constraintFields.map((field) => (
              <VerticalFieldEditor
                key={field.key}
                field={field}
                value={draftValues[field.key] ?? ""}
                isPending={verticalMutation.isPending}
                onChange={(value) => setDraftValues((current) => ({ ...current, [field.key]: value }))}
                onSave={(value) => saveVerticalField(field, value)}
              />
            ))}
          </FieldGroup>
        )}

        {standardFields.length > 0 && (
          <FieldGroup title={t`Client details`}>
            {standardFields.map((field) => (
              <VerticalFieldEditor
                key={field.key}
                field={field}
                value={draftValues[field.key] ?? ""}
                isPending={verticalMutation.isPending}
                onChange={(value) => setDraftValues((current) => ({ ...current, [field.key]: value }))}
                onSave={(value) => saveVerticalField(field, value)}
              />
            ))}
          </FieldGroup>
        )}

        {hasSensitiveFields && !hideSensitiveSection && (
          <SensitiveFieldsSection
            fields={sensitiveDefinitions}
            values={draftValues}
            keysWithValues={verticalFields.sensitiveFieldKeysWithValues}
            isExpanded={isSensitiveExpanded}
            isLoading={sensitiveFieldsQuery.isLoading}
            isReady={!!sensitiveFieldsQuery.data}
            isPending={sensitiveMutation.isPending}
            onExpand={() => setIsSensitiveExpanded(true)}
            onChange={(key, value) => setDraftValues((current) => ({ ...current, [key]: value }))}
            onSave={saveSensitiveField}
          />
        )}
      </div>
    </section>
  );
}

async function invalidateClientFieldQueries(queryClient: ReturnType<typeof useQueryClient>) {
  await queryClient.invalidateQueries({
    predicate: (query) => {
      const key = query.queryKey;
      return (
        Array.isArray(key) && key[0] === "get" && typeof key[1] === "string" && key[1].startsWith("/api/main/clients")
      );
    }
  });
}
