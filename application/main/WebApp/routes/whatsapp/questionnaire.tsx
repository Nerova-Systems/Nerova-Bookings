// TODO(phase-6): tier-based option locks wired to useWhatsAppFlowTierLimits; full per-tier granularity pending GET /api/whatsapp-flows/tier-limits endpoint in main SCS.
/* eslint-disable max-lines-per-function */
import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { AppLayout } from "@repo/ui/components/AppLayout";
import { Button } from "@repo/ui/components/Button";
import { Form } from "@repo/ui/components/Form";
import { Link } from "@repo/ui/components/Link";
import { NumberField } from "@repo/ui/components/NumberField";
import { SelectContent, SelectItem, SelectTrigger, SelectValue } from "@repo/ui/components/Select";
import { SelectField } from "@repo/ui/components/SelectField";
import { Separator } from "@repo/ui/components/Separator";
import { SidebarInset, SidebarProvider } from "@repo/ui/components/Sidebar";
import { Steps } from "@repo/ui/components/Steps";
import { SwitchField } from "@repo/ui/components/SwitchField";
import { TextAreaField } from "@repo/ui/components/TextAreaField";
import { TextField } from "@repo/ui/components/TextField";
import { Tooltip, TooltipContent, TooltipTrigger } from "@repo/ui/components/Tooltip";
import { mutationSubmitter } from "@repo/ui/forms/mutationSubmitter";
import { createFileRoute, useNavigate } from "@tanstack/react-router";
import { useEffect, useState } from "react";

import type { CustomQuestionType } from "@/shared/lib/api/client";

import { MainSideMenu } from "@/shared/components/MainSideMenu";
import { api, BusinessVertical, PaymentTiming, StaffAssignment } from "@/shared/lib/api/client";

import type { CustomQuestion } from "./-customPreBookingQuestionsSection";

import { CustomPreBookingQuestionsSection } from "./-customPreBookingQuestionsSection";
import { useWhatsAppFlowTierLimits } from "./-useWhatsAppFlowTierLimits";

export const Route = createFileRoute("/whatsapp/questionnaire")({
  staticData: { trackingTitle: "WhatsApp questionnaire" },
  component: QuestionnairePage
});

const TOTAL_STEPS = 5;

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
          <Link href="/account/billing" underline>
            Upgrade your plan
          </Link>{" "}
          to unlock this option.
        </Trans>
      </TooltipContent>
    </Tooltip>
  );
}

