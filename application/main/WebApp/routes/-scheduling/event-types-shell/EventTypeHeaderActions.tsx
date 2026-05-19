import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Button } from "@repo/ui/components/Button";
import { ButtonGroup } from "@repo/ui/components/ButtonGroup";
import { Separator } from "@repo/ui/components/Separator";
import { Switch } from "@repo/ui/components/Switch";
import { SaveIcon, Trash2Icon } from "lucide-react";

import type { EventType, EventTypePayload } from "../schedulingTypes";

import { CopyEventTypeButton, PreviewEventTypeButton } from "./EventTypeActionButtons";
import { eventTypeFormId } from "./EventTypeEditorTabs";

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
    <div className="flex flex-wrap items-center justify-end gap-3" data-testid="event-type-action-bar">
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
        <DeleteEventTypeHeaderButton variant="outline" onDelete={onDelete} />
      </ButtonGroup>
      <div className="flex items-center gap-1 lg:hidden">
        <PreviewEventTypeButton eventType={eventType} publicHandle={publicHandle} />
        <CopyEventTypeButton eventType={eventType} publicHandle={publicHandle} />
        <DeleteEventTypeHeaderButton variant="ghost" onDelete={onDelete} />
      </div>
      <Button type="submit" form={eventTypeFormId} size="sm" disabled={!canSave} isPending={isSaving}>
        <SaveIcon />
        {isSaving ? <Trans>Saving...</Trans> : <Trans>Save</Trans>}
      </Button>
    </div>
  );
}

function DeleteEventTypeHeaderButton({
  variant,
  onDelete
}: Readonly<{ variant: "ghost" | "outline"; onDelete: () => void }>) {
  return (
    <Button type="button" variant={variant} size="icon-sm" aria-label={t`Delete`} onClick={onDelete}>
      <Trash2Icon />
    </Button>
  );
}
