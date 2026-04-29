import { t } from "@lingui/core/macro";
import { MoreHorizontalIcon } from "lucide-react";

import type { Service } from "@/shared/lib/appointmentsApi";

function modeBadgeClasses(mode: Service["mode"]): string {
  if (mode === "physical") return "bg-success/10 text-success";
  if (mode === "virtual") return "bg-[rgba(0,153,255,0.1)] text-[#0066c2]";
  return "bg-warning/10 text-warning";
}

export function ServiceCard({ service }: { service: Service }) {
  return (
    <article
      className={`flex flex-col gap-2 rounded-xl border border-border bg-background p-3.5 transition-colors hover:border-foreground/20 ${
        service.archived ? "bg-muted opacity-60" : ""
      }`}
    >
      <div className="flex items-center gap-2">
        <span
          className={`inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-[11px] font-medium ${modeBadgeClasses(service.mode)}`}
        >
          <span className="size-[5px] rounded-full bg-current" />
          {service.modeLabel}
        </span>
        {service.archived ? (
          <span className="ml-auto text-[10.5px] tracking-[0.04em] text-muted-foreground uppercase">Archived</span>
        ) : (
          <button
            type="button"
            className="ml-auto p-0.5 text-muted-foreground hover:text-foreground"
            aria-label={t`More options`}
          >
            <MoreHorizontalIcon className="size-3.5" />
          </button>
        )}
      </div>
      <h4 className="font-display text-[15px]">{service.name}</h4>
      <div className="flex justify-between font-mono text-[0.8125rem]">
        <span>{service.duration}</span>
        <span>{service.price}</span>
      </div>
      <div className="flex flex-col gap-0.5 border-t border-border pt-2 text-[11.5px] text-muted-foreground">
        <span className="text-foreground">{service.location}</span>
        {!service.archived && <span>{service.bookingsThisMonth} booked · this month</span>}
      </div>
    </article>
  );
}
