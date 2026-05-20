import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Button } from "@repo/ui/components/Button";
import { ButtonGroup } from "@repo/ui/components/ButtonGroup";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger
} from "@repo/ui/components/DropdownMenu";
import { Separator } from "@repo/ui/components/Separator";
import { Switch } from "@repo/ui/components/Switch";
import { CodeIcon, CopyIcon, EllipsisIcon, EyeIcon, SaveIcon, Trash2Icon } from "lucide-react";
import { toast } from "sonner";

import type { EventType, EventTypePayload } from "../schedulingTypes";

import { CopyEventTypeButton, PreviewEventTypeButton } from "./EventTypeActionButtons";
import { eventTypeFormId } from "./EventTypeEditorTabs";
import { getEventTypePublicUrl } from "./eventTypeShellTypes";

export function EventTypeHeaderActions({
  eventType,
  draft,
  publicHandle,
  canSave,
  isSaving,
  onDraftChange,
  onDelete
}: Readonly<{
  eventType: EventType;
  draft: EventTypePayload;
  publicHandle?: string | null;
  canSave: boolean;
  isSaving: boolean;
  onDraftChange: (draft: EventTypePayload) => void;
  onDelete: () => void;
}>) {
  return (
    <div className="flex shrink-0 items-center justify-end gap-2" data-testid="event-type-action-bar">
      <div className="hidden items-center gap-2 rounded-md px-2 py-1 text-sm text-muted-foreground lg:flex">
        {draft.hidden && (
          <span className="font-medium text-foreground">
            <Trans>Hidden</Trans>
          </span>
        )}
        <Switch
          aria-label={draft.hidden ? t`Show on profile` : t`Hide from profile`}
          checked={!draft.hidden}
          onCheckedChange={(isVisible) => onDraftChange({ ...draft, hidden: !isVisible })}
        />
      </div>
      <Separator orientation="vertical" className="hidden h-8 lg:block" />
      <ButtonGroup className="hidden lg:flex">
        <PreviewEventTypeButton eventType={eventType} publicHandle={publicHandle} />
        <CopyEventTypeButton eventType={eventType} publicHandle={publicHandle} />
        <EventTypeEmbedPlaceholderButton />
        <DeleteEventTypeHeaderButton variant="outline" onDelete={onDelete} />
      </ButtonGroup>
      <MobileEventTypeActions
        eventType={eventType}
        draft={draft}
        publicHandle={publicHandle}
        onDraftChange={onDraftChange}
        onDelete={onDelete}
      />
      <Button type="submit" form={eventTypeFormId} size="sm" disabled={!canSave} isPending={isSaving}>
        <SaveIcon />
        {isSaving ? <Trans>Saving...</Trans> : <Trans>Save</Trans>}
      </Button>
    </div>
  );
}

function MobileEventTypeActions({
  eventType,
  draft,
  publicHandle,
  onDraftChange,
  onDelete
}: Readonly<{
  eventType: EventType;
  draft: EventTypePayload;
  publicHandle?: string | null;
  onDraftChange: (draft: EventTypePayload) => void;
  onDelete: () => void;
}>) {
  const publicUrl = getEventTypePublicUrl(eventType, publicHandle);

  return (
    <DropdownMenu trackingTitle={t`Event type actions`}>
      <DropdownMenuTrigger
        render={
          <Button
            type="button"
            variant="outline"
            size="icon-sm"
            aria-label={t`Event type actions`}
            className="lg:hidden"
          >
            <EllipsisIcon />
            <span className="sr-only">
              <Trans>Event type actions</Trans>
            </span>
          </Button>
        }
      />
      <DropdownMenuContent align="end" className="w-56">
        <DropdownMenuItem onClick={() => window.open(publicUrl, "_blank", "noopener,noreferrer")}>
          <EyeIcon />
          <Trans>Preview</Trans>
        </DropdownMenuItem>
        <DropdownMenuItem
          onClick={() => {
            void navigator.clipboard?.writeText(publicUrl);
            toast.success(t`Public link copied`);
          }}
        >
          <CopyIcon />
          <Trans>Copy link</Trans>
        </DropdownMenuItem>
        <DropdownMenuItem disabled>
          <CodeIcon />
          <Trans>Embed</Trans>
        </DropdownMenuItem>
        <DropdownMenuSeparator />
        <DropdownMenuItem onClick={() => onDraftChange({ ...draft, hidden: !draft.hidden })}>
          <EyeIcon />
          {draft.hidden ? <Trans>Show on profile</Trans> : <Trans>Hide from profile</Trans>}
        </DropdownMenuItem>
        <DropdownMenuItem onClick={onDelete} variant="destructive">
          <Trash2Icon />
          <Trans>Delete</Trans>
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}

function EventTypeEmbedPlaceholderButton() {
  return (
    <Button type="button" variant="ghost" size="icon-sm" disabled aria-label={t`Embed`}>
      <CodeIcon />
    </Button>
  );
}

function DeleteEventTypeHeaderButton({
  variant,
  onDelete
}: Readonly<{ variant: "ghost" | "outline"; onDelete: () => void }>) {
  return (
    <Button type="button" variant={variant} size="icon-sm" aria-label={t`Delete`} onClick={onDelete}>
      <Trash2Icon />
      <span className="sr-only">
        <Trans>Delete</Trans>
      </span>
    </Button>
  );
}
