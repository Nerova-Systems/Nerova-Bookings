import type { FormEvent } from "react";

import { Trans } from "@lingui/react/macro";
import { Button } from "@repo/ui/components/Button";

export interface BlockTimeForm {
  title: string;
  date: string;
  start: string;
  end: string;
}

export function BlockTimeDialog({
  form,
  pending,
  onChange,
  onClose,
  onSubmit
}: {
  form: BlockTimeForm;
  pending: boolean;
  onChange: (form: BlockTimeForm) => void;
  onClose: () => void;
  onSubmit: (event: FormEvent<HTMLFormElement>) => void;
}) {
  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/30 px-4">
      <form onSubmit={onSubmit} className="w-full max-w-md rounded-xl border border-border bg-background p-4 shadow-xl">
        <div className="mb-3">
          <h2 className="font-display text-lg">
            <Trans>Block time</Trans>
          </h2>
          <p className="text-xs text-muted-foreground">Africa/Johannesburg</p>
        </div>
        <div className="grid gap-3">
          <label className="grid gap-1 text-xs font-medium">
            Title
            <input
              value={form.title}
              onChange={(event) => onChange({ ...form, title: event.target.value })}
              className={inputClassName}
            />
          </label>
          <label className="grid gap-1 text-xs font-medium">
            Date
            <input
              type="date"
              value={form.date}
              onChange={(event) => onChange({ ...form, date: event.target.value })}
              className={inputClassName}
              required
            />
          </label>
          <div className="grid grid-cols-2 gap-3">
            <label className="grid gap-1 text-xs font-medium">
              Start
              <input
                type="time"
                value={form.start}
                onChange={(event) => onChange({ ...form, start: event.target.value })}
                className={inputClassName}
                required
              />
            </label>
            <label className="grid gap-1 text-xs font-medium">
              End
              <input
                type="time"
                value={form.end}
                onChange={(event) => onChange({ ...form, end: event.target.value })}
                className={inputClassName}
                required
              />
            </label>
          </div>
        </div>
        <div className="mt-4 flex justify-end gap-2">
          <Button type="button" variant="outline" size="sm" onClick={onClose}>
            <Trans>Cancel</Trans>
          </Button>
          <Button type="submit" size="sm" disabled={pending}>
            <Trans>Save block</Trans>
          </Button>
        </div>
      </form>
    </div>
  );
}

const inputClassName = "h-9 rounded-lg border border-border bg-background px-2.5 text-sm font-normal";
