/* eslint-disable max-lines */
import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Button } from "@repo/ui/components/Button";
import { Input } from "@repo/ui/components/Input";
import { Switch } from "@repo/ui/components/Switch";
import { createFileRoute, useNavigate } from "@tanstack/react-router";
import { ArrowLeftIcon, BookmarkIcon, Clock3Icon, FilterIcon, PlusIcon, SearchIcon, Trash2Icon } from "lucide-react";
import { useEffect, useMemo, useState, type FormEvent, type ReactNode } from "react";
import { toast } from "sonner";

import type { BusinessClosure, HolidaySettings } from "@/shared/lib/appointmentsApi";
import { useAppointmentShell } from "@/shared/lib/appointmentsApi";
import { useCreateClosure, useDeleteClosure } from "@/shared/lib/availabilitySettingsApi";

import { HolidaySettingsPanel } from "../../calendar/-components/HolidaySettingsPanel";

type OooTab = "my-ooo" | "holidays";

export const Route = createFileRoute("/dashboard/settings/out-of-office/")({
  staticData: { trackingTitle: "Out of office" },
  component: OutOfOfficePage
});

function OutOfOfficePage() {
  const navigate = useNavigate();
  const shellQuery = useAppointmentShell();
  const [tab, setTab] = useState<OooTab>("my-ooo");
  const [createOpen, setCreateOpen] = useState(false);
  const manualClosures = (shellQuery.data?.closures ?? []).filter((closure) => closure.type === "manual");

  useEffect(() => {
    document.title = t`Out of office | Nerova`;
  }, []);

  return (
    <main className="flex min-h-0 flex-1 overflow-y-auto bg-[#0f0f0f] text-white">
      <aside className="hidden w-72 shrink-0 border-r border-white/10 bg-[#141414] px-6 py-7 lg:block">
        <button
          type="button"
          onClick={() => navigate({ to: "/dashboard/availability" })}
          className="mb-12 flex items-center gap-3 text-lg font-semibold text-white"
        >
          <ArrowLeftIcon className="size-5" />
          <Trans>Back</Trans>
        </button>
        <nav className="space-y-3 text-lg">
          <div className="flex items-center gap-3 text-white/55">
            <Clock3Icon className="size-5" />
            <Trans>Overview</Trans>
          </div>
          <div className="mt-9 text-white/55">Colin Swart</div>
          {["Profile", "General", "Calendars", "Conferencing", "Appearance", "Out of office", "Push notifications", "Features"].map((item) => (
            <div key={item} className={`rounded-xl px-3 py-2 font-semibold ${item === "Out of office" ? "bg-white/[0.08] text-white" : "text-white"}`}>
              {item}
            </div>
          ))}
        </nav>
      </aside>

      <section className="min-w-0 flex-1 px-8 py-7">
        <h1 className="font-display text-3xl font-semibold">
          <Trans>Out of office</Trans>
        </h1>
        <p className="mt-1 text-xl text-white/55">
          {tab === "holidays" ? (
            <Trans>We will automatically mark you as unavailable for the selected holidays</Trans>
          ) : (
            <Trans>Let your bookers know when you're OOO.</Trans>
          )}
        </p>

        <div className="mt-10 flex rounded-xl bg-white/[0.04] p-1 w-fit">
          <OooTabButton active={tab === "my-ooo"} onClick={() => setTab("my-ooo")}>
            <Trans>My OOO</Trans>
          </OooTabButton>
          <OooTabButton active={tab === "holidays"} onClick={() => setTab("holidays")}>
            <Trans>Holidays</Trans>
          </OooTabButton>
        </div>

        {tab === "my-ooo" ? (
          <MyOooTab closures={manualClosures} holidaySettings={shellQuery.data?.holidaySettings} onAdd={() => setCreateOpen(true)} />
        ) : (
          <HolidaysTab holidaySettings={shellQuery.data?.holidaySettings} />
        )}
      </section>

      {createOpen && (
        <CreateOooModal
          holidaySettings={shellQuery.data?.holidaySettings}
          onClose={() => setCreateOpen(false)}
          onCreated={() => setCreateOpen(false)}
        />
      )}
    </main>
  );
}

