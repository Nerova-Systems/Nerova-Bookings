import type { RowKey } from "@repo/ui/components/Table";

import { t } from "@lingui/core/macro";
import { useUserInfo } from "@repo/infrastructure/auth/hooks";
import { Table, TableBody } from "@repo/ui/components/Table";
import { TablePagination } from "@repo/ui/components/TablePagination";
import { useInfiniteScroll } from "@repo/ui/hooks/useInfiniteScroll";
import { useNavigate, useSearch } from "@tanstack/react-router";
import { useCallback, useEffect, useMemo, useRef, useState } from "react";

import { type components, SortableUserProperties, SortOrder } from "@/shared/lib/api/client";

import { UserTableEmptyState } from "./UserTableEmptyState";
import { type SortDescriptor, UserTableHeader } from "./UserTableHeader";
import { UserTableRow } from "./UserTableRow";
import { UserTableSkeleton } from "./UserTableSkeleton";

type UserDetails = components["schemas"]["UserDetails"];

export interface UserTableContentProps {
  selectedUsers: UserDetails[];
  onSelectedUsersChange: (users: UserDetails[]) => void;
  onViewProfile: (user: UserDetails | null) => void;
  onDeleteUser: (user: UserDetails) => void;
  onChangeRole: (user: UserDetails) => void;
  onUsersLoaded?: (users: UserDetails[]) => void;
  usersList: UserDetails[];
  isLoading: boolean;
  isMobile: boolean;
  totalPages: number;
  currentPageOffset: number;
  isLoadingMore?: boolean;
  hasMore?: boolean;
  loadMore?: () => void;
  hasFilters?: boolean;
}

export function UserTableContent({
  selectedUsers,
  onSelectedUsersChange,
  onViewProfile,
  onDeleteUser,
  onChangeRole,
  onUsersLoaded,
  usersList,
  isLoading,
  isMobile,
  totalPages,
  currentPageOffset,
  isLoadingMore = false,
  hasMore = false,
  loadMore,
  hasFilters = false
}: Readonly<UserTableContentProps>) {
  const navigate = useNavigate();
  const { orderBy, sortOrder, pageOffset, userId } = useSearch({ strict: false });
  const userInfo = useUserInfo();

  const [sortDescriptor, setSortDescriptor] = useState<SortDescriptor>(() => ({
    column: orderBy ?? SortableUserProperties.Name,
    direction: sortOrder === SortOrder.Descending ? "descending" : "ascending"
  }));

  const selectedKeys = useMemo<ReadonlySet<RowKey>>(
    () => new Set(selectedUsers.map((user) => user.id)),
    [selectedUsers]
  );

  const handleSelectionChange = useCallback(
    (keys: Set<RowKey>) => {
      onSelectedUsersChange(usersList.filter((user) => keys.has(user.id)));
      if (keys.size > 1) onViewProfile(null);
    },
    [onSelectedUsersChange, onViewProfile, usersList]
  );

  const handleActivate = useCallback(
    (key: RowKey) => {
      onViewProfile(userId === key ? null : (usersList.find((user) => user.id === key) ?? null));
    },
    [userId, onViewProfile, usersList]
  );

  const handlePageChange = useCallback(
    (page: number) => {
      navigate({
        to: "/account/users",
        search: (prev) => ({
          ...prev,
          pageOffset: page === 1 ? undefined : page - 1
        })
      });
    },
    [navigate]
  );

  const handleSortChange = useCallback(
    (columnId: string) => {
      const newDirection =
        sortDescriptor.column === columnId && sortDescriptor.direction === "ascending" ? "descending" : "ascending";
      setSortDescriptor({ column: columnId, direction: newDirection });
      onSelectedUsersChange([]);
      const newOrderBy = columnId as SortableUserProperties;
      const newSortOrder = newDirection === "ascending" ? SortOrder.Ascending : SortOrder.Descending;
      navigate({
        to: "/account/users",
        search: (prev) => ({
          ...prev,
          orderBy: newOrderBy === SortableUserProperties.Name ? undefined : newOrderBy,
          sortOrder: newSortOrder === SortOrder.Ascending ? undefined : newSortOrder,
          pageOffset: undefined
        })
      });
    },
    [navigate, sortDescriptor, onSelectedUsersChange]
  );

  const previousPageOffset = useRef(pageOffset);
  useEffect(() => {
    if (previousPageOffset.current !== pageOffset) {
      previousPageOffset.current = pageOffset;
      onSelectedUsersChange([]);
    }
  }, [onSelectedUsersChange, pageOffset]);

  const previousUserIds = useRef<string>("");
  useEffect(() => {
    const userIds = usersList.map((u) => u.id).join(",");
    if (userIds !== previousUserIds.current) {
      previousUserIds.current = userIds;
      onUsersLoaded?.(usersList);
    }
  }, [usersList, onUsersLoaded]);

  const loadMoreRef = useInfiniteScroll({
    enabled: isMobile,
    hasMore,
    isLoadingMore,
    onLoadMore: loadMore ?? (() => {})
  });

  if (isLoading && usersList.length === 0) {
    return <UserTableSkeleton isMobile={isMobile} />;
  }

  if (!isLoading && usersList.length === 0 && hasFilters) {
    return <UserTableEmptyState />;
  }

  const currentPage = currentPageOffset + 1;

  return (
    <>
      <div className="flex-1 overflow-visible rounded-md bg-background outline-ring focus-visible:outline-2 focus-visible:outline-offset-2 max-sm:pb-18 sm:min-h-48 sm:overflow-auto">
        <Table
          rowSize="spacious"
          aria-label={t`Users`}
          selectionMode="multiple"
          selectedKeys={selectedKeys}
          onSelectionChange={handleSelectionChange}
          onActivate={handleActivate}
          activateOnNavigate={userId != null}
          scrollToKey={userId}
        >
          <UserTableHeader sortDescriptor={sortDescriptor} isMobile={isMobile} onSortChange={handleSortChange} />
          <TableBody>
            {usersList.map((user) => (
              <UserTableRow
                key={user.id}
                user={user}
                isMobile={isMobile}
                currentUserRole={userInfo?.role}
                currentUserId={userInfo?.id}
                onSelectedUsersChange={onSelectedUsersChange}
                onViewProfile={onViewProfile}
                onDeleteUser={onDeleteUser}
                onChangeRole={onChangeRole}
              />
            ))}
          </TableBody>
        </Table>
        {isMobile && <div ref={loadMoreRef} className="h-1" />}
      </div>

      {!isMobile && (
        <div className="shrink-0 pt-4">
          <TablePagination
            currentPage={currentPage}
            totalPages={totalPages}
            onPageChange={handlePageChange}
            previousLabel={t`Previous`}
            nextLabel={t`Next`}
            trackingTitle="Users"
            className="w-full"
          />
        </div>
      )}
    </>
  );
}
