/* eslint-disable max-lines-per-function */
import type { FileUploadMutation } from "@repo/ui/types/FileUpload";

import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Button } from "@repo/ui/components/Button";
import { Form } from "@repo/ui/components/Form";
import { SelectContent, SelectItem, SelectTrigger, SelectValue } from "@repo/ui/components/Select";
import { SelectField } from "@repo/ui/components/SelectField";
import { Separator } from "@repo/ui/components/Separator";
import { TextAreaField } from "@repo/ui/components/TextAreaField";
import { TextField } from "@repo/ui/components/TextField";
import { Tooltip, TooltipContent, TooltipTrigger } from "@repo/ui/components/Tooltip";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { Link } from "@tanstack/react-router";
import { PlusIcon, Trash2Icon } from "lucide-react";
import { useState, useEffect } from "react";
import { toast } from "sonner";

import { TenantLogoPicker } from "@/shared/components/TenantLogoPicker";
import { api, MetaBusinessVertical, SubscriptionPlan, UserRole, type Schemas } from "@/shared/lib/api/client";

// Tier helpers
function getMaxWebsites(plan: SubscriptionPlan | null | undefined): number {
  if (plan === SubscriptionPlan.Standard || plan === SubscriptionPlan.Premium) return 2;
  return 1;
}

function isPaidPlan(plan: SubscriptionPlan | null | undefined): boolean {
  return plan != null;
}

type BrandFormValues = {
  businessDisplayName: string;
  brandAboutText: string;
  brandDescription: string;
  brandEmail: string;
  brandAddress: string;
  brandVertical: MetaBusinessVertical | null;
  brandWebsites: string[];
};

const VERTICAL_LABELS: Record<MetaBusinessVertical, string> = {
  [MetaBusinessVertical.Beauty]: t`Beauty`,
  [MetaBusinessVertical.Education]: t`Education`,
  [MetaBusinessVertical.Health]: t`Health`,
  [MetaBusinessVertical.ProfessionalServices]: t`Professional services`,
  [MetaBusinessVertical.Retail]: t`Retail`,
  [MetaBusinessVertical.Restaurant]: t`Restaurant`,
  [MetaBusinessVertical.Travel]: t`Travel`,
  [MetaBusinessVertical.Other]: t`Other`
};

