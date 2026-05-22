import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { useUserInfo } from "@repo/infrastructure/auth/hooks";
import { isFeatureFlagEnabled } from "@repo/infrastructure/featureFlags/useFeatureFlag";
import { AppLayout } from "@repo/ui/components/AppLayout";
import { Badge } from "@repo/ui/components/Badge";
import { Button, buttonVariants } from "@repo/ui/components/Button";
import { Empty, EmptyContent, EmptyDescription, EmptyHeader, EmptyMedia, EmptyTitle } from "@repo/ui/components/Empty";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@repo/ui/components/Table";
import { createFileRoute, Link, redirect } from "@tanstack/react-router";
import { PencilIcon, PlusIcon, ShieldCheckIcon, Trash2Icon } from "lucide-react";
import { useState } from "react";

import { api, type Schemas } from "@/shared/lib/api/client";

import { DeleteRoleDialog } from "./-components/DeleteRoleDialog";

type RoleResponse = Schemas["RoleResponse"];

export const Route = createFileRoute("/account/settings/roles/")({
  beforeLoad: () => {
    // The tier-enterprise feature flag gates the whole PBAC admin UI. The sidebar hides the entry
    // when disabled, but direct navigation must redirect rather than render an unsupported page.
    if (!isFeatureFlagEnabled("tier-enterprise")) {
      throw redirect({ to: "/account/settings" });
    }
  },
  staticData: { trackingTitle: "Roles" },
  component: RolesPage
});

function RolesPage() {
  const userInfo = useUserInfo();
  const [roleToDelete, setRoleToDelete] = useState<RoleResponse | null>(null);

  // TODO(pbac): replace this role-based gate with a usePermission(Role.Manage) hook once the
  // frontend self-gating story lands. Until then we fall back to the legacy Owner/Admin gate.
  const canManageRoles = userInfo?.role === "Owner" || userInfo?.role === "Admin";

  const { data: roles, isLoading } = api.useQuery("get", "/api/account/roles");

  return (
    <>
      <AppLayout
        variant="center"
        maxWidth="64rem"
        title={t`Roles`}
        subtitle={t`Define custom roles and assign permissions to control what your members can do.`}
      >
        {canManageRoles && (
          <div className="flex justify-end">
            <Link
              to="/account/settings/roles/new"
              className={buttonVariants({ variant: "default" })}
              aria-label={t`New role`}
            >
              <PlusIcon />
              <Trans>New role</Trans>
            </Link>
          </div>
        )}

        {isLoading ? null : !roles || roles.length === 0 ? (
          <Empty>
            <EmptyHeader>
              <EmptyMedia variant="icon">
                <ShieldCheckIcon />
              </EmptyMedia>
              <EmptyTitle>
                <Trans>No roles yet</Trans>
              </EmptyTitle>
              <EmptyDescription>
                <Trans>Create a custom role to grant members the exact permissions they need.</Trans>
              </EmptyDescription>
            </EmptyHeader>
            {canManageRoles && (
              <EmptyContent>
                <Link
                  to="/account/settings/roles/new"
                  className={buttonVariants({ variant: "default" })}
                  aria-label={t`Create a role`}
                >
                  <PlusIcon />
                  <Trans>Create a role</Trans>
                </Link>
              </EmptyContent>
            )}
          </Empty>
        ) : (
          <Table rowSize="compact">
            <TableHeader>
              <TableRow>
                <TableHead>
                  <Trans>Name</Trans>
                </TableHead>
                <TableHead>
                  <Trans>Description</Trans>
                </TableHead>
                <TableHead className="w-32">
                  <Trans>Members</Trans>
                </TableHead>
                <TableHead className="w-px text-right" />
              </TableRow>
            </TableHeader>
            <TableBody>
              {roles.map((role) => (
                <TableRow key={role.id}>
                  <TableCell>
                    <div className="flex items-center gap-2">
                      <span className="font-medium">{role.name}</span>
                      {role.isSystem && (
                        <Badge variant="secondary">
                          <Trans>System</Trans>
                        </Badge>
                      )}
                    </div>
                  </TableCell>
                  <TableCell className="text-muted-foreground">{role.description ?? "—"}</TableCell>
                  <TableCell>
                    <Badge variant="outline">{role.memberCount}</Badge>
                  </TableCell>
                  <TableCell className="text-right">
                    <div className="flex justify-end gap-1">
                      <Link
                        to="/account/settings/roles/$roleId"
                        params={{ roleId: role.id }}
                        className={buttonVariants({ variant: "ghost", size: "sm" })}
                        aria-label={t`Edit ${role.name}`}
                      >
                        <PencilIcon className="size-4" />
                      </Link>
                      {canManageRoles && !role.isSystem && (
                        <Button
                          variant="ghost"
                          size="sm"
                          onClick={() => setRoleToDelete(role)}
                          aria-label={t`Delete ${role.name}`}
                        >
                          <Trash2Icon className="size-4" />
                        </Button>
                      )}
                    </div>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}
      </AppLayout>

      <DeleteRoleDialog
        role={roleToDelete}
        isOpen={roleToDelete !== null}
        onOpenChange={(isOpen) => !isOpen && setRoleToDelete(null)}
      />
    </>
  );
}
