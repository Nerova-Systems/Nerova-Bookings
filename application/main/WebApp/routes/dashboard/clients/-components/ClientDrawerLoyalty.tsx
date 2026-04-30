import { Trans } from "@lingui/react/macro";
import { GiftIcon } from "lucide-react";

export function ClientDrawerLoyalty() {
  return (
    <div className="mx-auto grid max-w-xl gap-3 rounded-lg border border-dashed border-border px-5 py-8 text-center">
      <div className="mx-auto flex size-10 items-center justify-center rounded-full bg-muted">
        <GiftIcon className="size-5 text-muted-foreground" />
      </div>
      <h3 className="font-display text-lg font-semibold">
        <Trans>Loyalty program coming later</Trans>
      </h3>
      <p className="text-sm text-muted-foreground">
        <Trans>Future client rewards, visit milestones, packages, and credit tracking will live here.</Trans>
      </p>
    </div>
  );
}
