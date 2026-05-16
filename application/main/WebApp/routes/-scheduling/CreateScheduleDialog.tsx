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
import { TextField } from "@repo/ui/components/TextField";
import { useNavigate } from "@tanstack/react-router";
import { PlusIcon } from "lucide-react";
import { useState } from "react";
import { toast } from "sonner";

import { api } from "@/shared/lib/api/client";

import { GeneralApiErrors } from "./ApiErrors";
import { newSchedulePayload } from "./schedulingTypes";

export function CreateScheduleDialog({ isFirstSchedule }: Readonly<{ isFirstSchedule: boolean }>) {
  const navigate = useNavigate();
  const [open, setOpen] = useState(false);
  const [name, setName] = useState(() => newSchedulePayload(isFirstSchedule).name);
  const { error, isPending, mutate, reset } = api.useMutation("post", "/api/schedules", {
    onSuccess: (schedule) => {
      toast.success(t`Schedule created`);
      setOpen(false);
      navigate({ to: "/availability/$scheduleId", params: { scheduleId: schedule.id } });
    }
  });
  const canSubmit = name.trim().length > 0;

  const handleOpenChange = (nextOpen: boolean) => {
    if (nextOpen) {
      setName(newSchedulePayload(isFirstSchedule).name);
      reset();
    }
    setOpen(nextOpen);
  };

  return (
    <Dialog trackingTitle={t`Create schedule`} open={open} onOpenChange={handleOpenChange}>
      <DialogTrigger render={<Button />}>
        <PlusIcon />
        <Trans>New schedule</Trans>
      </DialogTrigger>
      <DialogContent className="sm:max-w-lg">
        <DialogForm
          validationErrors={error?.errors}
          onSubmit={() => {
            if (!canSubmit) return;
            mutate({
              body: { ...newSchedulePayload(isFirstSchedule), name: name.trim() }
            });
          }}
        >
          <DialogHeader>
            <DialogTitle>
              <Trans>Add new schedule</Trans>
            </DialogTitle>
            <DialogDescription>
              <Trans>Start with a weekly schedule, then refine availability on the next screen.</Trans>
            </DialogDescription>
          </DialogHeader>
          <DialogBody>
            <GeneralApiErrors error={error} />
            <TextField name="name" label={t`Name`} required={true} autoFocus={true} value={name} onChange={setName} />
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
