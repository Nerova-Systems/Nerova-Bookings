import type { RowKey } from "@repo/ui/components/Table";

import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Checkbox } from "@repo/ui/components/Checkbox";
import { Empty, EmptyDescription, EmptyHeader, EmptyMedia, EmptyTitle } from "@repo/ui/components/Empty";
import { Table, TableBody, TableHead, TableHeader, TableRow } from "@repo/ui/components/Table";
import { TablePagination } from "@repo/ui/components/TablePagination";
import { useViewportResize } from "@repo/ui/hooks/useViewportResize";
import { isMediumViewportOrLarger, isSmallViewportOrLarger, isTouchDevice } from "@repo/ui/utils/responsive";
import { Trash2Icon } from "lucide-react";
import { useCallback, useMemo } from "react";

import { api, type components } from "@/shared/lib/api/client";

import { DeletedUserRow } from "./DeletedUserRow";
import { DeletedUsersTableSkeleton } from "./DeletedUsersTableSkeleton";

type DeletedUserDetails = components["schemas"]["DeletedUserDetails"];

function DeletedUsersEmptyState() {
  return (
    <Empty>
      <EmptyHeader>
        <EmptyMedia variant="icon">
          <Trash2Icon />
        </EmptyMedia>
        <EmptyTitle>
          <Trans>Recycle bin is empty</Trans>
        </EmptyTitle>
        <EmptyDescription>
          <Trans>Deleted users will appear here for recovery</Trans>
        </EmptyDescription>
      </EmptyHeader>
    </Empty>
  );
}

interface DeletedUsersTableProps {
  selectedUsers: DeletedUserDetails[];
  onSelectedUsersChange: (users: DeletedUserDetails[]) => void;
  pageOffset: number;
  onPageChange: (page: number) => void;
}

export function DeletedUsersTable({
  selectedUsers,
  onSelectedUsersChange,
  pageOffset,
  onPageChange
}: Readonly<DeletedUsersTableProps>) {
  const isMobile = useViewportResize();

  const { data: deletedUsersData, isLoading } = api.useQuery("get", "/api/account/users/deleted", {
    params: {
      query: {
        PageOffset: pageOffset,
        PageSize: 25
      }
    }
  });

  const users = useMemo(() => deletedUsersData?.users ?? [], [deletedUsersData?.users]);
  const isMultiSelectMode = !isTouchDevice() && isMediumViewportOrLarger();

  const selectedKeys = useMemo<ReadonlySet<RowKey>>(
    () => new Set(selectedUsers.map((user) => user.id)),
    [selectedUsers]
  );

  const handleSelectionChange = useCallback(
    (keys: Set<RowKey>) => {
      onSelectedUsersChange(users.filter((user) => keys.has(user.id)));
    },
    [onSelectedUsersChange, users]
  );

  const handleSelectAll = useCallback(
    (checked: boolean) => {
      onSelectedUsersChange(checked ? users : []);
    },
    [onSelectedUsersChange, users]
  );

  // NOTE: Skeleton loading state uses <table><tbody> without <thead> to work around a Firefox
  // bug where rendering <thead> during page load after Playwright's clearCookies() causes
  // HttpOnly cookies to not be sent with API requests. This only affects Firefox E2E tests.
  if (isLoading) {
    return <DeletedUsersTableSkeleton isMultiSelectMode={isMultiSelectMode} isMobile={isMobile} />;
  }

  const currentPage = (deletedUsersData?.currentPageOffset ?? 0) + 1;
  const totalPages = deletedUsersData?.totalPages ?? 1;

  if (users.length === 0) {
    return <DeletedUsersEmptyState />;
  }

  const allSelected = users.length > 0 && selectedKeys.size === users.length;
  const someSelected = selectedKeys.size > 0 && selectedKeys.size < users.length;

  return (
    <>
      <div className="deleted-users-table min-h-48 flex-1 overflow-auto">
        <Table
          rowSize="spacious"
          aria-label={t`Deleted users`}
          selectionMode={isMultiSelectMode ? "multiple" : "single"}
          selectedKeys={selectedKeys}
          onSelectionChange={handleSelectionChange}
        >
          <TableHeader className="sticky top-0 z-10 bg-inherit">
            <TableRow>
              {isMultiSelectMode && (
                <TableHead className="w-[3.5rem]">
                  <Checkbox
                    checked={allSelected}
                    indeterminate={someSelected}
                    onCheckedChange={handleSelectAll}
                    aria-label={t`Select all users`}
                  />
                </TableHead>
              )}
              <TableHead className={isSmallViewportOrLarger() ? "min-w-[16rem]" : ""}>
                <Trans>Name</Trans>
              </TableHead>
              {isSmallViewportOrLarger() && (
                <TableHead className="min-w-[10rem]">
                  <Trans>Email</Trans>
                </TableHead>
              )}
              {isMediumViewportOrLarger() && (
                <TableHead className="w-[9rem] min-w-[7.5rem]">
                  <Trans>Deleted</Trans>
                </TableHead>
              )}
              {isSmallViewportOrLarger() && (
                <TableHead className="w-[6rem]">
                  <Trans>Role</Trans>
                </TableHead>
              )}
            </TableRow>
          </TableHeader>
          <TableBody>
            {users.map((user) => (
              <DeletedUserRow
                key={user.id}
                user={user}
                isSelected={selectedKeys.has(user.id)}
                isMultiSelectMode={isMultiSelectMode}
              />
            ))}
          </TableBody>
        </Table>
      </div>

      {!isMobile && totalPages > 1 && (
        <div className="flex-shrink-0 pt-4">
          <TablePagination
            currentPage={currentPage}
            totalPages={totalPages}
            onPageChange={onPageChange}
            previousLabel={t`Previous`}
            nextLabel={t`Next`}
            trackingTitle="Deleted users"
            className="w-full"
          />
        </div>
      )}
    </>
  );
}
