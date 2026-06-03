import type { RowKey } from "@repo/ui/components/Table";

import { t } from "@lingui/core/macro";
import { Table, TableBody } from "@repo/ui/components/Table";
import { TablePagination } from "@repo/ui/components/TablePagination";
import { useInfiniteScroll } from "@repo/ui/hooks/useInfiniteScroll";
import { useNavigate, useSearch } from "@tanstack/react-router";
import { useCallback, useEffect, useMemo, useRef, useState } from "react";

import { type components, SortableClientProperties, SortOrder } from "@/shared/lib/api/client";

import { ClientTableEmptyState } from "./ClientTableEmptyState";
import { type SortDescriptor, ClientTableHeader } from "./ClientTableHeader";
import { ClientTableRow } from "./ClientTableRow";
import { ClientTableSkeleton } from "./ClientTableSkeleton";

type ClientDetails = components["schemas"]["ClientDetails"];

export interface ClientTableContentProps {
  selectedClients: ClientDetails[];
  onSelectedClientsChange: (clients: ClientDetails[]) => void;
  onViewProfile: (client: ClientDetails | null) => void;
  onManageClient: (client: ClientDetails) => void;
  onDeleteClient: (client: ClientDetails) => void;
  onClientsLoaded?: (clients: ClientDetails[]) => void;
  clientsList: ClientDetails[];
  isLoading: boolean;
  isMobile: boolean;
  totalPages: number;
  currentPageOffset: number;
  isLoadingMore?: boolean;
  hasMore?: boolean;
  loadMore?: () => void;
  hasFilters?: boolean;
}

export function ClientTableContent({
  selectedClients,
  onSelectedClientsChange,
  onViewProfile,
  onManageClient,
  onDeleteClient,
  onClientsLoaded,
  clientsList,
  isLoading,
  isMobile,
  totalPages,
  currentPageOffset,
  isLoadingMore = false,
  hasMore = false,
  loadMore,
  hasFilters = false
}: Readonly<ClientTableContentProps>) {
  const navigate = useNavigate();
  const { orderBy, sortOrder, pageOffset, clientId } = useSearch({ strict: false });

  const [sortDescriptor, setSortDescriptor] = useState<SortDescriptor>(() => ({
    column: orderBy ?? SortableClientProperties.Name,
    direction: sortOrder === SortOrder.Descending ? "descending" : "ascending"
  }));

  const selectedKeys = useMemo<ReadonlySet<RowKey>>(
    () => new Set(selectedClients.map((client) => client.id)),
    [selectedClients]
  );

  const handleSelectionChange = useCallback(
    (keys: Set<RowKey>) => {
      onSelectedClientsChange(clientsList.filter((client) => keys.has(client.id)));
      if (keys.size > 1) onViewProfile(null);
    },
    [onSelectedClientsChange, onViewProfile, clientsList]
  );

  const handleActivate = useCallback(
    (key: RowKey) => {
      onViewProfile(clientId === key ? null : (clientsList.find((client) => client.id === key) ?? null));
    },
    [clientId, onViewProfile, clientsList]
  );

  const handlePageChange = useCallback(
    (page: number) => {
      navigate({
        to: "/clients",
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
      onSelectedClientsChange([]);
      const newOrderBy = columnId as SortableClientProperties;
      const newSortOrder = newDirection === "ascending" ? SortOrder.Ascending : SortOrder.Descending;
      navigate({
        to: "/clients",
        search: (prev) => ({
          ...prev,
          orderBy: newOrderBy === SortableClientProperties.Name ? undefined : newOrderBy,
          sortOrder: newSortOrder === SortOrder.Ascending ? undefined : newSortOrder,
          pageOffset: undefined
        })
      });
    },
    [navigate, sortDescriptor, onSelectedClientsChange]
  );

  const previousPageOffset = useRef(pageOffset);
  useEffect(() => {
    if (previousPageOffset.current !== pageOffset) {
      previousPageOffset.current = pageOffset;
      onSelectedClientsChange([]);
    }
  }, [onSelectedClientsChange, pageOffset]);

  const previousClientIds = useRef<string>("");
  useEffect(() => {
    const clientIds = clientsList.map((c) => c.id).join(",");
    if (clientIds !== previousClientIds.current) {
      previousClientIds.current = clientIds;
      onClientsLoaded?.(clientsList);
    }
  }, [clientsList, onClientsLoaded]);

  const loadMoreRef = useInfiniteScroll({
    enabled: isMobile,
    hasMore,
    isLoadingMore,
    onLoadMore: loadMore ?? (() => {})
  });

  if (isLoading && clientsList.length === 0) {
    return <ClientTableSkeleton isMobile={isMobile} />;
  }

  if (!isLoading && clientsList.length === 0) {
    return <ClientTableEmptyState hasFilters={hasFilters} />;
  }

  const currentPage = currentPageOffset + 1;

  return (
    <>
      <div className="flex-1 overflow-visible rounded-md bg-background outline-ring focus-visible:outline-2 focus-visible:outline-offset-2 max-sm:pb-18 sm:min-h-48 sm:overflow-auto">
        <Table
          rowSize="spacious"
          aria-label={t`Clients`}
          selectionMode="multiple"
          selectedKeys={selectedKeys}
          onSelectionChange={handleSelectionChange}
          onActivate={handleActivate}
          activateOnNavigate={clientId != null}
          scrollToKey={clientId}
        >
          <ClientTableHeader sortDescriptor={sortDescriptor} isMobile={isMobile} onSortChange={handleSortChange} />
          <TableBody>
            {clientsList.map((client) => (
              <ClientTableRow
                key={client.id}
                client={client}
                isMobile={isMobile}
                onSelectedClientsChange={onSelectedClientsChange}
                onViewProfile={onViewProfile}
                onManageClient={onManageClient}
                onDeleteClient={onDeleteClient}
              />
            ))}
          </TableBody>
        </Table>
        {isMobile && <div ref={loadMoreRef} className="h-1" />}
      </div>

      {!isMobile && totalPages > 1 && (
        <div className="shrink-0 pt-4">
          <TablePagination
            currentPage={currentPage}
            totalPages={totalPages}
            onPageChange={handlePageChange}
            previousLabel={t`Previous`}
            nextLabel={t`Next`}
            trackingTitle="Clients"
            className="w-full"
          />
        </div>
      )}
    </>
  );
}
