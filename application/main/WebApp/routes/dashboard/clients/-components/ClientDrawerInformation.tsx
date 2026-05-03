import type { Dispatch, SetStateAction } from "react";

import { Trans } from "@lingui/react/macro";
import { Button } from "@repo/ui/components/Button";
import { Loader2Icon } from "lucide-react";

import type { Client } from "@/shared/lib/appointmentsApi";

export interface ClientForm {
  name: string;
  phone: string;
  email: string;
  status: string;
  alert: string;
  internalNote: string;
}

interface ClientDrawerInformationProps {
  client: Client;
  form: ClientForm;
  pending: boolean;
  error?: Error | null;
  onChange: Dispatch<SetStateAction<ClientForm>>;
  onReset: () => void;
  onSubmit: () => void;
}

export function ClientDrawerInformation({
  form,
  pending,
  error,
  onChange,
  onReset,
  onSubmit
}: ClientDrawerInformationProps) {
  return (
    <div className="mx-auto grid max-w-3xl gap-5">
      <div className="grid gap-4 md:grid-cols-2">
        <Field label="Name" value={form.name} onChange={(name) => onChange((current) => ({ ...current, name }))} />
        <Field label="Phone" value={form.phone} onChange={(phone) => onChange((current) => ({ ...current, phone }))} />
        <Field label="Email" value={form.email} onChange={(email) => onChange((current) => ({ ...current, email }))} />
        <label className="grid gap-1.5 text-sm">
          <span className="text-xs font-semibold text-muted-foreground">Status</span>
          <select
            value={form.status}
            onChange={(event) => onChange((current) => ({ ...current, status: event.target.value }))}
            className="h-10 rounded-md border border-border bg-background px-3 text-sm outline-none focus:border-foreground/40"
          >
            <option value="Active">Active</option>
            <option value="New">New</option>
            <option value="VIP">VIP</option>
            <option value="Blocked">Blocked</option>
          </select>
        </label>
      </div>
      <TextArea
        label="Client alert"
        value={form.alert}
        onChange={(alert) => onChange((current) => ({ ...current, alert }))}
        placeholder="Important note shown in Activity before appointments."
      />
      <TextArea
        label="Internal note"
        value={form.internalNote}
        onChange={(internalNote) => onChange((current) => ({ ...current, internalNote }))}
        placeholder="Private operational context for staff."
      />
      {error && (
        <div className="rounded-md border border-destructive/20 bg-destructive/5 px-3 py-2 text-sm text-destructive">
          {error.message}
        </div>
      )}
      <div className="flex justify-end gap-2 border-t border-border pt-4">
        <Button type="button" variant="outline" onClick={onReset}>
          <Trans>Reset</Trans>
        </Button>
        <Button
          type="button"
          disabled={pending || form.name.trim() === "" || form.phone.trim() === ""}
          onClick={onSubmit}
        >
          {pending && <Loader2Icon className="mr-2 size-4 animate-spin" />}
          <Trans>Save client</Trans>
        </Button>
      </div>
    </div>
  );
}

export function toClientForm(client: Client): ClientForm {
  return {
    name: client.name,
    phone: client.phone,
    email: client.email,
    status: client.status,
    alert: client.alert ?? "",
    internalNote: client.internalNote ?? ""
  };
}

function Field({ label, value, onChange }: { label: string; value: string; onChange: (value: string) => void }) {
  return (
    <label className="grid gap-1.5 text-sm">
      <span className="text-xs font-semibold text-muted-foreground">{label}</span>
      <input
        value={value}
        onChange={(event) => onChange(event.target.value)}
        className="h-10 rounded-md border border-border bg-background px-3 text-sm outline-none focus:border-foreground/40"
      />
    </label>
  );
}

function TextArea({
  label,
  value,
  onChange,
  placeholder
}: {
  label: string;
  value: string;
  onChange: (value: string) => void;
  placeholder: string;
}) {
  return (
    <label className="grid gap-1.5 text-sm">
      <span className="text-xs font-semibold text-muted-foreground">{label}</span>
      <textarea
        value={value}
        placeholder={placeholder}
        onChange={(event) => onChange(event.target.value)}
        className="min-h-24 resize-y rounded-md border border-border bg-background px-3 py-2 text-sm outline-none placeholder:text-muted-foreground focus:border-foreground/40"
      />
    </label>
  );
}
