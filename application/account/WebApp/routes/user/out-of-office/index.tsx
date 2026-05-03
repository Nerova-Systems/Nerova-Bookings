/* eslint-disable max-lines */
import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { AppLayout } from "@repo/ui/components/AppLayout";
import { Button } from "@repo/ui/components/Button";
import { Input } from "@repo/ui/components/Input";
import { Switch } from "@repo/ui/components/Switch";
import { createFileRoute } from "@tanstack/react-router";
import { Clock3Icon, PlusIcon, SearchIcon, Trash2Icon } from "lucide-react";
import { useMemo, useState, type FormEvent, type ReactNode } from "react";
import { toast } from "sonner";

import {
  type BusinessClosure,
  type HolidaySettings,
  type PublicHoliday,
  useCreateClosure,
  useDeleteClosure,
  useMainAppointmentShell,
  useUpdateHolidaySettings
} from "@/shared/lib/mainAppSettingsApi";

type OooTab = "my-ooo" | "holidays";

export const Route = createFileRoute("/user/out-of-office/")({
  staticData: { trackingTitle: "Out of office" },
  component: OutOfOfficePage
});

function OutOfOfficePage() {
  const shellQuery = useMainAppointmentShell();
  const [tab, setTab] = useState<OooTab>("my-ooo");
  const [createOpen, setCreateOpen] = useState(false);
  const manualClosures = (shellQuery.data?.closures ?? []).filter((closure) => closure.type === "manual");

  return (
    <AppLayout
      variant="center"
      maxWidth="72rem"
      balanceWidth="10rem"
      title={t`Out of office`}
      subtitle={
        tab === "holidays"
          ? t`We will automatically mark you as unavailable for the selected holidays`
          : t`Let your bookers know when you're OOO.`
      }
    >
      <div className="pt-6">
        <div className="flex w-fit rounded-xl bg-muted p-1">
          <OooTabButton active={tab === "my-ooo"} onClick={() => setTab("my-ooo")}>
            <Trans>My OOO</Trans>
          </OooTabButton>
          <OooTabButton active={tab === "holidays"} onClick={() => setTab("holidays")}>
            <Trans>Holidays</Trans>
          </OooTabButton>
        </div>

        {tab === "my-ooo" ? (
          <MyOooTab
            closures={manualClosures}
            holidaySettings={shellQuery.data?.holidaySettings}
            onAdd={() => setCreateOpen(true)}
          />
        ) : (
          <HolidaysTab holidaySettings={shellQuery.data?.holidaySettings} />
        )}
      </div>

      {createOpen && (
        <CreateOooModal
          holidaySettings={shellQuery.data?.holidaySettings}
          onClose={() => setCreateOpen(false)}
          onCreated={() => setCreateOpen(false)}
        />
      )}
    </AppLayout>
  );
}

