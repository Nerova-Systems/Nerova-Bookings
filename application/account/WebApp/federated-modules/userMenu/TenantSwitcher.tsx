import type { useUserInfo } from "@repo/infrastructure/auth/hooks";

import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { trackInteraction } from "@repo/infrastructure/applicationInsights/ApplicationInsightsProvider";
import { hasPermission } from "@repo/infrastructure/auth/routeGuards";
import { useFeatureFlag } from "@repo/infrastructure/featureFlags/useFeatureFlag";
import { Badge } from "@repo/ui/components/Badge";
import {
  DropdownMenuGroup,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuSub,
  DropdownMenuSubContent,
  DropdownMenuSubTrigger
} from "@repo/ui/components/DropdownMenu";
import { TenantLogo } from "@repo/ui/components/TenantLogo";
import { ArrowRightLeftIcon, Check, PlusIcon } from "lucide-react";

import type { TenantInfo } from "../common/tenantUtils";

import { groupScopes } from "../common/tenantUtils";

interface TenantSwitcherProps {
  sortedTenants: TenantInfo[];
  currentTenantId: string | undefined;
  isLoadingTenants: boolean;
  userInfo: NonNullable<ReturnType<typeof useUserInfo>>;
  onTenantSwitch: (tenant: TenantInfo) => void;
  onCreateTeam?: () => void;
  onCreateOrganization?: () => void;
}

function ScopeRow({
  scope,
  isCurrent,
  indent,
  onSelect
}: Readonly<{ scope: TenantInfo; isCurrent: boolean; indent: boolean; onSelect: () => void }>) {
  return (
    <DropdownMenuItem key={scope.tenantId} onClick={onSelect} className={indent ? "pl-8" : undefined}>
      <TenantLogo logoUrl={scope.logoUrl} tenantName={scope.tenantName || ""} />
      <div className="flex flex-1 items-center justify-between gap-2">
        <span className="overflow-hidden text-ellipsis whitespace-nowrap">
          {scope.tenantName || t`Unnamed account`}
        </span>
        <div className="flex shrink-0 items-center gap-2">
          {scope.isNew && (
            <Badge variant="secondary" className="bg-warning text-xs text-warning-foreground">
              <Trans>Invitation pending</Trans>
            </Badge>
          )}
          <Check className={`ml-2 size-4 shrink-0 ${isCurrent ? "" : "invisible"}`} />
        </div>
      </div>
    </DropdownMenuItem>
  );
}

export function TenantSwitcher({
  sortedTenants,
  currentTenantId,
  isLoadingTenants,
  onTenantSwitch,
  onCreateTeam,
  onCreateOrganization
}: Readonly<TenantSwitcherProps>) {
  const teamsTier = useFeatureFlag("tier-teams");
  const orgsTier = useFeatureFlag("tier-organizations");
  // PBAC is server-side only; use role-based gating as a pragmatic substitute for `team.create`
  // / `organization.create` permissions until the PBAC HTTP surface lands.
  const canCreateScopes = hasPermission({ allowedRoles: ["Owner", "Admin"] });

  const showCreateTeam = teamsTier.enabled && canCreateScopes && onCreateTeam !== undefined;
  const showCreateOrg = orgsTier.enabled && canCreateScopes && onCreateOrganization !== undefined;
  const hasCreateActions = showCreateTeam || showCreateOrg;

  // Hide entire sub-menu when there is nothing to switch to AND no create CTAs to surface.
  if (sortedTenants.length <= 1 && !hasCreateActions) {
    return null;
  }

  const { solo, orgs, orphanTeams } = groupScopes(sortedTenants);

  return (
    <DropdownMenuSub
      onOpenChange={(open) => {
        if (open) {
          trackInteraction("Switch account", "menu", "Open");
        }
      }}
    >
      <DropdownMenuSubTrigger aria-label={t`Switch account`}>
        <ArrowRightLeftIcon className="size-5" />
        <Trans>Switch account</Trans>
      </DropdownMenuSubTrigger>
      <DropdownMenuSubContent className="w-fit min-w-64">
        {isLoadingTenants ? (
          <DropdownMenuGroup>
            <DropdownMenuLabel>
              <Trans>Loading...</Trans>
            </DropdownMenuLabel>
          </DropdownMenuGroup>
        ) : (
          <>
            {solo.length > 0 && (
              <DropdownMenuGroup>
                <DropdownMenuLabel>
                  <Trans>Solo</Trans>
                </DropdownMenuLabel>
                {solo.map((scope) => (
                  <ScopeRow
                    key={scope.tenantId}
                    scope={scope}
                    isCurrent={scope.tenantId === currentTenantId}
                    indent={false}
                    onSelect={() => onTenantSwitch(scope)}
                  />
                ))}
              </DropdownMenuGroup>
            )}

            {orgs.length > 0 && (
              <>
                {solo.length > 0 && <DropdownMenuSeparator />}
                <DropdownMenuGroup>
                  <DropdownMenuLabel>
                    <Trans>Organizations</Trans>
                  </DropdownMenuLabel>
                  {orgs.map(({ org, teams: orgTeams }) => (
                    <DropdownMenuGroup key={org.tenantId}>
                      <ScopeRow
                        scope={org}
                        isCurrent={org.tenantId === currentTenantId}
                        indent={false}
                        onSelect={() => onTenantSwitch(org)}
                      />
                      {orgTeams.map((team) => (
                        <ScopeRow
                          key={team.tenantId}
                          scope={team}
                          isCurrent={team.tenantId === currentTenantId}
                          indent
                          onSelect={() => onTenantSwitch(team)}
                        />
                      ))}
                    </DropdownMenuGroup>
                  ))}
                </DropdownMenuGroup>
              </>
            )}

            {orphanTeams.length > 0 && (
              <>
                {(solo.length > 0 || orgs.length > 0) && <DropdownMenuSeparator />}
                <DropdownMenuGroup>
                  <DropdownMenuLabel>
                    <Trans>Teams</Trans>
                  </DropdownMenuLabel>
                  {orphanTeams.map((team) => (
                    <ScopeRow
                      key={team.tenantId}
                      scope={team}
                      isCurrent={team.tenantId === currentTenantId}
                      indent={false}
                      onSelect={() => onTenantSwitch(team)}
                    />
                  ))}
                </DropdownMenuGroup>
              </>
            )}

            {hasCreateActions && (
              <>
                <DropdownMenuSeparator />
                <DropdownMenuGroup>
                  {showCreateTeam && (
                    <DropdownMenuItem onClick={onCreateTeam} aria-label={t`Create new team`}>
                      <PlusIcon className="size-5" />
                      <Trans>Create new team</Trans>
                    </DropdownMenuItem>
                  )}
                  {showCreateOrg && (
                    <DropdownMenuItem onClick={onCreateOrganization} aria-label={t`Create new organization`}>
                      <PlusIcon className="size-5" />
                      <Trans>Create new organization</Trans>
                    </DropdownMenuItem>
                  )}
                </DropdownMenuGroup>
              </>
            )}
          </>
        )}
      </DropdownMenuSubContent>
    </DropdownMenuSub>
  );
}
