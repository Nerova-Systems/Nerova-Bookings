import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { createFileRoute, useNavigate } from "@tanstack/react-router";
import { useEffect, useState } from "react";

import { useCreatePublicBooking, usePublicBookingProfile, usePublicSlots } from "@/shared/lib/publicBookingApi";

import {
  BookingFields,
  BookingFooter,
  ServicePicker,
  SlotPicker
} from "./-components/PublicBookingControls";
import { BookingIntro, PublicShell } from "./-components/PublicBookingParts";

export const Route = createFileRoute("/book/$businessSlug")({
  component: PublicBookingPage
});

function PublicBookingPage() {
  const { businessSlug } = Route.useParams();
  const navigate = useNavigate();
  const profileQuery = usePublicBookingProfile(businessSlug);
  const firstServiceId = profileQuery.data?.services[0]?.id;
  const [serviceId, setServiceId] = useState<string | undefined>(firstServiceId);
  const [date, setDate] = useState(() => new Date().toISOString().slice(0, 10));
  const [slotStart, setSlotStart] = useState("");
  const [name, setName] = useState("");
  const [phone, setPhone] = useState("");
  const [email, setEmail] = useState("");
  const [note, setNote] = useState("");
  const slotsQuery = usePublicSlots(businessSlug, serviceId, date);
  const createBooking = useCreatePublicBooking(businessSlug);
  const selectedService = profileQuery.data?.services.find((service) => service.id === serviceId);

  useEffect(() => {
    document.title = t`Book appointment | Nerova`;
  }, []);

  useEffect(() => {
    if (!serviceId && firstServiceId) setServiceId(firstServiceId);
  }, [firstServiceId, serviceId]);

  const submit = async () => {
    if (!serviceId || !slotStart || !name || !phone || !email) return;
    const result = await createBooking.mutateAsync({
      serviceId,
      startAt: slotStart,
      name,
      phone,
      email,
      answers: { note }
    });
    if (result.paymentUrl) {
      window.location.assign(result.paymentUrl);
      return;
    }
    navigate({ to: "/book/confirmation/$reference", params: { reference: result.reference } });
  };

  if (profileQuery.isLoading) {
    return <PublicShell title="Loading booking page" subtitle="Preparing available services." />;
  }

  if (!profileQuery.data) {
    return <PublicShell title="Booking page unavailable" subtitle="This business is not accepting public bookings." />;
  }

  return (
    <main className="min-h-screen bg-[#f7f7f5] text-foreground">
      <div className="mx-auto grid min-h-screen max-w-6xl grid-cols-[22rem_1fr] gap-0 border-x border-border bg-background">
        <BookingIntro profile={profileQuery.data} />

        <section className="p-8">
          <div className="mb-6">
            <h2 className="font-display text-2xl font-semibold">
              <Trans>Book an appointment</Trans>
            </h2>
            <p className="mt-1 text-sm text-muted-foreground">
              <Trans>Choose a service, pick a slot, then enter your details.</Trans>
            </p>
          </div>

          <div className="grid gap-6">
            <ServicePicker
              services={profileQuery.data.services}
              serviceId={serviceId}
              onSelect={(nextServiceId) => {
                setServiceId(nextServiceId);
                setSlotStart("");
              }}
            />
            <SlotPicker
              date={date}
              slots={slotsQuery.data ?? []}
              slotStart={slotStart}
              onDateChange={(nextDate) => {
                setDate(nextDate);
                setSlotStart("");
              }}
              onSlotSelect={setSlotStart}
            />
            <BookingFields
              name={name}
              phone={phone}
              email={email}
              note={note}
              onNameChange={setName}
              onPhoneChange={setPhone}
              onEmailChange={setEmail}
              onNoteChange={setNote}
            />
            <BookingFooter
              selectedService={selectedService}
              disabled={!serviceId || !slotStart || !name || !phone || !email}
              onSubmit={submit}
            />
          </div>
        </section>
      </div>
    </main>
  );
}
