import type { FileUploadMutation } from "@repo/ui/types/FileUpload";

import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { enhancedFetch } from "@repo/infrastructure/http/httpClient";
import { Button } from "@repo/ui/components/Button";
import { Form } from "@repo/ui/components/Form";
import { Link } from "@repo/ui/components/Link";
import { Logo } from "@repo/ui/components/Logo";
import { Skeleton } from "@repo/ui/components/Skeleton";
import { mutationSubmitter } from "@repo/ui/forms/mutationSubmitter";
import { useMutation } from "@tanstack/react-query";
import { useState } from "react";

import { AccountFields } from "@/shared/components/AccountFields";
import { api, type Schemas } from "@/shared/lib/api/client";

import { toVerticalValue, VerticalPicker, type NerovaVerticalValue } from "./VerticalPicker";

interface AccountSetupFormProps {
  onComplete: () => void;
}

export function AccountSetupForm({ onComplete }: AccountSetupFormProps) {
  const [selectedLogoFile, setSelectedLogoFile] = useState<File | null>(null);
  const [selectedVertical, setSelectedVertical] = useState<NerovaVerticalValue | null>(null);

  const { data: tenant, isLoading } = api.useQuery("get", "/api/account/tenants/current");

  const updateTenantMutation = api.useMutation("put", "/api/account/tenants/current");
  const updateTenantLogoMutation = api.useMutation("post", "/api/account/tenants/current/update-logo");
  const updateTenantVerticalMutation = api.useMutation("put", "/api/account/tenants/current/vertical");

  const saveMutation = useMutation<void, Schemas["HttpValidationProblemDetails"], { body: { name: string } }>({
    mutationFn: async (data) => {
      const vertical = selectedVertical ?? toVerticalValue(tenant?.vertical) ?? null;

      // Upload logo if selected
      if (selectedLogoFile) {
        const logoFormData = new FormData();
        logoFormData.append("file", selectedLogoFile);
        await (updateTenantLogoMutation as unknown as FileUploadMutation).mutateAsync({ body: logoFormData });
      }

      // Update tenant name
      await updateTenantMutation.mutateAsync({ body: data.body });

      if (vertical) {
        await updateTenantVerticalMutation.mutateAsync({ body: { vertical: vertical as Schemas["NerovaVertical"] } });
        // The main-SCS scheduling profile is created lazily on first scheduling use, so it may not exist
        // yet during onboarding. The account vertical above is the source of truth; mirroring it to the
        // scheduling profile is best-effort and must never block the welcome flow.
        try {
          const verticalResponse = await enhancedFetch("/api/scheduling/profile/vertical", {
            method: "PUT",
            headers: { "content-type": "application/json" },
            body: JSON.stringify({ vertical })
          });
          if (!verticalResponse.ok && verticalResponse.status !== 404) {
            console.warn(`Could not mirror the vertical to the scheduling profile (${verticalResponse.status}).`);
          }
        } catch (error) {
          console.warn("Could not mirror the vertical to the scheduling profile.", error);
        }
      }
    },
    onSuccess: () => {
      onComplete();
    }
  });

  const isPending = saveMutation.isPending;

  return (
    <Form
      onSubmit={mutationSubmitter(saveMutation)}
      validationErrors={saveMutation.error?.errors}
      validationBehavior="aria"
      className="flex w-full max-w-[25rem] flex-col items-center gap-4"
    >
      <Link href="/" className="cursor-pointer">
        <Logo variant="mark" className="size-12" alt={t`Logo`} />
      </Link>
      <h2>
        <Trans>Let's set up your account</Trans>
      </h2>
      <div className="text-center text-sm text-muted-foreground">
        <Trans>Add your account name and logo.</Trans>
      </div>

      {isLoading ? (
        <div className="flex w-full flex-col gap-4">
          <div className="flex items-start gap-4">
            <Skeleton className="size-16 rounded-md" />
            <Skeleton className="h-16 flex-1" />
          </div>
          <Skeleton className="mt-4 h-[var(--control-height)] w-full" />
        </div>
      ) : (
        <>
          <AccountFields
            autoFocus={true}
            tenant={tenant}
            isPending={isPending}
            onLogoFileSelect={setSelectedLogoFile}
          />
          <VerticalPicker
            selectedVertical={selectedVertical ?? toVerticalValue(tenant?.vertical) ?? null}
            isPending={isPending}
            onSelect={setSelectedVertical}
          />

          <Button type="submit" isPending={isPending} className="mt-4 w-full">
            {isPending ? <Trans>Saving...</Trans> : <Trans>Continue</Trans>}
          </Button>
        </>
      )}
    </Form>
  );
}
