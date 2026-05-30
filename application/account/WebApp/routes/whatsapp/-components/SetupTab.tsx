/* eslint-disable max-lines-per-function */
import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Button } from "@repo/ui/components/Button";
import { Form } from "@repo/ui/components/Form";
import { SelectContent, SelectItem, SelectTrigger, SelectValue } from "@repo/ui/components/Select";
import { SelectField } from "@repo/ui/components/SelectField";
import { TextField } from "@repo/ui/components/TextField";
import { CheckCircle2Icon, KeyRoundIcon, MessageSquareIcon, ShieldCheckIcon, WalletIcon } from "lucide-react";
import { useEffect, useRef, useState } from "react";
import { toast } from "sonner";

import type { FBEmbeddedSignupData } from "@/shared/utils/metaSDK";

import { EmbeddedSignupButton } from "@/shared/components/EmbeddedSignupButton";
import { api, queryClient, MetaBusinessVertical } from "@/shared/lib/api/client";

// TODO: verify Paystack ZA bank codes against latest API
const SA_BANKS = [
  { code: "044", name: "ABSA Bank" },
  { code: "632", name: "African Bank" },
  { code: "462", name: "Capitec Bank" },
  { code: "679", name: "Discovery Bank" },
  { code: "250", name: "First National Bank" },
  { code: "580", name: "Investec" },
  { code: "490", name: "Nedbank" },
  { code: "051", name: "Standard Bank" },
  { code: "678", name: "TymeBank" },
  { code: "462", name: "Bidvest Bank" }
];

const VERTICAL_LABELS: Record<MetaBusinessVertical, string> = {
  [MetaBusinessVertical.Beauty]: t`Beauty`,
  [MetaBusinessVertical.Education]: t`Education`,
  [MetaBusinessVertical.Health]: t`Health`,
  [MetaBusinessVertical.ProfessionalServices]: t`Professional services`,
  [MetaBusinessVertical.Retail]: t`Retail`,
  [MetaBusinessVertical.Restaurant]: t`Restaurant`,
  [MetaBusinessVertical.Travel]: t`Travel`,
  [MetaBusinessVertical.Other]: t`Other`
};

// ─── Step card ────────────────────────────────────────────────────────────────

type StepCardProps = {
  step: number;
  icon: React.ReactNode;
  title: React.ReactNode;
  description: React.ReactNode;
  isComplete: boolean;
  isLocked: boolean;
  completedBadge?: React.ReactNode;
  children?: React.ReactNode;
};

function StepCard({
  step,
  icon,
  title,
  description,
  isComplete,
  isLocked,
  completedBadge,
  children
}: Readonly<StepCardProps>) {
  return (
    <div
      className={`rounded-xl border transition-all ${
        isComplete
          ? "border-green-300 bg-green-50/50 dark:border-green-800 dark:bg-green-950/30"
          : isLocked
            ? "border-border bg-muted/30 opacity-60"
            : "border-border bg-card"
      }`}
    >
      <div className="flex items-center gap-4 p-5">
        <div
          className={`flex size-10 shrink-0 items-center justify-center rounded-lg ${
            isComplete
              ? "bg-green-500 text-white"
              : isLocked
                ? "bg-muted text-muted-foreground"
                : "bg-primary/10 text-primary"
          }`}
        >
          {isComplete ? <CheckCircle2Icon className="size-5" /> : icon}
        </div>

        <div className="min-w-0 flex-1">
          <div className="flex items-center gap-2">
            <span className="text-xs font-bold tracking-wider text-muted-foreground uppercase">Step {step}</span>
            {isComplete && (
              <span className="rounded-full bg-green-100 px-2 py-0.5 text-xs font-semibold text-green-700 dark:bg-green-900/50 dark:text-green-400">
                ✓ Done
              </span>
            )}
          </div>
          <h3 className="mt-0.5 text-sm font-bold">{title}</h3>
          <p className="text-xs text-muted-foreground">{description}</p>
        </div>

        {completedBadge && (
          <span className="shrink-0 rounded bg-muted px-2.5 py-1 font-mono text-xs text-muted-foreground">
            {completedBadge}
          </span>
        )}
      </div>

      {children && !isComplete && !isLocked && <div className="border-t border-border p-5">{children}</div>}
    </div>
  );
}

// ─── Setup tab ────────────────────────────────────────────────────────────────

