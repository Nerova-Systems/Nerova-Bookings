import { t } from "@lingui/core/macro";
import { createFileRoute, useNavigate } from "@tanstack/react-router";
import { useEffect, useState } from "react";

import {
  useCheckPublicPhoneVerification,
  useCreatePublicBooking,
  usePublicBookingProfile,
  usePublicSlots,
  useStartPublicPhoneVerification
} from "@/shared/lib/publicBookingApi";

import { PhoneVerificationStep } from "./-components/PhoneVerificationStep";
import { BookingIntro, BookingPageHeader, PublicShell } from "./-components/PublicBookingParts";
import { PublicBookingSteps } from "./-components/PublicBookingSteps";

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
  const [verificationCode, setVerificationCode] = useState("");
  const [phoneVerificationToken, setPhoneVerificationToken] = useState("");
  const [maskedPhone, setMaskedPhone] = useState<string | undefined>();
  const [verificationError, setVerificationError] = useState<string | undefined>();
  const [email, setEmail] = useState("");
  const [note, setNote] = useState("");
  const slotsQuery = usePublicSlots(businessSlug, serviceId, date);
  const startPhoneVerification = useStartPublicPhoneVerification(businessSlug);
  const checkPhoneVerification = useCheckPublicPhoneVerification(businessSlug);
  const createBooking = useCreatePublicBooking(businessSlug);
  const selectedService = profileQuery.data?.services.find((service) => service.id === serviceId);
  const isPhoneVerified = Boolean(phoneVerificationToken);
  const isSubmitDisabled =
    !serviceId || !slotStart || !name || !phone || !email || !phoneVerificationToken || createBooking.isPending;

  useEffect(() => {
    document.title = t`Book appointment | Nerova`;
  }, []);

  useEffect(() => {
    if (!serviceId && firstServiceId) setServiceId(firstServiceId);
  }, [firstServiceId, serviceId]);

  const submit = async () => {
    if (!serviceId || !slotStart || !name || !phone || !email || !phoneVerificationToken) return;
    const result = await createBooking.mutateAsync({
      serviceId,
      startAt: slotStart,
      name,
      phone,
      email,
      phoneVerificationToken,
      answers: { note }
    });
    if (result.paymentUrl) {
      window.location.assign(result.paymentUrl);
      return;
    }
    navigate({ to: "/book/confirmation/$reference", params: { reference: result.reference } });
  };

  const resetVerifiedPhone = (nextPhone: string) => {
    setPhone(nextPhone);
    setVerificationCode("");
    setPhoneVerificationToken("");
    setMaskedPhone(undefined);
    setVerificationError(undefined);
  };

  const sendCode = async () => {
    setVerificationError(undefined);
    try {
      const result = await startPhoneVerification.mutateAsync({ phone });
      setMaskedPhone(result.maskedPhone);
      setVerificationCode("");
    } catch (error) {
      setVerificationError(error instanceof Error ? error.message : "Could not send verification code.");
    }
  };

  const verifyCode = async () => {
    setVerificationError(undefined);
    try {
      const result = await checkPhoneVerification.mutateAsync({ phone, code: verificationCode });
      setPhoneVerificationToken(result.phoneVerificationToken);
      setMaskedPhone(result.maskedPhone);
      setName(result.name);
      setEmail(result.email);
    } catch (error) {
      setVerificationError(error instanceof Error ? error.message : "Could not verify that code.");
    }
  };

  const selectService = (nextServiceId: string) => {
    setServiceId(nextServiceId);
    setSlotStart("");
  };

  const selectDate = (nextDate: string) => {
    setDate(nextDate);
    setSlotStart("");
  };

  if (profileQuery.isLoading) {
    return <PublicShell title="Loading booking page" subtitle="Preparing available services." />;
  }

  if (!profileQuery.data) {
    return <PublicShell title="Booking page unavailable" subtitle="This business is not accepting public bookings." />;
  }

  return (
    <main className="min-h-screen bg-muted text-foreground">
      <div className="mx-auto grid min-h-screen max-w-7xl grid-cols-[minmax(18rem,24rem)_1fr] bg-background shadow-[0_0_80px_rgba(24,24,27,0.10)] max-lg:block dark:shadow-none">
        <BookingIntro profile={profileQuery.data} selectedService={selectedService} />

        <section className="px-6 py-6 sm:px-8 lg:px-10 lg:py-8">
          <BookingPageHeader timeZone={profileQuery.data.timeZone} />

          <div className="grid gap-8">
            <PhoneVerificationStep
              phone={phone}
              code={verificationCode}
              maskedPhone={maskedPhone}
              isVerified={isPhoneVerified}
              isSending={startPhoneVerification.isPending}
              isChecking={checkPhoneVerification.isPending}
              error={verificationError}
              onPhoneChange={resetVerifiedPhone}
              onCodeChange={setVerificationCode}
              onSendCode={sendCode}
              onCheckCode={verifyCode}
            />
            {!isPhoneVerified && (
              <div className="rounded-2xl border border-dashed border-border bg-muted/40 px-5 py-4 text-sm text-muted-foreground">
                Verify your phone number to unlock service selection, available times, and booking submission.
              </div>
            )}
            {isPhoneVerified && (
              <PublicBookingSteps
                profile={profileQuery.data}
                serviceId={serviceId}
                selectedService={selectedService}
                date={date}
                slots={slotsQuery.data ?? []}
                slotStart={slotStart}
                name={name}
                email={email}
                note={note}
                isSubmitDisabled={isSubmitDisabled}
                isSubmitting={createBooking.isPending}
                onServiceSelect={selectService}
                onDateChange={selectDate}
                onSlotSelect={setSlotStart}
                onNameChange={setName}
                onEmailChange={setEmail}
                onNoteChange={setNote}
                onSubmit={submit}
              />
            )}
          </div>
        </section>
      </div>
    </main>
  );
}