function MyOooTab({
  closures,
  holidaySettings,
  onAdd
}: {
  closures: BusinessClosure[];
  holidaySettings?: HolidaySettings;
  onAdd: () => void;
}) {
  const [search, setSearch] = useState("");
  const deleteClosure = useDeleteClosure();
  const filtered = closures.filter((closure) =>
    `${closure.label} ${closure.startDate} ${closure.endDate}`.toLowerCase().includes(search.toLowerCase())
  );
  const closedHolidayCount = holidaySettings?.holidays.filter((holiday) => !holiday.isOpen).length ?? 0;

  return (
    <>
      <div className="mt-8 flex flex-wrap items-center gap-3">
        <div className="flex h-12 min-w-[20rem] items-center gap-3 rounded-xl border border-border px-4 text-muted-foreground">
          <SearchIcon className="size-5" />
          <Input
            value={search}
            onChange={(event) => setSearch(event.target.value)}
            placeholder={t`Search`}
            className="h-auto border-0 bg-transparent px-0 py-0 shadow-none focus-visible:outline-none"
          />
        </div>
        <div className="ml-auto" />
      </div>

      <section className="mt-7 min-h-[28rem] rounded-xl border border-dashed border-border bg-card">
        {filtered.length === 0 ? (
          <div className="flex min-h-[28rem] items-center justify-center text-center">
            <div>
              <div className="mx-auto flex size-16 items-center justify-center rounded-2xl border border-border bg-muted">
                <Clock3Icon className="size-8 text-muted-foreground" />
              </div>
              <h2 className="mt-8 text-2xl font-semibold">
                <Trans>Create an OOO</Trans>
              </h2>
              <p className="mx-auto mt-4 max-w-lg leading-7 text-muted-foreground">
                <Trans>Communicate to your bookers when you're not available to take bookings.</Trans>
              </p>
              <Button type="button" className="mt-8" onClick={onAdd}>
                <PlusIcon className="size-4" />
                <Trans>Add</Trans>
              </Button>
            </div>
          </div>
        ) : (
          <div>
            {filtered.map((closure) => (
              <div
                key={closure.id}
                className="flex items-center gap-4 border-b border-border px-6 py-5 last:border-b-0"
              >
                <div className="flex size-11 items-center justify-center rounded-full bg-muted">
                  <Clock3Icon className="size-5 text-muted-foreground" />
                </div>
                <div>
                  <div className="font-semibold">{closure.label}</div>
                  <div className="mt-1 text-sm text-muted-foreground">
                    {formatDate(closure.startDate)}
                    {closure.endDate !== closure.startDate ? ` - ${formatDate(closure.endDate)}` : ""}
                  </div>
                </div>
                <Button
                  type="button"
                  variant="ghost"
                  size="icon-sm"
                  className="ml-auto"
                  isPending={deleteClosure.isPending}
                  onClick={() =>
                    deleteClosure.mutate(closure.id, {
                      onSuccess: () => toast.success(t`OOO removed.`),
                      onError: (error) => toast.error(error instanceof Error ? error.message : t`Could not remove OOO.`)
                    })
                  }
                  aria-label={t`Remove OOO`}
                >
                  <Trash2Icon className="size-4" />
                </Button>
              </div>
            ))}
            <div className="px-6 py-5">
              <Button type="button" onClick={onAdd}>
                <PlusIcon className="size-4" />
                <Trans>Add OOO</Trans>
              </Button>
            </div>
          </div>
        )}
      </section>
      {holidaySettings && (
        <div className="mt-4 text-sm text-muted-foreground">
          {closedHolidayCount} holidays are currently blocking bookings.
        </div>
      )}
    </>
  );
}

function HolidaysTab({ holidaySettings }: { holidaySettings?: HolidaySettings }) {
  if (!holidaySettings) {
    return (
      <section className="mt-7 rounded-xl border border-border bg-card p-6 text-muted-foreground">
        <Trans>Holiday settings are loading.</Trans>
      </section>
    );
  }

  return (
    <section className="mt-7 overflow-hidden rounded-xl border border-border bg-card">
      <div className="flex flex-wrap items-center gap-3 border-b border-border px-6 py-5">
        <div>
          <h2 className="text-lg font-semibold">
            <Trans>Holidays</Trans>
          </h2>
          <p className="mt-1 text-sm text-muted-foreground">
            <Trans>Closed by default. Toggle a holiday on when the business is open.</Trans>
          </p>
        </div>
        <CountrySelect holidaySettings={holidaySettings} />
      </div>
      <div>
        {holidaySettings.holidays.map((holiday) => (
          <HolidayRow key={holiday.id} holiday={holiday} holidaySettings={holidaySettings} />
        ))}
      </div>
    </section>
  );
}

