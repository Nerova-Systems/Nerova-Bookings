import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Button } from "@repo/ui/components/Button";
import { createFileRoute, useNavigate } from "@tanstack/react-router";
import { ArrowLeftIcon } from "lucide-react";

import { api } from "@/shared/lib/api/client";

import { SchedulingPageShell } from "../-scheduling/SchedulingPageShell";

export const Route = createFileRoute("/availability/troubleshoot")({
  staticData: { trackingTitle: "Availability troubleshoot" },
  component: AvailabilityTroubleshootPage
});

function AvailabilityTroubleshootPage() {
  const navigate = useNavigate();
  const { data } = api.useQuery("get", "/api/schedules");
  const schedules = data?.schedules ?? [];
  const defaultSchedules = schedules.filter((schedule) => schedule.isDefault);
  const schedulesWithoutWindows = schedules.filter((schedule) => schedule.availabilityWindows.length === 0);

  return (
    <SchedulingPageShell
      title={t`Availability troubleshoot`}
      subtitle={t`Review schedule setup issues that can prevent clients from finding bookable times.`}
      actions={
        <Button type="button" variant="outline" onClick={() => navigate({ to: "/availability" })}>
          <ArrowLeftIcon />
          <Trans>Back to availability</Trans>
        </Button>
      }
    >
      <div className="rounded-md border p-6">
        <h2>
          <Trans>Schedule checks</Trans>
        </h2>
        <div className="mt-4 flex flex-col gap-3 text-sm">
          <CheckRow label={t`At least one schedule exists`} passed={schedules.length > 0} />
          <CheckRow label={t`Exactly one default schedule exists`} passed={defaultSchedules.length === 1} />
          <CheckRow label={t`Every schedule has weekly availability`} passed={schedulesWithoutWindows.length === 0} />
        </div>
      </div>
    </SchedulingPageShell>
  );
}

function CheckRow({ label, passed }: Readonly<{ label: string; passed: boolean }>) {
  return (
    <div className="flex items-center justify-between gap-4 rounded-md border p-3">
      <span>{label}</span>
      <span className={passed ? "text-primary" : "text-destructive"}>
        {passed ? <Trans>Passed</Trans> : <Trans>Needs attention</Trans>}
      </span>
    </div>
  );
}
