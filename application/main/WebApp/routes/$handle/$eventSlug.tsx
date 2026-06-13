import { Trans } from "@lingui/react/macro";
import { createFileRoute } from "@tanstack/react-router";
import { MessageSquareIcon, QrCodeIcon, SparklesIcon, CalendarDaysIcon, ExternalLinkIcon } from "lucide-react";

import { api } from "@/shared/lib/api/client";

export const Route = createFileRoute("/$handle/$eventSlug")({
  staticData: { trackingTitle: "Public client" },
  validateSearch: (search: Record<string, unknown>) => ({
    privateLink: search.privateLink as string | undefined
  }),
  component: WhatsAppBookingOnlyPage
});

function WhatsAppBookingOnlyPage() {
  const { handle, eventSlug } = Route.useParams();
  const search = Route.useSearch();

  const { data: eventType, isLoading } = api.useQuery("get", "/api/public/event-types/{handle}/{slug}", {
    params: { path: { handle, slug: eventSlug }, query: { privateLink: search.privateLink } }
  });

  const wabaPhoneNumber = eventType?.wabaPhoneNumber ?? "";
  const phoneDigits = wabaPhoneNumber.replace(/\D/g, "");
  const textMessage = `Book ${eventType?.title ?? "Appointment"}`;
  const waUrl = phoneDigits ? `https://wa.me/${phoneDigits}?text=${encodeURIComponent(textMessage)}` : "";
  const qrUrl = waUrl
    ? `https://api.qrserver.com/v1/create-qr-code/?size=250x250&data=${encodeURIComponent(waUrl)}`
    : "";

  if (isLoading) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-slate-950 text-white">
        <div className="flex flex-col items-center gap-4">
          <div className="size-12 animate-spin rounded-full border-4 border-primary border-t-transparent" />
          <p className="animate-pulse text-sm font-medium text-slate-400">
            <Trans>Opening secure booking portal...</Trans>
          </p>
        </div>
      </div>
    );
  }

  if (!eventType) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-slate-950 p-4 text-white">
        <div className="flex max-w-md flex-col items-center gap-4 rounded-3xl border border-white/10 bg-slate-900/60 p-8 text-center shadow-2xl backdrop-blur-md">
          <CalendarDaysIcon className="size-16 animate-bounce text-rose-500" />
          <h2 className="text-2xl font-bold tracking-tight">
            <Trans>Booking Page Not Found</Trans>
          </h2>
          <p className="text-sm text-slate-400">
            <Trans>The booking link you followed might have expired, or the business might have moved it.</Trans>
          </p>
        </div>
      </div>
    );
  }

  return (
    <div className="relative flex min-h-screen items-center justify-center overflow-hidden bg-slate-950 px-4 py-12 text-white md:px-8">
      {/* Decorative premium gradients */}
      <div className="absolute top-[-20%] left-[-20%] size-[600px] rounded-full bg-primary/10 blur-[150px]" />
      <div className="absolute right-[-20%] bottom-[-20%] size-[600px] rounded-full bg-primary/10 blur-[150px]" />

      <div className="relative flex w-full max-w-lg flex-col gap-6" data-testid="public-booker">
        {/* Brand Banner */}
        <div className="mx-auto flex animate-pulse items-center gap-2 rounded-full border border-primary/30 bg-primary/10 px-4 py-1.5 text-xs font-semibold tracking-widest text-primary uppercase shadow-lg backdrop-blur-md">
          <SparklesIcon className="size-3.5" />
          <span>
            <Trans>WhatsApp Booking First</Trans>
          </span>
        </div>

        {/* Premium Card */}
        <div className="overflow-hidden rounded-3xl border border-white/10 bg-slate-900/40 p-8 shadow-2xl backdrop-blur-xl transition-all duration-300 hover:border-primary/30 hover:shadow-primary/5">
          {/* Header */}
          <div className="flex flex-col items-center gap-3 text-center">
            {eventType.profile?.avatarUrl ? (
              <img
                src={eventType.profile.avatarUrl}
                alt={eventType.profile.displayName}
                className="size-20 rounded-2xl object-cover shadow-lg ring-2 ring-primary/30"
              />
            ) : (
              <div className="flex size-16 items-center justify-center rounded-2xl bg-gradient-to-tr from-primary to-primary/80 text-xl font-bold text-slate-950 shadow-lg">
                {eventType.profile?.displayName?.charAt(0) || "B"}
              </div>
            )}
            <div>
              <h1 className="bg-gradient-to-r from-white via-slate-200 to-slate-400 bg-clip-text text-2xl font-bold tracking-tight text-transparent">
                {eventType.profile?.displayName}
              </h1>
              <p className="mt-1 text-sm font-medium text-slate-400">{eventType.title}</p>
            </div>
            {eventType.description && (
              <p className="mt-2 max-w-sm text-xs leading-relaxed text-slate-400">{eventType.description}</p>
            )}
            <div className="mt-1 flex items-center justify-center gap-2 rounded-full border border-white/5 bg-white/5 px-3 py-1 text-xs text-slate-400">
              <CalendarDaysIcon className="size-3.5 text-primary" />
              <span>
                {eventType.durationMinutes} <Trans>Minutes duration</Trans>
              </span>
            </div>
          </div>

          <div className="my-8 border-t border-white/5" />

          {/* Booking Content */}
          <div className="flex flex-col items-center gap-6">
            {waUrl ? (
              <>
                {/* QR Code Container */}
                <div className="group relative rounded-2xl border border-white/5 bg-slate-950/40 p-4 shadow-inner backdrop-blur-md transition-all duration-300 hover:border-primary/20">
                  <img
                    src={qrUrl}
                    alt="Scan to Book"
                    className="size-48 rounded-lg object-contain transition-transform duration-300 group-hover:scale-[1.02]"
                  />
                  <div className="absolute inset-0 flex items-center justify-center rounded-2xl bg-slate-950/60 opacity-0 backdrop-blur-xs transition-opacity group-hover:opacity-100">
                    <QrCodeIcon className="size-10 animate-ping text-primary" />
                  </div>
                </div>

                <div className="flex max-w-xs flex-col gap-1.5 text-center">
                  <p className="text-sm font-semibold text-slate-200">
                    <Trans>Scan QR code or click below</Trans>
                  </p>
                  <p className="text-xs text-slate-400">
                    <Trans>Scan with your phone's camera to book instantly inside WhatsApp.</Trans>
                  </p>
                </div>

                {/* Big Booking Button */}
                <a
                  href={waUrl}
                  target="_blank"
                  rel="noopener noreferrer"
                  className="group relative flex w-full items-center justify-center gap-2.5 overflow-hidden rounded-2xl bg-gradient-to-r from-primary to-primary/80 px-6 py-4 text-center font-bold text-slate-950 shadow-xl transition-all duration-300 hover:scale-[1.02] hover:shadow-primary/20 active:scale-[0.98]"
                >
                  <MessageSquareIcon className="size-5 transition-transform group-hover:rotate-12" />
                  <span>
                    <Trans>Book Instantly on WhatsApp</Trans>
                  </span>
                  <ExternalLinkIcon className="size-4 opacity-70" />

                  {/* Hover shine effect */}
                  <span className="absolute inset-0 block translate-x-[-100%] bg-gradient-to-r from-transparent via-white/30 to-transparent transition-transform duration-1000 group-hover:translate-x-[100%]" />
                </a>
              </>
            ) : (
              <div className="flex max-w-xs flex-col items-center gap-3 py-6 text-center">
                <div className="rounded-full border border-amber-500/20 bg-amber-500/10 p-3 text-amber-400">
                  <SparklesIcon className="size-8" />
                </div>
                <h3 className="text-sm font-semibold text-slate-200">
                  <Trans>WhatsApp Connection Pending</Trans>
                </h3>
                <p className="text-xs leading-relaxed text-slate-400">
                  <Trans>
                    This business is currently configuring their secure WhatsApp booking line. Please try again shortly!
                  </Trans>
                </p>
              </div>
            )}
          </div>
        </div>

        {/* Footer */}
        <p className="text-center text-[10px] text-slate-500">
          <Trans>Secure end-to-end encrypted appointment scheduling powered by Nerova.</Trans>
        </p>
      </div>
    </div>
  );
}
