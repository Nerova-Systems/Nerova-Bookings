import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { isFeatureFlagEnabled } from "@repo/infrastructure/featureFlags/useFeatureFlag";
import { AppLayout } from "@repo/ui/components/AppLayout";
import { useQueryClient } from "@tanstack/react-query";
import { createFileRoute, redirect, useNavigate } from "@tanstack/react-router";
import { toast } from "sonner";

import { api } from "@/shared/lib/api/client";

import { RoleForm } from "./-components/RoleForm";

export const Route = createFileRoute("/account/settings/roles/new")({
  beforeLoad: () => {
    if (!isFeatureFlagEnabled("tier-enterprise")) {
      throw redirect({ to: "/account/settings" });
    }
  },
  staticData: { trackingTitle: "Create role" },
  component: NewRolePage
});

function NewRolePage() {
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  const createRoleMutation = api.useMutation("post", "/api/account/roles", {
    meta: { skipQueryInvalidation: true }
  });

  const handleSubmit = async (values: { name: string; description: string | null; permissions: string[] }) => {
    await createRoleMutation.mutateAsync(
      {
        body: {
          name: values.name,
          description: values.description,
          permissions: values.permissions
        }
      },
      {
        onSuccess: async (created) => {
          await queryClient.invalidateQueries({
            predicate: (query) => Array.isArray(query.queryKey) && query.queryKey[1] === "/api/account/roles"
          });
          toast.success(t`Role created: ${created.name}`);
          navigate({ to: "/account/settings/roles" });
        }
      }
    );
  };

  return (
    <AppLayout
      variant="center"
      maxWidth="64rem"
      title={t`Create role`}
      subtitle={t`Choose a name and select the permissions this role grants.`}
    >
      <RoleForm
        submitLabel={createRoleMutation.isPending ? t`Creating...` : t`Create role`}
        isPending={createRoleMutation.isPending}
        errors={createRoleMutation.error?.errors}
        onSubmit={handleSubmit}
        onCancel={() => navigate({ to: "/account/settings/roles" })}
      />
      {createRoleMutation.error && !createRoleMutation.error.errors && (
        <p className="mt-2 text-sm text-destructive">
          <Trans>Failed to create role. Please try again.</Trans>
        </p>
      )}
    </AppLayout>
  );
}
