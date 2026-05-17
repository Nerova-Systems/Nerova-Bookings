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
import { CopyIcon, EllipsisIcon, EyeIcon, FilesIcon, Trash2Icon } from "lucide-react";
import { toast } from "sonner";

import type { EventType } from "../schedulingTypes";

import { getEventTypePublicUrl } from "./eventTypeShellTypes";

function copyPublicUrl(eventType: EventType) {
  const url = getEventTypePublicUrl(eventType);
  void navigator.clipboard?.writeText(url);
  toast.success(t`Public link copied`);
}

function previewPublicUrl(eventType: EventType) {
  window.open(getEventTypePublicUrl(eventType), "_blank", "noopener,noreferrer");
}

export function CopyEventTypeButton({ eventType }: Readonly<{ eventType: EventType }>) {
  return (
    <Tooltip>
      <TooltipTrigger
        render={
          <Button type="button" variant="ghost" size="icon-sm" onClick={() => copyPublicUrl(eventType)}>
            <CopyIcon />
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

export function PreviewEventTypeButton({ eventType }: Readonly<{ eventType: EventType }>) {
  return (
    <Tooltip>
      <TooltipTrigger
        render={
          <Button type="button" variant="ghost" size="icon-sm" onClick={() => previewPublicUrl(eventType)}>
            <EyeIcon />
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
  onDuplicate,
  onDelete
}: Readonly<{
  eventType: EventType;
  onDuplicate: () => void;
  onDelete: () => void;
}>) {
  return (
    <DropdownMenu trackingTitle={t`Event type actions`}>
      <DropdownMenuTrigger
        render={
          <Button type="button" variant="ghost" size="icon-sm">
            <EllipsisIcon />
            <span className="sr-only">
              <Trans>Event type actions</Trans>
            </span>
          </Button>
        }
      />
      <DropdownMenuContent align="end">
        <DropdownMenuItem onClick={() => copyPublicUrl(eventType)} trackingLabel={t`Copy public link`}>
          <CopyIcon />
          <Trans>Copy link</Trans>
        </DropdownMenuItem>
        <DropdownMenuItem onClick={() => previewPublicUrl(eventType)} trackingLabel={t`Preview booking page`}>
          <EyeIcon />
          <Trans>Preview</Trans>
        </DropdownMenuItem>
        <DropdownMenuItem onClick={onDuplicate} trackingLabel={t`Duplicate event type`}>
          <FilesIcon />
          <Trans>Duplicate</Trans>
        </DropdownMenuItem>
        <DropdownMenuItem onClick={onDelete} trackingLabel={t`Delete event type`} variant="destructive">
          <Trash2Icon />
          <Trans>Delete</Trans>
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
