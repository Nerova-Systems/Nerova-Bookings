import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { AppLayout } from "@repo/ui/components/AppLayout";
import { Badge } from "@repo/ui/components/Badge";
import { Button } from "@repo/ui/components/Button";
import { Empty, EmptyDescription, EmptyHeader, EmptyMedia, EmptyTitle } from "@repo/ui/components/Empty";
import { Skeleton } from "@repo/ui/components/Skeleton";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@repo/ui/components/Table";
import { useFormatDate } from "@repo/ui/hooks/useSmartDate";
import { useQueryClient } from "@tanstack/react-query";
import { createFileRoute, Link as RouterLink } from "@tanstack/react-router";
import { ArrowLeftIcon, Building2Icon, RotateCcwIcon } from "lucide-react";
import { toast } from "sonner";

import { api, type components } from "@/shared/lib/api/client";

type TenantDetails = components["schemas"]["TenantDetails"];
type UserSummary = components["schemas"]["UserSummary"];

export const Route = createFileRoute("/back-office/tenants/$tenantId")({
  staticData: { trackingTitle: "Tenant details" },
  component: TenantDetailsPage
});

function tenantStateVariant(tenant: TenantDetails) {
  if (tenant.deletedAt) {
    return "destructive";
  }
  return tenant.state === "Active" ? "default" : "secondary";
}

function userStateVariant(user: UserSummary) {
  return user.deletedAt ? "destructive" : "secondary";
}

function TenantDetailsSkeleton() {
  return (
    <div className="flex flex-col gap-6">
      <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
        {Array.from({ length: 4 }, (_, index) => (
          <Skeleton key={index} className="h-16" />
        ))}
      </div>
      <Skeleton className="h-72" />
    </div>
  );
}

function DetailItem({ label, value }: { label: string; value: React.ReactNode }) {
  return (
    <div className="rounded-md border border-border p-3">
      <div className="text-xs font-medium text-muted-foreground">{label}</div>
      <div className="mt-1 min-h-5 text-sm font-medium">{value}</div>
    </div>
  );
}

function UsersTable({ users }: { users: UserSummary[] }) {
  const formatDate = useFormatDate();

  return (
    <Table rowSize="compact">
      <TableHeader>
        <TableRow>
          <TableHead>
            <Trans>User</Trans>
          </TableHead>
          <TableHead>
            <Trans>Role</Trans>
          </TableHead>
          <TableHead>
            <Trans>Status</Trans>
          </TableHead>
          <TableHead>
            <Trans>Last seen</Trans>
          </TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        {users.map((user) => (
          <TableRow key={user.id}>
            <TableCell>
              <div className="font-medium">{`${user.firstName} ${user.lastName}`.trim() || user.email}</div>
              <div className="text-xs text-muted-foreground">{user.email}</div>
            </TableCell>
            <TableCell>{user.role}</TableCell>
            <TableCell>
              <Badge variant={userStateVariant(user)}>
                {user.deletedAt ? <Trans>Deleted</Trans> : user.emailConfirmed ? <Trans>Active</Trans> : <Trans>Pending</Trans>}
              </Badge>
            </TableCell>
            <TableCell>{formatDate(user.lastSeenAt, true)}</TableCell>
          </TableRow>
        ))}
      </TableBody>
    </Table>
  );
}

export default function TenantDetailsPage() {
  const { tenantId } = Route.useParams();
  const queryClient = useQueryClient();
  const formatDate = useFormatDate();
  const { data: tenant, isLoading } = api.useQuery("get", "/api/back-office/tenants/{id}", {
    params: { path: { id: tenantId } }
  });
  const restoreTenantMutation = api.useMutation("post", "/api/back-office/tenants/{id}/restore", {
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["get", "/api/back-office/tenants"] });
      toast.success(t`Tenant restore requested`);
    }
  });

  return (
    <AppLayout
      variant="center"
      maxWidth="72rem"
      browserTitle={tenant?.name ?? t`Tenant details`}
      title={tenant?.name ?? t`Tenant details`}
      subtitle={tenant ? t`Operational catalog details for tenant ${tenant.id}.` : undefined}
      beforeHeader={
        <Button variant="ghost" size="sm" render={<RouterLink to="/back-office/tenants" />}>
          <ArrowLeftIcon />
          <Trans>Tenants</Trans>
        </Button>
      }
    >
      {isLoading && <TenantDetailsSkeleton />}

      {!isLoading && !tenant && (
        <Empty>
          <EmptyHeader>
            <EmptyMedia variant="icon">
              <Building2Icon />
            </EmptyMedia>
            <EmptyTitle>
              <Trans>Tenant not found</Trans>
            </EmptyTitle>
            <EmptyDescription>
              <Trans>The catalog does not contain this tenant.</Trans>
            </EmptyDescription>
          </EmptyHeader>
        </Empty>
      )}

      {tenant && (
        <div className="flex flex-col gap-6">
          <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
            <Badge variant={tenantStateVariant(tenant)}>
              {tenant.deletedAt ? <Trans>Deleted</Trans> : tenant.state}
            </Badge>
            {tenant.deletedAt && (
              <Button
                onClick={() => restoreTenantMutation.mutate({ params: { path: { id: tenant.id } } })}
                isPending={restoreTenantMutation.isPending}
              >
                <RotateCcwIcon />
                <Trans>Restore tenant</Trans>
              </Button>
            )}
          </div>

          <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
            <DetailItem label={t`Plan`} value={tenant.plan} />
            <DetailItem label={t`Created`} value={formatDate(tenant.createdAt, true)} />
            <DetailItem label={t`Modified`} value={formatDate(tenant.modifiedAt, true)} />
            <DetailItem label={t`Deleted`} value={formatDate(tenant.deletedAt, true)} />
          </div>

          <section className="flex flex-col gap-3">
            <div>
              <h2 className="text-lg font-semibold">
                <Trans>Users</Trans>
              </h2>
              <p className="text-sm text-muted-foreground">
                <Trans>Users currently known for this tenant.</Trans>
              </p>
            </div>
            {tenant.users.length > 0 ? (
              <UsersTable users={tenant.users} />
            ) : (
              <Empty>
                <EmptyHeader>
                  <EmptyTitle>
                    <Trans>No users in catalog</Trans>
                  </EmptyTitle>
                  <EmptyDescription>
                    <Trans>User rows will appear after Account publishes user catalog events.</Trans>
                  </EmptyDescription>
                </EmptyHeader>
              </Empty>
            )}
          </section>
        </div>
      )}
    </AppLayout>
  );
}