function QuestionnairePage() {
  const navigate = useNavigate();
  const [currentStep, setCurrentStep] = useState(0);
  const [paymentTiming, setPaymentTiming] = useState<string>("");
  const [customQuestions, setCustomQuestions] = useState<CustomQuestion[]>([]);
  const tierLimits = useWhatsAppFlowTierLimits();

  const { data: config } = api.useQuery("get", "/api/whatsapp-flows/config");

  useEffect(() => {
    if (config) {
      if (config.paymentTiming) {
        setPaymentTiming(config.paymentTiming);
      }
      if (config.customPreBookingQuestions) {
        setCustomQuestions(
          config.customPreBookingQuestions.map((q) => ({
            text: q.questionText,
            type: q.questionType as "Text" | "MultipleChoice" | "YesNo",
            options: q.choices ?? undefined
          }))
        );
      }
    }
  }, [config]);

  const addQuestionMutation = api.useMutation("post", "/api/whatsapp-flows/config/questions");
  const submitMutation = api.useMutation("post", "/api/whatsapp-flows/config", {
    onSuccess: async () => {
      await Promise.all(
        customQuestions.map((question) =>
          addQuestionMutation.mutateAsync({
            body: {
              questionText: question.text,
              questionType: question.type as CustomQuestionType,
              isRequired: true,
              choices: question.type === "MultipleChoice" ? (question.options?.filter(Boolean) ?? []) : null
            }
          })
        )
      );
      void navigate({ to: "/whatsapp/preview" });
    }
  });

  const isSubmitting = submitMutation.isPending || addQuestionMutation.isPending;

  const stepHeadings = [
    t`Tell us about your business`,
    t`Booking windows & durations`,
    t`Services & team`,
    t`Payment setup`,
    t`Final details`
  ];

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
            validationErrors={submitMutation.error?.errors ?? addQuestionMutation.error?.errors}
            validationBehavior="aria"
            className="flex flex-col gap-6"
          >
            {/* Step indicator */}
            <div className="flex flex-col items-center gap-3">
              <Steps
                currentStep={currentStep}
                maxSteps={TOTAL_STEPS}
                stepLabel={(current, total) => t`Step ${current} of ${total}`}
              />
              <div className="text-center">
                <h2>{stepHeadings[currentStep]}</h2>
                {currentStep === 0 && (
                  <p className="mt-1 text-sm text-muted-foreground">
                    <Trans>We'll use this to set up your booking flow the right way.</Trans>
                  </p>
                )}
              </div>
            </div>

            {/* Step 1: Business Profile */}
            {currentStep === 0 && (
              <div className="flex flex-col gap-4">
                <SelectField
                  name="businessVertical"
                  label={t`Business vertical`}
                  defaultValue={config?.businessVertical}
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
            )}

            {/* Step 2: Booking Preferences */}
            {currentStep === 1 && (
              <div className="flex flex-col gap-4">
                 <NumberField name="bookingWindowDays" label={t`Booking window (days)`} defaultValue={config?.bookingWindowDays ?? 30} minValue={1} maxValue={365} />
                <NumberField
                  name="defaultSessionMinutes"
                  label={t`Default session length (minutes)`}
                  defaultValue={config?.defaultSessionMinutes ?? 60}
                  minValue={15}
                  maxValue={480}
                  step={5}
                />
                 <SwitchField name="allowSameDayBookings" label={t`Allow same-day bookings`} defaultChecked={config?.allowSameDayBookings ?? true} />
              </div>
            )}

            {/* Step 3: Services & Staff */}
            {currentStep === 2 && (
              <div className="flex flex-col gap-4">
                <SelectField
                  name="staffAssignment"
                  label={t`Staff assignment`}
                  defaultValue={config?.staffAssignment}
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
                <TierGatedOption locked={!tierLimits.multipleServicesInFlow}>
                    <SwitchField
                     name="hasMultipleServices"
                     label={t`Offer multiple services`}
                     defaultChecked={config?.hasMultipleServices ?? false}
                     disabled={!tierLimits.multipleServicesInFlow}
                   />
                </TierGatedOption>
              </div>
            )}

            {/* Step 4: Payments */}
            {currentStep === 3 && (
              <div className="flex flex-col gap-4">
                <SelectField
                  name="paymentTiming"
                  label={t`Payment timing`}
                  defaultValue={config?.paymentTiming}
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
                    defaultValue={config?.depositAmountCents ?? 0}
                    minValue={0}
                  />
                )}
              </div>
            )}

            {/* Step 5: Final Details */}
            {currentStep === 4 && (
              <div className="flex flex-col gap-6">
                {/* Q7 component goes here */}
                {tierLimits.maxCustomPreBookingQuestions === 0 ? (
                  <p className="text-sm text-muted-foreground">
                    <Trans>
                      Custom pre-booking questions require an upgraded plan —{" "}
                      <Link href="/account/billing" underline>
                        upgrade your plan
                      </Link>{" "}
                      to unlock this option.
                    </Trans>
                  </p>
                ) : (
                  <CustomPreBookingQuestionsSection
                    questions={customQuestions}
                    onChange={setCustomQuestions}
                    maxQuestions={tierLimits.maxCustomPreBookingQuestions}
                  />
                )}
                <Separator />
                 <TextAreaField
                  name="confirmationMessageTemplate"
                  label={t`Confirmation message template`}
                  defaultValue={config?.confirmationMessageTemplate ?? "Hi {name}, your booking for {service} on {time} with {staff} is confirmed."}
                  description={t`{{customerName}}, {{bookingTime}}, {{businessName}} placeholders supported`}
                />
                <Separator />
                <TextField
                  name="cancellationContact"
                  label={t`Cancellation contact`}
                  defaultValue={config?.cancellationContact ?? ""}
                  description={t`Phone or email customers should use to cancel`}
                />
              </div>
            )}

            {/* Navigation */}
            <div className="flex items-center justify-between gap-2">
              <Button
                type="button"
                variant="outline"
                onClick={() => setCurrentStep((s) => s - 1)}
                disabled={currentStep === 0 || isSubmitting}
              >
                <Trans>Back</Trans>
              </Button>
              {currentStep < TOTAL_STEPS - 1 ? (
                <Button type="button" onClick={() => setCurrentStep((s) => s + 1)}>
                  <Trans>Next</Trans>
                </Button>
              ) : (
                <Button type="submit" isPending={isSubmitting}>
                  <Trans>Publish my flow</Trans>
                </Button>
              )}
            </div>
          </Form>
        </AppLayout>
      </SidebarInset>
    </SidebarProvider>
  );
}