function CountrySelect({ holidaySettings }: { holidaySettings: HolidaySettings }) {
  const updateHolidaySettings = useUpdateHolidaySettings();
  return (
    <select
      className="ml-auto h-10 rounded-md border border-input bg-background px-3 text-sm outline-ring focus-visible:outline-2 focus-visible:outline-offset-2"
      value={holidaySettings.countryCode}
      disabled={updateHolidaySettings.isPending}
      onChange={(event) =>
        updateHolidaySettings.mutate(
          { countryCode: event.target.value, openHolidayIds: [] },
          {
            onSuccess: () => toast.success(t`Public holiday country updated.`),
            onError: (error) =>
              toast.error(error instanceof Error ? error.message : t`Could not update public holidays.`)
          }
        )
      }
    >
      {holidaySettings.countries.map((country) => (
        <option key={country.code} value={country.code}>
          {country.code} {country.name}
        </option>
      ))}
    </select>
  );
}

function HolidayRow({ holiday, holidaySettings }: { holiday: PublicHoliday; holidaySettings: HolidaySettings }) {
  const updateHolidaySettings = useUpdateHolidaySettings();
  const openHolidayIds = holidaySettings.holidays.filter((item) => item.isOpen).map((item) => item.id);

  const toggleHoliday = (isOpen: boolean) => {
    const nextOpenHolidayIds = isOpen
      ? [...openHolidayIds, holiday.id]
      : openHolidayIds.filter((holidayId) => holidayId !== holiday.id);

    updateHolidaySettings.mutate(
      {
        countryCode: holidaySettings.countryCode,
        openHolidayIds: Array.from(new Set(nextOpenHolidayIds))
      },
      {
        onSuccess: () => toast.success(isOpen ? t`Public holiday marked open.` : t`Public holiday marked closed.`),
        onError: (error) => toast.error(error instanceof Error ? error.message : t`Could not update public holiday.`)
      }
    );
  };

  return (
    <div className="flex items-center gap-5 border-b border-border px-6 py-5 last:border-b-0">
      <div className="flex size-12 items-center justify-center rounded-full bg-muted text-xl">📅</div>
      <div className="min-w-0">
        <div className="truncate text-lg font-semibold">{holiday.label}</div>
        <div className="mt-1 text-muted-foreground">{formatDate(holiday.date)}</div>
      </div>
      <Switch
        className="ml-auto"
        checked={holiday.isOpen}
        disabled={updateHolidaySettings.isPending}
        onCheckedChange={toggleHoliday}
      />
    </div>
  );
}

