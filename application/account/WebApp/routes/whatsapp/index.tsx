/* eslint-disable max-lines-per-function */
import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { AppLayout } from "@repo/ui/components/AppLayout";
import { Button } from "@repo/ui/components/Button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@repo/ui/components/Card";
import { Form } from "@repo/ui/components/Form";
import { SelectContent, SelectItem, SelectTrigger, SelectValue } from "@repo/ui/components/Select";
import { SelectField } from "@repo/ui/components/SelectField";
import { TextField } from "@repo/ui/components/TextField";
import { createFileRoute } from "@tanstack/react-router";
import { CheckCircle2Icon } from "lucide-react";
import { useState } from "react";
import { toast } from "sonner";

import { api } from "@/shared/lib/api/client";

export const Route = createFileRoute("/whatsapp/")({
  staticData: { trackingTitle: "WhatsApp setup" },
  component: WhatsappSetupPage
});

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

function WhatsappSetupPage() {
  const { data: status, refetch: refetchStatus } = api.useQuery("get", "/api/whatsapp/onboarding-status");

  const linkWabaMutation = api.useMutation("post", "/api/whatsapp/link-waba", {
    onSuccess: () => {
      void refetchStatus();
      toast.success(t`WhatsApp Business Account connected`);
    }
  });

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

  const handleConnectWaba = () => {
    // TODO(phase-5-followup): integrate Meta Embedded Signup SDK (PUBLIC_META_APP_ID) instead of placeholder values
    linkWabaMutation.mutate({
      body: { wabaId: "stub-waba-id", phoneNumberId: "stub-phone-id", displayPhoneNumber: "+27000000000" }
    });
  };

  const fingerprintShort = status?.publicKeyFingerprint ? status.publicKeyFingerprint.slice(0, 16) + "…" : null;

  const paystackSubaccountMasked = subaccountCode ? subaccountCode.slice(0, 6) + "****" : null;

  return (
    <AppLayout
      variant="center"
      maxWidth="64rem"
      browserTitle={t`WhatsApp setup`}
      title={t`WhatsApp setup`}
      subtitle={t`Connect WhatsApp and Paystack to start taking bookings.`}
    >
      <div className="flex flex-col gap-4">
        {/* Step 1: Connect WhatsApp Business Account */}
        <Card>
          <CardHeader>
            <div className="flex items-center justify-between">
              <div>
                <CardTitle>
                  <Trans>1. Connect WhatsApp Business Account</Trans>
                </CardTitle>
                <CardDescription>
                  <Trans>Link your WABA to enable WhatsApp bookings.</Trans>
                </CardDescription>
              </div>
              {status?.wabaLinked && (
                <div className="flex items-center gap-2 text-sm text-green-600">
                  <CheckCircle2Icon className="size-4" />
                  <span>{status.displayPhoneNumber}</span>
                </div>
              )}
            </div>
          </CardHeader>
          <CardContent>
            {!status?.wabaLinked && (
              <Button onClick={handleConnectWaba} isPending={linkWabaMutation.isPending}>
                <Trans>Connect WhatsApp</Trans>
              </Button>
            )}
          </CardContent>
        </Card>

        {/* Step 2: Phone number registered */}
        <Card>
          <CardHeader>
            <div className="flex items-center justify-between">
              <div>
                <CardTitle>
                  <Trans>2. Phone number registered</Trans>
                </CardTitle>
                <CardDescription>
                  <Trans>Your phone number must be registered with WhatsApp.</Trans>
                </CardDescription>
              </div>
              {status?.phoneRegistered && (
                <div className="flex items-center gap-2 text-sm text-green-600">
                  <CheckCircle2Icon className="size-4" />
                </div>
              )}
            </div>
          </CardHeader>
          <CardContent>
            <p className="text-sm">
              {status?.displayPhoneNumber ?? (
                <span className="text-muted-foreground">{t`Pending — complete step 1 first`}</span>
              )}
            </p>
          </CardContent>
        </Card>

        {/* Step 3: Generate encryption key pair */}
        <Card>
          <CardHeader>
            <div className="flex items-center justify-between">
              <div>
                <CardTitle>
                  <Trans>3. Generate encryption key pair</Trans>
                </CardTitle>
                <CardDescription>
                  <Trans>Required for end-to-end encrypted WhatsApp Flows.</Trans>
                </CardDescription>
              </div>
              {status?.keyPairGenerated && (
                <div className="flex items-center gap-2 text-sm text-green-600">
                  <CheckCircle2Icon className="size-4" />
                  {fingerprintShort && <span className="font-mono text-xs">{fingerprintShort}</span>}
                </div>
              )}
            </div>
          </CardHeader>
          <CardContent>
            {!status?.keyPairGenerated && (
              <Button
                onClick={() => generateKeyPairMutation.mutate({})}
                isPending={generateKeyPairMutation.isPending}
                disabled={!status?.phoneRegistered}
              >
                <Trans>Generate key pair</Trans>
              </Button>
            )}
          </CardContent>
        </Card>

        {/* Step 4: Connect Paystack subaccount */}
        <Card>
          <CardHeader>
            <div className="flex items-center justify-between">
              <div>
                <CardTitle>
                  <Trans>4. Connect Paystack subaccount</Trans>
                </CardTitle>
                <CardDescription>
                  <Trans>Receive booking payments via Paystack.</Trans>
                </CardDescription>
              </div>
              {status?.paystackConnected && (
                <div className="flex items-center gap-2 text-sm text-green-600">
                  <CheckCircle2Icon className="size-4" />
                  {paystackSubaccountMasked && <span className="font-mono text-xs">{paystackSubaccountMasked}</span>}
                </div>
              )}
            </div>
          </CardHeader>
          <CardContent>
            {!status?.paystackConnected && (
              <Form
                onSubmit={(e) => {
                  e.preventDefault();
                  const formData = new FormData(e.currentTarget);
                  connectPaystackMutation.mutate({
                    body: {
                      businessName: String(formData.get("businessName") ?? ""),
                      bankCode: String(formData.get("bankCode") ?? ""),
                      accountNumber: String(formData.get("accountNumber") ?? ""),
                      percentageFee: 5
                    }
                  });
                }}
                validationErrors={connectPaystackMutation.error?.errors}
                validationBehavior="aria"
                className="flex flex-col gap-4"
              >
                <TextField
                  name="businessName"
                  label={t`Business name`}
                  required={true}
                  disabled={!status?.keyPairGenerated}
                />
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
                  description={t`10-digit South African bank account`}
                  inputMode="numeric"
                  pattern="\d{10}"
                  required={true}
                  disabled={!status?.keyPairGenerated}
                />
                <div>
                  <Button
                    type="submit"
                    isPending={connectPaystackMutation.isPending}
                    disabled={!status?.keyPairGenerated}
                  >
                    <Trans>Connect Paystack</Trans>
                  </Button>
                </div>
              </Form>
            )}
          </CardContent>
        </Card>

        {/* All steps complete */}
        {status?.canPublishFlow && (
          <Card className="border-green-200 bg-green-50 dark:border-green-800 dark:bg-green-950">
            <CardHeader>
              <div className="flex items-center gap-2">
                <CheckCircle2Icon className="size-5 text-green-600" />
                <CardTitle className="text-green-700 dark:text-green-300">
                  <Trans>You&apos;re all set!</Trans>
                </CardTitle>
              </div>
              <CardDescription>
                <Trans>All steps complete. Configure your booking flow to start accepting WhatsApp bookings.</Trans>
              </CardDescription>
            </CardHeader>
            <CardContent>
              <a
                href="/whatsapp/questionnaire"
                className="inline-flex h-9 items-center justify-center rounded-md bg-primary px-4 py-2 text-sm font-medium text-primary-foreground shadow transition-colors hover:bg-primary/90 focus-visible:ring-1 focus-visible:ring-ring focus-visible:outline-none"
              >
                <Trans>Configure your booking flow</Trans>
              </a>
            </CardContent>
          </Card>
        )}
      </div>
    </AppLayout>
  );
}
