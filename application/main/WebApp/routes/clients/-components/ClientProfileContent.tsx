import { Trans } from "@lingui/react/macro";
import { Avatar, AvatarFallback, AvatarImage } from "@repo/ui/components/Avatar";
import { Badge } from "@repo/ui/components/Badge";
import { Separator } from "@repo/ui/components/Separator";
import { useFormatDate } from "@repo/ui/hooks/useSmartDate";
import { getInitials } from "@repo/utils/string/getInitials";
import { HashIcon } from "lucide-react";

import type { components } from "@/shared/lib/api/client";

type ClientDetails = components["schemas"]["ClientDetails"];

export function ClientProfileContent({ client }: Readonly<{ client: ClientDetails }>) {
  const formatDate = useFormatDate();

  return (
    <>
      {/* Client Avatar and Basic Info */}
      <div className="mb-6 text-center">
        <Avatar className="mx-auto mb-3 size-16">
          <AvatarImage src={client.avatarUrl ?? undefined} />
          <AvatarFallback>{getInitials(client.firstName, client.lastName, client.email ?? "")}</AvatarFallback>
        </Avatar>
        <h4>
          {client.firstName} {client.lastName}
        </h4>
        <span className="mt-1 inline-flex items-center gap-1.5 font-mono text-xs text-muted-foreground">
          <HashIcon className="size-3" aria-hidden={true} />
          {client.id}
        </span>
      </div>

      {/* Contact Information */}
      <div className="mb-4">
        <div className="space-y-4">
          <div className="flex items-start justify-between">
            <p className="text-sm">
              <Trans>Email</Trans>
            </p>
            <div className="flex flex-col items-end gap-1">
              {client.email ? (
                <p className="text-right text-sm">{client.email}</p>
              ) : (
                <Badge variant="outline" className="gap-1 text-xs text-warning">
                  <Trans>Missing</Trans>
                </Badge>
              )}
            </div>
          </div>
          <div className="flex items-start justify-between">
            <p className="text-sm">
              <Trans>Phone</Trans>
            </p>
            <div className="flex flex-col items-end gap-1">
              {client.phoneNumber ? (
                <p className="text-right text-sm">{client.phoneNumber}</p>
              ) : (
                <Badge variant="outline" className="gap-1 text-xs text-warning">
                  <Trans>Missing</Trans>
                </Badge>
              )}
            </div>
          </div>
        </div>
      </div>

      <Separator className="mb-4" />

      {/* Visit Details */}
      <div className="mb-4">
        <div className="space-y-4">
          <div className="flex justify-between">
            <p className="text-sm">
              <Trans>First visit</Trans>
            </p>
            <p className="text-sm">{formatDate(client.createdAt, true)}</p>
          </div>
          <div className="flex justify-between">
            <p className="text-sm">
              <Trans>Last visit</Trans>
            </p>
            <p className="text-sm">{client.lastVisitAt ? formatDate(client.lastVisitAt, true) : "—"}</p>
          </div>
        </div>
      </div>
    </>
  );
}