export function SetupTab() {
  const { data: status, refetch: refetchStatus } = api.useQuery("get", "/api/whatsapp/onboarding-status");
  const { data: tenant } = api.useQuery("get", "/api/account/tenants/current");

  const installMutation = (api as any).useMutation("post", "/api/apps/{slug}/install", {
    onSuccess: () => {
      void refetchStatus();
      void queryClient.invalidateQueries();
    }
  });

  const linkWabaMutation = api.useMutation("post", "/api/whatsapp/link-waba", {
    onSuccess: () => {
      (installMutation as any).mutate({ params: { path: { slug: "whatsapp" } } });
      toast.success(t`WhatsApp Business Account connected`);
    }
  });

  const pendingWabaData = useRef<FBEmbeddedSignupData | null>(null);

  useEffect(() => {
    const handleMessage = (event: MessageEvent) => {
      if (event.origin !== "https://www.facebook.com") return;
      const payload = event.data as { type?: string; data?: FBEmbeddedSignupData };
      if (payload.type === "WA_EMBEDDED_SIGNUP" && payload.data) {
        pendingWabaData.current = payload.data;
      }
    };
    window.addEventListener("message", handleMessage);
    return () => {
      window.removeEventListener("message", handleMessage);
    };
  }, []);

  const handleEmbeddedSignupSuccess = (_code: string) => {
    const wabaData = pendingWabaData.current;
    if (!wabaData) {
      toast.error(t`WhatsApp connection failed. Please try again.`);
      return;
    }
    linkWabaMutation.mutate({
      body: {
        wabaId: wabaData.waba_id,
        phoneNumberId: wabaData.phone_number_id,
        displayPhoneNumber: wabaData.display_phone_number ?? ""
      }
    });
    pendingWabaData.current = null;
  };

  const handleEmbeddedSignupCancel = () => {
    pendingWabaData.current = null;
    toast.info(t`WhatsApp connection was cancelled.`);
  };

  const handleEmbeddedSignupError = (_error: unknown) => {
    pendingWabaData.current = null;
    toast.error(t`WhatsApp sign-up failed. Please try again.`);
  };

  const generateKeyPairMutation = api.useMutation("post", "/api/whatsapp/generate-key-pair", {
    onSuccess: () => {
      void refetchStatus();
      toast.success(t`Key pair generated`);
    }
  });

  const connectPaystackMutation = api.useMutation("post", "/api/whatsapp/connect-paystack", {
    onSuccess: (data) => {
      void refetchStatus();
      setSubaccountCode(data.subaccountCode);
      toast.success(t`Paystack subaccount connected`);
    }
  });

  const [subaccountCode, setSubaccountCode] = useState<string | null>(null);

  const fingerprintShort = status?.publicKeyFingerprint ? status.publicKeyFingerprint.slice(0, 16) + "…" : null;
  const paystackSubaccountMasked = subaccountCode ? subaccountCode.slice(0, 6) + "****" : null;

  return (
    <div className="flex flex-col gap-3">
      {/* Step 1: Connect WABA */}
      <StepCard
        step={1}
        icon={<MessageSquareIcon className="size-5" />}
        title={<Trans>Connect WhatsApp Business Account</Trans>}
        description={<Trans>Sign in with Meta to link your WhatsApp Business number.</Trans>}
        isComplete={Boolean(status?.wabaLinked)}
        isLocked={false}
        completedBadge={status?.displayPhoneNumber}
      >
        <div className="flex flex-col items-center gap-3 py-1 text-center">
          <EmbeddedSignupButton
            onSuccess={handleEmbeddedSignupSuccess}
            onCancel={handleEmbeddedSignupCancel}
            onError={handleEmbeddedSignupError}
            disabled={linkWabaMutation.isPending}
            businessName={tenant?.name}
          />
          <div className="flex items-center gap-1 text-[11px] text-muted-foreground">
            <ShieldCheckIcon className="size-3 text-emerald-600 dark:text-emerald-400" />
            <Trans>Secured by Meta</Trans>
          </div>
        </div>
      </StepCard>

      {/* Step 2: Phone registered */}
      <StepCard
        step={2}
        icon={<CheckCircle2Icon className="size-5" />}
        title={<Trans>Phone number verified</Trans>}
        description={
          status?.phoneRegistered ? (
            <Trans>Your number {status.displayPhoneNumber} is registered and active.</Trans>
          ) : (
            <Trans>Automatically registered once step 1 is complete.</Trans>
          )
        }
        isComplete={Boolean(status?.phoneRegistered)}
        isLocked={!status?.wabaLinked}
      />

      {/* Step 3: Encryption key pair */}
      <StepCard
        step={3}
        icon={<KeyRoundIcon className="size-5" />}
        title={<Trans>Generate encryption key pair</Trans>}
        description={<Trans>Required for end-to-end encrypted WhatsApp Flows.</Trans>}
        isComplete={Boolean(status?.keyPairGenerated)}
        isLocked={!status?.phoneRegistered}
        completedBadge={fingerprintShort}
      >
        <div className="flex flex-col gap-3">
          <div className="flex items-center gap-2.5 rounded-lg border border-border bg-muted/40 p-3 text-xs text-muted-foreground">
            <KeyRoundIcon className="size-4 shrink-0" />
            <Trans>A 2048-bit RSA key pair will be generated. The private key never leaves our servers.</Trans>
          </div>
          <Button
            onClick={() => generateKeyPairMutation.mutate({})}
            isPending={generateKeyPairMutation.isPending}
            disabled={!status?.phoneRegistered}
          >
            <KeyRoundIcon className="size-4" />
            <Trans>Generate key pair</Trans>
          </Button>
        </div>
      </StepCard>

      {/* Step 4: Connect Paystack */}
      <StepCard
        step={4}
        icon={<WalletIcon className="size-5" />}
        title={<Trans>Connect Paystack for payments</Trans>}
        description={<Trans>Enter your SA bank details to receive booking payments.</Trans>}
        isComplete={Boolean(status?.paystackConnected)}
        isLocked={!status?.keyPairGenerated}
        completedBadge={paystackSubaccountMasked}
      >
        <Form
          onSubmit={(e) => {
            e.preventDefault();
            const formData = new FormData(e.currentTarget);
            connectPaystackMutation.mutate({
              body: {
                businessName: String(formData.get("businessName") ?? ""),
                bankCode: String(formData.get("bankCode") ?? ""),
                accountNumber: String(formData.get("accountNumber") ?? ""),
                percentageFee: 1
              }
            });
          }}
          validationErrors={connectPaystackMutation.error?.errors}
          validationBehavior="aria"
          className="flex flex-col gap-4"
        >
          <TextField
            name="businessName"
            label={t`Business trading name`}
            required={true}
            disabled={!status?.keyPairGenerated}
          />
          <div className="grid grid-cols-2 gap-3">
            <SelectField
              name="bankCode"
              label={t`Bank`}
              items={SA_BANKS.map((b) => ({ value: b.code, label: b.name }))}
              disabled={!status?.keyPairGenerated}
            >
              <SelectTrigger>
                <SelectValue>{(code: string) => SA_BANKS.find((b) => b.code === code)?.name}</SelectValue>
              </SelectTrigger>
              <SelectContent>
                {SA_BANKS.map((bank) => (
                  <SelectItem key={`${bank.code}-${bank.name}`} value={bank.code}>
                    {bank.name}
                  </SelectItem>
                ))}
              </SelectContent>
            </SelectField>
            <TextField
              name="accountNumber"
              label={t`Account number`}
              inputMode="numeric"
              pattern="\d{10}"
              required={true}
              disabled={!status?.keyPairGenerated}
            />
          </div>
          <div className="rounded-lg border border-green-200 bg-green-50/50 p-2.5 text-xs text-muted-foreground dark:border-green-900 dark:bg-green-950/30">
            💡 <Trans>A 1% platform fee applies to all bookings processed through Nerova.</Trans>
          </div>
          <Button type="submit" isPending={connectPaystackMutation.isPending} disabled={!status?.keyPairGenerated}>
            <WalletIcon className="size-4" />
            <Trans>Connect Paystack</Trans>
          </Button>
        </Form>
      </StepCard>

      {/* All done banner */}
      {status?.canPublishFlow && (
        <div className="flex flex-col items-center gap-3 rounded-xl border border-green-300 bg-green-50/60 p-6 text-center dark:border-green-800 dark:bg-green-950/30">
          <CheckCircle2Icon className="size-8 text-green-500" />
          <div>
            <h3 className="text-base font-bold">
              <Trans>Setup complete 🎉</Trans>
            </h3>
            <p className="mt-1 text-sm text-muted-foreground">
              <Trans>
                Your WhatsApp Business Account is connected and ready. Configure your business profile in the Profile
                tab.
              </Trans>
            </p>
          </div>
        </div>
      )}
    </div>
  );
}
