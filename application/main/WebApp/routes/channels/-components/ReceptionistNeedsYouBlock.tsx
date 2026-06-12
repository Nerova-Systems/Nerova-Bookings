import { Trans } from "@lingui/react/macro";
import { Badge } from "@repo/ui/components/Badge";
import { Button } from "@repo/ui/components/Button";

import { SmartDate } from "@/shared/components/SmartDate";
import { api, queryClient, type Schemas } from "@/shared/lib/api/client";

import { escalationsQueryKey } from "./receptionistHelpers";

type Escalation = Schemas["EscalationResponse"];

export function ReceptionistNeedsYouBlock({
  escalations,
  openCount
}: Readonly<{ escalations: Escalation[]; openCount: number }>) {
  const resolveMutation = api.useMutation("post", "/api/main/receptionist/escalations/{id}/resolve", {
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: escalationsQueryKey });
    }
  });

  if (openCount === 0) {
    return (
      <div className="rounded-lg border bg-muted/30 p-4 text-sm text-muted-foreground">
        <Trans>Nothing needs you right now.</Trans>
      </div>
    );
  }

  return (
    <section className="rounded-lg border border-warning/40 bg-warning/10 p-4">
      <div className="mb-3 flex items-center justify-between gap-3">
        <h3>
          <Trans>Needs you</Trans>
        </h3>
        <Badge variant="warning">
          <Trans>{openCount} open</Trans>
        </Badge>
      </div>
      <div className="flex flex-col gap-3">
        {escalations.map((escalation) => (
          <div key={escalation.id} className="rounded-md border bg-card p-3">
            <div className="flex flex-col gap-1">
              <div className="flex items-center justify-between gap-3">
                <span className="font-medium">{escalation.reason}</span>
                <SmartDate date={escalation.createdAt} className="text-xs text-muted-foreground" />
              </div>
              <p className="text-sm text-muted-foreground">{escalation.summary}</p>
            </div>
            <div className="mt-3 flex flex-wrap gap-2">
              <Button
                size="sm"
                onClick={() =>
                  resolveMutation.mutate({
                    params: { path: { id: escalation.id } },
                    body: { dismiss: false, resolutionNote: null }
                  })
                }
                isPending={resolveMutation.isPending}
              >
                <Trans>Mark handled</Trans>
              </Button>
              <Button
                size="sm"
                variant="outline"
                onClick={() =>
                  resolveMutation.mutate({
                    params: { path: { id: escalation.id } },
                    body: { dismiss: true, resolutionNote: null }
                  })
                }
                isPending={resolveMutation.isPending}
              >
                <Trans>Dismiss</Trans>
              </Button>
            </div>
          </div>
        ))}
      </div>
    </section>
  );
}
