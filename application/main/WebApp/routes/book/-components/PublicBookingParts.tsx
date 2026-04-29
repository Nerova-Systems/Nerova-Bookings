import { Trans } from "@lingui/react/macro";

import type { PublicBookingProfile } from "@/shared/lib/publicBookingApi";

export function BookingIntro({ profile }: { profile: PublicBookingProfile }) {
  return (
    <aside className="border-r border-border p-8">
      <div className="mb-8">
        <div className="mb-2 text-xs font-semibold tracking-[0.12em] text-muted-foreground uppercase">Nerova</div>
        <h1 className="font-display text-3xl font-semibold">{profile.name}</h1>
        <p className="mt-2 text-sm text-muted-foreground">{profile.address}</p>
      </div>
      <div className="rounded-xl border border-border bg-muted p-4 text-sm">
        <div className="font-medium">
          <Trans>Public booking test path</Trans>
        </div>
        <p className="mt-1 text-muted-foreground">
          <Trans>
            This uses the same service, availability, client, appointment, and payment records that future fixed
            WhatsApp flows will use.
          </Trans>
        </p>
      </div>
    </aside>
  );
}

export function TextInput({
  label,
  value,
  onChange
}: {
  label: string;
  value: string;
  onChange: (value: string) => void;
}) {
  return (
    <label className="text-sm font-medium">
      <span className="mb-1 block">{label}</span>
      <input
        value={value}
        onChange={(event) => onChange(event.target.value)}
        className="w-full rounded-lg border border-border bg-background px-3 py-2 text-sm"
      />
    </label>
  );
}

export function PublicShell({ title, subtitle }: { title: string; subtitle: string }) {
  return (
    <main className="flex min-h-screen items-center justify-center bg-[#f7f7f5] px-6">
      <div className="rounded-xl border border-border bg-background p-8 text-center">
        <h1 className="font-display text-2xl font-semibold">{title}</h1>
        <p className="mt-2 text-sm text-muted-foreground">{subtitle}</p>
      </div>
    </main>
  );
}
