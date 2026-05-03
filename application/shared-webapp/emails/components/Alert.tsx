import type { ReactNode } from "react";

import { Section } from "@react-email/components";

type AlertVariant = "default" | "info" | "success" | "warning" | "destructive";

const variantClass: Record<AlertVariant, string> = {
  default: "border-[#e2e8f0] bg-[#f8fafc] text-[#0f172a]",
  info: "border-[#bae6fd] bg-[#f0f9ff] text-[#075985]",
  success: "border-[#bbf7d0] bg-[#f0fdf4] text-[#166534]",
  warning: "border-[#fde68a] bg-[#fffbeb] text-[#92400e]",
  destructive: "border-[#fecaca] bg-[#fef2f2] text-[#991b1b]"
};

type AlertProps = {
  variant?: AlertVariant;
  title?: ReactNode;
  children: ReactNode;
};

export function Alert({ variant = "default", title, children }: AlertProps) {
  return (
    <Section
      className={`my-[1rem] rounded-[0.5rem] border border-solid p-[1rem] text-[0.875rem] ${variantClass[variant]}`}
    >
      {title ? <div className="mb-[0.25rem] font-semibold">{title}</div> : null}
      <div>{children}</div>
    </Section>
  );
}
