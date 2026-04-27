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
import { SearchIcon, UsersIcon } from "lucide-react";
import { useState } from "react";
import { z } from "zod";

import { api, type components } from "@/shared/lib/api/client";

type UserDetails = components["schemas"]["UserDetails"];

const usersSearchSchema = z.object({
  search: z.string().optional(),
  pageOffset: z.coerce.number().optional()
});

export const Route = createFileRoute("/back-office/users/")({
  staticData: { trackingTitle: "Users" },
  component: UsersPage,
  validateSearch: usersSearchSchema
});

function statusVariant(user: UserDetails) {
  return user.deletedAt ? "destructive" : "secondary";
}

function UserRows({ users }: { users: UserDetails[] }) {
  const formatDate = useFormatDate();

  return users.map((user) => (
    <TableRow key={user.id}>
      <TableCell>
        <div className="font-medium">{`${user.firstName} ${user.lastName}`.trim() || user.email}</div>
        <div className="text-xs text-muted-foreground">{user.email}</div>
      </TableCell>
      <TableCell>
        <RouterLink
          className="text-primary underline-offset-4 hover:underline"
          to="/back-office/tenants/$tenantId"
          params={{ tenantId: user.tenantId }}
        >
          {user.tenantName}
        </RouterLink>
      </TableCell>
      <TableCell>{user.role}</TableCell>
      <TableCell>
        <Badge variant={statusVariant(user)}>
          {user.deletedAt ? <Trans>Deleted</Trans> : user.emailConfirmed ? <Trans>Active</Trans> : <Trans>Pending</Trans>}
        </Badge>
      </TableCell>
      <TableCell>{formatDate(user.lastSeenAt, true)}</TableCell>
      <TableCell>{user.deletedAt ? formatDate(user.deletedAt, true) : ""}</TableCell>
    </TableRow>
  ));
}

function UserTableSkeleton() {
  return Array.from({ length: 6 }, (_, index) => (
    <TableRow key={index}>
      <TableCell>
        <Skeleton className="h-9 w-48" />
      </TableCell>
      <TableCell>
        <Skeleton className="h-5 w-32" />
      </TableCell>
      <TableCell>
        <Skeleton className="h-5 w-20" />
      </TableCell>
      <TableCell>
        <Skeleton className="h-5 w-20" />
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

export default function UsersPage() {
  const navigate = useNavigate({ from: Route.fullPath });
  const { search, pageOffset = 0 } = Route.useSearch();
  const [searchValue, setSearchValue] = useState(search ?? "");
  const { data, isLoading } = api.useQuery(
    "get",
    "/api/back-office/users",
    { params: { query: { Search: search, PageOffset: pageOffset, PageSize: 25 } } },
    { placeholderData: keepPreviousData }
  );

  const users = data?.users ?? [];
  const hasSearch = Boolean(search);

  return (
    <AppLayout
      variant="center"
      maxWidth="72rem"
      browserTitle={t`Users`}
      title={t`Users`}
      subtitle={t`Lookup users across tenants from the Back-office catalog.`}
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
            placeholder={t`Search users`}
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
              <Trans>User</Trans>
            </TableHead>
            <TableHead>
              <Trans>Tenant</Trans>
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
            <TableHead>
              <Trans>Deleted</Trans>
            </TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>{isLoading ? <UserTableSkeleton /> : <UserRows users={users} />}</TableBody>
      </Table>

      {!isLoading && users.length === 0 && (
        <Empty className="mt-6">
          <EmptyHeader>
            <EmptyMedia variant="icon">
              <UsersIcon />
            </EmptyMedia>
            <EmptyTitle>{hasSearch ? <Trans>No matching users</Trans> : <Trans>No users yet</Trans>}</EmptyTitle>
            <EmptyDescription>
              {hasSearch ? (
                <Trans>Adjust the search and try again.</Trans>
              ) : (
                <Trans>The user catalog will populate as Account publishes catalog events.</Trans>
              )}
            </EmptyDescription>
          </EmptyHeader>
        </Empty>
      )}
    </AppLayout>
  );
}
