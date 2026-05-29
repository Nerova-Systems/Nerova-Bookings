import type { FileUploadMutation } from "@repo/ui/types/FileUpload";

import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { useFeatureFlag } from "@repo/infrastructure/featureFlags/useFeatureFlag";
import { AppLayout } from "@repo/ui/components/AppLayout";
import { Button } from "@repo/ui/components/Button";
import { Form } from "@repo/ui/components/Form";
import { Separator } from "@repo/ui/components/Separator";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@repo/ui/components/Tabs";
import { mutationSubmitter } from "@repo/ui/forms/mutationSubmitter";
import { useUnsavedChangesGuard } from "@repo/ui/hooks/useUnsavedChangesGuard";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { createFileRoute } from "@tanstack/react-router";
import { Trash2 } from "lucide-react";
import { useState } from "react";
import { toast } from "sonner";

import { AccountFields } from "@/shared/components/AccountFields";
import { UnsavedChangesDialog } from "@/shared/components/UnsavedChangesDialog";
import { api, type Schemas, UserRole } from "@/shared/lib/api/client";

import { AccountInfoFields } from "./-components/AccountInfoFields";
import { BrandProfileTab } from "./-components/BrandProfileTab";
import DeleteAccountConfirmation from "./-components/DeleteAccountConfirmation";
import { FeaturesSection } from "./-components/FeaturesSection";
import { OrgSmtpTab } from "./organization/-components/SmtpTab";
import { OrgSsoTab } from "./organization/-components/SsoTab";

export const Route = createFileRoute("/account/settings/")({
  staticData: { trackingTitle: "Account settings" },
  component: AccountSettings
});

// Danger zone component
function DangerZone({ setIsDeleteModalOpen }: Readonly<{ setIsDeleteModalOpen: (open: boolean) => void }>) {
  return (
    <div className="mt-12 flex flex-col gap-4">
      <h3>
        <Trans>Danger zone</Trans>
      </h3>
      <Separator />
      <div className="flex flex-col gap-4">
        <p className="text-sm">
          <Trans>Delete your account and all data. This action is irreversible—proceed with caution.</Trans>
        </p>

        <Button variant="destructive" onClick={() => setIsDeleteModalOpen(true)}>
          <Trash2 />
          <Trans>Delete account</Trans>
        </Button>
      </div>
    </div>
  );
}

