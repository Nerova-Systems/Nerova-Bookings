import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { DialogContent, DialogDescription, DialogHeader, DialogTitle } from "@repo/ui/components/Dialog";
import { DirtyDialog } from "@repo/ui/components/DirtyDialog";
import { useMemo, useState } from "react";

import type { Schedule } from "./schedulingTypes";

import { CreateEventTypeDialogBody } from "./CreateEventTypeDialogBody";
import { CreateEventTypeNoScheduleBody } from "./CreateEventTypeNoScheduleBody";

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
    <DirtyDialog trackingTitle={t`Create service`} open={open} onOpenChange={handleOpenChange}>
      <DialogContent className="sm:w-dialog-lg">
        <DialogHeader>
          <DialogTitle>
            <Trans>Add new service</Trans>
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
