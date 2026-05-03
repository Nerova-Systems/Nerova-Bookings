import type { ReactNode } from "react";

import { Heading as ReactEmailHeading } from "@react-email/components";

type HeadingLevel = 1 | 2 | 3 | 4;

const levelClass: Record<HeadingLevel, string> = {
  1: "text-[1.5rem] leading-[2rem] font-semibold tracking-tight",
  2: "text-[1.25rem] leading-[1.75rem] font-semibold tracking-tight",
  3: "text-[1.125rem] leading-[1.5rem] font-semibold",
  4: "text-[1rem] leading-[1.5rem] font-medium"
};

type HeadingProps = {
  level?: HeadingLevel;
  className?: string;
  children: ReactNode;
};

export function Heading({ level = 1, className, children }: HeadingProps) {
  return (
    <ReactEmailHeading
      as={`h${level}`}
      className={`m-0 mb-[1rem] text-[#0f172a] ${levelClass[level]} ${className ?? ""}`}
    >
      {children}
    </ReactEmailHeading>
  );
}
