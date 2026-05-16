import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Button } from "@repo/ui/components/Button";
import {
  Dialog,
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
import { NumberField } from "@repo/ui/components/NumberField";
import { TextAreaField } from "@repo/ui/components/TextAreaField";
import { TextField } from "@repo/ui/components/TextField";
import { useNavigate } from "@tanstack/react-router";
import { PlusIcon } from "lucide-react";
import { useMemo, useState } from "react";
import { toast } from "sonner";

import { api } from "@/shared/lib/api/client";

import { GeneralApiErrors } from "./ApiErrors";
import { isEventTypePayloadSubmittable, newEventTypePayload, type Schedule, slugify } from "./schedulingTypes";

export function CreateEventTypeDialog({ schedules }: Readonly<{ schedules: Schedule[] }>) {
  const navigate = useNavigate();
  const defaultSchedule = useMemo(() => schedules.find((schedule) => schedule.isDefault) ?? schedules[0], [schedules]);
  const [open, setOpen] = useState(false);
  const [slugWasEdited, setSlugWasEdited] = useState(false);
  const [draft, setDraft] = useState(() => newEventTypePayload(defaultSchedule?.id ?? ""));
  const { error, isPending, mutate, reset } = api.useMutation("post", "/api/event-types", {
    onSuccess: (eventType) => {
      toast.success(t`Event type created`);
      setOpen(false);
      navigate({ to: "/event-types/$eventTypeId", params: { eventTypeId: eventType.id } });
    }
  });
  const canSubmit = defaultSchedule !== undefined && isEventTypePayloadSubmittable(draft);

  const handleOpenChange = (nextOpen: boolean) => {
    if (nextOpen) {
      setSlugWasEdited(false);
      setDraft(newEventTypePayload(defaultSchedule?.id ?? ""));
      reset();
    }
    setOpen(nextOpen);
  };

  return (
    <Dialog trackingTitle={t`Create event type`} open={open} onOpenChange={handleOpenChange}>
      <DialogTrigger render={<Button disabled={!defaultSchedule} />}>
        <PlusIcon />
        <Trans>New event type</Trans>
      </DialogTrigger>
      <DialogContent className="sm:max-w-2xl">
        <DialogForm
          validationErrors={error?.errors}
          onSubmit={() => {
            if (!canSubmit) return;
            mutate({ body: { ...draft, scheduleId: defaultSchedule.id } });
          }}
        >
          <DialogHeader>
            <DialogTitle>
              <Trans>Add new event type</Trans>
            </DialogTitle>
            <DialogDescription>
              <Trans>Create the booking page basics now, then finish setup on the next screen.</Trans>
            </DialogDescription>
          </DialogHeader>
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
                  setDraft((current) => ({ ...current, title, slug: slugWasEdited ? current.slug : slugify(title) }))
                }
              />
              <TextField
                name="slug"
                label={t`Slug`}
                required={true}
                value={draft.slug}
                onChange={(slug) => {
                  setSlugWasEdited(true);
                  setDraft((current) => ({ ...current, slug: slugify(slug) }));
                }}
              />
            </div>
            <TextAreaField
              name="description"
              label={t`Description`}
              lines={3}
              value={draft.description ?? ""}
              onChange={(description) => setDraft((current) => ({ ...current, description: description || null }))}
            />
            <NumberField
              name="durationMinutes"
              label={t`Duration`}
              minValue={5}
              maxValue={1440}
              value={draft.durationMinutes}
              onChange={(durationMinutes) =>
                setDraft((current) => ({ ...current, durationMinutes: durationMinutes ?? 30 }))
              }
            />
          </DialogBody>
          <DialogFooter>
            <DialogClose render={<Button type="button" variant="outline" />}>
              <Trans>Cancel</Trans>
            </DialogClose>
            <Button type="submit" disabled={!canSubmit} isPending={isPending}>
              <Trans>Continue</Trans>
            </Button>
          </DialogFooter>
        </DialogForm>
      </DialogContent>
    </Dialog>
  );
}