export function BrandProfileTab() {
  const queryClient = useQueryClient();
  const { data: tenant, isLoading: tenantLoading } = api.useQuery("get", "/api/account/tenants/current");
  const { data: currentUser, isLoading: userLoading } = api.useQuery("get", "/api/account/users/me");
  const { data: subscription } = api.useQuery("get", "/api/account/subscriptions/current");

  const [selectedLogoFile, setSelectedLogoFile] = useState<File | null>(null);

  const updateTenantLogoMutation = api.useMutation("post", "/api/account/tenants/current/update-logo");

  const isOwner = currentUser?.role === UserRole.Owner;
  const currentPlan = subscription?.plan ?? null;
  const hasPaidPlan = isPaidPlan(currentPlan);
  const maxWebsites = getMaxWebsites(currentPlan);

  const [formValues, setFormValues] = useState<BrandFormValues>({
    businessDisplayName: "",
    brandAboutText: "",
    brandDescription: "",
    brandEmail: "",
    brandAddress: "",
    brandVertical: null,
    brandWebsites: [""]
  });

  useEffect(() => {
    if (tenant) {
      setFormValues((prev) => ({
        ...prev,
        businessDisplayName: tenant.name
      }));
    }
  }, [tenant]);

  /**
   * TODO: Replace with real API mutation once backend wires up UpdateTenantBrandProfileCommand.
   */
  const saveBrandProfileMutation = useMutation<void, Schemas["HttpValidationProblemDetails"], BrandFormValues>({
    mutationFn: async (values) => {
      if (selectedLogoFile) {
        const formData = new FormData();
        formData.append("file", selectedLogoFile);
        await (updateTenantLogoMutation as unknown as FileUploadMutation).mutateAsync({ body: formData });
      }
      await Promise.resolve(values);
    },
    onSuccess: async () => {
      setSelectedLogoFile(null);
      await queryClient.invalidateQueries();
      toast.success(t`Brand profile updated successfully`);
    }
  });

  const handleLogoFileSelect = (file: File | null) => {
    setSelectedLogoFile(file);
  };

  const handleWebsiteChange = (index: number, value: string) => {
    setFormValues((prev) => {
      const updated = [...prev.brandWebsites];
      updated[index] = value;
      return { ...prev, brandWebsites: updated };
    });
  };

  const handleAddWebsite = () => {
    setFormValues((prev) => ({
      ...prev,
      brandWebsites: [...prev.brandWebsites, ""]
    }));
  };

  const handleRemoveWebsite = (index: number) => {
    setFormValues((prev) => ({
      ...prev,
      brandWebsites: prev.brandWebsites.filter((_, i) => i !== index)
    }));
  };

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (isOwner) {
      saveBrandProfileMutation.mutate(formValues);
    }
  };

  if (tenantLoading || userLoading) {
    return null;
  }

  const readOnly = !isOwner;

  return (
    <Form onSubmit={handleSubmit} validationBehavior="aria" className="flex flex-col gap-8 pt-4">
        {/* Section A: Business identity */}
        <div className="flex flex-col gap-4">
          <div>
            <h3 className="text-base font-semibold">
              <Trans>Business identity</Trans>
            </h3>
            <p className="text-sm text-muted-foreground">
              <Trans>Basic information shown on your WhatsApp profile.</Trans>
            </p>
          </div>
          <Separator />

          <TextField
            name="businessDisplayName"
            label={t`Business display name`}
            value={formValues.businessDisplayName}
            onChange={(v) => setFormValues((prev) => ({ ...prev, businessDisplayName: v }))}
            required={true}
            readOnly={readOnly}
            description={t`The name shown on your WhatsApp Business profile`}
          />

          <TierGatedField plan={currentPlan}>
            <TextAreaField
              name="brandAboutText"
              label={t`About / tagline`}
              value={formValues.brandAboutText}
              onChange={(v) => setFormValues((prev) => ({ ...prev, brandAboutText: v }))}
              readOnly={readOnly || !hasPaidPlan}
              disabled={!hasPaidPlan}
              description={t`A short tagline shown on your WhatsApp profile (max 139 characters)`}
              maxLength={139}
              lines={2}
            />
          </TierGatedField>

          <TierGatedField plan={currentPlan}>
            <TextAreaField
              name="brandDescription"
              label={t`Business description`}
              value={formValues.brandDescription}
              onChange={(v) => setFormValues((prev) => ({ ...prev, brandDescription: v }))}
              readOnly={readOnly || !hasPaidPlan}
              disabled={!hasPaidPlan}
              description={t`A longer description of your business`}
              lines={3}
            />
          </TierGatedField>

          <TierGatedField plan={currentPlan}>
            <TextField
              name="brandEmail"
              label={t`Business email`}
              type="email"
              value={formValues.brandEmail}
              onChange={(v) => setFormValues((prev) => ({ ...prev, brandEmail: v }))}
              readOnly={readOnly || !hasPaidPlan}
              disabled={!hasPaidPlan}
            />
          </TierGatedField>

          <TierGatedField plan={currentPlan}>
            <TextField
              name="brandAddress"
              label={t`Business address`}
              value={formValues.brandAddress}
              onChange={(v) => setFormValues((prev) => ({ ...prev, brandAddress: v }))}
              readOnly={readOnly || !hasPaidPlan}
              disabled={!hasPaidPlan}
            />
          </TierGatedField>
        </div>

        {/* Section B: Logo */}
        <div className="flex flex-col gap-4">
          <div>
            <h3 className="text-base font-semibold">
              <Trans>Profile logo</Trans>
            </h3>
            <p className="text-sm text-muted-foreground">
              <Trans>Your business logo shown on WhatsApp.</Trans>
            </p>
          </div>
          <Separator />

          <TierGatedField plan={currentPlan}>
            <TenantLogoPicker
              logoUrl={tenant?.logoUrl}
              tenantName={tenant?.name ?? ""}
              isPending={saveBrandProfileMutation.isPending}
              readOnly={readOnly || !hasPaidPlan}
              size="lg"
              onFileSelect={handleLogoFileSelect}
              onRemove={() => {
                setSelectedLogoFile(null);
              }}
            />
          </TierGatedField>
        </div>

        {/* Section C: Business vertical */}
        <div className="flex flex-col gap-4">
          <div>
            <h3 className="text-base font-semibold">
              <Trans>Business category</Trans>
            </h3>
            <p className="text-sm text-muted-foreground">
              <Trans>Select the category that best describes your business.</Trans>
            </p>
          </div>
          <Separator />

          <SelectField
            name="brandVertical"
            label={t`Business category`}
            value={formValues.brandVertical}
            onValueChange={(v) =>
              setFormValues((prev) => ({ ...prev, brandVertical: v as MetaBusinessVertical | null }))
            }
            disabled={readOnly}
          >
            <SelectTrigger>
              <SelectValue>
                {(v: MetaBusinessVertical | null) =>
                  v ? VERTICAL_LABELS[v] : <span className="text-muted-foreground">{t`Select category`}</span>
                }
              </SelectValue>
            </SelectTrigger>
            <SelectContent>
              {Object.values(MetaBusinessVertical).map((v) => (
                <SelectItem key={v} value={v}>
                  {VERTICAL_LABELS[v]}
                </SelectItem>
              ))}
            </SelectContent>
          </SelectField>
        </div>

        {/* Section D: Websites */}
        <div className="flex flex-col gap-4">
          <div>
            <h3 className="text-base font-semibold">
              <Trans>Websites</Trans>
            </h3>
            <p className="text-sm text-muted-foreground">
              {maxWebsites === 1 ? (
                <Trans>URLs shown on your WhatsApp profile. Your plan supports 1 website.</Trans>
              ) : (
                <Trans>URLs shown on your WhatsApp profile. Your plan supports up to 2 websites.</Trans>
              )}
            </p>
          </div>
          <Separator />

          <div className="flex flex-col gap-2">
            {formValues.brandWebsites.map((url, index) => (
              <div key={index} className="flex items-end gap-2">
                <TextField
                  className="flex-1"
                  name={`brandWebsites[${index}]`}
                  label={index === 0 ? t`Website URL` : undefined}
                  type="url"
                  value={url}
                  onChange={(v) => handleWebsiteChange(index, v)}
                  readOnly={readOnly}
                  placeholder="https://"
                />
                {index > 0 && !readOnly && (
                  <Button
                    type="button"
                    variant="ghost"
                    size="icon"
                    aria-label={t`Remove website`}
                    onClick={() => handleRemoveWebsite(index)}
                  >
                    <Trash2Icon />
                  </Button>
                )}
              </div>
            ))}

            {!readOnly && formValues.brandWebsites.length < maxWebsites && (
              <Button type="button" variant="outline" size="sm" onClick={handleAddWebsite} className="w-fit">
                <PlusIcon />
                <Trans>Add website</Trans>
              </Button>
            )}

            {!readOnly && maxWebsites < 2 && (
              <p className="text-sm text-muted-foreground">
                <Trans>
                  <Link to="/account/billing" className="text-primary underline underline-offset-4">
                    Upgrade your plan
                  </Link>{" "}
                  to add a second website.
                </Trans>
              </p>
            )}
          </div>
        </div>

        {/* Save button */}
        {isOwner && (
          <div className="mt-4 flex justify-end">
            <Button type="submit" isPending={saveBrandProfileMutation.isPending}>
              {saveBrandProfileMutation.isPending ? <Trans>Saving...</Trans> : <Trans>Save changes</Trans>}
            </Button>
          </div>
        )}
      </Form>
  );
}

function TierGatedField({
  plan,
  children
}: Readonly<{
  plan: SubscriptionPlan | null | undefined;
  children: React.ReactNode;
}>) {
  if (isPaidPlan(plan)) {
    return <>{children}</>;
  }

  return (
    <Tooltip>
      <TooltipTrigger render={<div className="cursor-not-allowed opacity-60" />}>{children}</TooltipTrigger>
      <TooltipContent>
        <Trans>
          <Link to="/account/billing" className="text-primary underline underline-offset-4">
            Upgrade your plan
          </Link>{" "}
          to unlock this feature.
        </Trans>
      </TooltipContent>
    </Tooltip>
  );
}
