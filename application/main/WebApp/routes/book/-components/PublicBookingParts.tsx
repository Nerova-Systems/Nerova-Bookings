import { Trans } from "@lingui/react/macro";
import { useState } from "react";

import type { PublicBookingProfile, PublicBookingService } from "@/shared/lib/publicBookingApi";

import { money } from "@/shared/lib/appointmentsApi";

export function StepHeading({ step, title, description }: { step: string; title: string; description: string }) {
  return (
    <div className="mb-4 flex items-start gap-3">
      <div className="flex size-8 shrink-0 items-center justify-center rounded-full bg-foreground text-xs font-semibold text-background">
        {step}
      </div>
      <div>
        <h3 className="font-display text-xl font-semibold tracking-tight">{title}</h3>
        <p className="mt-1 text-sm text-muted-foreground">{description}</p>
      </div>
    </div>
  );
}

export function BookingIntro({
  profile,
  selectedService
}: {
  profile: PublicBookingProfile;
  selectedService?: PublicBookingService;
}) {
  return (
    <aside className="relative overflow-hidden border-r border-border bg-[#181818] p-8 text-white max-lg:border-r-0 max-lg:p-6">
      <div className="absolute inset-x-0 top-0 h-48 bg-[radial-gradient(circle_at_top_left,rgba(255,255,255,0.28),transparent_45%)]" />
      <div className="relative flex min-h-[calc(100vh-4rem)] flex-col justify-between gap-8 max-lg:min-h-0">
        <div>
          <BusinessLogo profile={profile} />
          <div className="mt-6 text-xs font-semibold tracking-[0.16em] text-white/50 uppercase">Book with</div>
          <h1 className="mt-2 font-display text-4xl font-semibold tracking-tight">{profile.name}</h1>
          <p className="mt-3 max-w-xs text-sm leading-6 text-white/65">{profile.address}</p>
        </div>

        <div className="grid gap-4">
          {selectedService && (
            <div className="rounded-2xl border border-white/10 bg-white/[0.08] p-4">
              <div className="text-xs font-semibold tracking-[0.14em] text-white/50 uppercase">Selected service</div>
              <div className="mt-3 font-medium">{selectedService.name}</div>
              <div className="mt-2 flex flex-wrap gap-2 text-xs text-white/70">
                <span>{selectedService.durationMinutes} min</span>
                <span>{money(selectedService.priceCents)}</span>
                {selectedService.depositCents > 0 && <span>{money(selectedService.depositCents)} deposit</span>}
              </div>
            </div>
          )}
          <div className="text-xs text-white/45">
            <Trans>Powered by Nerova</Trans>
          </div>
        </div>
      </div>
    </aside>
  );
}

export function BookingPageHeader({ timeZone }: { timeZone: string }) {
  return (
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
        {timeZone}
      </div>
    </div>
  );
}

function BusinessLogo({ profile }: { profile: PublicBookingProfile }) {
  const [imageFailed, setImageFailed] = useState(false);
  const initials = profile.name
    .split(" ")
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0]?.toUpperCase())
    .join("");

  if (profile.logoUrl && !imageFailed) {
    return (
      <img
        src={profile.logoUrl}
        alt={`${profile.name} logo`}
        onError={() => setImageFailed(true)}
        className="size-16 rounded-2xl border border-white/15 bg-white object-cover"
      />
    );
  }

  return (
    <div className="flex size-16 items-center justify-center rounded-2xl border border-white/15 bg-white text-xl font-semibold text-[#181818]">
      {initials || "N"}
    </div>
  );
}

export function TextInput({
  label,
  value,
  onChange,
  autoComplete
}: {
  label: string;
  value: string;
  onChange: (value: string) => void;
  autoComplete?: string;
}) {
  return (
    <label className="text-sm font-medium">
      <span className="mb-2 block">{label}</span>
      <input
        value={value}
        autoComplete={autoComplete}
        onChange={(event) => onChange(event.target.value)}
        className="h-12 w-full rounded-xl border border-border bg-background px-3 text-sm transition-colors outline-none focus:border-foreground"
      />
    </label>
  );
}

export function PublicShell({ title, subtitle }: { title: string; subtitle: string }) {
  return (
    <main className="flex min-h-screen items-center justify-center bg-muted px-6 text-foreground">
      <div className="rounded-xl border border-border bg-card p-8 text-center text-card-foreground">
        <h1 className="font-display text-2xl font-semibold">{title}</h1>
        <p className="mt-2 text-sm text-muted-foreground">{subtitle}</p>
      </div>
    </main>
  );
}
