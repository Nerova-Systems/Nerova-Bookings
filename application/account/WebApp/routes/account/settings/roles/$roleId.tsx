import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { isFeatureFlagEnabled } from "@repo/infrastructure/featureFlags/useFeatureFlag";
import { AppLayout } from "@repo/ui/components/AppLayout";
import { Badge } from "@repo/ui/components/Badge";
import { useQueryClient } from "@tanstack/react-query";
import { createFileRoute, redirect, useNavigate } from "@tanstack/react-router";
import { toast } from "sonner";

import { api } from "@/shared/lib/api/client";

import { RoleForm } from "./-components/RoleForm";

export const Route = createFileRoute("/account/settings/roles/$roleId")({
  beforeLoad: () => {
    if (!isFeatureFlagEnabled("tier-enterprise")) {
      throw redirect({ to: "/account/settings" });
    }
  },
  staticData: { trackingTitle: "Edit role" },
  component: EditRolePage
});

function EditRolePage() {
  const { roleId } = Route.useParams();
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  const { data: role, isLoading } = api.useQuery("get", "/api/account/roles/{id}", {
    params: { path: { id: roleId } }
  });

  const updateRoleMutation = api.useMutation("put", "/api/account/roles/{id}", {
    meta: { skipQueryInvalidation: true }
  });

  const handleSubmit = async (values: { name: string; description: string | null; permissions: string[] }) => {
    await updateRoleMutation.mutateAsync(
      {
        params: { path: { id: roleId } },
        body: {
          name: values.name,
          description: values.description,
          permissions: values.permissions
        }
      },
      {
        onSuccess: async (updated) => {
          await queryClient.invalidateQueries({
            predicate: (query) => Array.isArray(query.queryKey) && query.queryKey[1] === "/api/account/roles"
          });
          toast.success(t`Role updated: ${updated.name}`);
          navigate({ to: "/account/settings/roles" });
        }
      }
    );
  };

  if (isLoading || !role) {
    return null;
  }

  return (
    <AppLayout
      variant="center"
      maxWidth="64rem"
      title={role.name}
      subtitle={
        role.isSystem
          ? t`System roles cannot be edited. The permission set is fixed.`
          : t`Update the role's permissions or description.`
      }
    >
      <div className="mb-4 flex items-center gap-2 text-sm text-muted-foreground">
        <Badge variant="outline">
          <Trans>{role.memberCount} members assigned</Trans>
        </Badge>
        {role.isSystem && (
          <Badge variant="secondary">
            <Trans>System</Trans>
          </Badge>
        )}
      </div>

      <RoleForm
        initialRole={role}
        submitLabel={updateRoleMutation.isPending ? t`Saving...` : t`Save changes`}
        isPending={updateRoleMutation.isPending}
        errors={updateRoleMutation.error?.errors}
        onSubmit={handleSubmit}
        onCancel={() => navigate({ to: "/account/settings/roles" })}
      />
      {updateRoleMutation.error && !updateRoleMutation.error.errors && (
        <p className="mt-2 text-sm text-destructive">
          <Trans>Failed to update role. Please try again.</Trans>
        </p>
      )}
    </AppLayout>
  );
}
