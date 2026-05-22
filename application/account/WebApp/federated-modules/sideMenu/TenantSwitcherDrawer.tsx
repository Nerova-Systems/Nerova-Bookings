import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Badge } from "@repo/ui/components/Badge";
import { Button } from "@repo/ui/components/Button";
import { Dialog, DialogBody, DialogContent, DialogHeader, DialogTitle } from "@repo/ui/components/Dialog";
import { TenantLogo } from "@repo/ui/components/TenantLogo";
import { Check } from "lucide-react";

import { groupScopes, sortTenants, type TenantInfo } from "../common/tenantUtils";

function ScopeButton({
  scope,
  isCurrent,
  indent,
  onSelect
}: Readonly<{ scope: TenantInfo; isCurrent: boolean; indent: boolean; onSelect: () => void }>) {
  return (
    <Button
      variant="ghost"
      onClick={onSelect}
      disabled={isCurrent || scope.isNew}
      className={`flex h-[var(--control-height)] w-full items-center justify-start gap-3 rounded-md py-2 text-sm font-normal hover:bg-hover-background active:bg-hover-background disabled:cursor-default disabled:opacity-100 ${indent ? "pr-3 pl-9" : "px-3"}`}
    >
      <TenantLogo logoUrl={scope.logoUrl} tenantName={scope.tenantName || ""} />
      <div className="flex min-w-0 flex-1 items-center justify-between gap-2">
        <span className="overflow-hidden text-left text-ellipsis whitespace-nowrap">
          {scope.tenantName || t`Unnamed account`}
        </span>
        <div className="flex shrink-0 items-center gap-2">
          {scope.isNew && (
            <Badge variant="secondary" className="bg-warning text-xs text-warning-foreground">
              <Trans>Invitation pending</Trans>
            </Badge>
          )}
          {isCurrent && <Check className="size-4" />}
        </div>
      </div>
    </Button>
  );
}

export function TenantSwitcherDrawer({
  isOpen,
  onOpenChange,
  tenants,
  currentTenantId,
  onTenantSwitch
}: {
  isOpen: boolean;
  onOpenChange: (open: boolean) => void;
  tenants: TenantInfo[];
  currentTenantId: string | undefined;
  onTenantSwitch: (tenant: TenantInfo) => void;
}) {
  const sortedTenants = sortTenants(tenants);
  const { solo, orgs, orphanTeams } = groupScopes(sortedTenants);

  return (
    <Dialog open={isOpen} onOpenChange={onOpenChange} modal={false} trackingTitle="Switch account">
      <DialogContent
        className="top-auto bottom-0 h-auto max-h-[70dvh] translate-y-0 rounded-t-2xl sm:top-auto sm:bottom-0 sm:max-h-[70dvh] sm:-translate-y-0 sm:rounded-t-2xl sm:rounded-b-none"
        showCloseButton={false}
      >
        <DialogHeader>
          <DialogTitle>
            <Trans>Select account</Trans>
          </DialogTitle>
        </DialogHeader>
        <DialogBody>
          <div className="flex flex-col gap-3">
            {solo.length > 0 && (
              <div className="flex flex-col gap-1">
                <h6 className="px-3 text-xs font-medium text-muted-foreground">
                  <Trans>Solo</Trans>
                </h6>
                {solo.map((scope) => (
                  <ScopeButton
                    key={scope.tenantId}
                    scope={scope}
                    isCurrent={scope.tenantId === currentTenantId}
                    indent={false}
                    onSelect={() => onTenantSwitch(scope)}
                  />
                ))}
              </div>
            )}

            {orgs.length > 0 && (
              <div className="flex flex-col gap-1">
                <h6 className="px-3 text-xs font-medium text-muted-foreground">
                  <Trans>Organizations</Trans>
                </h6>
                {orgs.map(({ org, teams: orgTeams }) => (
                  <div key={org.tenantId} className="flex flex-col">
                    <ScopeButton
                      scope={org}
                      isCurrent={org.tenantId === currentTenantId}
                      indent={false}
                      onSelect={() => onTenantSwitch(org)}
                    />
                    {orgTeams.map((team) => (
                      <ScopeButton
                        key={team.tenantId}
                        scope={team}
                        isCurrent={team.tenantId === currentTenantId}
                        indent
                        onSelect={() => onTenantSwitch(team)}
                      />
                    ))}
                  </div>
                ))}
              </div>
            )}

            {orphanTeams.length > 0 && (
              <div className="flex flex-col gap-1">
                <h6 className="px-3 text-xs font-medium text-muted-foreground">
                  <Trans>Teams</Trans>
                </h6>
                {orphanTeams.map((team) => (
                  <ScopeButton
                    key={team.tenantId}
                    scope={team}
                    isCurrent={team.tenantId === currentTenantId}
                    indent={false}
                    onSelect={() => onTenantSwitch(team)}
                  />
                ))}
              </div>
            )}
          </div>
        </DialogBody>
      </DialogContent>
    </Dialog>
  );
}
