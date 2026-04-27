import { t } from "@lingui/core/macro";
import { TextField } from "@repo/ui/components/TextField";

import type { components } from "@/shared/lib/api/api.generated";

import { CountrySelect, type CountryOption } from "./CountrySelect";

type BillingInfo = components["schemas"]["BillingInfo"];

interface BillingInfoFormFieldsProps {
  billingInfo: BillingInfo | null | undefined;
  tenantName: string;
  defaultEmail: string;
  countries: CountryOption[];
  selectedCountry: string | undefined;
  onCountryChange: (value: string | null) => void;
  onFieldChange: () => void;
}

/**
 * Renders the billing info form fields used in EditBillingInfoDialog. All fields are persisted on
 * the Subscription aggregate (PUT /api/account/billing/billing-info) and used purely for display
 * and invoicing — PayFast does not store this data.
 */
export function BillingInfoFormFields({
  billingInfo,
  tenantName,
  defaultEmail,
  countries,
  selectedCountry,
  onCountryChange,
  onFieldChange
}: Readonly<BillingInfoFormFieldsProps>) {
  return (
    <div className="flex flex-col gap-4">
      <TextField
        autoFocus
        name="name"
        label={t`Billing name`}
        defaultValue={billingInfo?.name ?? tenantName}
        onChange={onFieldChange}
        required
      />
      <TextField
        name="email"
        type="email"
        label={t`Billing email`}
        defaultValue={billingInfo?.email ?? defaultEmail}
        onChange={onFieldChange}
        required
      />
      <TextField
        name="line1"
        label={t`Address line 1`}
        defaultValue={billingInfo?.address?.line1 ?? ""}
        onChange={onFieldChange}
        required
      />
      <TextField
        name="line2"
        label={t`Address line 2 (optional)`}
        defaultValue={billingInfo?.address?.line2 ?? ""}
        onChange={onFieldChange}
      />
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
        <TextField
          name="postalCode"
          label={t`Postal code`}
          defaultValue={billingInfo?.address?.postalCode ?? ""}
          onChange={onFieldChange}
          required
        />
        <TextField
          name="city"
          label={t`City`}
          defaultValue={billingInfo?.address?.city ?? ""}
          onChange={onFieldChange}
          required
        />
      </div>
      <TextField
        name="state"
        label={t`State / Province (optional)`}
        defaultValue={billingInfo?.address?.state ?? ""}
        onChange={onFieldChange}
      />
      <CountrySelect countries={countries} defaultValue={selectedCountry} onValueChange={onCountryChange} />
      <TextField
        name="taxId"
        label={t`Tax ID (optional)`}
        defaultValue={billingInfo?.taxId ?? ""}
        onChange={onFieldChange}
      />
    </div>
  );
}
