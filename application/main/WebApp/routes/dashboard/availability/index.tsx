import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { createFileRoute, useNavigate } from "@tanstack/react-router";
import { Clock3Icon, Globe2Icon, MoreHorizontalIcon } from "lucide-react";
import { useEffect, useMemo } from "react";

import { useAppointmentShell, type AvailabilityRule } from "@/shared/lib/appointmentsApi";

export const Route = createFileRoute("/dashboard/availability/")({
  staticData: { trackingTitle: "Availability" },
  component: AvailabilityPage
});

function AvailabilityPage() {
  const navigate = useNavigate();
  const shellQuery = useAppointmentShell();
  const summary = useMemo(() => summarizeAvailability(shellQuery.data?.availabilityRules ?? []), [shellQuery.data?.availabilityRules]);

  useEffect(() => {
    document.title = t`Availability | Nerova`;
  }, []);

  return (
    <main className="flex min-h-0 flex-1 flex-col overflow-y-auto bg-[#0f0f0f] px-8 py-7 text-white">
      <header className="flex flex-wrap items-start gap-4">
        <div>
          <h1 className="font-display text-3xl font-semibold">
            <Trans>Availability</Trans>
          </h1>
          <p className="mt-1 text-lg text-white/80">
            <Trans>Configure times when you are available for bookings.</Trans>
          </p>
        </div>
      </header>

      <section className="mt-10 overflow-hidden rounded-xl border border-white/10 bg-[#171717]">
        <button
          type="button"
          onClick={() => navigate({ to: "/dashboard/availability/$scheduleId", params: { scheduleId: "default" } })}
          className="flex w-full items-center gap-4 px-6 py-7 text-left transition-colors hover:bg-white/[0.035]"
        >
          <div className="flex size-11 items-center justify-center rounded-full bg-white/[0.08]">
            <Clock3Icon className="size-5 text-white/75" />
          </div>
          <div className="min-w-0">
            <div className="flex flex-wrap items-center gap-2">
              <h2 className="text-lg font-semibold">
                <Trans>Working hours</Trans>
              </h2>
              <span className="rounded-md bg-white/[0.12] px-2 py-1 text-xs font-semibold text-white/85">
                <Trans>Default</Trans>
              </span>
            </div>
            <p className="mt-2 text-base text-white/75">{summary || "No working hours configured"}</p>
            <p className="mt-2 flex items-center gap-1.5 text-base text-white/55">
              <Globe2Icon className="size-4" />
              {shellQuery.data?.profile.timeZone ?? "Africa/Johannesburg"}
            </p>
          </div>
          <span className="ml-auto flex size-11 items-center justify-center rounded-xl border border-white/15 text-white/70">
            <MoreHorizontalIcon className="size-4" />
          </span>
        </button>
      </section>

      <div className="mt-6 text-center text-sm text-white/80">
        <Trans>Temporarily out-of-office?</Trans>{" "}
        <button
          type="button"
          className="font-semibold underline underline-offset-4"
          onClick={() => navigate({ href: "/user/out-of-office" })}
        >
          <Trans>Add a redirect</Trans>
        </button>
      </div>
    </main>
  );
}

function summarizeAvailability(rules: AvailabilityRule[]) {
  if (rules.length === 0) return "";
  const dayOrder = ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday"];
  const activeDays = dayOrder.filter((day) => rules.some((rule) => rule.dayOfWeek === day));
  const firstWindow = rules[0];
  const dayText = compactDayRange(activeDays);
  return `${dayText}, ${formatTimeLabel(firstWindow.startTime)} - ${formatTimeLabel(firstWindow.endTime)}`;
}

function compactDayRange(days: string[]) {
  if (days.length === 0) return "No days";
  const short = days.map((day) => day.slice(0, 3));
  if (short.join(",") === "Mon,Tue,Wed,Thu,Fri") return "Mon - Fri";
  if (short.join(",") === "Mon,Tue,Wed,Thu,Fri,Sat,Sun") return "Every day";
  return short.join(", ");
}

function formatTimeLabel(value: string) {
  const [hourText, minute] = value.split(":");
  const hour = Number(hourText);
  const suffix = hour >= 12 ? "PM" : "AM";
  const displayHour = hour % 12 || 12;
  return `${displayHour}:${minute} ${suffix}`;
}
