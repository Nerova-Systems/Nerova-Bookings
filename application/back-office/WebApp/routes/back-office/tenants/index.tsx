import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { AppLayout } from "@repo/ui/components/AppLayout";
import { Badge } from "@repo/ui/components/Badge";
import { Button } from "@repo/ui/components/Button";
import { Empty, EmptyDescription, EmptyHeader, EmptyMedia, EmptyTitle } from "@repo/ui/components/Empty";
import { Input } from "@repo/ui/components/Input";
import { Skeleton } from "@repo/ui/components/Skeleton";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@repo/ui/components/Table";
import { useFormatDate } from "@repo/ui/hooks/useSmartDate";
import { keepPreviousData } from "@tanstack/react-query";
import { createFileRoute, Link as RouterLink, useNavigate } from "@tanstack/react-router";
import { Building2Icon, SearchIcon } from "lucide-react";
import { useState } from "react";
import { z } from "zod";

import { api, type components } from "@/shared/lib/api/client";

type TenantSummary = components["schemas"]["TenantSummary"];

const tenantsSearchSchema = z.object({
  search: z.string().optional(),
  pageOffset: z.coerce.number().optional()
});

export const Route = createFileRoute("/back-office/tenants/")({
  staticData: { trackingTitle: "Tenants" },
  component: TenantsPage,
  validateSearch: tenantsSearchSchema
});

function stateVariant(tenant: TenantSummary) {
  if (tenant.deletedAt) {
    return "destructive";
  }
  return tenant.state === "Active" ? "default" : "secondary";
}

function TenantRows({ tenants }: { tenants: TenantSummary[] }) {
  const formatDate = useFormatDate();

  return tenants.map((tenant) => (
    <TableRow key={tenant.id}>
      <TableCell className="font-medium">
        <RouterLink className="text-primary underline-offset-4 hover:underline" to="/back-office/tenants/$tenantId" params={{ tenantId: tenant.id }}>
          {tenant.name}
        </RouterLink>
      </TableCell>
      <TableCell>
        <Badge variant={stateVariant(tenant)}>{tenant.deletedAt ? <Trans>Deleted</Trans> : tenant.state}</Badge>
      </TableCell>
      <TableCell>{tenant.plan}</TableCell>
      <TableCell>{tenant.userCount}</TableCell>
      <TableCell>{formatDate(tenant.createdAt)}</TableCell>
      <TableCell>{tenant.deletedAt ? formatDate(tenant.deletedAt, true) : ""}</TableCell>
    </TableRow>
  ));
}

function TenantTableSkeleton() {
  return Array.from({ length: 6 }, (_, index) => (
    <TableRow key={index}>
      <TableCell>
        <Skeleton className="h-5 w-40" />
      </TableCell>
      <TableCell>
        <Skeleton className="h-5 w-20" />
      </TableCell>
      <TableCell>
        <Skeleton className="h-5 w-16" />
      </TableCell>
      <TableCell>
        <Skeleton className="h-5 w-8" />
      </TableCell>
      <TableCell>
        <Skeleton className="h-5 w-24" />
      </TableCell>
      <TableCell>
        <Skeleton className="h-5 w-24" />
      </TableCell>
    </TableRow>
  ));
}

export default function TenantsPage() {
  const navigate = useNavigate({ from: Route.fullPath });
  const { search, pageOffset = 0 } = Route.useSearch();
  const [searchValue, setSearchValue] = useState(search ?? "");
  const { data, isLoading } = api.useQuery(
    "get",
    "/api/back-office/tenants",
    { params: { query: { Search: search, PageOffset: pageOffset, PageSize: 25 } } },
    { placeholderData: keepPreviousData }
  );

  const tenants = data?.tenants ?? [];
  const hasSearch = Boolean(search);

  return (
    <AppLayout
      variant="center"
      maxWidth="72rem"
      browserTitle={t`Tenants`}
      title={t`Tenants`}
      subtitle={t`Review the operational tenant catalog synchronized from Account.`}
    >
      <form
        className="mb-4 flex flex-col gap-2 sm:flex-row"
        onSubmit={(event) => {
          event.preventDefault();
          navigate({ search: () => ({ search: searchValue || undefined, pageOffset: 0 }) });
        }}
      >
        <div className="relative min-w-0 flex-1">
          <SearchIcon className="pointer-events-none absolute top-1/2 left-2.5 size-4 -translate-y-1/2 text-muted-foreground" />
          <Input
            className="pl-8"
            value={searchValue}
            onChange={(event) => setSearchValue(event.currentTarget.value)}
            placeholder={t`Search tenants`}
          />
        </div>
        <Button type="submit" variant="secondary">
          <SearchIcon />
          <Trans>Search</Trans>
        </Button>
      </form>

      <Table rowSize="compact">
        <TableHeader>
          <TableRow>
            <TableHead>
              <Trans>Tenant</Trans>
            </TableHead>
            <TableHead>
              <Trans>State</Trans>
            </TableHead>
            <TableHead>
              <Trans>Plan</Trans>
            </TableHead>
            <TableHead>
              <Trans>Users</Trans>
            </TableHead>
            <TableHead>
              <Trans>Created</Trans>
            </TableHead>
            <TableHead>
              <Trans>Deleted</Trans>
            </TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>{isLoading ? <TenantTableSkeleton /> : <TenantRows tenants={tenants} />}</TableBody>
      </Table>

      {!isLoading && tenants.length === 0 && (
        <Empty className="mt-6">
          <EmptyHeader>
            <EmptyMedia variant="icon">
              <Building2Icon />
            </EmptyMedia>
            <EmptyTitle>{hasSearch ? <Trans>No matching tenants</Trans> : <Trans>No tenants yet</Trans>}</EmptyTitle>
            <EmptyDescription>
              {hasSearch ? (
                <Trans>Adjust the search and try again.</Trans>
              ) : (
                <Trans>The tenant catalog will populate as Account publishes catalog events.</Trans>
              )}
            </EmptyDescription>
          </EmptyHeader>
        </Empty>
      )}
    </AppLayout>
  );
}
