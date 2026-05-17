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
  DialogTitle,
  DialogTrigger
} from "@repo/ui/components/Dialog";
import { DirtyDialog } from "@repo/ui/components/DirtyDialog";
import { useDialogSetDirty } from "@repo/ui/components/DirtyDialogContext";
import { NumberField } from "@repo/ui/components/NumberField";
import { TextAreaField } from "@repo/ui/components/TextAreaField";
import { TextField } from "@repo/ui/components/TextField";
import { useNavigate } from "@tanstack/react-router";
import { PlusIcon } from "lucide-react";
import { useMemo, useState } from "react";
import { toast } from "sonner";

import { api, queryClient } from "@/shared/lib/api/client";

import { GeneralApiErrors } from "./ApiErrors";
import { isEventTypePayloadSubmittable, newEventTypePayload, type Schedule, slugify } from "./schedulingTypes";

export function CreateEventTypeDialog({
  schedules,
  isOpen,
  onOpenChange,
  showTrigger = true
}: Readonly<{
  schedules: Schedule[];
  isOpen?: boolean;
  onOpenChange?: (isOpen: boolean) => void;
  showTrigger?: boolean;
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
      {showTrigger && (
        <DialogTrigger render={<Button disabled={!defaultSchedule} />}>
          <PlusIcon />
          <Trans>New event type</Trans>
        </DialogTrigger>
      )}
      <DialogContent className="sm:max-w-2xl">
        <DialogHeader>
          <DialogTitle>
            <Trans>Add new event type</Trans>
          </DialogTitle>
          <DialogDescription>
            <Trans>Create the booking page basics now, then finish setup on the next screen.</Trans>
          </DialogDescription>
        </DialogHeader>
        {open && <CreateEventTypeDialogBody defaultSchedule={defaultSchedule} onClose={() => handleOpenChange(false)} />}
      </DialogContent>
    </DirtyDialog>
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
      onClose();
      navigate({ to: "/event-types/$eventTypeId", params: { eventTypeId: eventType.id }, search: { tabName: "setup" } });
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
            onChange={(title) =>
              updateDraft({ ...draft, title, slug: slugWasEdited ? draft.slug : slugify(title) })
            }
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
