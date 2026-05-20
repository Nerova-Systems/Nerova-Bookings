import { Trans } from "@lingui/react/macro";
import { SidebarInset, SidebarProvider } from "@repo/ui/components/Sidebar";
import { Skeleton } from "@repo/ui/components/Skeleton";
import { createFileRoute, Link as RouterLink } from "@tanstack/react-router";
import { ArrowLeftIcon } from "lucide-react";
import { useEffect, useRef } from "react";

import { BackOfficeSideMenu } from "@/shared/components/BackOfficeSideMenu";
import { NotFoundPage } from "@/shared/components/errorPages/NotFoundPage";
import { api, type Schemas } from "@/shared/lib/api/client";

import { BackOfficeSupportSidePane } from "../-components/BackOfficeSupportSidePane";
import { CategoryPill } from "../-components/CategoryPill";
import { MessageBubble } from "../-components/MessageBubble";
import { StaffReplyComposer } from "../-components/StaffReplyComposer";
import { StatusPill } from "../-components/StatusPill";

export const Route = createFileRoute("/support/tickets/$ticketId")({
  staticData: { trackingTitle: "Support ticket" },
  component: SupportTicketDetailPage
});

function SupportTicketDetailPage() {
  const { ticketId } = Route.useParams();

  const {
    data: ticket,
    isLoading,
    isError
  } = api.useQuery("get", "/api/back-office/support-tickets/{id}", {
    params: { path: { id: ticketId } }
  });

  // Any non-success outcome (404 not-found, 400 malformed ID, 5xx, network) renders the standard
  // not-found page rather than holding the user on a perpetual skeleton.
  if (isError && !ticket) {
    return <NotFoundPage />;
  }

  return (
    <SidebarProvider>
      <BackOfficeSideMenu />
      <SidebarInset>
        <div className="flex h-full min-h-0 flex-1 flex-row">
          <div className="flex min-h-0 flex-1 flex-col">
            {isLoading || !ticket ? <TicketDetailSkeleton /> : <TicketDetailBody ticket={ticket} />}
          </div>
          <BackOfficeSupportSidePane ticketId={ticket?.id} mode="detail" />
        </div>
      </SidebarInset>
    </SidebarProvider>
  );
}

function TicketDetailBody({ ticket }: { ticket: Schemas["StaffTicketDetailResponse"] }) {
  const scrollRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (scrollRef.current) {
      scrollRef.current.scrollTop = scrollRef.current.scrollHeight;
    }
  }, [ticket.messages.length]);

  return (
    <div className="flex min-h-0 flex-1 flex-col">
      <TicketDetailHeader ticket={ticket} />
      <div ref={scrollRef} className="min-h-0 flex-1 overflow-y-auto px-4 sm:px-8">
        <div className="mx-auto flex w-full max-w-[48rem] flex-col gap-5 py-4">
          {ticket.messages.map((message) => (
            <MessageBubble key={message.id} message={message} />
          ))}
        </div>
      </div>
      <StaffReplyComposer ticketId={ticket.id} />
    </div>
  );
}

function TicketDetailHeader({ ticket }: { ticket: Schemas["StaffTicketDetailResponse"] }) {
  return (
    <div className="sticky top-0 z-10 border-b border-border bg-background px-4 pt-4 pb-3 sm:px-8">
      <div className="mx-auto flex w-full max-w-[48rem] flex-col">
        <RouterLink
          to="/support/tickets"
          className="inline-flex w-fit items-center gap-1.5 text-xs text-muted-foreground hover:text-foreground"
        >
          <ArrowLeftIcon className="size-3.5" aria-hidden={true} />
          <Trans>All tickets</Trans>
        </RouterLink>
        <div className="mt-2 flex flex-wrap items-center gap-3">
          <h1 className="flex-1">{ticket.subject}</h1>
          <StatusPill status={ticket.status} />
        </div>
        <div className="mt-1.5 flex flex-wrap items-center gap-2 text-xs text-muted-foreground">
          <CategoryPill category={ticket.category} />
          <span className="font-mono">#{ticket.shortDisplayId}</span>
        </div>
      </div>
    </div>
  );
}

function TicketDetailSkeleton() {
  return (
    <div className="flex min-h-0 flex-1 flex-col">
      <div className="border-b border-border px-4 pt-4 pb-3 sm:px-8">
        <div className="mx-auto flex w-full max-w-[48rem] flex-col gap-2">
          <Skeleton className="h-3 w-20" />
          <Skeleton className="h-7 w-2/3" />
          <Skeleton className="h-4 w-32" />
        </div>
      </div>
      <div className="min-h-0 flex-1 overflow-hidden px-4 sm:px-8">
        <div className="mx-auto flex w-full max-w-[48rem] flex-col gap-5 py-4">
          <Skeleton className="h-20 w-3/4" />
          <Skeleton className="ml-auto h-20 w-2/3" />
          <Skeleton className="h-16 w-2/3" />
        </div>
      </div>
    </div>
  );
}
