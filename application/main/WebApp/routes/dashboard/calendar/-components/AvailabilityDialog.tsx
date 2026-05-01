import {
  Dialog,
  DialogBody,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle
} from "@repo/ui/components/Dialog";
import { useEffect, useMemo, useState } from "react";

import type { AvailabilityRule, BusinessClosure, HolidaySettings } from "@/shared/lib/appointmentsApi";

import { buildInitialDays, type DayState } from "./availabilityState";
import { ClosureSettings } from "./ClosureSettings";
import { HolidaySettingsPanel } from "./HolidaySettingsPanel";
import { WeeklyAvailabilityForm } from "./WeeklyAvailabilityForm";

interface AvailabilityDialogProps {
  open: boolean;
  rules: AvailabilityRule[];
  closures: BusinessClosure[];
  holidaySettings?: HolidaySettings;
  onClose: () => void;
}

export function AvailabilityDialog({ open, rules, closures, holidaySettings, onClose }: AvailabilityDialogProps) {
  const initialDays = useMemo(() => buildInitialDays(rules), [rules]);
  const [days, setDays] = useState<DayState[]>(initialDays);

  useEffect(() => {
    if (open) setDays(initialDays);
  }, [initialDays, open]);

  return (
    <Dialog
      trackingTitle="Calendar availability"
      open={open}
      onOpenChange={(nextOpen) => {
        if (!nextOpen) onClose();
      }}
    >
      <DialogContent className="sm:max-w-[56rem]">
        <DialogHeader>
          <DialogTitle>Business availability</DialogTitle>
        </DialogHeader>
        <DialogBody className="gap-6">
          <WeeklyAvailabilityForm days={days} setDays={setDays} />
          <HolidaySettingsPanel holidaySettings={holidaySettings} />
          <ClosureSettings closures={closures} />
        </DialogBody>
        <DialogFooter showCloseButton />
      </DialogContent>
    </Dialog>
  );
}
