import { useLingui } from "@lingui/react/macro";
import { XIcon } from "lucide-react";
import { useRef, useState, type KeyboardEvent } from "react";

import { cn } from "../utils";
import { Badge } from "./Badge";

/**
 * Multi-email input that renders each email as a dismissible badge tag.
 * Keyboard: Enter / Tab / comma → add; Backspace on empty input → remove last.
 * Ported from cal.com `packages/ui/components/address/MultiEmail.tsx` (cf2a55c).
 *
 * No prop deviations.
 */
interface MultiEmailProps {
  value?: string[];
  defaultValue?: string[];
  onChange?: (emails: string[]) => void;
  placeholder?: string;
  disabled?: boolean;
  className?: string;
  "aria-label"?: string;
  "aria-describedby"?: string;
}

export function MultiEmail({
  value: controlledValue,
  defaultValue = [],
  onChange,
  placeholder,
  disabled,
  className,
  "aria-label": ariaLabel,
  "aria-describedby": ariaDescribedBy
}: MultiEmailProps) {
  const { t } = useLingui();
  const [internalEmails, setInternalEmails] = useState<string[]>(defaultValue);
  const [inputValue, setInputValue] = useState("");
  const inputRef = useRef<HTMLInputElement>(null);

  const isControlled = controlledValue !== undefined;
  const emails = isControlled ? controlledValue : internalEmails;

  const setEmails = (next: string[]) => {
    if (!isControlled) setInternalEmails(next);
    onChange?.(next);
  };

  const addEmail = (raw: string) => {
    const email = raw.trim().toLowerCase();
    if (email && !emails.includes(email)) {
      setEmails([...emails, email]);
    }
    setInputValue("");
  };

  const removeEmail = (idx: number) => {
    setEmails(emails.filter((_, i) => i !== idx));
  };

  const handleKeyDown = (e: KeyboardEvent<HTMLInputElement>) => {
    if (e.key === "Enter" || e.key === "Tab" || e.key === ",") {
      e.preventDefault();
      addEmail(inputValue);
    } else if (e.key === "Backspace" && inputValue === "" && emails.length > 0) {
      removeEmail(emails.length - 1);
    }
  };

  return (
    <div
      data-slot="multi-email"
      role="group"
      className={cn(
        "flex min-h-[var(--control-height)] flex-wrap items-center gap-1.5 rounded-md border border-input bg-background px-3 py-1.5 ring-0 transition-colors focus-within:outline focus-within:outline-2 focus-within:outline-offset-2 focus-within:outline-ring",
        disabled && "pointer-events-none opacity-50",
        className
      )}
      onClick={() => inputRef.current?.focus()}
      onKeyDown={(e) => {
        if (e.key === "Enter" || e.key === " ") inputRef.current?.focus();
      }}
    >
      {emails.map((email, i) => (
        <Badge key={email} variant="secondary" className="flex items-center gap-1 py-0.5">
          {email}
          {!disabled && (
            <button
              type="button"
              aria-label={t`Remove ${email}`}
              className="ml-0.5 rounded-sm opacity-60 hover:opacity-100 focus-visible:outline focus-visible:outline-2"
              onClick={(e) => {
                e.stopPropagation();
                removeEmail(i);
              }}
            >
              <XIcon className="size-3" />
            </button>
          )}
        </Badge>
      ))}
      <input
        ref={inputRef}
        type="email"
        value={inputValue}
        onChange={(e) => setInputValue(e.target.value)}
        onKeyDown={handleKeyDown}
        onBlur={() => {
          if (inputValue.trim()) addEmail(inputValue);
        }}
        placeholder={emails.length === 0 ? (placeholder ?? t`Add email address`) : undefined}
        disabled={disabled}
        aria-label={ariaLabel}
        aria-describedby={ariaDescribedBy}
        className="min-w-[8rem] flex-1 bg-transparent text-sm outline-none placeholder:text-muted-foreground"
      />
    </div>
  );
}
