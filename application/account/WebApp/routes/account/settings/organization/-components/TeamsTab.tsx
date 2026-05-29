import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { buttonVariants } from "@repo/ui/components/Button";
import { Section } from "@repo/ui/components/Section";
import { Link } from "@tanstack/react-router";
import { ArrowRightIcon } from "lucide-react";

import { api } from "@/shared/lib/api/client";

export function OrgTeamsTab() {
  const { data: teams } = api.useQuery("get", "/api/account/teams");
  const count = teams?.length ?? 0;

  return (
    <Section
      title={t`Teams`}
      description={t`Group members into teams to share event types, schedules, and round-robin assignments.`}
    >
      <div className="flex items-center justify-between gap-4">
        <p className="text-sm text-muted-foreground">
          {count === 0 ? (
            <Trans>No teams yet. Create your first team to start collaborating.</Trans>
          ) : (
            <Trans>This organization has {count} team(s).</Trans>
          )}
        </p>
        <Link to="/account/teams" className={buttonVariants({ variant: "default" })} aria-label={t`Manage teams`}>
          <Trans>Manage teams</Trans>
          <ArrowRightIcon />
        </Link>
      </div>
    </Section>
  );
}
