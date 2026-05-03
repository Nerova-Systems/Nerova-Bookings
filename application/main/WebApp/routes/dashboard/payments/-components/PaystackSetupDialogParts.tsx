import type { Dispatch, SetStateAction } from "react";

import { Trans } from "@lingui/react/macro";
import { Button } from "@repo/ui/components/Button";
import { CheckCircle2Icon, ChevronRightIcon, Loader2Icon, LockKeyholeIcon, ShieldCheckIcon, XIcon } from "lucide-react";

import type { PaystackBank, PaystackSubaccount } from "@/shared/lib/paymentsApi";

export interface PaystackSetupFormState {
  bankCode: string;
  accountNumber: string;
  accountName: string;
  primaryContactName: string;
  primaryContactEmail: string;
  primaryContactPhone: string;
}

interface SidebarProps {
  activeStep: string;
  subaccount?: PaystackSubaccount;
  onClose: () => void;
}

interface FormProps {
  banks: PaystackBank[];
  banksLoading: boolean;
  canResolve: boolean;
  canSave: boolean;
  error?: string;
  form: PaystackSetupFormState;
  resolving: boolean;
  saving: boolean;
  setForm: Dispatch<SetStateAction<PaystackSetupFormState>>;
  onResolve: () => void;
  onSave: () => void;
}

export function PaystackSetupSidebar({ activeStep, subaccount, onClose }: SidebarProps) {
  return (
    <div className="bg-[#011b33] px-5 py-5 text-white">
      <div className="flex items-start justify-between gap-4">
        <div>
          <div className="text-[11px] font-semibold tracking-[0.18em] text-[#7fd8ff] uppercase">Paystack</div>
          <h2 className="mt-2 font-display text-2xl leading-tight font-semibold">
            <Trans>Set up payouts</Trans>
          </h2>
          <p className="mt-2 text-sm leading-5 text-white/70">
            <Trans>Send appointment payments directly to your business bank account.</Trans>
          </p>
        </div>
        <button
          type="button"
          className="rounded-full p-1.5 text-white/70 hover:bg-white/10 hover:text-white"
          onClick={onClose}
        >
          <XIcon className="size-4" />
        </button>
      </div>
      <div className="mt-6 grid gap-2 text-[11px] font-medium">
        {["Bank", "Verify", "Confirm"].map((step, index) => (
          <StepItem
            key={step}
            active={step === activeStep}
            done={index < ["Bank", "Verify", "Confirm"].indexOf(activeStep)}
            label={step}
          />
        ))}
      </div>
      <div className="mt-6 grid gap-3 rounded-[4px] border border-white/10 bg-white/[0.06] p-4">
        <SummaryRow label="Payout account" value={subaccount?.subaccountCode ?? "New subaccount"} />
        <SummaryRow label="Business" value={subaccount?.businessName ?? "Workspace profile"} />
        <SummaryRow label="Platform fee" value="0%" />
        <SummaryRow label="Stored account" value={subaccount?.maskedAccountNumber ?? "Not configured"} />
      </div>
    </div>
  );
}

export function PaystackSetupForm({
  banks,
  banksLoading,
  canResolve,
  canSave,
  error,
  form,
  resolving,
  saving,
  setForm,
  onResolve,
  onSave
}: FormProps) {
  return (
    <div className="flex min-h-0 min-w-0 flex-col bg-white">
      <div className="border-b border-[#eef3f7] px-5 py-4">
        <h3 className="font-display text-lg font-semibold">Bank details</h3>
        <p className="mt-1 text-sm text-[#5f7285]">Verify the account before saving the Paystack subaccount.</p>
      </div>
      <div className="min-h-0 min-w-0 overflow-y-auto px-5 py-4">
        <div className="grid gap-4 md:grid-cols-2">
          <BankSelect banks={banks} banksLoading={banksLoading} value={form.bankCode} setForm={setForm} />
          <Field
            label="Account number"
            value={form.accountNumber}
            onChange={(accountNumber) => setForm((current) => ({ ...current, accountNumber }))}
          />
          <ResolveButton disabled={!canResolve || resolving} resolving={resolving} onResolve={onResolve} />
          <div className="md:col-span-2">
            <Field
              label="Account holder"
              value={form.accountName}
              onChange={(accountName) => setForm((current) => ({ ...current, accountName }))}
            />
          </div>
          {form.accountName && <VerifiedAccount accountName={form.accountName} />}
          <ContactFields form={form} setForm={setForm} />
          {error && <ErrorMessage message={error} />}
        </div>
      </div>
      <SaveFooter canSave={canSave} saving={saving} onSave={onSave} />
    </div>
  );
}

