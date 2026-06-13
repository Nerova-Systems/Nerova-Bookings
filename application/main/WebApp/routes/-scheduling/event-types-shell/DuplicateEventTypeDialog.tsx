import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Button } from "@repo/ui/components/Button";
import {
  DialogBody,
  DialogClose,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogForm,
  DialogHeader,
  DialogTitle
} from "@repo/ui/components/Dialog";
import { DirtyDialog } from "@repo/ui/components/DirtyDialog";
import { useDialogSetDirty } from "@repo/ui/components/DirtyDialogContext";
import { NumberField } from "@repo/ui/components/NumberField";
import { TextAreaField } from "@repo/ui/components/TextAreaField";
import { TextField } from "@repo/ui/components/TextField";
import { useNavigate } from "@tanstack/react-router";
import { useEffect, useState } from "react";
import { flushSync } from "react-dom";
import { toast } from "sonner";

import { api, queryClient } from "@/shared/lib/api/client";

import type { EventType, EventTypePayload } from "../schedulingTypes";

import { GeneralApiErrors } from "../ApiErrors";
import { isEventTypePayloadSubmittable, slugify } from "../schedulingTypes";
import { eventTypeToDuplicatePayload } from "./eventTypeShellTypes";

export function DuplicateEventTypeDialog({
  eventType,
  isOpen,
  onOpenChange
}: Readonly<{
  eventType: EventType | null;
  isOpen: boolean;
  onOpenChange: (isOpen: boolean) => void;
}>) {
  return (
    <DirtyDialog trackingTitle={t`Duplicate service`} open={isOpen} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-2xl">
        <DialogHeader>
          <DialogTitle>
            <Trans>Duplicate service</Trans>
          </DialogTitle>
          <DialogDescription>
            <Trans>Review the copied booking basics before creating the duplicate.</Trans>
          </DialogDescription>
        </DialogHeader>
        {eventType && <DuplicateEventTypeDialogBody eventType={eventType} onClose={() => onOpenChange(false)} />}
      </DialogContent>
    </DirtyDialog>
  );
}

function DuplicateEventTypeDialogBody({ eventType, onClose }: Readonly<{ eventType: EventType; onClose: () => void }>) {
  const navigate = useNavigate();
  const setDirty = useDialogSetDirty();
  const [draft, setDraft] = useState<EventTypePayload>(() => eventTypeToDuplicatePayload(eventType));
  const duplicateMutation = api.useMutation("post", "/api/event-types", {
    onSuccess: (createdEventType) => {
      toast.success(t`Service duplicated`);
      void queryClient.invalidateQueries();
      flushSync(() => setDirty(false));
      onClose();
      navigate({
        to: "/event-types/$eventTypeId",
        params: { eventTypeId: createdEventType.id },
        search: { tabName: "setup" }
      });
    }
  });
  const canSubmit = isEventTypePayloadSubmittable(draft);

  useEffect(() => {
    setDraft(eventTypeToDuplicatePayload(eventType));
  }, [eventType]);

  const updateDraft = (nextDraft: EventTypePayload) => {
    setDirty(true);
    setDraft(nextDraft);
  };

  return (
    <DialogForm
      validationErrors={duplicateMutation.error?.errors}
      onSubmit={(event) => {
        event.preventDefault();
        if (!canSubmit) return;
        duplicateMutation.mutate({ body: draft });
      }}
    >
      <DialogBody>
        <GeneralApiErrors error={duplicateMutation.error} />
        <div className="grid gap-4 sm:grid-cols-2">
          <TextField
            name="title"
            label={t`Title`}
            required={true}
            autoFocus={true}
            value={draft.title}
            onChange={(title) => updateDraft({ ...draft, title })}
          />
          <TextField
            name="slug"
            label={t`Slug`}
            required={true}
            value={draft.slug}
            onChange={(slug) => updateDraft({ ...draft, slug: slugify(slug) })}
          />
        </div>
        <TextAreaField
          name="description"
          label={t`Description`}
          lines={3}
          value={draft.description ?? ""}
          onChange={(description) => updateDraft({ ...draft, description: description || null })}
        />
        <NumberField
          name="durationMinutes"
          label={t`Duration`}
          minValue={5}
          maxValue={1440}
          value={draft.durationMinutes}
          onChange={(durationMinutes) => updateDraft({ ...draft, durationMinutes: durationMinutes ?? 30 })}
        />
      </DialogBody>
      <DialogFooter>
        <DialogClose render={<Button type="reset" variant="outline" disabled={duplicateMutation.isPending} />}>
          <Trans>Cancel</Trans>
        </DialogClose>
        <Button type="submit" disabled={!canSubmit} isPending={duplicateMutation.isPending}>
          <Trans>Create duplicate</Trans>
        </Button>
      </DialogFooter>
    </DialogForm>
  );
}
