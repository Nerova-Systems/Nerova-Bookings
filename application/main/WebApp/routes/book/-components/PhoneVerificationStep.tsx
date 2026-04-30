import { Button } from "@repo/ui/components/Button";
import { CheckCircle2, MessageSquareText } from "lucide-react";

import { StepHeading } from "./PublicBookingParts";

export function PhoneVerificationStep({
  phone,
  code,
  maskedPhone,
  isVerified,
  isSending,
  isChecking,
  error,
  onPhoneChange,
  onCodeChange,
  onSendCode,
  onCheckCode
}: {
  phone: string;
  code: string;
  maskedPhone?: string;
  isVerified: boolean;
  isSending: boolean;
  isChecking: boolean;
  error?: string;
  onPhoneChange: (value: string) => void;
  onCodeChange: (value: string) => void;
  onSendCode: () => void;
  onCheckCode: () => void;
}) {
  return (
    <section className="rounded-3xl border border-border bg-[#fbfaf8] p-5 shadow-sm">
      <StepHeading
        step="1"
        title="Verify your phone"
        description="Your phone number identifies your booking and protects saved client details."
      />

      <div className="grid grid-cols-[minmax(0,1fr)_auto] gap-3 max-sm:grid-cols-1">
        <label className="text-sm font-medium">
          <span className="mb-2 block">Phone number</span>
          <input
            value={phone}
            disabled={isVerified}
            autoComplete="tel"
            onChange={(event) => onPhoneChange(event.target.value)}
            className="h-12 w-full rounded-xl border border-border bg-background px-3 text-sm transition-colors outline-none focus:border-foreground disabled:bg-muted disabled:text-muted-foreground"
          />
        </label>
        <div className="flex items-end">
          <Button type="button" variant="secondary" disabled={!phone || isSending || isVerified} onClick={onSendCode}>
            {isSending ? "Sending..." : maskedPhone ? "Resend code" : "Send code"}
          </Button>
        </div>
      </div>

      {maskedPhone && !isVerified && (
        <div className="mt-4 grid grid-cols-[minmax(0,14rem)_auto] gap-3 max-sm:grid-cols-1">
          <label className="text-sm font-medium">
            <span className="mb-2 block">SMS code sent to {maskedPhone}</span>
            <input
              value={code}
              inputMode="numeric"
              autoComplete="one-time-code"
              onChange={(event) => onCodeChange(event.target.value)}
              className="h-12 w-full rounded-xl border border-border bg-background px-3 text-sm tracking-[0.18em] transition-colors outline-none focus:border-foreground"
            />
          </label>
          <div className="flex items-end">
            <Button type="button" disabled={code.trim().length < 4 || isChecking} onClick={onCheckCode}>
              {isChecking ? "Checking..." : "Verify code"}
            </Button>
          </div>
        </div>
      )}

      {isVerified && (
        <div className="mt-4 flex items-start gap-3 rounded-2xl border border-emerald-200 bg-emerald-50 px-4 py-3 text-sm text-emerald-800">
          <CheckCircle2 className="mt-0.5 size-4 shrink-0" />
          <div>
            <div className="font-medium">Phone verified</div>
            <div className="mt-1 text-emerald-700">Saved booking details are unlocked for {maskedPhone}.</div>
          </div>
        </div>
      )}

      {error && (
        <div className="mt-4 flex items-start gap-3 rounded-2xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-800">
          <MessageSquareText className="mt-0.5 size-4 shrink-0" />
          <span>{error}</span>
        </div>
      )}
    </section>
  );
}
