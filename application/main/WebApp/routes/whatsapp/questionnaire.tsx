// TODO(phase-6): tier-based option locks wired to useWhatsAppFlowTierLimits; full per-tier granularity pending GET /api/whatsapp-flows/tier-limits endpoint in main SCS.
/* eslint-disable max-lines-per-function */
import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { AppLayout } from "@repo/ui/components/AppLayout";
import { Button } from "@repo/ui/components/Button";
import { Form } from "@repo/ui/components/Form";
import { NumberField } from "@repo/ui/components/NumberField";
import { SelectContent, SelectItem, SelectTrigger, SelectValue } from "@repo/ui/components/Select";
import { SelectField } from "@repo/ui/components/SelectField";
import { Separator } from "@repo/ui/components/Separator";
import { SidebarInset, SidebarProvider } from "@repo/ui/components/Sidebar";
import { SwitchField } from "@repo/ui/components/SwitchField";
import { TextAreaField } from "@repo/ui/components/TextAreaField";
import { TextField } from "@repo/ui/components/TextField";
import { Tooltip, TooltipContent, TooltipTrigger } from "@repo/ui/components/Tooltip";
import { mutationSubmitter } from "@repo/ui/forms/mutationSubmitter";
import { createFileRoute, useNavigate } from "@tanstack/react-router";
import { useState } from "react";

import { MainSideMenu } from "@/shared/components/MainSideMenu";
import { api, BusinessVertical, PaymentTiming, StaffAssignment } from "@/shared/lib/api/client";

import { useWhatsAppFlowTierLimits } from "./-useWhatsAppFlowTierLimits";

export const Route = createFileRoute("/whatsapp/questionnaire")({
  staticData: { trackingTitle: "WhatsApp questionnaire" },
  component: QuestionnairePage
});

function TierGatedOption({
  locked,
  children
}: Readonly<{
  locked: boolean;
  children: React.ReactNode;
}>) {
  if (!locked) return <>{children}</>;
  return (
    <Tooltip>
      <TooltipTrigger render={<div className="cursor-not-allowed opacity-60" />}>{children}</TooltipTrigger>
      <TooltipContent>
        <Trans>
          <a href="/account/billing" className="underline underline-offset-4">
            Upgrade your plan
          </a>{" "}
          to unlock this option.
        </Trans>
      </TooltipContent>
    </Tooltip>
  );
}

