import { Trans } from "@lingui/react/macro";
import { Empty, EmptyDescription, EmptyHeader, EmptyMedia, EmptyTitle } from "@repo/ui/components/Empty";
import { SearchIcon, UsersIcon } from "lucide-react";

interface ClientTableEmptyStateProps {
  hasFilters?: boolean;
}

export function ClientTableEmptyState({ hasFilters = true }: Readonly<ClientTableEmptyStateProps>) {
  if (!hasFilters) {
    return (
      <Empty>
        <EmptyHeader>
          <EmptyMedia variant="icon">
            <UsersIcon />
          </EmptyMedia>
          <EmptyTitle>
            <Trans>No clients yet</Trans>
          </EmptyTitle>
          <EmptyDescription>
            <Trans>Clients will appear here once bookings are made.</Trans>
          </EmptyDescription>
        </EmptyHeader>
      </Empty>
    );
  }

  return (
    <Empty>
      <EmptyHeader>
        <EmptyMedia variant="icon">
          <SearchIcon />
        </EmptyMedia>
        <EmptyTitle>
          <Trans>No clients found</Trans>
        </EmptyTitle>
        <EmptyDescription>
          <Trans>Try adjusting your search or filters</Trans>
        </EmptyDescription>
      </EmptyHeader>
    </Empty>
  );
}
