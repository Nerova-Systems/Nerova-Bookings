import { Trans } from "@lingui/react/macro";
import { Button } from "@repo/ui/components/Button";
import { createFileRoute, useNavigate } from "@tanstack/react-router";
import { Loader2Icon } from "lucide-react";
import { toast } from "sonner";

import { usePublicRescheduleApproval, useRespondToRescheduleApproval } from "@/shared/lib/publicBookingApi";

export const Route = createFileRoute("/book/approval/$token")({
  staticData: { trackingTitle: "Reschedule approval" },
  component: ApprovalPage
});

function ApprovalPage() {
  const { token } = Route.useParams();
  const navigate = useNavigate();
  const approvalQuery = usePublicRescheduleApproval(token);
  const approveMutation = useRespondToRescheduleApproval(token, "approve");
  const rejectMutation = useRespondToRescheduleApproval(token, "reject");

  const respond = async (decision: "approve" | "reject") => {
    try {
      const result = decision === "approve" ? await approveMutation.mutateAsync() : await rejectMutation.mutateAsync();
      toast.success(decision === "approve" ? "Reschedule approved." : "Reschedule declined.");
      navigate({ to: "/book/confirmation/$reference", params: { reference: result.appointmentReference } });
    } catch (error) {
      toast.error(error instanceof Error ? error.message : "Could not submit your response.");
    }
  };

  if (approvalQuery.isLoading) {
    return (
      <main className="flex min-h-dvh items-center justify-center bg-[#0f0f0f] text-white">
        <Loader2Icon className="size-5 animate-spin" />
      </main>
    );
  }

  if (approvalQuery.isError || !approvalQuery.data) {
    return (
      <main className="flex min-h-dvh items-center justify-center bg-[#0f0f0f] px-5 text-white">
        <div className="max-w-lg rounded-2xl border border-white/10 bg-[#191919] p-8 text-center">
          <h1 className="font-display text-3xl font-semibold">Approval link unavailable</h1>
          <p className="mt-3 text-white/60">This reschedule link is invalid, expired, or already answered.</p>
        </div>
      </main>
    );
  }

  const approval = approvalQuery.data;
  const proposedStart = new Date(approval.proposedStartAt);
  const proposedEnd = new Date(approval.proposedEndAt);
  const originalStart = new Date(approval.appointment.startAt);
  const isPending = approveMutation.isPending || rejectMutation.isPending;

  return (
    <main className="flex min-h-dvh items-center justify-center bg-[#0f0f0f] px-5 py-10 text-white">
      <section className="w-full max-w-2xl rounded-3xl border border-white/10 bg-[#191919] p-8 shadow-2xl">
        <p className="text-sm font-semibold text-white/50">Booking reschedule request</p>
        <h1 className="mt-3 font-display text-3xl font-semibold">{approval.appointment.serviceName}</h1>
        <div className="mt-7 grid gap-4 rounded-2xl border border-white/10 bg-[#101010] p-5 text-sm md:grid-cols-2">
          <Info label="Current time" value={formatWhen(originalStart, new Date(approval.appointment.endAt))} />
          <Info label="Proposed time" value={formatWhen(proposedStart, proposedEnd)} />
          <Info label="Client" value={approval.appointment.clientName} />
          <Info label="Location" value={approval.appointment.location} />
        </div>
        {approval.note && (
          <p className="mt-5 rounded-2xl border border-white/10 bg-white/[0.04] p-4 text-white/70">{approval.note}</p>
        )}
        <div className="mt-8 flex flex-wrap justify-end gap-3">
          <Button
            variant="outline"
            disabled={isPending}
            className="border-white/15 bg-transparent text-white hover:bg-white/[0.08]"
            onClick={() => respond("reject")}
          >
            <Trans>Reject</Trans>
          </Button>
          <Button disabled={isPending} onClick={() => respond("approve")}>
            {isPending && <Loader2Icon className="size-4 animate-spin" />}
            <Trans>Approve reschedule</Trans>
          </Button>
        </div>
      </section>
    </main>
  );
}

function Info({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <div className="text-white/45">{label}</div>
      <div className="mt-1 font-semibold text-white">{value}</div>
    </div>
  );
}

function formatWhen(start: Date, end: Date) {
  const day = new Intl.DateTimeFormat(undefined, {
    weekday: "short",
    month: "short",
    day: "numeric",
    year: "numeric"
  }).format(start);
  const time = new Intl.DateTimeFormat(undefined, { hour: "numeric", minute: "2-digit" }).formatRange(start, end);
  return `${day}, ${time}`;
}
