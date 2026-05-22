import type { FileUploadMutation } from "@repo/ui/types/FileUpload";

import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Avatar, AvatarFallback, AvatarImage } from "@repo/ui/components/Avatar";
import { Button } from "@repo/ui/components/Button";
import { Form } from "@repo/ui/components/Form";
import { Input } from "@repo/ui/components/Input";
import { Label } from "@repo/ui/components/Label";
import { Section } from "@repo/ui/components/Section";
import { useQueryClient } from "@tanstack/react-query";
import { useRef, useState } from "react";
import { toast } from "sonner";

import { api, type Schemas } from "@/shared/lib/api/client";

type TenantResponse = Schemas["TenantResponse"];

interface OrgProfileTabProps {
  tenant: TenantResponse;
  canManage: boolean;
}

export function OrgProfileTab({ tenant, canManage }: Readonly<OrgProfileTabProps>) {
  const queryClient = useQueryClient();
  const [name, setName] = useState(tenant.name);
  const fileInputRef = useRef<HTMLInputElement>(null);

  const updateMutation = api.useMutation("put", "/api/account/tenants/current", {
    meta: { skipQueryInvalidation: true }
  });
  const uploadLogoMutation = api.useMutation("post", "/api/account/tenants/current/update-logo");
  const removeLogoMutation = api.useMutation("delete", "/api/account/tenants/current/remove-logo");

  const invalidateTenant = async () => {
    await queryClient.invalidateQueries({
      predicate: (query) => Array.isArray(query.queryKey) && query.queryKey[1] === "/api/account/tenants/current"
    });
    window.dispatchEvent(new CustomEvent("tenant-updated"));
  };

  const handleSubmit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    await updateMutation.mutateAsync(
      { body: { name: name.trim() } },
      {
        onSuccess: async () => {
          await invalidateTenant();
          toast.success(t`Organization profile updated`);
        }
      }
    );
  };

  const handleLogoSelect = async (event: React.ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    if (!file) {
      return;
    }
    const formData = new FormData();
    formData.append("file", file);
    await (uploadLogoMutation as unknown as FileUploadMutation).mutateAsync({ body: formData });
    await invalidateTenant();
    toast.success(t`Logo updated`);
    if (fileInputRef.current) {
      fileInputRef.current.value = "";
    }
  };

  const handleRemoveLogo = async () => {
    await removeLogoMutation.mutateAsync(
      {},
      {
        onSuccess: async () => {
          await invalidateTenant();
          toast.success(t`Logo removed`);
        }
      }
    );
  };

  const disabled = !canManage || updateMutation.isPending;

  return (
    <div className="flex flex-col gap-6">
      <Section
        title={t`Logo`}
        description={t`Shown across booking pages and email notifications for this organization.`}
      >
        <div className="flex items-center gap-4">
          <Avatar className="size-16">
            {tenant.logoUrl && <AvatarImage src={tenant.logoUrl} alt={tenant.name} />}
            <AvatarFallback>{tenant.name.charAt(0).toUpperCase()}</AvatarFallback>
          </Avatar>
          {canManage && (
            <div className="flex gap-2">
              <input
                ref={fileInputRef}
                type="file"
                accept="image/png,image/jpeg,image/gif,image/webp"
                className="hidden"
                onChange={handleLogoSelect}
                aria-label={t`Upload logo`}
              />
              <Button
                type="button"
                variant="secondary"
                onClick={() => fileInputRef.current?.click()}
                isPending={uploadLogoMutation.isPending}
              >
                <Trans>Upload</Trans>
              </Button>
              {tenant.logoUrl && (
                <Button
                  type="button"
                  variant="ghost"
                  onClick={handleRemoveLogo}
                  isPending={removeLogoMutation.isPending}
                >
                  <Trans>Remove</Trans>
                </Button>
              )}
            </div>
          )}
        </div>
      </Section>

      <Form onSubmit={handleSubmit} validationErrors={updateMutation.error?.errors} className="flex flex-col gap-6">
        <Section
          title={t`General`}
          description={t`The organization name appears throughout the product and in outgoing emails.`}
        >
          <div className="flex flex-col gap-2">
            <Label htmlFor="org-name">
              <Trans>Name</Trans>
            </Label>
            <Input
              id="org-name"
              name="name"
              value={name}
              onChange={(e) => setName(e.target.value)}
              disabled={disabled}
              required={true}
              aria-label={t`Organization name`}
            />
          </div>
        </Section>

        {canManage && (
          <div className="flex justify-end">
            <Button type="submit" isPending={updateMutation.isPending}>
              {updateMutation.isPending ? <Trans>Saving...</Trans> : <Trans>Save changes</Trans>}
            </Button>
          </div>
        )}
      </Form>

      {/* TODO(u4-org-settings): slug, banner and bio fields are scoped out — backend
          UpdateCurrentTenantCommand currently only accepts `name`. Re-enable when the
          backend exposes those columns. */}
    </div>
  );
}