function BankSelect({
  banks,
  banksLoading,
  value,
  setForm
}: {
  banks: PaystackBank[];
  banksLoading: boolean;
  value: string;
  setForm: FormProps["setForm"];
}) {
  return (
    <label className="grid gap-1.5 text-sm">
      <span className="text-xs font-semibold text-[#5f7285]">Bank</span>
      <select
        value={value}
        onChange={(event) => setForm((current) => ({ ...current, bankCode: event.target.value }))}
        className="h-12 rounded-[4px] border border-[#d9e3ec] bg-white px-3 text-sm transition outline-none focus:border-[#00a5d8] focus:ring-2 focus:ring-[#00a5d8]/15"
      >
        <option value="">{banksLoading ? "Loading banks..." : "Select a South African bank"}</option>
        {banks.map((bank) => (
          <option key={bank.code} value={bank.code}>
            {bank.name}
          </option>
        ))}
      </select>
    </label>
  );
}

function ResolveButton({
  disabled,
  resolving,
  onResolve
}: {
  disabled: boolean;
  resolving: boolean;
  onResolve: () => void;
}) {
  return (
    <div className="grid content-end gap-1.5">
      <Button
        type="button"
        variant="outline"
        className="h-12 justify-center border-[#d9e3ec]"
        disabled={disabled}
        onClick={onResolve}
      >
        {resolving ? (
          <Loader2Icon className="mr-2 size-4 animate-spin" />
        ) : (
          <CheckCircle2Icon className="mr-2 size-4" />
        )}
        <Trans>Verify account with Paystack</Trans>
      </Button>
    </div>
  );
}

function VerifiedAccount({ accountName }: { accountName: string }) {
  return (
    <div className="flex items-center gap-2 rounded-[4px] border border-[#bfead3] bg-[#effaf4] px-3 py-2 text-sm text-[#087a3c] md:col-span-2">
      <ShieldCheckIcon className="size-4" />
      <span>Verified account holder: {accountName}</span>
    </div>
  );
}

function ContactFields({ form, setForm }: { form: PaystackSetupFormState; setForm: FormProps["setForm"] }) {
  return (
    <div className="grid min-w-0 gap-4 border-t border-[#eef3f7] pt-4 md:col-span-2 md:grid-cols-2">
      <Field
        label="Contact name"
        value={form.primaryContactName}
        onChange={(primaryContactName) => setForm((current) => ({ ...current, primaryContactName }))}
      />
      <Field
        label="Contact email"
        value={form.primaryContactEmail}
        onChange={(primaryContactEmail) => setForm((current) => ({ ...current, primaryContactEmail }))}
      />
      <div className="md:col-span-2">
        <Field
          label="Contact phone"
          value={form.primaryContactPhone}
          onChange={(primaryContactPhone) => setForm((current) => ({ ...current, primaryContactPhone }))}
        />
      </div>
    </div>
  );
}

function ErrorMessage({ message }: { message: string }) {
  return (
    <div className="rounded-[4px] border border-[#f4b8b8] bg-[#fff3f3] px-3 py-2 text-sm text-[#b42318] md:col-span-2">
      {message}
    </div>
  );
}

function SaveFooter({ canSave, saving, onSave }: { canSave: boolean; saving: boolean; onSave: () => void }) {
  return (
    <div className="grid min-w-0 gap-3 border-t border-[#eef3f7] px-5 py-4 md:grid-cols-[minmax(0,1fr)_auto] md:items-center">
      <div className="flex min-w-0 items-center gap-2 text-xs text-[#6b7f93]">
        <LockKeyholeIcon className="size-3.5 shrink-0" />
        <span>Secured by Paystack. Nerova stores only masked bank details.</span>
      </div>
      <Button
        type="button"
        className="h-12 w-full bg-[#011b33] px-5 text-white hover:bg-[#092844] md:w-auto"
        disabled={!canSave || saving}
        onClick={onSave}
      >
        {saving && <Loader2Icon className="mr-2 size-4 animate-spin" />}
        <Trans>Save payout account</Trans>
        {!saving && <ChevronRightIcon className="ml-2 size-4" />}
      </Button>
    </div>
  );
}

function StepItem({ active, done, label }: { active: boolean; done: boolean; label: string }) {
  return (
    <div
      className={`flex items-center justify-center rounded-full px-3 py-1.5 ${active || done ? "bg-[#00c3f7] text-[#011b33]" : "bg-white/10 text-white/60"}`}
    >
      {done ? "Done" : label}
    </div>
  );
}

function SummaryRow({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex items-center justify-between gap-4 text-sm">
      <span className="text-white/60">{label}</span>
      <span className="truncate font-semibold text-white">{value}</span>
    </div>
  );
}

function Field({ label, value, onChange }: { label: string; value: string; onChange: (value: string) => void }) {
  return (
    <label className="grid min-w-0 gap-1.5 text-sm">
      <span className="text-xs font-semibold text-[#5f7285]">{label}</span>
      <input
        value={value}
        onChange={(event) => onChange(event.target.value)}
        className="h-12 w-full min-w-0 rounded-[4px] border border-[#d9e3ec] bg-white px-3 text-sm transition outline-none focus:border-[#00a5d8] focus:ring-2 focus:ring-[#00a5d8]/15"
      />
    </label>
  );
}
