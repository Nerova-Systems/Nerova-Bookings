import { Trans } from "@lingui/react/macro";
import { Button } from "@repo/ui/components/Button";
import { CheckCircle2Icon, ChevronRightIcon, Loader2Icon, LockKeyholeIcon, ShieldCheckIcon, XIcon } from "lucide-react";
import { useMemo, useState } from "react";

import {
  usePaystackBanks,
  useResolvePaystackAccount,
  useSavePaystackSubaccount,
  type PaystackBank,
  type PaystackSubaccount
} from "@/shared/lib/paymentsApi";

export function PaystackSetupDialog({ subaccount, onClose }: { subaccount?: PaystackSubaccount; onClose: () => void }) {
  const banksQuery = usePaystackBanks();
  const resolveMutation = useResolvePaystackAccount();
  const saveMutation = useSavePaystackSubaccount();
  const [form, setForm] = useState({
    bankCode: subaccount?.settlementBankCode ?? "",
    accountNumber: "",
    accountName: subaccount?.accountName ?? "",
    primaryContactName: "",
    primaryContactEmail: "",
    primaryContactPhone: ""
  });
  const selectedBank = useMemo(
    () => banksQuery.data?.find((bank) => bank.code === form.bankCode),
    [banksQuery.data, form.bankCode]
  );
  const activeStep = form.accountName ? "Confirm" : form.accountNumber ? "Verify" : "Bank";
  const canResolve = form.bankCode.length > 0 && form.accountNumber.length >= 6;
  const canSave = Boolean(selectedBank && form.accountNumber && form.accountName);

  const resolve = async () => {
    const resolved = await resolveMutation.mutateAsync({ bankCode: form.bankCode, accountNumber: form.accountNumber });
    setForm((current) => ({ ...current, accountName: resolved.accountName }));
  };

  const save = async () => {
    if (!selectedBank) return;
    await saveMutation.mutateAsync({
      bankName: selectedBank.name,
      bankCode: selectedBank.code,
      accountNumber: form.accountNumber,
      accountName: form.accountName,
      primaryContactName: form.primaryContactName || undefined,
      primaryContactEmail: form.primaryContactEmail || undefined,
      primaryContactPhone: form.primaryContactPhone || undefined
    });
    onClose();
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-[#011b33]/55 p-4 backdrop-blur-sm">
      <div className="max-h-[calc(100vh-2rem)] w-full max-w-[56rem] overflow-hidden rounded-[6px] border border-[#d9e3ec] bg-white text-[#011b33] shadow-2xl">
        <div className="grid max-h-[calc(100vh-2rem)] overflow-y-auto md:grid-cols-[20rem_minmax(0,1fr)] md:overflow-hidden">
          <div className="bg-[#011b33] px-5 py-5 text-white">
            <div className="flex items-start justify-between gap-4">
              <div>
                <div className="text-[11px] font-semibold tracking-[0.18em] text-[#7fd8ff] uppercase">Paystack</div>
                <h2 className="mt-2 font-display text-2xl font-semibold leading-tight">
                  <Trans>Set up payouts</Trans>
                </h2>
                <p className="mt-2 text-sm leading-5 text-white/70">
                  <Trans>Send appointment payments directly to your business bank account.</Trans>
                </p>
              </div>
              <button type="button" className="rounded-full p-1.5 text-white/70 hover:bg-white/10 hover:text-white" onClick={onClose}>
                <XIcon className="size-4" />
              </button>
            </div>
            <div className="mt-6 grid gap-2 text-[11px] font-medium">
              {["Bank", "Verify", "Confirm"].map((step, index) => (
                <StepItem key={step} active={step === activeStep} done={index < ["Bank", "Verify", "Confirm"].indexOf(activeStep)} label={step} />
              ))}
            </div>
            <div className="mt-6 grid gap-3 rounded-[4px] border border-white/10 bg-white/[0.06] p-4">
              <SummaryRow label="Payout account" value={subaccount?.subaccountCode ?? "New subaccount"} />
              <SummaryRow label="Business" value={subaccount?.businessName ?? "Workspace profile"} />
              <SummaryRow label="Platform fee" value="0%" />
              <SummaryRow label="Stored account" value={subaccount?.maskedAccountNumber ?? "Not configured"} />
            </div>
          </div>

          <div className="flex min-w-0 min-h-0 flex-col bg-white">
            <div className="border-b border-[#eef3f7] px-5 py-4">
              <h3 className="font-display text-lg font-semibold">Bank details</h3>
              <p className="mt-1 text-sm text-[#5f7285]">Verify the account before saving the Paystack subaccount.</p>
            </div>
            <div className="min-w-0 min-h-0 overflow-y-auto px-5 py-4">
              <div className="grid gap-4 md:grid-cols-2">
                <label className="grid gap-1.5 text-sm">
                  <span className="text-xs font-semibold text-[#5f7285]">Bank</span>
                  <select
                    value={form.bankCode}
                    onChange={(event) => setForm((current) => ({ ...current, bankCode: event.target.value }))}
                    className="h-12 rounded-[4px] border border-[#d9e3ec] bg-white px-3 text-sm outline-none transition focus:border-[#00a5d8] focus:ring-2 focus:ring-[#00a5d8]/15"
                  >
                    <option value="">{banksQuery.isLoading ? "Loading banks..." : "Select a South African bank"}</option>
                    {banksQuery.data?.map((bank: PaystackBank) => (
                      <option key={bank.code} value={bank.code}>
                        {bank.name}
                      </option>
                    ))}
                  </select>
                </label>
                <Field label="Account number" value={form.accountNumber} onChange={(accountNumber) => setForm((current) => ({ ...current, accountNumber }))} />
                <div className="grid content-end gap-1.5">
                  <Button
                    type="button"
                    variant="outline"
                    className="h-12 justify-center border-[#d9e3ec]"
                    disabled={!canResolve || resolveMutation.isPending}
                    onClick={resolve}
                  >
                    {resolveMutation.isPending ? <Loader2Icon className="mr-2 size-4 animate-spin" /> : <CheckCircle2Icon className="mr-2 size-4" />}
                    <Trans>Verify account with Paystack</Trans>
                  </Button>
                </div>
                <div className="md:col-span-2">
                  <Field label="Account holder" value={form.accountName} onChange={(accountName) => setForm((current) => ({ ...current, accountName }))} />
                </div>
                {form.accountName && (
                  <div className="flex items-center gap-2 rounded-[4px] border border-[#bfead3] bg-[#effaf4] px-3 py-2 text-sm text-[#087a3c] md:col-span-2">
                    <ShieldCheckIcon className="size-4" />
                    <span>Verified account holder: {form.accountName}</span>
                  </div>
                )}
                <div className="grid min-w-0 gap-4 border-t border-[#eef3f7] pt-4 md:col-span-2 md:grid-cols-2">
                  <Field label="Contact name" value={form.primaryContactName} onChange={(primaryContactName) => setForm((current) => ({ ...current, primaryContactName }))} />
                  <Field label="Contact email" value={form.primaryContactEmail} onChange={(primaryContactEmail) => setForm((current) => ({ ...current, primaryContactEmail }))} />
                  <div className="md:col-span-2">
                    <Field label="Contact phone" value={form.primaryContactPhone} onChange={(primaryContactPhone) => setForm((current) => ({ ...current, primaryContactPhone }))} />
                  </div>
                </div>
                {(banksQuery.error || resolveMutation.error || saveMutation.error) && (
                  <div className="rounded-[4px] border border-[#f4b8b8] bg-[#fff3f3] px-3 py-2 text-sm text-[#b42318] md:col-span-2">
                    {String((banksQuery.error ?? resolveMutation.error ?? saveMutation.error)?.message ?? "Paystack request failed.")}
                  </div>
                )}
              </div>
            </div>

            <div className="grid min-w-0 gap-3 border-t border-[#eef3f7] px-5 py-4 md:grid-cols-[minmax(0,1fr)_auto] md:items-center">
              <div className="flex min-w-0 items-center gap-2 text-xs text-[#6b7f93]">
                <LockKeyholeIcon className="size-3.5 shrink-0" />
                <span>Secured by Paystack. Nerova stores only masked bank details.</span>
              </div>
              <Button type="button" className="h-12 w-full bg-[#011b33] px-5 text-white hover:bg-[#092844] md:w-auto" disabled={!canSave || saveMutation.isPending} onClick={save}>
                {saveMutation.isPending && <Loader2Icon className="mr-2 size-4 animate-spin" />}
                <Trans>Save payout account</Trans>
                {!saveMutation.isPending && <ChevronRightIcon className="ml-2 size-4" />}
              </Button>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}

function StepItem({ active, done, label }: { active: boolean; done: boolean; label: string }) {
  return (
    <div className={`flex items-center justify-center rounded-full px-3 py-1.5 ${active || done ? "bg-[#00c3f7] text-[#011b33]" : "bg-white/10 text-white/60"}`}>
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
        className="h-12 w-full min-w-0 rounded-[4px] border border-[#d9e3ec] bg-white px-3 text-sm outline-none transition focus:border-[#00a5d8] focus:ring-2 focus:ring-[#00a5d8]/15"
      />
    </label>
  );
}
