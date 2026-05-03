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
  const phoneDigits = phone.replace(/\D/g, "");
  const isPhoneComplete = phoneDigits.length === 10 && phoneDigits.startsWith("0");

  return (
    <section className="rounded-2xl border border-border bg-card p-5 text-card-foreground shadow-sm sm:p-6">
      <StepHeading
        step="1"
        title="Verify your phone"
        description="Use a South African mobile number. We will send a one-time SMS code before showing saved details."
      />

      <div className="grid grid-cols-[minmax(0,1fr)_auto] items-end gap-3 max-sm:grid-cols-1">
        <label className="text-sm font-medium">
          <span className="mb-2 block text-xs font-semibold tracking-[0.12em] text-muted-foreground uppercase">
            Phone number
          </span>
          <input
            value={phone}
            disabled={isVerified}
            type="tel"
            inputMode="numeric"
            maxLength={12}
            placeholder="082 123 4567"
            autoComplete="tel"
            onChange={(event) => onPhoneChange(formatZaPhoneInput(event.target.value))}
            className="h-14 w-full rounded-xl border border-input bg-background px-4 text-lg font-medium text-foreground transition-colors outline-none placeholder:text-muted-foreground focus:border-foreground disabled:bg-muted disabled:text-muted-foreground"
          />
          <span className="mt-2 block text-xs text-muted-foreground">Enter 10 digits, for example 082 123 4567.</span>
        </label>
        <div className="flex items-end max-sm:block">
          <Button
            type="button"
            className="h-14 max-sm:w-full"
            disabled={!isPhoneComplete || isSending || isVerified}
            onClick={onSendCode}
          >
            {isSending ? "Sending..." : maskedPhone ? "Resend code" : "Send code"}
          </Button>
        </div>
      </div>

      {maskedPhone && !isVerified && (
        <div className="mt-5 grid grid-cols-[minmax(0,14rem)_auto] items-end gap-3 max-sm:grid-cols-1">
          <label className="text-sm font-medium">
            <span className="mb-2 block text-xs font-semibold tracking-[0.12em] text-muted-foreground uppercase">
              SMS code sent to {maskedPhone}
            </span>
            <input
              value={code}
              inputMode="numeric"
              maxLength={6}
              autoComplete="one-time-code"
              onChange={(event) => onCodeChange(event.target.value.replace(/\D/g, "").slice(0, 6))}
              className="h-14 w-full rounded-xl border border-input bg-background px-4 text-center text-xl font-semibold tracking-[0.24em] text-foreground transition-colors outline-none focus:border-foreground"
            />
          </label>
          <div className="flex items-end max-sm:block">
            <Button
              type="button"
              className="h-14 max-sm:w-full"
              disabled={code.trim().length < 4 || isChecking}
              onClick={onCheckCode}
            >
              {isChecking ? "Checking..." : "Verify code"}
            </Button>
          </div>
        </div>
      )}

      {isVerified && (
        <div className="mt-4 flex items-start gap-3 rounded-2xl border border-emerald-200 bg-emerald-50 px-4 py-3 text-sm text-emerald-800 dark:border-emerald-900/70 dark:bg-emerald-950/30 dark:text-emerald-200">
          <CheckCircle2 className="mt-0.5 size-4 shrink-0" />
          <div>
            <div className="font-medium">Phone verified</div>
            <div className="mt-1 text-emerald-700 dark:text-emerald-300">
              Saved booking details are unlocked for {maskedPhone}.
            </div>
          </div>
        </div>
      )}

      {error && (
        <div className="mt-4 flex items-start gap-3 rounded-2xl border border-destructive/30 bg-destructive/10 px-4 py-3 text-sm text-destructive">
          <MessageSquareText className="mt-0.5 size-4 shrink-0" />
          <span>{error}</span>
        </div>
      )}
    </section>
  );
}

function formatZaPhoneInput(value: string) {
  let digits = value.replace(/\D/g, "");
  if (digits.startsWith("27")) digits = `0${digits.slice(2)}`;
  digits = digits.slice(0, 10);
  if (digits.length <= 3) return digits;
  if (digits.length <= 6) return `${digits.slice(0, 3)} ${digits.slice(3)}`;
  return `${digits.slice(0, 3)} ${digits.slice(3, 6)} ${digits.slice(6)}`;
}
