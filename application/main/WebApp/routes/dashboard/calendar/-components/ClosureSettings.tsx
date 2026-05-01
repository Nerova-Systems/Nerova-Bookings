import { Button } from "@repo/ui/components/Button";
import { Input } from "@repo/ui/components/Input";
import { CalendarOffIcon, Trash2Icon } from "lucide-react";
import { useState, type FormEvent } from "react";
import { toast } from "sonner";

import type { BusinessClosure } from "@/shared/lib/appointmentsApi";
import { useCreateClosure, useDeleteClosure } from "@/shared/lib/availabilitySettingsApi";

export function ClosureSettings({ closures }: { closures: BusinessClosure[] }) {
  const [closureForm, setClosureForm] = useState({ startDate: "", endDate: "", label: "" });
  const createClosure = useCreateClosure();
  const deleteClosure = useDeleteClosure();
  const manualClosures = closures.filter((closure) => closure.type === "manual");

  const addClosure = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const startDate = closureForm.startDate;
    const endDate = closureForm.endDate || startDate;
    if (!startDate) {
      toast.error("Choose a closure date.");
      return;
    }
    if (endDate < startDate) {
      toast.error("Closure end date must be on or after start date.");
      return;
    }

    createClosure.mutate(
      { startDate, endDate, label: closureForm.label || "Closed" },
      {
        onSuccess: () => {
          toast.success("Closed date added.");
          setClosureForm({ startDate: "", endDate: "", label: "" });
        },
        onError: (error) => toast.error(error instanceof Error ? error.message : "Could not add closed date.")
      }
    );
  };

  return (
    <div className="grid gap-5 border-t border-border pt-5">
      <div className="mb-3 flex items-center gap-2">
        <CalendarOffIcon className="size-4 text-muted-foreground" />
        <h2 className="text-sm font-medium">Manual closed dates</h2>
      </div>
      <form className="grid gap-2 md:grid-cols-[1fr_1fr_1.5fr_auto]" onSubmit={addClosure}>
        <Input
          type="date"
          value={closureForm.startDate}
          onChange={(event) => setClosureForm((current) => ({ ...current, startDate: event.target.value }))}
        />
        <Input
          type="date"
          value={closureForm.endDate}
          onChange={(event) => setClosureForm((current) => ({ ...current, endDate: event.target.value }))}
        />
        <Input
          value={closureForm.label}
          placeholder="Reason"
          onChange={(event) => setClosureForm((current) => ({ ...current, label: event.target.value }))}
        />
        <Button type="submit" size="sm" isPending={createClosure.isPending}>
          Add closed date
        </Button>
      </form>
      <div className="mt-3 max-h-52 overflow-y-auto rounded-lg border border-border">
        {manualClosures.length === 0 && <div className="px-3 py-2 text-sm text-muted-foreground">No manual closed dates.</div>}
        {manualClosures.map((closure) => (
          <ClosureRow key={closure.id} closure={closure} deleteClosure={deleteClosure} />
        ))}
      </div>
    </div>
  );
}

function ClosureRow({
  closure,
  deleteClosure
}: {
  closure: BusinessClosure;
  deleteClosure: ReturnType<typeof useDeleteClosure>;
}) {
  return (
    <div className="flex items-center gap-3 border-b border-border px-3 py-2 last:border-0">
      <div className="min-w-0">
        <div className="truncate text-sm font-medium">{closure.label}</div>
        <div className="text-xs text-muted-foreground">
          {closure.startDate}
          {closure.endDate !== closure.startDate ? ` to ${closure.endDate}` : ""} -{" "}
          {closure.type === "publicHoliday" ? "Public holiday" : "Manual closure"}
        </div>
      </div>
      {closure.type === "manual" && (
        <Button
          type="button"
          variant="ghost"
          size="icon-sm"
          className="ml-auto"
          isPending={deleteClosure.isPending}
          onClick={() =>
            deleteClosure.mutate(closure.id, {
              onSuccess: () => toast.success("Closed date removed."),
              onError: (error) => toast.error(error instanceof Error ? error.message : "Could not remove closed date.")
            })
          }
        >
          <Trash2Icon className="size-4" />
        </Button>
      )}
    </div>
  );
}