export function AccountSettings() {
  const [isDeleteModalOpen, setIsDeleteModalOpen] = useState(false);
  const [selectedLogoFile, setSelectedLogoFile] = useState<File | null>(null);
  const [removeLogoFlag, setRemoveLogoFlag] = useState(false);
  const [isFormDirty, setIsFormDirty] = useState(false);
  const queryClient = useQueryClient();

  const { enabled: isSsoEnabled } = useFeatureFlag("sso");
  const { enabled: isSmtpEnabled } = useFeatureFlag("cap-custom-smtp");

  const { data: tenant, isLoading: tenantLoading } = api.useQuery("get", "/api/account/tenants/current");
  const { data: currentUser, isLoading: userLoading } = api.useQuery("get", "/api/account/users/me");
  const updateCurrentTenantMutation = api.useMutation("put", "/api/account/tenants/current");
  const updateTenantLogoMutation = api.useMutation("post", "/api/account/tenants/current/update-logo");
  const removeTenantLogoMutation = api.useMutation("delete", "/api/account/tenants/current/remove-logo");

  const isOwner = currentUser?.role === UserRole.Owner;
  const canManage = currentUser?.role === UserRole.Owner || currentUser?.role === UserRole.Admin;

  const saveMutation = useMutation<
    void,
    Schemas["HttpValidationProblemDetails"],
    { body: Schemas["UpdateCurrentTenantCommand"] }
  >({
    mutationFn: async (data) => {
      if (selectedLogoFile) {
        const formData = new FormData();
        formData.append("file", selectedLogoFile);
        await (updateTenantLogoMutation as unknown as FileUploadMutation).mutateAsync({ body: formData });
      } else if (removeLogoFlag) {
        await removeTenantLogoMutation.mutateAsync({});
      }

      await updateCurrentTenantMutation.mutateAsync(data);
      await queryClient.invalidateQueries();
      window.dispatchEvent(new CustomEvent("tenant-updated"));
    },
    onSuccess: () => {
      setSelectedLogoFile(null);
      setRemoveLogoFlag(false);
      setIsFormDirty(false);
      toast.success(t`Account settings updated successfully`);
    }
  });

  const handleLogoFileSelect = (file: File | null) => {
    setSelectedLogoFile(file);
    setRemoveLogoFlag(false);
    setIsFormDirty(true);
  };

  const handleLogoRemove = () => {
    setRemoveLogoFlag(true);
    setIsFormDirty(true);
  };

  const { isConfirmDialogOpen, confirmLeave, cancelLeave } = useUnsavedChangesGuard({
    hasUnsavedChanges: isFormDirty && isOwner
  });

  if (tenantLoading || userLoading || !tenant) {
    return null;
  }

  return (
    <>
      <AppLayout
        variant="center"
        maxWidth="64rem"
        balanceWidth="16rem"
        title={tenant.name}
        subtitle={t`Account settings`}
      >
        <Tabs defaultValue="profile">
          <TabsList className="mb-4">
            <TabsTrigger value="profile">
              <Trans>Profile</Trans>
            </TabsTrigger>
            <TabsTrigger value="brand">
              <Trans>WhatsApp Brand</Trans>
            </TabsTrigger>
            {isSsoEnabled && (
              <TabsTrigger value="sso">
                <Trans>SSO</Trans>
              </TabsTrigger>
            )}
            {isSmtpEnabled && (
              <TabsTrigger value="smtp">
                <Trans>SMTP</Trans>
              </TabsTrigger>
            )}
          </TabsList>

          <TabsContent value="profile">
            <Form
              onSubmit={isOwner ? mutationSubmitter(saveMutation) : undefined}
              validationErrors={isOwner ? saveMutation.error?.errors : undefined}
              validationBehavior="aria"
              className="flex flex-col gap-4 pt-4"
              onChange={() => setIsFormDirty(true)}
            >
              <AccountFields
                layout="horizontal"
                tenant={tenant}
                isPending={saveMutation.isPending}
                onLogoFileSelect={handleLogoFileSelect}
                onLogoRemove={handleLogoRemove}
                readOnly={!isOwner}
                tooltip={isOwner ? t`The name of your account, shown to users and in email notifications` : undefined}
                description={!isOwner ? t`Only account owners can modify the account name` : undefined}
                onChange={() => setIsFormDirty(true)}
                infoFields={<AccountInfoFields tenant={tenant} />}
              />

              {isOwner && (
                <div className="mt-4 md:grid md:grid-cols-[8.5rem_1fr] md:gap-8">
                  <div />
                  <div className="flex sm:justify-end">
                    <Button type="submit" isPending={saveMutation.isPending}>
                      {saveMutation.isPending ? <Trans>Saving...</Trans> : <Trans>Save changes</Trans>}
                    </Button>
                  </div>
                </div>
              )}
            </Form>

            {isOwner && <FeaturesSection />}
            {isOwner && <DangerZone setIsDeleteModalOpen={setIsDeleteModalOpen} />}
          </TabsContent>

          <TabsContent value="brand">
            <BrandProfileTab />
          </TabsContent>

          {isSsoEnabled && (
            <TabsContent value="sso">
              <OrgSsoTab canManage={canManage} />
            </TabsContent>
          )}

          {isSmtpEnabled && (
            <TabsContent value="smtp">
              <OrgSmtpTab canManage={canManage} />
            </TabsContent>
          )}
        </Tabs>
      </AppLayout>

      <DeleteAccountConfirmation isOpen={isDeleteModalOpen} onOpenChange={setIsDeleteModalOpen} />

      <UnsavedChangesDialog
        isOpen={isConfirmDialogOpen}
        onConfirmLeave={confirmLeave}
        onCancel={cancelLeave}
        parentTrackingTitle="Account settings"
      />
    </>
  );
}
