import type { PublicBookingProfile, PublicBookingService, Slot } from "@/shared/lib/publicBookingApi";

import { BookingFields, ServicePicker, SlotPicker } from "./PublicBookingControls";
import { BookingFooter } from "./PublicBookingFooter";

export function PublicBookingSteps({
  profile,
  serviceId,
  selectedService,
  date,
  slots,
  slotStart,
  name,
  email,
  note,
  isSubmitDisabled,
  isSubmitting,
  onServiceSelect,
  onDateChange,
  onSlotSelect,
  onNameChange,
  onEmailChange,
  onNoteChange,
  onSubmit
}: {
  profile: PublicBookingProfile;
  serviceId?: string;
  selectedService?: PublicBookingService;
  date: string;
  slots: Slot[];
  slotStart: string;
  name: string;
  email: string;
  note: string;
  isSubmitDisabled: boolean;
  isSubmitting: boolean;
  onServiceSelect: (serviceId: string) => void;
  onDateChange: (date: string) => void;
  onSlotSelect: (slotStart: string) => void;
  onNameChange: (value: string) => void;
  onEmailChange: (value: string) => void;
  onNoteChange: (value: string) => void;
  onSubmit: () => void;
}) {
  return (
    <>
      <ServicePicker services={profile.services} serviceId={serviceId} onSelect={onServiceSelect} />
      <SlotPicker
        date={date}
        slots={slots}
        slotStart={slotStart}
        onDateChange={onDateChange}
        onSlotSelect={onSlotSelect}
      />
      <BookingFields
        name={name}
        email={email}
        note={note}
        onNameChange={onNameChange}
        onEmailChange={onEmailChange}
        onNoteChange={onNoteChange}
      />
      <BookingFooter
        selectedService={selectedService}
        disabled={isSubmitDisabled}
        isSubmitting={isSubmitting}
        onSubmit={onSubmit}
      />
    </>
  );
}