function MyOooTab({ closures, holidaySettings, onAdd }: { closures: BusinessClosure[]; holidaySettings?: HolidaySettings; onAdd: () => void }) {
  const [search, setSearch] = useState("");
  const deleteClosure = useDeleteClosure();
  const filtered = closures.filter((closure) => `${closure.label} ${closure.startDate} ${closure.endDate}`.toLowerCase().includes(search.toLowerCase()));

  return (
    <>
      <div className="mt-8 flex flex-wrap items-center gap-3">
        <div className="flex h-12 min-w-[20rem] items-center gap-3 rounded-xl border border-white/15 px-4 text-white/60">
          <SearchIcon className="size-5" />
          <Input
            value={search}
            onChange={(event) => setSearch(event.target.value)}
            placeholder="Search"
            className="h-auto border-0 bg-transparent px-0 py-0 text-lg text-white shadow-none placeholder:text-white/50 focus-visible:outline-none"
          />
        </div>
        <Button variant="outline" className="h-12 border-white/15 bg-transparent text-white hover:bg-white/[0.08]">
          <FilterIcon className="size-4" />
          <Trans>Filter</Trans>
        </Button>
        <Button variant="outline" className="ml-auto h-12 border-white/15 bg-transparent text-white/45 hover:bg-white/[0.08]">
          <BookmarkIcon className="size-4" />
          <Trans>Save</Trans>
        </Button>
        <Button variant="outline" className="h-12 border-white/15 bg-transparent text-white hover:bg-white/[0.08]">
          <FilterIcon className="size-4" />
          <Trans>Saved filters</Trans>
        </Button>
      </div>

      <section className="mt-7 min-h-[28rem] rounded-xl border border-dashed border-white/10">
        {filtered.length === 0 ? (
          <div className="flex min-h-[28rem] items-center justify-center text-center">
            <div>
              <div className="mx-auto flex size-16 items-center justify-center rounded-2xl border border-white/10 bg-white/[0.06]">
                <Clock3Icon className="size-8 text-white/85" />
              </div>
              <h2 className="mt-8 font-display text-2xl font-semibold">
                <Trans>Create an OOO</Trans>
              </h2>
              <p className="mx-auto mt-4 max-w-lg text-lg leading-8 text-white/55">
                <Trans>Communicate to your bookers when you're not available to take bookings.</Trans>
              </p>
              <Button className="mt-8" onClick={onAdd}>
                <PlusIcon className="size-4" />
                <Trans>Add</Trans>
              </Button>
            </div>
          </div>
        ) : (
          <div>
            {filtered.map((closure) => (
              <div key={closure.id} className="flex items-center gap-4 border-b border-white/10 px-7 py-5 last:border-b-0">
                <div className="flex size-11 items-center justify-center rounded-full bg-white/[0.08]">
                  <Clock3Icon className="size-5 text-white/70" />
                </div>
                <div>
                  <div className="text-lg font-semibold">{closure.label}</div>
                  <div className="mt-1 text-sm text-white/55">
                    {formatDate(closure.startDate)}
                    {closure.endDate !== closure.startDate ? ` - ${formatDate(closure.endDate)}` : ""}
                  </div>
                </div>
                <Button
                  variant="ghost"
                  size="icon-sm"
                  className="ml-auto text-white/60 hover:bg-white/[0.08] hover:text-white"
                  isPending={deleteClosure.isPending}
                  onClick={() =>
                    deleteClosure.mutate(closure.id, {
                      onSuccess: () => toast.success("OOO removed."),
                      onError: (error) => toast.error(error instanceof Error ? error.message : "Could not remove OOO.")
                    })
                  }
                >
                  <Trash2Icon className="size-4" />
                </Button>
              </div>
            ))}
            <div className="px-7 py-5">
              <Button onClick={onAdd}>
                <PlusIcon className="size-4" />
                <Trans>Add OOO</Trans>
              </Button>
            </div>
          </div>
        )}
      </section>
      {holidaySettings && <div className="mt-4 text-sm text-white/45">{holidaySettings.holidays.filter((holiday) => !holiday.isOpen).length} holidays are currently blocking bookings.</div>}
    </>
  );
}

