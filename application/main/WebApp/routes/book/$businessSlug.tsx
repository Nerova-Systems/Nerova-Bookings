import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { createFileRoute, useNavigate } from "@tanstack/react-router";
import { useEffect, useState } from "react";

import {
  useCreatePublicBooking,
  usePublicBookingProfile,
  usePublicClientPrefill,
  usePublicSlots
} from "@/shared/lib/publicBookingApi";

import {
  BookingFields,
  ServicePicker,
  SlotPicker
} from "./-components/PublicBookingControls";
import { BookingFooter } from "./-components/PublicBookingFooter";
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
  const prefillQuery = usePublicClientPrefill(businessSlug, phone);
  const createBooking = useCreatePublicBooking(businessSlug);
  const selectedService = profileQuery.data?.services.find((service) => service.id === serviceId);

  useEffect(() => {
    document.title = t`Book appointment | Nerova`;
  }, []);

  useEffect(() => {
    if (!serviceId && firstServiceId) setServiceId(firstServiceId);
  }, [firstServiceId, serviceId]);

  useEffect(() => {
    if (!prefillQuery.data) return;
    setName(prefillQuery.data.name);
    setEmail(prefillQuery.data.email);
  }, [prefillQuery.data]);

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
    <main className="min-h-screen bg-[#f5f2ec] text-foreground">
      <div className="mx-auto grid min-h-screen max-w-7xl grid-cols-[minmax(18rem,24rem)_1fr] bg-background shadow-[0_0_80px_rgba(24,24,27,0.10)] max-lg:block">
        <BookingIntro profile={profileQuery.data} selectedService={selectedService} />

        <section className="px-6 py-6 sm:px-8 lg:px-10 lg:py-8">
          <div className="mb-8 flex flex-wrap items-start justify-between gap-4 border-b border-border pb-6">
            <div>
              <div className="text-xs font-semibold tracking-[0.16em] text-muted-foreground uppercase">
                <Trans>Public booking</Trans>
              </div>
              <h2 className="mt-2 font-display text-3xl font-semibold tracking-tight">
                <Trans>Book an appointment</Trans>
              </h2>
            </div>
            <div className="rounded-full border border-border bg-muted px-3 py-1 text-xs font-medium text-muted-foreground">
              {profileQuery.data.timeZone}
            </div>
          </div>

          <div className="grid gap-8">
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
              isCheckingPhone={prefillQuery.isFetching}
              onNameChange={setName}
              onPhoneChange={setPhone}
              onEmailChange={setEmail}
              onNoteChange={setNote}
            />
            <BookingFooter
              selectedService={selectedService}
              disabled={!serviceId || !slotStart || !name || !phone || !email || createBooking.isPending}
              isSubmitting={createBooking.isPending}
              onSubmit={submit}
            />
          </div>
        </section>
      </div>
    </main>
  );
}
