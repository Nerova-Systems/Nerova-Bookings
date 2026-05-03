import type { ReactNode } from "react";

import { Button as ReactEmailButton } from "@react-email/components";

type ButtonVariant = "default" | "secondary" | "outline";

// The dark-mode counterpart for each variant lives in the @media block injected by
// TransactionalEmail (e.g. .email-button-default flips bg/text in dark). Keeping the dark
// rules in one centralized <style> block (rather than per-element dark: classes) avoids
// React Email's Tailwind plugin emitting non-standard nested CSS that some clients render
// unconditionally instead of guarded by the media query.
const variantClass: Record<ButtonVariant, string> = {
  default: "email-button-default bg-[#0f172a] text-[#ffffff]",
  secondary: "bg-[#f1f5f9] text-[#0f172a]",
  outline: "border border-solid border-[#cbd5e1] bg-[#ffffff] text-[#0f172a]"
};

type ButtonProps = {
  href: string;
  variant?: ButtonVariant;
  className?: string;
  children: ReactNode;
};

export function Button({ href, variant = "default", className, children }: ButtonProps) {
  return (
    <ReactEmailButton
      href={href}
      className={`inline-block rounded-[8px] px-[24px] py-[12px] text-[14px] font-medium no-underline ${variantClass[variant]} ${className ?? ""}`}
    >
      {children}
    </ReactEmailButton>
  );
}