function HolidaysTab({ holidaySettings }: { holidaySettings?: HolidaySettings }) {
  return (
    <div className="mt-7 max-w-5xl [&_section]:rounded-3xl [&_section]:border-white/10 [&_section]:bg-[#202020] [&_select]:bg-[#202020]">
      <HolidaySettingsPanel holidaySettings={holidaySettings} />
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
  const overlappingHoliday = useMemo(() => findOverlappingHoliday(holidaySettings, form.startDate, form.endDate), [holidaySettings, form.startDate, form.endDate]);

  const submit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (!form.startDate) {
      toast.error("Choose an OOO start date.");
      return;
    }
    if (form.endDate < form.startDate) {
      toast.error("OOO end date must be on or after the start date.");
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
          toast.success("OOO created.");
          onCreated();
        },
        onError: (error) => toast.error(error instanceof Error ? error.message : "Could not create OOO.")
      }
    );
  };

  return (
    <div className="fixed inset-0 z-50 flex items-start justify-center overflow-y-auto bg-black/70 px-4 py-8">
      <form onSubmit={submit} className="w-full max-w-3xl overflow-hidden rounded-3xl border border-white/10 bg-[#202020] text-white shadow-2xl">
        <div className="max-h-[calc(100vh-7rem)] overflow-y-auto px-9 py-8">
          <h2 className="font-display text-3xl font-semibold">
            <Trans>Go Out of Office</Trans>
          </h2>
          <div className="mt-7 grid gap-3 text-xl font-semibold">
            <Trans>Date range</Trans>
            <div className="grid grid-cols-2 gap-3">
              <Input
                type="date"
                value={form.startDate}
                onChange={(event) => setForm((current) => ({ ...current, startDate: event.target.value }))}
                className="h-12 border-white/15 bg-[#262626] text-white"
              />
              <Input
                type="date"
                value={form.endDate}
                onChange={(event) => setForm((current) => ({ ...current, endDate: event.target.value }))}
                className="h-12 border-white/15 bg-[#262626] text-white"
              />
            </div>
          </div>

          {overlappingHoliday && (
            <div className="mt-6 rounded-2xl border border-sky-500/40 bg-sky-500/10 px-5 py-4">
              <div className="font-semibold">
                <Trans>Holiday notice</Trans>
              </div>
              <div className="mt-2 text-white/60">
                {formatDate(overlappingHoliday.date)} is {overlappingHoliday.label}, which is already blocked as a holiday.
              </div>
            </div>
          )}

          <label className="mt-7 grid gap-3 text-xl font-semibold">
            <Trans>Reason</Trans>
            <select
              value={form.reason}
              onChange={(event) => setForm((current) => ({ ...current, reason: event.target.value }))}
              className="h-12 rounded-xl border border-white/15 bg-[#262626] px-4 text-base text-white outline-none"
            >
              <option>Unspecified</option>
              <option>Annual leave</option>
              <option>Sick leave</option>
              <option>Public holiday</option>
              <option>Training</option>
            </select>
          </label>

          <label className="mt-7 grid gap-3 text-xl font-semibold">
            <Trans>Notes</Trans>
            <textarea
              value={form.notes}
              onChange={(event) => setForm((current) => ({ ...current, notes: event.target.value }))}
              placeholder="Additional notes"
              className="min-h-28 resize-y rounded-xl border border-white/15 bg-[#262626] px-4 py-3 text-base text-white outline-none placeholder:text-white/35"
            />
          </label>

          <div className="mt-4 flex items-center gap-3 text-base font-semibold text-white/65">
            <Switch checked={form.showPublicNote} onCheckedChange={(showPublicNote) => setForm((current) => ({ ...current, showPublicNote }))} />
            <Trans>Show note on public booking page</Trans>
          </div>
        </div>

        <footer className="flex items-center justify-end gap-3 border-t border-white/10 bg-[#1a1a1a] px-9 py-5">
          <Button type="button" variant="ghost" className="text-white hover:bg-white/[0.08]" onClick={onClose}>
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
        active ? "bg-white/[0.12] text-white shadow-[0_0_0_1px_rgba(255,255,255,0.06)]" : "text-white/45 hover:text-white"
      }`}
    >
      {children}
    </button>
  );
}

function findOverlappingHoliday(holidaySettings: HolidaySettings | undefined, startDate: string, endDate: string) {
  return holidaySettings?.holidays.find((holiday) => holiday.date >= startDate && holiday.date <= endDate && !holiday.isOpen);
}

function formatDate(date: string) {
  return new Intl.DateTimeFormat(undefined, { day: "numeric", month: "short", year: "numeric" }).format(new Date(`${date}T00:00:00`));
}

function toDateInputValue(date: Date) {
  const year = date.getFullYear();
  const month = `${date.getMonth() + 1}`.padStart(2, "0");
  const day = `${date.getDate()}`.padStart(2, "0");
  return `${year}-${month}-${day}`;
}
