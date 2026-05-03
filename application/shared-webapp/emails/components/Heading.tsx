import type { ReactNode } from "react";

import { Heading as ReactEmailHeading } from "@react-email/components";

type HeadingLevel = 1 | 2 | 3 | 4;

const levelClass: Record<HeadingLevel, string> = {
  1: "text-[24px] leading-[32px] font-semibold",
  2: "text-[20px] leading-[28px] font-semibold",
  3: "text-[18px] leading-[24px] font-semibold",
  4: "text-[16px] leading-[24px] font-medium"
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
      className={`email-heading m-[0px] mb-[16px] text-[#0f172a] ${levelClass[level]} ${className ?? ""}`}
    >
      {children}
    </ReactEmailHeading>
  );
}
