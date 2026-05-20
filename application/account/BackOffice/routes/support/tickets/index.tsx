import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { AppLayout } from "@repo/ui/components/AppLayout";
import { Empty, EmptyDescription, EmptyHeader, EmptyMedia, EmptyTitle } from "@repo/ui/components/Empty";
import { SidebarInset, SidebarProvider } from "@repo/ui/components/Sidebar";
import { keepPreviousData } from "@tanstack/react-query";
import { createFileRoute, useNavigate, useRouterState } from "@tanstack/react-router";
import { InboxIcon } from "lucide-react";
import { z } from "zod";

import { BackOfficeSideMenu } from "@/shared/components/BackOfficeSideMenu";
import { api, SupportTicketAssigneeFilter, SupportTicketCategory, SupportTicketStatus } from "@/shared/lib/api/client";

import { BackOfficeSupportSidePane } from "../-components/BackOfficeSupportSidePane";
import { InboxStatTiles } from "../-components/InboxStatTiles";
import { InboxTable } from "../-components/InboxTable";
import { InboxToolbar } from "../-components/InboxToolbar";

const inboxSearchSchema = z.object({
  search: z.string().optional(),
  status: z.nativeEnum(SupportTicketStatus).optional(),
  category: z.nativeEnum(SupportTicketCategory).optional(),
  assignee: z.nativeEnum(SupportTicketAssigneeFilter).optional(),
  pageOffset: z.number().int().nonnegative().optional(),
  selectedTicketId: z.string().optional()
});

export const Route = createFileRoute("/support/tickets/")({
  staticData: { trackingTitle: "Support tickets" },
  validateSearch: inboxSearchSchema,
  component: SupportInboxPage
});

function SupportInboxPage() {
  const { search, status, category, assignee, pageOffset, selectedTicketId } = Route.useSearch();
  const navigate = useNavigate();
  const currentPathname = useRouterState({ select: (state) => state.location.pathname });
  const trimmed = search?.trim() ?? "";

  const { data, isLoading } = api.useQuery(
    "get",
    "/api/back-office/support-tickets",
    {
      params: {
        query: {
          Search: trimmed.length > 0 ? trimmed : undefined,
          Status: status,
          Category: category,
          Assignee: assignee,
          PageOffset: pageOffset
        }
      }
    },
    { placeholderData: keepPreviousData }
  );

  const tickets = data?.tickets ?? [];
  const hasActiveFilters =
    trimmed.length > 0 || status !== undefined || category !== undefined || assignee !== undefined;
  const showEmpty = !isLoading && tickets.length === 0 && !hasActiveFilters;
  const showNoResults = !isLoading && tickets.length === 0 && hasActiveFilters;

  const handleStatusChange = (next: SupportTicketStatus | undefined) => {
    navigate({
      to: "/support/tickets",
      search: (previous) => ({ ...previous, status: next, pageOffset: undefined })
    });
  };

  const handlePageChange = (page: number) => {
    navigate({
      to: "/support/tickets",
      search: (previous) => ({ ...previous, pageOffset: page === 1 ? undefined : page - 1 })
    });
  };

  const handleSelectTicket = (ticketId: string | undefined) => {
    // SidePane fires onOpenChange(false) when navigating away (e.g. Open ticket deep-link); ignore
    // it once we've left the inbox, otherwise we'd re-navigate back and cancel the deep-link.
    if (currentPathname !== "/support/tickets") {
      return;
    }
    navigate({
      to: "/support/tickets",
      search: (previous) => ({ ...previous, selectedTicketId: ticketId })
    });
  };

  return (
    <SidebarProvider>
      <BackOfficeSideMenu />
      <SidebarInset>
        <AppLayout
          variant="center"
          maxWidth="80rem"
          browserTitle={t`Support tickets`}
          title={t`Support tickets`}
          subtitle={t`Across all accounts. Sort by last activity.`}
          sidePane={
            <BackOfficeSupportSidePane
              ticketId={selectedTicketId}
              onClose={() => handleSelectTicket(undefined)}
              mode="preview"
            />
          }
        >
          <InboxStatTiles counts={data?.counts} selectedStatus={status} onSelect={handleStatusChange} />

          <div className="mt-4">
            <InboxToolbar search={search} category={category} assignee={assignee} resultCount={data?.totalCount} />
          </div>

          {showEmpty ? (
            <Empty>
              <EmptyHeader>
                <EmptyMedia variant="icon">
                  <InboxIcon />
                </EmptyMedia>
                <EmptyTitle>
                  <Trans>Inbox zero</Trans>
                </EmptyTitle>
                <EmptyDescription>
                  <Trans>No active tickets across any account.</Trans>
                </EmptyDescription>
              </EmptyHeader>
            </Empty>
          ) : showNoResults ? (
            <Empty>
              <EmptyHeader>
                <EmptyMedia variant="icon">
                  <InboxIcon />
                </EmptyMedia>
                <EmptyTitle>
                  <Trans>No matches</Trans>
                </EmptyTitle>
                <EmptyDescription>
                  <Trans>Try clearing filters or searching for something else.</Trans>
                </EmptyDescription>
              </EmptyHeader>
            </Empty>
          ) : (
            <div className="flex min-h-0 flex-1 flex-col">
              <InboxTable
                tickets={tickets}
                isLoading={isLoading}
                totalPages={data?.totalPages ?? 0}
                currentPageOffset={data?.currentPageOffset ?? 0}
                selectedTicketId={selectedTicketId}
                onSelectTicket={handleSelectTicket}
                onPageChange={handlePageChange}
              />
            </div>
          )}
        </AppLayout>
      </SidebarInset>
    </SidebarProvider>
  );
}
