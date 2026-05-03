import type { ReactNode } from "react";

type BadgeVariant = "default" | "secondary" | "success" | "warning" | "destructive" | "outline";

const variantClass: Record<BadgeVariant, string> = {
  default: "bg-[#0f172a] text-white",
  secondary: "bg-[#f1f5f9] text-[#0f172a]",
  success: "bg-[#dcfce7] text-[#166534]",
  warning: "bg-[#fef3c7] text-[#92400e]",
  destructive: "bg-[#fee2e2] text-[#991b1b]",
  outline: "border border-solid border-[#cbd5e1] bg-white text-[#0f172a]"
};

type BadgeProps = {
  variant?: BadgeVariant;
  children: ReactNode;
};

export function Badge({ variant = "default", children }: BadgeProps) {
  return (
    <span
      className={`inline-block rounded-full px-[0.625rem] py-[0.125rem] text-[0.75rem] font-medium ${variantClass[variant]}`}
    >
      {children}
    </span>
  );
}
