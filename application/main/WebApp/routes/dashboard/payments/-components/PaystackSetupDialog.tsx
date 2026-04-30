import { useMemo, useState } from "react";

import {
  usePaystackBanks,
  useResolvePaystackAccount,
  useSavePaystackSubaccount,
  type PaystackSubaccount
} from "@/shared/lib/paymentsApi";

import { PaystackSetupForm, PaystackSetupSidebar, type PaystackSetupFormState } from "./PaystackSetupDialogParts";

export function PaystackSetupDialog({ subaccount, onClose }: { subaccount?: PaystackSubaccount; onClose: () => void }) {
  const banksQuery = usePaystackBanks();
  const resolveMutation = useResolvePaystackAccount();
  const saveMutation = useSavePaystackSubaccount();
  const [form, setForm] = useState<PaystackSetupFormState>({
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
  const error = banksQuery.error ?? resolveMutation.error ?? saveMutation.error;

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
          <PaystackSetupSidebar activeStep={activeStep} subaccount={subaccount} onClose={onClose} />
          <PaystackSetupForm
            banks={banksQuery.data ?? []}
            banksLoading={banksQuery.isLoading}
            canResolve={canResolve}
            canSave={canSave}
            error={error?.message}
            form={form}
            resolving={resolveMutation.isPending}
            saving={saveMutation.isPending}
            setForm={setForm}
            onResolve={resolve}
            onSave={save}
          />
        </div>
      </div>
    </div>
  );
}
