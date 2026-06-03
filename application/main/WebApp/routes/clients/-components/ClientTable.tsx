import { useViewportResize } from "@repo/ui/hooks/useViewportResize";
import { keepPreviousData } from "@tanstack/react-query";
import { useSearch } from "@tanstack/react-router";

import { api, type components } from "@/shared/lib/api/client";

import { useInfiniteClients } from "../-hooks/useInfiniteClients";
import { ClientTableContent } from "./ClientTableContent";

type ClientDetails = components["schemas"]["ClientDetails"];

interface ClientTableProps {
  selectedClients: ClientDetails[];
  onSelectedClientsChange: (clients: ClientDetails[]) => void;
  onViewProfile: (client: ClientDetails | null) => void;
  onManageClient: (client: ClientDetails) => void;
  onDeleteClient: (client: ClientDetails) => void;
  onClientsLoaded?: (clients: ClientDetails[]) => void;
}

export function ClientTable(props: Readonly<ClientTableProps>) {
  const isMobile = useViewportResize();
  return isMobile ? <MobileClientTable {...props} /> : <DesktopClientTable {...props} />;
}

function DesktopClientTable(props: Readonly<ClientTableProps>) {
  const { search, startDate, endDate, orderBy, sortOrder, pageOffset } = useSearch({
    strict: false
  });

  const { data, isLoading } = api.useQuery(
    "get",
    "/api/main/clients",
    {
      params: {
        query: {
          Search: search,
          StartDate: startDate,
          EndDate: endDate,
          OrderBy: orderBy,
          SortOrder: sortOrder,
          PageOffset: pageOffset
        }
      }
    },
    { placeholderData: keepPreviousData }
  );

  const hasFilters = Boolean(search || startDate || endDate);

  return (
    <ClientTableContent
      {...props}
      clientsList={data?.clients ?? []}
      isLoading={isLoading}
      isMobile={false}
      totalPages={data?.totalPages ?? 1}
      currentPageOffset={data?.currentPageOffset ?? 0}
      hasFilters={hasFilters}
    />
  );
}

function MobileClientTable(props: Readonly<ClientTableProps>) {
  const { search, startDate, endDate, orderBy, sortOrder } = useSearch({
    strict: false
  });

  const { clients, isLoading, isLoadingMore, hasMore, loadMore } = useInfiniteClients({
    search,
    startDate,
    endDate,
    orderBy,
    sortOrder,
    enabled: true
  });

  const hasFilters = Boolean(search || startDate || endDate);

  return (
    <ClientTableContent
      {...props}
      clientsList={clients}
      isLoading={isLoading}
      isMobile={true}
      totalPages={1}
      currentPageOffset={0}
      isLoadingMore={isLoadingMore}
      hasMore={hasMore}
      loadMore={loadMore}
      hasFilters={hasFilters}
    />
  );
}
