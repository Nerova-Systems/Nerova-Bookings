import { createFileRoute } from "@tanstack/react-router";
import { api } from "@/shared/lib/api/client";
import { MessageSquareIcon, QrCodeIcon, SparklesIcon, CalendarDaysIcon, ExternalLinkIcon } from "lucide-react";
import { Trans } from "@lingui/react/macro";
import { t } from "@lingui/core/macro";

export const Route = createFileRoute("/$handle/$eventSlug")({
  staticData: { trackingTitle: "Public booker" },
  validateSearch: (search: Record<string, unknown>) => ({
    privateLink: search.privateLink as string | undefined
  }),
  component: WhatsAppBookingOnlyPage
});

function WhatsAppBookingOnlyPage() {
  const { handle, eventSlug } = Route.useParams();
  const search = Route.useSearch();

  const { data: eventType, isLoading } = api.useQuery(
    "get",
    "/api/public/event-types/{handle}/{slug}",
    {
      params: { path: { handle, slug: eventSlug }, query: { privateLink: search.privateLink } }
    }
  );

  const wabaPhoneNumber = eventType?.wabaPhoneNumber ?? "";
  const phoneDigits = wabaPhoneNumber.replace(/\D/g, "");
  const textMessage = `Book ${eventType?.title ?? "Appointment"}`;
  const waUrl = phoneDigits ? `https://wa.me/${phoneDigits}?text=${encodeURIComponent(textMessage)}` : "";
  const qrUrl = waUrl ? `https://api.qrserver.com/v1/create-qr-code/?size=250x250&data=${encodeURIComponent(waUrl)}` : "";

  if (isLoading) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-slate-950 text-white">
        <div className="flex flex-col items-center gap-4">
          <div className="size-12 animate-spin rounded-full border-4 border-emerald-500 border-t-transparent" />
          <p className="font-medium text-slate-400 animate-pulse text-sm">
            <Trans>Opening secure booking portal...</Trans>
          </p>
        </div>
      </div>
    );
  }

  if (!eventType) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-slate-950 text-white p-4">
        <div className="max-w-md text-center flex flex-col items-center gap-4 rounded-3xl border border-white/10 bg-slate-900/60 p-8 shadow-2xl backdrop-blur-md">
          <CalendarDaysIcon className="size-16 text-rose-500 animate-bounce" />
          <h2 className="text-2xl font-bold tracking-tight">
            <Trans>Booking Page Not Found</Trans>
          </h2>
          <p className="text-slate-400 text-sm">
            <Trans>The booking link you followed might have expired, or the business might have moved it.</Trans>
          </p>
        </div>
      </div>
    );
  }

  return (
    <div className="relative flex min-h-screen items-center justify-center overflow-hidden bg-slate-950 text-white px-4 py-12 md:px-8">
      {/* Decorative premium gradients */}
      <div className="absolute top-[-20%] left-[-20%] size-[600px] rounded-full bg-emerald-500/10 blur-[150px]" />
      <div className="absolute bottom-[-20%] right-[-20%] size-[600px] rounded-full bg-teal-500/10 blur-[150px]" />

      <div className="relative w-full max-w-lg flex flex-col gap-6" data-testid="public-booker">
        {/* Brand Banner */}
        <div className="mx-auto flex items-center gap-2 rounded-full border border-emerald-500/30 bg-emerald-500/10 px-4 py-1.5 text-xs font-semibold uppercase tracking-widest text-emerald-400 backdrop-blur-md shadow-lg animate-pulse">
          <SparklesIcon className="size-3.5" />
          <span><Trans>WhatsApp Booking First</Trans></span>
        </div>

        {/* Premium Card */}
        <div className="overflow-hidden rounded-3xl border border-white/10 bg-slate-900/40 p-8 shadow-2xl backdrop-blur-xl transition-all duration-300 hover:border-emerald-500/30 hover:shadow-emerald-500/5">
          
          {/* Header */}
          <div className="text-center flex flex-col items-center gap-3">
            {eventType.profile?.avatarUrl ? (
              <img
                src={eventType.profile.avatarUrl}
                alt={eventType.profile.displayName}
                className="size-20 rounded-2xl object-cover ring-2 ring-emerald-500/30 shadow-lg"
              />
            ) : (
              <div className="flex size-16 items-center justify-center rounded-2xl bg-gradient-to-tr from-emerald-500 to-teal-400 text-slate-950 font-bold text-xl shadow-lg">
                {eventType.profile?.displayName?.charAt(0) || "B"}
              </div>
            )}
            <div>
              <h1 className="text-2xl font-bold tracking-tight bg-gradient-to-r from-white via-slate-200 to-slate-400 bg-clip-text text-transparent">
                {eventType.profile?.displayName}
              </h1>
              <p className="mt-1 text-sm font-medium text-slate-400">{eventType.title}</p>
            </div>
            {eventType.description && (
              <p className="mt-2 text-xs leading-relaxed text-slate-400 max-w-sm">
                {eventType.description}
              </p>
            )}
            <div className="mt-1 flex items-center justify-center gap-2 text-xs text-slate-400 bg-white/5 border border-white/5 rounded-full px-3 py-1">
              <CalendarDaysIcon className="size-3.5 text-emerald-400" />
              <span>{eventType.durationMinutes} <Trans>Minutes duration</Trans></span>
            </div>
          </div>

          <div className="my-8 border-t border-white/5" />

          {/* Booking Content */}
          <div className="flex flex-col items-center gap-6">
            {waUrl ? (
              <>
                {/* QR Code Container */}
                <div className="relative group p-4 rounded-2xl border border-white/5 bg-slate-950/40 backdrop-blur-md transition-all duration-300 hover:border-emerald-500/20 shadow-inner">
                  <img
                    src={qrUrl}
                    alt="Scan to Book"
                    className="size-48 rounded-lg object-contain transition-transform duration-300 group-hover:scale-[1.02]"
                  />
                  <div className="absolute inset-0 flex items-center justify-center opacity-0 group-hover:opacity-100 transition-opacity bg-slate-950/60 rounded-2xl backdrop-blur-xs">
                    <QrCodeIcon className="size-10 text-emerald-400 animate-ping" />
                  </div>
                </div>

                <div className="text-center max-w-xs flex flex-col gap-1.5">
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
                  className="group relative flex w-full items-center justify-center gap-2.5 overflow-hidden rounded-2xl bg-gradient-to-r from-emerald-500 to-teal-400 px-6 py-4 text-center font-bold text-slate-950 shadow-xl transition-all duration-300 hover:scale-[1.02] hover:shadow-emerald-500/20 active:scale-[0.98]"
                >
                  <MessageSquareIcon className="size-5 transition-transform group-hover:rotate-12" />
                  <span><Trans>Book Instantly on WhatsApp</Trans></span>
                  <ExternalLinkIcon className="size-4 opacity-70" />
                  
                  {/* Hover shine effect */}
                  <span className="absolute inset-0 block translate-x-[-100%] bg-gradient-to-r from-transparent via-white/30 to-transparent transition-transform duration-1000 group-hover:translate-x-[100%]" />
                </a>
              </>
            ) : (
              <div className="flex flex-col items-center gap-3 py-6 text-center max-w-xs">
                <div className="rounded-full bg-amber-500/10 p-3 text-amber-400 border border-amber-500/20">
                  <SparklesIcon className="size-8" />
                </div>
                <h3 className="text-sm font-semibold text-slate-200">
                  <Trans>WhatsApp Connection Pending</Trans>
                </h3>
                <p className="text-xs text-slate-400 leading-relaxed">
                  <Trans>This business is currently configuring their secure WhatsApp booking line. Please try again shortly!</Trans>
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
