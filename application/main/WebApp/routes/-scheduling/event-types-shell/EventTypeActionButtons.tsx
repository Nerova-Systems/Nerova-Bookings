import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Button } from "@repo/ui/components/Button";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger
} from "@repo/ui/components/DropdownMenu";
import { Tooltip, TooltipContent, TooltipTrigger } from "@repo/ui/components/Tooltip";
import { EllipsisIcon, ExternalLinkIcon, FilesIcon, LinkIcon, Trash2Icon } from "lucide-react";
import { toast } from "sonner";

import type { EventType } from "../schedulingTypes";

import { getEventTypePublicUrl } from "./eventTypeShellTypes";

function copyPublicUrl(eventType: EventType, publicHandle?: string | null) {
  const url = getEventTypePublicUrl(eventType, publicHandle);
  void navigator.clipboard?.writeText(url);
  toast.success(t`Public link copied`);
}

function previewPublicUrl(eventType: EventType, publicHandle?: string | null) {
  window.open(getEventTypePublicUrl(eventType, publicHandle), "_blank", "noopener,noreferrer");
}

export function CopyEventTypeButton({
  eventType,
  publicHandle
}: Readonly<{ eventType: EventType; publicHandle?: string | null }>) {
  return (
    <Tooltip>
      <TooltipTrigger
        render={
          <Button type="button" variant="ghost" size="icon-sm" onClick={() => copyPublicUrl(eventType, publicHandle)}>
            <LinkIcon />
            <span className="sr-only">
              <Trans>Copy public link</Trans>
            </span>
          </Button>
        }
      />
      <TooltipContent>
        <Trans>Copy public link</Trans>
      </TooltipContent>
    </Tooltip>
  );
}

export function PreviewEventTypeButton({
  eventType,
  publicHandle
}: Readonly<{ eventType: EventType; publicHandle?: string | null }>) {
  return (
    <Tooltip>
      <TooltipTrigger
        render={
          <Button
            type="button"
            variant="ghost"
            size="icon-sm"
            aria-label={t`Preview booking page`}
            onClick={() => previewPublicUrl(eventType, publicHandle)}
          >
            <ExternalLinkIcon />
            <span className="sr-only">
              <Trans>Preview booking page</Trans>
            </span>
          </Button>
        }
      />
      <TooltipContent>
        <Trans>Preview booking page</Trans>
      </TooltipContent>
    </Tooltip>
  );
}

export function EventTypeOverflowActions({
  eventType,
  publicHandle,
  onDuplicate,
  onDelete
}: Readonly<{
  eventType: EventType;
  publicHandle?: string | null;
  onDuplicate: () => void;
  onDelete: () => void;
}>) {
  return (
    <DropdownMenu trackingTitle={t`Service actions`}>
      <DropdownMenuTrigger
        render={
          <Button type="button" variant="ghost" size="icon-sm" aria-label={t`Service actions`}>
            <EllipsisIcon />
            <span className="sr-only">
              <Trans>Service actions</Trans>
            </span>
          </Button>
        }
      />
      <DropdownMenuContent align="end">
        <DropdownMenuItem onClick={() => copyPublicUrl(eventType, publicHandle)} trackingLabel={t`Copy public link`}>
          <LinkIcon />
          <Trans>Copy link</Trans>
        </DropdownMenuItem>
        <DropdownMenuItem
          onClick={() => previewPublicUrl(eventType, publicHandle)}
          trackingLabel={t`Preview booking page`}
        >
          <ExternalLinkIcon />
          <Trans>Preview</Trans>
        </DropdownMenuItem>
        <DropdownMenuItem onClick={onDuplicate} trackingLabel={t`Duplicate service`}>
          <FilesIcon />
          <Trans>Duplicate</Trans>
        </DropdownMenuItem>
        <DropdownMenuItem onClick={onDelete} trackingLabel={t`Delete service`} variant="destructive">
          <Trash2Icon />
          <Trans>Delete</Trans>
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
