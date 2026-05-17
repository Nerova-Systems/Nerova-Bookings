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
import { Empty, EmptyDescription, EmptyHeader, EmptyMedia, EmptyTitle } from "@repo/ui/components/Empty";
import { NumberField } from "@repo/ui/components/NumberField";
import { TextField } from "@repo/ui/components/TextField";
import { useNavigate } from "@tanstack/react-router";
import { CalendarDaysIcon } from "lucide-react";
import { useMemo, useState } from "react";
import { flushSync } from "react-dom";
import { toast } from "sonner";

import { api, queryClient } from "@/shared/lib/api/client";

import { GeneralApiErrors } from "./ApiErrors";
import { isEventTypePayloadSubmittable, newEventTypePayload, type Schedule, slugify } from "./schedulingTypes";

export function CreateEventTypeDialog({
  schedules,
  isOpen,
  onOpenChange
}: Readonly<{
  schedules: Schedule[];
  isOpen?: boolean;
  onOpenChange?: (isOpen: boolean) => void;
}>) {
  const defaultSchedule = useMemo(() => schedules.find((schedule) => schedule.isDefault) ?? schedules[0], [schedules]);
  const [uncontrolledOpen, setUncontrolledOpen] = useState(false);
  const open = isOpen ?? uncontrolledOpen;

  const handleOpenChange = (nextOpen: boolean) => {
    onOpenChange?.(nextOpen);
    if (isOpen === undefined) setUncontrolledOpen(nextOpen);
  };

  return (
    <DirtyDialog trackingTitle={t`Create event type`} open={open} onOpenChange={handleOpenChange}>
      <DialogContent className="sm:w-dialog-lg">
        <DialogHeader>
          <DialogTitle>
            <Trans>Add new event type</Trans>
          </DialogTitle>
          <DialogDescription>
            <Trans>Create the booking page basics now, then finish setup on the next screen.</Trans>
          </DialogDescription>
        </DialogHeader>
        {open &&
          (defaultSchedule ? (
            <CreateEventTypeDialogBody defaultSchedule={defaultSchedule} onClose={() => handleOpenChange(false)} />
          ) : (
            <CreateEventTypeNoScheduleBody onClose={() => handleOpenChange(false)} />
          ))}
      </DialogContent>
    </DirtyDialog>
  );
}

function CreateEventTypeNoScheduleBody({ onClose }: Readonly<{ onClose: () => void }>) {
  const navigate = useNavigate();

  return (
    <>
      <DialogBody>
        <Empty className="min-h-48 border">
          <EmptyHeader>
            <EmptyMedia variant="icon">
              <CalendarDaysIcon />
            </EmptyMedia>
            <EmptyTitle>
              <Trans>Create availability first</Trans>
            </EmptyTitle>
            <EmptyDescription>
              <Trans>Create an availability schedule before adding event types.</Trans>
            </EmptyDescription>
          </EmptyHeader>
        </Empty>
      </DialogBody>
      <DialogFooter>
        <DialogClose render={<Button type="reset" variant="outline" />}>
          <Trans>Cancel</Trans>
        </DialogClose>
        <Button
          type="button"
          onClick={() => {
            onClose();
            navigate({ to: "/availability" });
          }}
        >
          <CalendarDaysIcon />
          <Trans>Go to availability</Trans>
        </Button>
      </DialogFooter>
    </>
  );
}

function CreateEventTypeDialogBody({
  defaultSchedule,
  onClose
}: Readonly<{
  defaultSchedule: Schedule | undefined;
  onClose: () => void;
}>) {
  const navigate = useNavigate();
  const setDirty = useDialogSetDirty();
  const [slugWasEdited, setSlugWasEdited] = useState(false);
  const [draft, setDraft] = useState(() => newEventTypePayload(defaultSchedule?.id ?? ""));
  const { error, isPending, mutate } = api.useMutation("post", "/api/event-types", {
    onSuccess: (eventType) => {
      toast.success(t`Event type created`);
      void queryClient.invalidateQueries();
      flushSync(() => setDirty(false));
      onClose();
      navigate({
        to: "/event-types/$eventTypeId",
        params: { eventTypeId: eventType.id },
        search: { tabName: "setup" }
      });
    }
  });
  const canSubmit = defaultSchedule !== undefined && isEventTypePayloadSubmittable(draft);

  const updateDraft = (nextDraft: typeof draft) => {
    setDirty(true);
    setDraft(nextDraft);
  };

  return (
    <DialogForm
      validationErrors={error?.errors}
      onSubmit={(event) => {
        event.preventDefault();
        if (!defaultSchedule || !canSubmit) return;
        mutate({ body: { ...draft, scheduleId: defaultSchedule.id } });
      }}
    >
      <DialogBody>
        <GeneralApiErrors error={error} />
        <div className="grid gap-4 sm:grid-cols-2">
          <TextField
            name="title"
            label={t`Title`}
            required={true}
            autoFocus={true}
            value={draft.title}
            onChange={(title) => updateDraft({ ...draft, title, slug: slugWasEdited ? draft.slug : slugify(title) })}
          />
          <TextField
            name="slug"
            label={t`Slug`}
            required={true}
            value={draft.slug}
            onChange={(slug) => {
              setSlugWasEdited(true);
              updateDraft({ ...draft, slug: slugify(slug) });
            }}
          />
        </div>
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
        <DialogClose render={<Button type="reset" variant="outline" disabled={isPending} />}>
          <Trans>Cancel</Trans>
        </DialogClose>
        <Button type="submit" disabled={!canSubmit} isPending={isPending}>
          <Trans>Continue</Trans>
        </Button>
      </DialogFooter>
    </DialogForm>
  );
}
