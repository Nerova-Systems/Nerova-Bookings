import type { ReactNode } from "react";

import { Button as ReactEmailButton } from "@react-email/components";

type ButtonVariant = "default" | "secondary" | "outline";

const variantClass: Record<ButtonVariant, string> = {
  default: "bg-[#0f172a] text-white",
  secondary: "bg-[#f1f5f9] text-[#0f172a]",
  outline: "border border-solid border-[#cbd5e1] bg-white text-[#0f172a]"
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
      className={`inline-block rounded-[0.5rem] px-[1.5rem] py-[0.75rem] text-[0.875rem] font-medium no-underline ${variantClass[variant]} ${className ?? ""}`}
    >
      {children}
    </ReactEmailButton>
  );
}
