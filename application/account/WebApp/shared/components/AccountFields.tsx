import type { ReactNode } from "react";

import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { TextField } from "@repo/ui/components/TextField";

import type { Schemas } from "@/shared/lib/api/client";

import { TenantLogoPicker } from "./TenantLogoPicker";

export interface AccountFieldsProps {
  tenant: Schemas["TenantResponse"] | undefined;
  isPending: boolean;
  onLogoFileSelect: (file: File | null) => void;
  onLogoRemove?: () => void;
  readOnly?: boolean;
  tooltip?: string;
  description?: string;
  autoFocus?: boolean;
  onChange?: () => void;
  layout?: "stacked" | "horizontal";
  infoFields?: ReactNode;
}

export function AccountFields({
  tenant,
  isPending,
  onLogoFileSelect,
  onLogoRemove,
  readOnly,
  autoFocus,
  tooltip,
  description,
  onChange,
  layout = "stacked",
  infoFields
}: AccountFieldsProps) {
  const logoSection = (
    <TenantLogoPicker
      logoUrl={tenant?.logoUrl}
      tenantName={tenant?.name ?? ""}
      isPending={isPending}
      readOnly={readOnly}
      size={layout === "horizontal" ? "lg" : "base"}
      onFileSelect={onLogoFileSelect}
      onRemove={onLogoRemove}
    />
  );

  const fieldsSection = (
    <TextField
      autoFocus={autoFocus}
      required={true}
      name="name"
      defaultValue={tenant?.name ?? ""}
      disabled={isPending}
      readOnly={readOnly}
      label={t`Account name`}
      placeholder={t`E.g. Acme Corp`}
      tooltip={tooltip}
      description={description}
      onChange={onChange}
    />
  );

  if (layout === "horizontal") {
    return (
      <div className="mt-8 flex flex-col gap-6 md:grid md:grid-cols-[8.5rem_1fr] md:items-stretch md:gap-8">
        <div className="flex flex-col md:items-stretch">
          <span className="pb-2.75 text-sm font-medium">
            <Trans>Account logo</Trans>
          </span>
          {logoSection}
        </div>
        <div className="flex flex-col gap-4">
          {fieldsSection}
          {infoFields}
        </div>
      </div>
    );
  }

  return (
    <>
      <div className="flex justify-center pb-4">{logoSection}</div>
      {fieldsSection}
    </>
  );
}