function QuestionnairePage() {
  const navigate = useNavigate();
  const [paymentTiming, setPaymentTiming] = useState<string>("");
  const tierLimits = useWhatsAppFlowTierLimits();

  const submitMutation = api.useMutation("post", "/api/whatsapp-flows/config", {
    onSuccess: () => {
      void navigate({ to: "/whatsapp/preview" });
    }
  });

  return (
    <SidebarProvider>
      <MainSideMenu />
      <SidebarInset>
        <AppLayout
          variant="center"
          maxWidth="64rem"
          browserTitle={t`Configure your booking flow`}
          title={t`Configure your booking flow`}
          subtitle={t`Answer 10 questions to publish your WhatsApp booking flow.`}
        >
          <Form
            onSubmit={mutationSubmitter(submitMutation)}
            validationErrors={submitMutation.error?.errors}
            validationBehavior="aria"
            className="flex flex-col gap-6"
          >
            {/* 1. Business vertical */}
            <div className="flex flex-col gap-4">
              <h3>
                <Trans>1. Business vertical</Trans>
              </h3>
              <SelectField
                name="businessVertical"
                label={t`Business vertical`}
                items={[
                  { value: BusinessVertical.HairSalon, label: t`Hair Salon` },
                  { value: BusinessVertical.BarberShop, label: t`Barber Shop` },
                  { value: BusinessVertical.PersonalTrainer, label: t`Personal Trainer` },
                  { value: BusinessVertical.Tutor, label: t`Tutor` },
                  { value: BusinessVertical.Clinic, label: t`Clinic` },
                  { value: BusinessVertical.Other, label: t`Other` }
                ]}
              >
                <SelectTrigger>
                  <SelectValue>
                    {(value: string) => {
                      const labels: Record<string, string> = {
                        [BusinessVertical.HairSalon]: t`Hair Salon`,
                        [BusinessVertical.BarberShop]: t`Barber Shop`,
                        [BusinessVertical.PersonalTrainer]: t`Personal Trainer`,
                        [BusinessVertical.Tutor]: t`Tutor`,
                        [BusinessVertical.Clinic]: t`Clinic`,
                        [BusinessVertical.Other]: t`Other`
                      };
                      return labels[value];
                    }}
                  </SelectValue>
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value={BusinessVertical.HairSalon}>
                    <Trans>Hair Salon</Trans>
                  </SelectItem>
                  <SelectItem value={BusinessVertical.BarberShop}>
                    <Trans>Barber Shop</Trans>
                  </SelectItem>
                  <SelectItem value={BusinessVertical.PersonalTrainer}>
                    <Trans>Personal Trainer</Trans>
                  </SelectItem>
                  <SelectItem value={BusinessVertical.Tutor}>
                    <Trans>Tutor</Trans>
                  </SelectItem>
                  <SelectItem value={BusinessVertical.Clinic}>
                    <Trans>Clinic</Trans>
                  </SelectItem>
                  <SelectItem value={BusinessVertical.Other}>
                    <Trans>Other</Trans>
                  </SelectItem>
                </SelectContent>
              </SelectField>
            </div>

            <Separator />

            {/* 2. Default session length */}
            <div className="flex flex-col gap-4">
              <h3>
                <Trans>2. Default session length</Trans>
              </h3>
              <NumberField
                name="defaultSessionMinutes"
                label={t`Default session length (minutes)`}
                minValue={15}
                maxValue={480}
                step={5}
              />
            </div>

            <Separator />

            {/* 3. Multiple services */}
            <div className="flex flex-col gap-4">
              <h3>
                <Trans>3. Multiple services</Trans>
              </h3>
              <TierGatedOption locked={!tierLimits.multipleServicesInFlow}>
                <SwitchField
                  name="hasMultipleServices"
                  label={t`Offer multiple services`}
                  disabled={!tierLimits.multipleServicesInFlow}
                />
              </TierGatedOption>
            </div>

            <Separator />

            {/* 4. Staff assignment */}
            <div className="flex flex-col gap-4">
              <h3>
                <Trans>4. Staff assignment</Trans>
              </h3>
              <SelectField
                name="staffAssignment"
                label={t`Staff assignment`}
                items={[
                  { value: StaffAssignment.SpecificStaff, label: t`Specific staff` },
                  { value: StaffAssignment.FirstAvailable, label: t`First available` },
                  { value: StaffAssignment.AutoAssign, label: t`Auto assign` }
                ]}
              >
                <SelectTrigger>
                  <SelectValue>
                    {(value: string) => {
                      const labels: Record<string, string> = {
                        [StaffAssignment.SpecificStaff]: t`Specific staff`,
                        [StaffAssignment.FirstAvailable]: t`First available`,
                        [StaffAssignment.AutoAssign]: t`Auto assign`
                      };
                      return labels[value];
                    }}
                  </SelectValue>
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value={StaffAssignment.SpecificStaff} disabled={!tierLimits.staffSelectionInFlow}>
                    <Trans>Specific staff</Trans>
                    {!tierLimits.staffSelectionInFlow && (
                      <span className="ml-auto text-xs text-muted-foreground">
                        <Trans>Upgrade</Trans>
                      </span>
                    )}
                  </SelectItem>
                  <SelectItem value={StaffAssignment.FirstAvailable}>
                    <Trans>First available</Trans>
                  </SelectItem>
                  <SelectItem value={StaffAssignment.AutoAssign}>
                    <Trans>Auto assign</Trans>
                  </SelectItem>
                </SelectContent>
              </SelectField>
            </div>

            <Separator />

            {/* 5. Booking window */}
            <div className="flex flex-col gap-4">
              <h3>
                <Trans>5. Booking window</Trans>
              </h3>
              <NumberField name="bookingWindowDays" label={t`Booking window (days)`} minValue={1} maxValue={365} />
            </div>

            <Separator />

            {/* 6. Same-day bookings */}
            <div className="flex flex-col gap-4">
              <h3>
                <Trans>6. Same-day bookings</Trans>
              </h3>
              <SwitchField name="allowSameDayBookings" label={t`Allow same-day bookings`} />
            </div>

            <Separator />

            {/* 7. Custom pre-booking questions */}
            <div className="flex flex-col gap-4">
              <h3>
                <Trans>7. Custom pre-booking questions</Trans>
              </h3>
              {tierLimits.maxCustomPreBookingQuestions === 0 ? (
                <p className="text-sm text-muted-foreground">
                  <Trans>
                    Custom pre-booking questions require an upgraded plan —{" "}
                    <a href="/account/billing" className="underline underline-offset-4">
                      upgrade your plan
                    </a>{" "}
                    to unlock this option.
                  </Trans>
                </p>
              ) : (
                <p className="text-sm text-muted-foreground">
                  <Trans>Custom pre-booking questions can be added after publishing — coming soon.</Trans>
                </p>
              )}
            </div>

            <Separator />

            {/* 8. Payment timing */}
            <div className="flex flex-col gap-4">
              <h3>
                <Trans>8. Payment timing</Trans>
              </h3>
              <SelectField
                name="paymentTiming"
                label={t`Payment timing`}
                items={[
                  { value: PaymentTiming.AfterSession, label: t`After session` },
                  { value: PaymentTiming.BeforeBooking, label: t`Before booking` },
                  { value: PaymentTiming.Deposit, label: t`Deposit` }
                ]}
                onValueChange={(value) => setPaymentTiming(String(value ?? ""))}
              >
                <SelectTrigger>
                  <SelectValue>
                    {(value: string) => {
                      const labels: Record<string, string> = {
                        [PaymentTiming.AfterSession]: t`After session`,
                        [PaymentTiming.BeforeBooking]: t`Before booking`,
                        [PaymentTiming.Deposit]: t`Deposit`
                      };
                      return labels[value];
                    }}
                  </SelectValue>
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value={PaymentTiming.AfterSession}>
                    <Trans>After session</Trans>
                  </SelectItem>
                  <SelectItem
                    value={PaymentTiming.BeforeBooking}
                    disabled={tierLimits.paymentTimingChoice === "AfterOnly"}
                  >
                    <Trans>Before booking</Trans>
                    {tierLimits.paymentTimingChoice === "AfterOnly" && (
                      <span className="ml-auto text-xs text-muted-foreground">
                        <Trans>Upgrade</Trans>
                      </span>
                    )}
                  </SelectItem>
                  <SelectItem value={PaymentTiming.Deposit} disabled={tierLimits.paymentTimingChoice === "AfterOnly"}>
                    <Trans>Deposit</Trans>
                    {tierLimits.paymentTimingChoice === "AfterOnly" && (
                      <span className="ml-auto text-xs text-muted-foreground">
                        <Trans>Upgrade</Trans>
                      </span>
                    )}
                  </SelectItem>
                </SelectContent>
              </SelectField>
              {paymentTiming === PaymentTiming.Deposit && (
                <NumberField
                  name="depositAmountCents"
                  label={t`Deposit amount`}
                  description={t`Amount in cents`}
                  minValue={0}
                />
              )}
            </div>

            <Separator />

            {/* 9. Cancellation contact */}
            <div className="flex flex-col gap-4">
              <h3>
                <Trans>9. Cancellation contact</Trans>
              </h3>
              <TextField
                name="cancellationContact"
                label={t`Cancellation contact`}
                description={t`Phone or email customers should use to cancel`}
              />
            </div>

            <Separator />

            {/* 10. Confirmation message template */}
            <div className="flex flex-col gap-4">
              <h3>
                <Trans>10. Confirmation message template</Trans>
              </h3>
              <TextAreaField
                name="confirmationMessageTemplate"
                label={t`Confirmation message template`}
                description="{{customerName}}, {{bookingTime}}, {{businessName}} placeholders supported"
              />
            </div>

            <div>
              <Button type="submit" isPending={submitMutation.isPending}>
                <Trans>Continue to preview</Trans>
              </Button>
            </div>
          </Form>
        </AppLayout>
      </SidebarInset>
    </SidebarProvider>
  );
}