function CreateOooModal({
  holidaySettings,
  onClose,
  onCreated
}: {
  holidaySettings?: HolidaySettings;
  onClose: () => void;
  onCreated: () => void;
}) {
  const today = toDateInputValue(new Date());
  const [form, setForm] = useState({
    startDate: today,
    endDate: today,
    reason: "Unspecified",
    notes: "",
    showPublicNote: false
  });
  const createClosure = useCreateClosure();
  const overlappingHoliday = useMemo(
    () => findOverlappingHoliday(holidaySettings, form.startDate, form.endDate),
    [holidaySettings, form.startDate, form.endDate]
  );

  const submit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (!form.startDate) {
      toast.error(t`Choose an OOO start date.`);
      return;
    }
    if (form.endDate < form.startDate) {
      toast.error(t`OOO end date must be on or after the start date.`);
      return;
    }

    createClosure.mutate(
      {
        startDate: form.startDate,
        endDate: form.endDate,
        label: form.reason === "Unspecified" ? "Out of office" : form.reason
      },
      {
        onSuccess: () => {
          toast.success(t`OOO created.`);
          onCreated();
        },
        onError: (error) => toast.error(error instanceof Error ? error.message : t`Could not create OOO.`)
      }
    );
  };

  return (
    <div className="fixed inset-0 z-50 flex items-start justify-center overflow-y-auto bg-black/70 px-4 py-8">
      <form
        onSubmit={submit}
        className="w-full max-w-3xl overflow-hidden rounded-3xl border border-border bg-card shadow-2xl"
      >
        <div className="max-h-[calc(100vh-7rem)] overflow-y-auto px-8 py-7">
          <h2 className="text-3xl font-semibold">
            <Trans>Go Out of Office</Trans>
          </h2>
          <div className="mt-7 grid gap-3 text-lg font-semibold">
            <Trans>Date range</Trans>
            <div className="grid grid-cols-2 gap-3">
              <Input
                type="date"
                value={form.startDate}
                onChange={(event) => setForm((current) => ({ ...current, startDate: event.target.value }))}
                className="h-12"
              />
              <Input
                type="date"
                value={form.endDate}
                onChange={(event) => setForm((current) => ({ ...current, endDate: event.target.value }))}
                className="h-12"
              />
            </div>
          </div>

          {overlappingHoliday && (
            <div className="mt-6 rounded-2xl border border-sky-500/40 bg-sky-500/10 px-5 py-4">
              <div className="font-semibold">
                <Trans>Holiday notice</Trans>
              </div>
              <div className="mt-2 text-muted-foreground">
                {formatDate(overlappingHoliday.date)} is {overlappingHoliday.label}, which is already blocked as a
                holiday.
              </div>
            </div>
          )}

          <label className="mt-7 grid gap-3 text-lg font-semibold">
            <Trans>Reason</Trans>
            <select
              value={form.reason}
              onChange={(event) => setForm((current) => ({ ...current, reason: event.target.value }))}
              className="h-12 rounded-xl border border-input bg-background px-4 text-base outline-none"
            >
              <option>Unspecified</option>
              <option>Annual leave</option>
              <option>Sick leave</option>
              <option>Public holiday</option>
              <option>Training</option>
            </select>
          </label>

          <label className="mt-7 grid gap-3 text-lg font-semibold">
            <Trans>Notes</Trans>
            <textarea
              value={form.notes}
              onChange={(event) => setForm((current) => ({ ...current, notes: event.target.value }))}
              placeholder={t`Additional notes`}
              className="min-h-28 resize-y rounded-xl border border-input bg-background px-4 py-3 text-base outline-none"
            />
          </label>

          <div className="mt-4 flex items-center gap-3 text-base font-semibold text-muted-foreground">
            <Switch
              checked={form.showPublicNote}
              onCheckedChange={(showPublicNote) => setForm((current) => ({ ...current, showPublicNote }))}
            />
            <Trans>Show note on public booking page</Trans>
          </div>
        </div>

        <footer className="flex items-center justify-end gap-3 border-t border-border bg-muted/30 px-8 py-5">
          <Button type="button" variant="ghost" onClick={onClose}>
            <Trans>Cancel</Trans>
          </Button>
          <Button type="submit" isPending={createClosure.isPending}>
            <Trans>Create</Trans>
          </Button>
        </footer>
      </form>
    </div>
  );
}

function OooTabButton({ active, onClick, children }: { active: boolean; onClick: () => void; children: ReactNode }) {
  return (
    <button
      type="button"
      onClick={onClick}
      className={`rounded-lg px-4 py-2.5 text-lg font-semibold transition-colors ${
        active ? "bg-background text-foreground shadow-sm" : "text-muted-foreground hover:text-foreground"
      }`}
    >
      {children}
    </button>
  );
}

function findOverlappingHoliday(holidaySettings: HolidaySettings | undefined, startDate: string, endDate: string) {
  return holidaySettings?.holidays.find(
    (holiday) => holiday.date >= startDate && holiday.date <= endDate && !holiday.isOpen
  );
}

function formatDate(date: string) {
  return new Intl.DateTimeFormat(undefined, { day: "numeric", month: "short", year: "numeric" }).format(
    new Date(`${date}T00:00:00`)
  );
}

function toDateInputValue(date: Date) {
  const year = date.getFullYear();
  const month = `${date.getMonth() + 1}`.padStart(2, "0");
  const day = `${date.getDate()}`.padStart(2, "0");
  return `${year}-${month}-${day}`;
}
